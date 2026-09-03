namespace CaseCompat.Bethesda.Plugins;

/*
 * Shared archive/provider evidence for one definitive checkpoint-10D
 * LooseUnresolved requested-path interpretation.
 *
 * The path interpretation is retained rather than flattened. Therefore
 * checkpoint-10C requested-path provenance and all underlying snapshot
 * evidence remain recoverable.
 *
 * ArchiveCandidates preserves every provider returned by the existing
 * Windows-logical archive index. ArchivePrecedence is the observational
 * decision produced by the existing runtime-evidence resolver.
 *
 * This record does not make a final consumer diagnostic or any repair
 * eligibility claim.
 */
public sealed record SkyrimWinningArmorAddonSnapshotPathArchiveEvidence(
    SkyrimArmorAddonSnapshotLoosePathInterpretation PathInterpretation,
    IReadOnlyList<SkyrimArchiveAssetProvider> ArchiveCandidates,
    SkyrimRuntimeArchivePrecedenceDecision ArchivePrecedence
)
{
    public string RequestedPath =>
        PathInterpretation.Evidence.RequestedPath;

    public int ArchiveCandidateCount =>
        ArchiveCandidates.Count;

    public bool HasArchiveCandidates =>
        ArchiveCandidates.Count > 0;

    public IReadOnlyList<SkyrimArchiveAssetProvider>
        RuntimeEvidencedArchiveCandidates =>
            ArchivePrecedence.RuntimeEvidencedProviders;

    public bool HasWinningRuntimeArchiveProvider =>
        ArchivePrecedence.HasWinner;

    public bool HasAmbiguousRuntimeArchivePrecedence =>
        ArchivePrecedence.IsAmbiguous;
}

/*
 * Path-level archive evidence composed over an already-produced
 * checkpoint-10F diagnostic result.
 *
 * Paths contains only archive-eligible path interpretations:
 *
 *   - winner search complete
 *   - structurally valid checkpoint-10D evidence
 *   - definitive LooseUnresolved state
 *
 * LooseResolved and IndeterminateEvidence are intentionally absent.
 * Incomplete winner search suppresses archive evaluation entirely.
 *
 * Aggregate archive-index/runtime-evidence completeness is retained from
 * its original source results rather than copied onto every path.
 */
public sealed record
    SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceResult(
        SkyrimWinningArmorAddonSnapshotConsumerDiagnosticResult
            Diagnostics,
        SkyrimArchiveCandidateIndexResult ArchiveIndex,
        SkyrimRuntimeArchiveEvidenceResult RuntimeArchiveEvidence,
        IReadOnlyList<
            SkyrimWinningArmorAddonSnapshotPathArchiveEvidence
        > Paths
    )
{
    public int EvaluatedPathCount =>
        Paths.Count;

    public bool WinnerSearchComplete =>
        Diagnostics.WinnerSearchComplete;

    public bool ArchiveCandidateIndexComplete =>
        ArchiveIndex.SearchComplete;

    public bool RuntimeArchiveEvidenceComplete =>
        RuntimeArchiveEvidence.SearchComplete;
}
