using CaseCompat.Core.LoadOrder;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;

namespace CaseCompat.Bethesda.Plugins;

public sealed record SkyrimWinningHeadPartRecord(
    string FormKey,
    string? EditorId,
    string WinningPluginName,
    int WinningLoadOrderIndex,
    IReadOnlyList<SkyrimHeadPartPartReference> PartReferences
)
{
    public int PartReferenceCount =>
        PartReferences.Count;
}

public sealed record SkyrimWinningHeadPartInventoryResult(
    string DataRoot,
    int RuntimeActivePluginCount,
    int PluginsOpened,
    IReadOnlyList<string> MissingPluginFiles,
    IReadOnlyList<SkyrimPluginReadError> ReadErrors,
    IReadOnlyList<SkyrimWinningHeadPartRecord> Winners
)
{
    public bool SearchComplete =>
        MissingPluginFiles.Count == 0 &&
        ReadErrors.Count == 0;

    public int WinningHeadPartCount =>
        Winners.Count;

    public int WinningPartReferenceCount =>
        Winners.Sum(record =>
            record.PartReferenceCount
        );
}

public static class SkyrimWinningHeadPartInventory
{
    private sealed record Provider(
        string PluginName,
        int LoadOrderIndex
    );

    public static SkyrimWinningHeadPartInventoryResult Inspect(
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
                        FormKey[] headPartKeys =
                            mod.HeadParts
                                .RecordCache
                                .Keys
                                .ToArray();

                        foreach (
                            FormKey formKey
                            in headPartKeys)
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

            IHeadPartGetter[] winningRecords =
                mods
                    .AsEnumerable()
                    .Reverse()
                    .WinningOverrides<IHeadPartGetter>(
                        false
                    )
                    .ToArray();

            var winners =
                new List<SkyrimWinningHeadPartRecord>(
                    winningRecords.Length
                );

            foreach (
                IHeadPartGetter winner
                in winningRecords)
            {
                if (!providers.TryGetValue(
                        winner.FormKey,
                        out Provider? provider))
                {
                    throw new InvalidOperationException(
                        $"No provider was recorded for " +
                        $"winning HeadPart {winner.FormKey}."
                    );
                }

                winners.Add(
                    new SkyrimWinningHeadPartRecord(
                        FormKey:
                            winner.FormKey.ToString(),
                        EditorId:
                            winner.EditorID,
                        WinningPluginName:
                            provider.PluginName,
                        WinningLoadOrderIndex:
                            provider.LoadOrderIndex,
                        PartReferences:
                            SkyrimHeadPartPartReferenceExtractor
                                .Extract(winner)
                    )
                );
            }

            return new SkyrimWinningHeadPartInventoryResult(
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
