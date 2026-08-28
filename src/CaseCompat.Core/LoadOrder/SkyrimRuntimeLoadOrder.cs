namespace CaseCompat.Core.LoadOrder;

public sealed record SkyrimRuntimeLoadOrderEntry(
    int LoadOrderIndex,
    string PluginName,
    bool ExplicitlyActive
);

public sealed record SkyrimRuntimeLoadOrderOrderFailure(
    string PluginName,
    int LoadOrderIndex,
    int PreviousLoadOrderIndex
);

public sealed record SkyrimRuntimeLoadOrder(
    string PluginsPath,
    string LoadOrderPath,
    int PluginsFileEntryCount,
    IReadOnlyList<string> ExplicitlyActivePluginNames,
    IReadOnlyList<SkyrimRuntimeLoadOrderEntry> LoadOrderEntries,
    IReadOnlyList<string> MissingActivePlugins,
    IReadOnlyList<string> DuplicatePluginsFileEntries,
    IReadOnlyList<string> DuplicateLoadOrderEntries,
    IReadOnlyList<SkyrimRuntimeLoadOrderOrderFailure> RelativeOrderFailures
)
{
    public int ExplicitlyActiveCount =>
        ExplicitlyActivePluginNames.Count;

    public int LoadOrderEntryCount =>
        LoadOrderEntries.Count;

    public IReadOnlyList<SkyrimRuntimeLoadOrderEntry>
        OrderedExplicitlyActiveEntries =>
            LoadOrderEntries
                .Where(entry =>
                    entry.ExplicitlyActive
                )
                .ToArray();

    public bool IsConsistent =>
        MissingActivePlugins.Count == 0 &&
        DuplicatePluginsFileEntries.Count == 0 &&
        DuplicateLoadOrderEntries.Count == 0 &&
        RelativeOrderFailures.Count == 0;
}
