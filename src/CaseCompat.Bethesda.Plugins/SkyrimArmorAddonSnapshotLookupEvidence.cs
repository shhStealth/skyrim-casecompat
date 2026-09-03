using CaseCompat.Core.Analysis;

namespace CaseCompat.Bethesda.Plugins;

/*
 * Pure composition evidence connecting one genuine ArmorAddon model
 * reference to one Windows-namespace snapshot lookup.
 *
 * This wrapper says only whether a matching analyzed namespace could be
 * selected and, when it could, preserves the complete checkpoint-10A
 * lookup result.
 *
 * LookupProduced does not mean that the requested file resolved.
 * Resolution, absence, ambiguity, and indeterminate lookup semantics
 * remain properties of the embedded WindowsNamespaceSnapshotFileLookup.
 *
 * This model does not infer:
 *
 * - loose-file provider precedence;
 * - archive/provider winners;
 * - canonical spelling;
 * - repair eligibility.
 */
public enum SkyrimArmorAddonSnapshotLookupEvidenceState
{
    InvalidRequestedPath,
    NoMatchingNamespaceAnalysis,
    AmbiguousMatchingNamespaceAnalysis,
    LookupProduced
}

public sealed record SkyrimArmorAddonSnapshotLookupEvidence(
    SkyrimArmorAddonModelReference Reference,
    WindowsLogicalPath? RequestedRootLogicalPath,
    SkyrimArmorAddonSnapshotLookupEvidenceState State,
    int MatchingAnalysisCount,
    WindowsNamespaceAnalysis? SelectedAnalysis,
    WindowsNamespaceSnapshotFileLookup? Lookup,
    string? Error
)
{
    public bool HasLookup =>
        State ==
            SkyrimArmorAddonSnapshotLookupEvidenceState
                .LookupProduced &&
        SelectedAnalysis is not null &&
        Lookup is not null;
}
