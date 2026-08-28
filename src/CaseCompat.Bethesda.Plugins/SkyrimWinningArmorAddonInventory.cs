using CaseCompat.Core.LoadOrder;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;

namespace CaseCompat.Bethesda.Plugins;

public sealed record SkyrimWinningArmorAddonRecord(
    string FormKey,
    string? EditorId,
    string WinningPluginName,
    int WinningLoadOrderIndex,
    IReadOnlyList<SkyrimArmorAddonModelReference> ModelReferences
)
{
    public int ModelReferenceCount =>
        ModelReferences.Count;
}

public sealed record SkyrimWinningArmorAddonInventoryResult(
    string DataRoot,
    int ExplicitlyActivePluginCount,
    int PluginsOpened,
    IReadOnlyList<string> MissingPluginFiles,
    IReadOnlyList<SkyrimPluginReadError> ReadErrors,
    IReadOnlyList<SkyrimWinningArmorAddonRecord> Winners
)
{
    public bool SearchComplete =>
        MissingPluginFiles.Count == 0 &&
        ReadErrors.Count == 0;

    public int WinningArmorAddonCount =>
        Winners.Count;

    public int WinningModelReferenceCount =>
        Winners.Sum(record =>
            record.ModelReferenceCount
        );
}

public static class SkyrimWinningArmorAddonInventory
{
    private sealed record Provider(
        string PluginName,
        int LoadOrderIndex
    );

    public static SkyrimWinningArmorAddonInventoryResult Inspect(
        string dataRoot,
        SkyrimRuntimeLoadOrder loadOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            dataRoot
        );

        ArgumentNullException.ThrowIfNull(
            loadOrder
        );

        string fullDataRoot =
            Path.GetFullPath(dataRoot);

        if (!Directory.Exists(fullDataRoot))
        {
            throw new DirectoryNotFoundException(
                fullDataRoot
            );
        }

        SkyrimRuntimeLoadOrderEntry[] active =
            loadOrder
                .OrderedExplicitlyActiveEntries
                .OrderBy(entry =>
                    entry.LoadOrderIndex
                )
                .ToArray();

        var mods =
            new List<IModGetter>();

        var disposables =
            new List<IDisposable>();

        var providers =
            new Dictionary<FormKey, Provider>();

        var missingFiles =
            new List<string>();

        var readErrors =
            new List<SkyrimPluginReadError>();

        try
        {
            foreach (
                SkyrimRuntimeLoadOrderEntry entry
                in active)
            {
                string pluginPath =
                    Path.Combine(
                        fullDataRoot,
                        entry.PluginName
                    );

                if (!File.Exists(pluginPath))
                {
                    missingFiles.Add(
                        entry.PluginName
                    );

                    continue;
                }

                try
                {
                    var mod =
                        SkyrimMod.CreateFromBinaryOverlay(
                            pluginPath,
                            SkyrimRelease.SkyrimSE
                        );

                    try
                    {
                        FormKey[] armorAddonKeys =
                            mod.ArmorAddons
                                .RecordCache
                                .Keys
                                .ToArray();

                        foreach (
                            FormKey formKey
                            in armorAddonKeys)
                        {
                            providers[formKey] =
                                new Provider(
                                    PluginName:
                                        entry.PluginName,
                                    LoadOrderIndex:
                                        entry.LoadOrderIndex
                                );
                        }

                        mods.Add(mod);
                        disposables.Add(mod);
                    }
                    catch
                    {
                        mod.Dispose();
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    readErrors.Add(
                        new SkyrimPluginReadError(
                            PluginName:
                                entry.PluginName,
                            FullPath:
                                pluginPath,
                            Error:
                                ex.Message
                        )
                    );
                }
            }

            IArmorAddonGetter[] winningRecords =
                mods
                    .WinningOverrides<IArmorAddonGetter>(
                        false
                    )
                    .ToArray();

            var winners =
                new List<SkyrimWinningArmorAddonRecord>(
                    winningRecords.Length
                );

            foreach (
                IArmorAddonGetter winner
                in winningRecords)
            {
                if (!providers.TryGetValue(
                        winner.FormKey,
                        out Provider? provider))
                {
                    throw new InvalidOperationException(
                        $"No provider was recorded for " +
                        $"winning ArmorAddon {winner.FormKey}."
                    );
                }

                winners.Add(
                    new SkyrimWinningArmorAddonRecord(
                        FormKey:
                            winner.FormKey.ToString(),
                        EditorId:
                            winner.EditorID,
                        WinningPluginName:
                            provider.PluginName,
                        WinningLoadOrderIndex:
                            provider.LoadOrderIndex,
                        ModelReferences:
                            SkyrimArmorAddonModelReferenceExtractor
                                .Extract(winner)
                    )
                );
            }

            return new SkyrimWinningArmorAddonInventoryResult(
                DataRoot:
                    fullDataRoot,
                ExplicitlyActivePluginCount:
                    active.Length,
                PluginsOpened:
                    mods.Count,
                MissingPluginFiles:
                    missingFiles.ToArray(),
                ReadErrors:
                    readErrors.ToArray(),
                Winners:
                    winners.ToArray()
            );
        }
        finally
        {
            for (
                int index =
                    disposables.Count - 1;
                index >= 0;
                index--)
            {
                disposables[index].Dispose();
            }
        }
    }
}
