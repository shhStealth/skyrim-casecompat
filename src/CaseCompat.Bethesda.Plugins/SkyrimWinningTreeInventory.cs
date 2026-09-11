using CaseCompat.Core.LoadOrder;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;

namespace CaseCompat.Bethesda.Plugins;

public sealed record SkyrimWinningTreeRecord(
    string FormKey,
    string? EditorId,
    string WinningPluginName,
    int WinningLoadOrderIndex,
    IReadOnlyList<SkyrimTreeModelReference> ModelReferences
)
{
    public int ModelReferenceCount =>
        ModelReferences.Count;
}

public sealed record SkyrimWinningTreeInventoryResult(
    string DataRoot,
    int RuntimeActivePluginCount,
    int PluginsOpened,
    IReadOnlyList<string> MissingPluginFiles,
    IReadOnlyList<SkyrimPluginReadError> ReadErrors,
    IReadOnlyList<SkyrimWinningTreeRecord> Winners
)
{
    public bool SearchComplete =>
        MissingPluginFiles.Count == 0 &&
        ReadErrors.Count == 0;

    public int WinningTreeCount =>
        Winners.Count;

    public int WinningModelReferenceCount =>
        Winners.Sum(record =>
            record.ModelReferenceCount
        );
}

public static class SkyrimWinningTreeInventory
{
    private sealed record Provider(
        string PluginName,
        int LoadOrderIndex
    );

    public static SkyrimWinningTreeInventoryResult Inspect(
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
                        FormKey[] treeKeys =
                            mod.Trees
                                .RecordCache
                                .Keys
                                .ToArray();

                        foreach (
                            FormKey formKey
                            in treeKeys)
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

            ITreeGetter[] winningRecords =
                mods
                    .AsEnumerable()
                    .Reverse()
                    .WinningOverrides<ITreeGetter>(
                        false
                    )
                    .ToArray();

            var winners =
                new List<SkyrimWinningTreeRecord>(
                    winningRecords.Length
                );

            foreach (
                ITreeGetter winner
                in winningRecords)
            {
                if (!providers.TryGetValue(
                        winner.FormKey,
                        out Provider? provider))
                {
                    throw new InvalidOperationException(
                        $"No provider was recorded for " +
                        $"winning Tree {winner.FormKey}."
                    );
                }

                winners.Add(
                    new SkyrimWinningTreeRecord(
                        FormKey:
                            winner.FormKey.ToString(),
                        EditorId:
                            winner.EditorID,
                        WinningPluginName:
                            provider.PluginName,
                        WinningLoadOrderIndex:
                            provider.LoadOrderIndex,
                        ModelReferences:
                            SkyrimTreeModelReferenceExtractor
                                .Extract(winner)
                    )
                );
            }

            return new SkyrimWinningTreeInventoryResult(
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
