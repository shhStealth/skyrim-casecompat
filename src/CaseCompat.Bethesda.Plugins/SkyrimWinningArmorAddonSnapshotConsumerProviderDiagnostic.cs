namespace CaseCompat.Bethesda.Plugins;

/*
 * Final observational provider diagnostic for the new descriptor-bound
 * ArmorAddon snapshot pipeline.
 *
 * This state surface intentionally does NOT reproduce the old
 * equivalent-candidate-search terminal states. The new snapshot pipeline
 * does not use that old resolver authority.
 */
public enum
    SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
{
    IncompleteWinnerSearch,
    LooseResolved,
    IndeterminateEvidence,

    IncompleteArchiveCandidateIndex,
    IncompleteRuntimeArchiveEvidence,

    LooseUnresolvedWithRuntimeArchiveWinner,
    LooseUnresolvedWithAmbiguousArchivePrecedence,
    LooseUnresolvedWithoutRuntimeArchiveProvider
}

/*
 * One final observational diagnostic for one checkpoint-10G-C consumer.
 *
 * Projection is retained by reference so all evidence remains recoverable:
 *
 *   10G-C consumer archive projection
 *   10G-B shared path archive evidence
 *   10F consumer diagnostic
 *   10E consumer projection
 *   10D path interpretation
 *   10C grouped requested-path evidence
 *   10A snapshot lookup
 *
 * This diagnostic does not imply canonical spelling or repair eligibility.
 */
public sealed record
    SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnostic(
        SkyrimWinningArmorAddonSnapshotConsumerArchiveProjection Projection,
        SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState State
    )
{
    public SkyrimWinningArmorAddonSnapshotConsumerDiagnostic Diagnostic =>
        Projection.Diagnostic;

    public SkyrimWinningArmorAddonSnapshotConsumerProjection Consumer =>
        Projection.Consumer;

    public SkyrimArmorAddonSnapshotLoosePathInterpretation
        PathInterpretation =>
            Projection.PathInterpretation;

    public SkyrimWinningArmorAddonSnapshotPathArchiveEvidence?
        PathArchiveEvidence =>
            Projection.PathArchiveEvidence;

    public SkyrimArchiveAssetProvider? WinningArchiveProvider =>
        State ==
        SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
            .LooseUnresolvedWithRuntimeArchiveWinner
            ? Projection
                .PathArchiveEvidence?
                .ArchivePrecedence
                .WinningProvider
            : null;
}

/*
 * Complete ordered final diagnostic projection.
 *
 * The exact checkpoint-10G-C result is retained as source authority.
 */
public sealed record
    SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticResult(
        SkyrimWinningArmorAddonSnapshotConsumerArchiveProjectionResult
            Projection,
        IReadOnlyList<
            SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnostic
        > Diagnostics
    )
{
    public int DiagnosticCount =>
        Diagnostics.Count;

    public bool WinnerSearchComplete =>
        Projection.WinnerSearchComplete;

    public bool ArchiveCandidateIndexComplete =>
        Projection.ArchiveCandidateIndexComplete;

    public bool RuntimeArchiveEvidenceComplete =>
        Projection.RuntimeArchiveEvidenceComplete;
}
