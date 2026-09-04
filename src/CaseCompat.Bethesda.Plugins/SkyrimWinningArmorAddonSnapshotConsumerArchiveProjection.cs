namespace CaseCompat.Bethesda.Plugins;

/*
 * Consumer-level projection of already-composed checkpoint-10G-B
 * path archive evidence.
 *
 * Diagnostic is retained by reference. Therefore the exact checkpoint-10E
 * consumer, checkpoint-10D path interpretation, checkpoint-10C path
 * evidence, and checkpoint-10A namespace lookup remain recoverable.
 *
 * PathArchiveEvidence is the exact shared checkpoint-10G-B object for
 * archive-eligible LooseUnresolved consumers. It is null for consumers
 * whose checkpoint-10F state makes archive evidence inapplicable.
 *
 * No archive lookup, runtime precedence calculation, provider
 * classification, canonical-spelling choice, or repair decision occurs
 * in this model.
 */
public sealed record
    SkyrimWinningArmorAddonSnapshotConsumerArchiveProjection(
        SkyrimWinningArmorAddonSnapshotConsumerDiagnostic Diagnostic,
        SkyrimWinningArmorAddonSnapshotPathArchiveEvidence?
            PathArchiveEvidence
    )
{
    public SkyrimWinningArmorAddonSnapshotConsumerProjection Consumer =>
        Diagnostic.Consumer;

    public SkyrimArmorAddonSnapshotLoosePathInterpretation
        PathInterpretation =>
            Diagnostic.PathInterpretation;

    public SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
        DiagnosticState =>
            Diagnostic.State;

    public bool HasArchiveEvidence =>
        PathArchiveEvidence is not null;
}

/*
 * Complete consumer projection over one checkpoint-10G-B result.
 *
 * Consumer cardinality and ordering are inherited exactly from the
 * retained checkpoint-10F diagnostic collection.
 *
 * Aggregate archive-index and runtime-evidence completeness remain owned
 * by checkpoint 10G-B and are exposed only as derived conveniences.
 */
public sealed record
    SkyrimWinningArmorAddonSnapshotConsumerArchiveProjectionResult(
        SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceResult
            PathArchiveResult,
        IReadOnlyList<
            SkyrimWinningArmorAddonSnapshotConsumerArchiveProjection
        > Consumers
    )
{
    public int ConsumerCount =>
        Consumers.Count;

    public bool WinnerSearchComplete =>
        PathArchiveResult.WinnerSearchComplete;

    public bool ArchiveCandidateIndexComplete =>
        PathArchiveResult.ArchiveCandidateIndexComplete;

    public bool RuntimeArchiveEvidenceComplete =>
        PathArchiveResult.RuntimeArchiveEvidenceComplete;
}
