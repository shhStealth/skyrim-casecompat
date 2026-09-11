using CaseCompat.Core.LoadOrder;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;

namespace CaseCompat.Bethesda.Plugins;

public sealed record SkyrimWinningFurnitureRecord(
    string FormKey,
    string? EditorId,
    string WinningPluginName,
    int WinningLoadOrderIndex,
    IReadOnlyList<SkyrimFurnitureModelReference> ModelReferences
)
{
    public int ModelReferenceCount =>
        ModelReferences.Count;
}

public sealed record SkyrimWinningFurnitureInventoryResult(
    string DataRoot,
    int RuntimeActivePluginCount,
    int PluginsOpened,
    IReadOnlyList<string> MissingPluginFiles,
    IReadOnlyList<SkyrimPluginReadError> ReadErrors,
    IReadOnlyList<SkyrimWinningFurnitureRecord> Winners
)
{
    public bool SearchComplete =>
        MissingPluginFiles.Count == 0 &&
        ReadErrors.Count == 0;

    public int WinningFurnitureCount =>
        Winners.Count;

    public int WinningModelReferenceCount =>
        Winners.Sum(record =>
            record.ModelReferenceCount
        );
}

public static class SkyrimWinningFurnitureInventory
{
    private sealed record Provider(
        string PluginName,
        int LoadOrderIndex
    );

    public static SkyrimWinningFurnitureInventoryResult Inspect(
        string dataRoot,
        SkyrimRuntimePluginSet runtimePluginSet)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            dataRoot
        );

        ArgumentNullException.ThrowIfNull(
            runtimePluginSet
        );

        string fullDataRoot =
            Path.GetFullPath(
                dataRoot
            );

        if (!Directory.Exists(fullDataRoot))
        {
            throw new DirectoryNotFoundException(
                $"Skyrim Data root was not found: {fullDataRoot}"
            );
        }

        SkyrimRuntimePluginSetEntry[] active =
            runtimePluginSet
                .OrderedRuntimeActiveEntries
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
                SkyrimRuntimePluginSetEntry entry
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
                        FormKey[] furnitureKeys =
                            mod.Furniture
                                .RecordCache
                                .Keys
                                .ToArray();

                        foreach (
                            FormKey formKey
                            in furnitureKeys)
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

            IFurnitureGetter[] winningRecords =
                mods
                    .AsEnumerable()
                    .Reverse()
                    .WinningOverrides<IFurnitureGetter>(
                        false
                    )
                    .ToArray();

            var winners =
                new List<SkyrimWinningFurnitureRecord>(
                    winningRecords.Length
                );

            foreach (
                IFurnitureGetter winner
                in winningRecords)
            {
                if (!providers.TryGetValue(
                        winner.FormKey,
                        out Provider? provider))
                {
                    throw new InvalidOperationException(
                        $"No provider was recorded for " +
                        $"winning Furniture {winner.FormKey}."
                    );
                }

                winners.Add(
                    new SkyrimWinningFurnitureRecord(
                        FormKey:
                            winner.FormKey.ToString(),
                        EditorId:
                            winner.EditorID,
                        WinningPluginName:
                            provider.PluginName,
                        WinningLoadOrderIndex:
                            provider.LoadOrderIndex,
                        ModelReferences:
                            SkyrimFurnitureModelReferenceExtractor
                                .Extract(winner)
                    )
                );
            }

            return new SkyrimWinningFurnitureInventoryResult(
                DataRoot:
                    fullDataRoot,
                RuntimeActivePluginCount:
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
            foreach (
                IDisposable disposable
                in disposables)
            {
                disposable.Dispose();
            }
        }
    }
}
