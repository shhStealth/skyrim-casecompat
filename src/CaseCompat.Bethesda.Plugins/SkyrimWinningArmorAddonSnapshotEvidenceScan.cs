using CaseCompat.Core.Analysis;

namespace CaseCompat.Bethesda.Plugins;

/*
 * One winning ArmorAddon consumer of an exact requested Data-relative
 * spelling.
 *
 * The SkyrimArmorAddonModelReference preserves source/request provenance.
 * WinningPluginName and WinningLoadOrderIndex preserve the winning-record
 * context that supplied that reference.
 */
public sealed record SkyrimWinningArmorAddonSnapshotReferenceContext(
    string WinningPluginName,
    int WinningLoadOrderIndex,
    SkyrimArmorAddonModelReference Reference
);

/*
 * Path-level snapshot evidence shared only by consumers whose
 * DataRelativePath strings are exactly equal under StringComparer.Ordinal.
 *
 * The path-level fields are lifted from one checkpoint-10B-B invocation.
 * The representative 10B-B Reference itself is intentionally not exposed
 * as group-wide provenance; every affected winning reference is retained
 * separately in References.
 *
 * LookupProduced still does not mean that the requested file resolved.
 * The embedded checkpoint-10A lookup retains that authority.
 */
public sealed record SkyrimWinningArmorAddonSnapshotPathEvidence(
    string RequestedPath,
    IReadOnlyList<SkyrimWinningArmorAddonSnapshotReferenceContext>
        References,
    WindowsLogicalPath? RequestedRootLogicalPath,
    SkyrimArmorAddonSnapshotLookupEvidenceState State,
    int MatchingAnalysisCount,
    WindowsNamespaceAnalysis? SelectedAnalysis,
    WindowsNamespaceSnapshotFileLookup? Lookup,
    string? Error
)
{
    public int AffectedReferenceCount =>
        References.Count;

    public bool HasLookup =>
        State ==
            SkyrimArmorAddonSnapshotLookupEvidenceState
                .LookupProduced &&
        SelectedAnalysis is not null &&
        Lookup is not null;
}

/*
 * Parallel snapshot evidence for all winning ArmorAddon model references.
 *
 * Inventory remains the authority for winner-search completeness.
 * This result does not replace EffectiveAssetReferenceFinding and does
 * not infer provider/archive precedence, canonical spelling, or repair
 * eligibility.
 */
public sealed record SkyrimWinningArmorAddonSnapshotEvidenceScanResult(
    SkyrimWinningArmorAddonInventoryResult Inventory,
    IReadOnlyList<SkyrimWinningArmorAddonSnapshotPathEvidence> Paths
)
{
    public int ReferenceCount =>
        Paths.Sum(path =>
            path.AffectedReferenceCount
        );

    public int UniqueRequestedPathCount =>
        Paths.Count;

    public int AvoidedLookupCalls =>
        ReferenceCount -
        UniqueRequestedPathCount;

    public bool WinnerSearchComplete =>
        Inventory.SearchComplete;
}
