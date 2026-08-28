namespace CaseCompat.Core.LoadOrder;

[Flags]
public enum SkyrimRuntimePluginActivationSource
{
    None = 0,
    CoreMaster = 1 << 0,
    SkyrimCcc = 1 << 1,
    ExplicitPluginsTxt = 1 << 2
}

public sealed record SkyrimRuntimePluginSetEntry(
    int LoadOrderIndex,
    string PluginName,
    SkyrimRuntimePluginActivationSource ActivationSources
)
{
    public bool RuntimeActive =>
        ActivationSources !=
        SkyrimRuntimePluginActivationSource.None;

    public bool IsActivatedBy(
        SkyrimRuntimePluginActivationSource source)
    {
        return (ActivationSources & source) != 0;
    }
}

public sealed record SkyrimRuntimePluginSet(
    SkyrimRuntimeLoadOrder SourceLoadOrder,
    string SkyrimCccPath,
    int SkyrimCccEntryCount,
    IReadOnlyList<string> SkyrimCccPluginNames,
    IReadOnlyList<SkyrimRuntimePluginSetEntry> LoadOrderEntries,
    IReadOnlyList<string> MissingCoreMasters,
    IReadOnlyList<string> MissingSkyrimCccPlugins,
    IReadOnlyList<string> DuplicateSkyrimCccEntries
)
{
    public IReadOnlyList<SkyrimRuntimePluginSetEntry>
        OrderedRuntimeActiveEntries =>
            LoadOrderEntries
                .Where(entry =>
                    entry.RuntimeActive
                )
                .ToArray();

    public IReadOnlyList<SkyrimRuntimePluginSetEntry>
        LoadOrderOnlyEntries =>
            LoadOrderEntries
                .Where(entry =>
                    !entry.RuntimeActive
                )
                .ToArray();

    public int RuntimeActiveCount =>
        OrderedRuntimeActiveEntries.Count;

    public int LoadOrderOnlyCount =>
        LoadOrderOnlyEntries.Count;

    public bool IsConsistent =>
        SourceLoadOrder.IsConsistent &&
        MissingCoreMasters.Count == 0 &&
        MissingSkyrimCccPlugins.Count == 0 &&
        DuplicateSkyrimCccEntries.Count == 0;
}
