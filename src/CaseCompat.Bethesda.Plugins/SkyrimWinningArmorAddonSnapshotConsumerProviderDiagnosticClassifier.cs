namespace CaseCompat.Bethesda.Plugins;

/*
 * Pure checkpoint-10G-D final diagnostic interpretation.
 *
 * This classifier consumes only already-produced checkpoint-10G-C
 * projections and retained aggregate completeness evidence.
 *
 * It performs no:
 *   - filesystem access,
 *   - plugin parsing,
 *   - loose-path resolution,
 *   - Windows namespace lookup,
 *   - archive candidate lookup,
 *   - runtime archive precedence calculation,
 *   - canonical-spelling inference,
 *   - repair-eligibility inference.
 */
public static class
    SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticClassifier
{
    public static
        SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticResult
        Classify(
            SkyrimWinningArmorAddonSnapshotConsumerArchiveProjectionResult
                projection)
    {
        ArgumentNullException.ThrowIfNull(
            projection
        );

        if (projection.PathArchiveResult is null)
        {
            throw new ArgumentException(
                "The checkpoint-10G-C result must retain its " +
                "checkpoint-10G-B path archive result.",
                nameof(projection)
            );
        }

        if (projection.Consumers is null)
        {
            throw new ArgumentException(
                "The checkpoint-10G-C result must retain its consumer " +
                "projection collection.",
                nameof(projection)
            );
        }

        bool winnerSearchComplete =
            projection.WinnerSearchComplete;

        bool archiveCandidateIndexComplete =
            projection.ArchiveCandidateIndexComplete;

        bool runtimeArchiveEvidenceComplete =
            projection.RuntimeArchiveEvidenceComplete;

        var diagnostics =
            new List<
                SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnostic
            >(
                projection.Consumers.Count
            );

        foreach (
            SkyrimWinningArmorAddonSnapshotConsumerArchiveProjection?
                consumer
            in projection.Consumers)
        {
            if (consumer is null)
            {
                throw new ArgumentException(
                    "The checkpoint-10G-C consumer collection must not " +
                    "contain null entries.",
                    nameof(projection)
                );
            }

            diagnostics.Add(
                new
                    SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnostic(
                        Projection:
                            consumer,
                        State:
                            ClassifyConsumer(
                                consumer,
                                winnerSearchComplete,
                                archiveCandidateIndexComplete,
                                runtimeArchiveEvidenceComplete
                            )
                    )
            );
        }

        return new
            SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticResult(
                Projection:
                    projection,
                Diagnostics:
                    diagnostics.ToArray()
            );
    }

    private static
        SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
        ClassifyConsumer(
            SkyrimWinningArmorAddonSnapshotConsumerArchiveProjection
                consumer,
            bool winnerSearchComplete,
            bool archiveCandidateIndexComplete,
            bool runtimeArchiveEvidenceComplete)
    {
        /*
         * Aggregate winner-search completeness remains the outermost
         * authority, matching checkpoint 10F and the old provider/final
         * classifiers.
         */
        if (!winnerSearchComplete)
        {
            return
                SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
                    .IncompleteWinnerSearch;
        }

        SkyrimWinningArmorAddonSnapshotConsumerDiagnostic?
            sourceDiagnostic =
                consumer.Diagnostic;

        if (sourceDiagnostic is null ||
            !Enum.IsDefined(
                typeof(
                    SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                ),
                sourceDiagnostic.State
            ))
        {
            return
                SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
                    .IndeterminateEvidence;
        }

        switch (sourceDiagnostic.State)
        {
            case
                SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                    .IncompleteWinnerSearch:
                /*
                 * Aggregate winner search is complete, so a retained
                 * per-consumer IncompleteWinnerSearch state is structurally
                 * inconsistent.
                 */
                return
                    SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
                        .IndeterminateEvidence;

            case
                SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                    .LooseResolved:
                /*
                 * Loose resolution terminates before archive completeness
                 * or precedence is considered.
                 */
                return
                    SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
                        .LooseResolved;

            case
                SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                    .IndeterminateEvidence:
                /*
                 * Checkpoint 10G-B deliberately does not archive-evaluate
                 * indeterminate loose evidence. Archive evidence therefore
                 * cannot rescue or replace this state.
                 */
                return
                    SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
                        .IndeterminateEvidence;

            case
                SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                    .LooseUnresolved:
                return ClassifyLooseUnresolved(
                    consumer,
                    archiveCandidateIndexComplete,
                    runtimeArchiveEvidenceComplete
                );

            default:
                return
                    SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
                        .IndeterminateEvidence;
        }
    }

    private static
        SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
        ClassifyLooseUnresolved(
            SkyrimWinningArmorAddonSnapshotConsumerArchiveProjection
                consumer,
            bool archiveCandidateIndexComplete,
            bool runtimeArchiveEvidenceComplete)
    {
        SkyrimWinningArmorAddonSnapshotPathArchiveEvidence?
            pathEvidence =
                consumer.PathArchiveEvidence;

        /*
         * Production checkpoint 10G-C requires an exact shared 10G-B
         * evidence object for every LooseUnresolved consumer.
         *
         * Synthetic malformed input fails closed.
         */
        if (pathEvidence is null ||
            pathEvidence.ArchivePrecedence is null)
        {
            return
                SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
                    .IndeterminateEvidence;
        }

        /*
         * Preserve the proven old provider ordering:
         *
         *   archive-index completeness
         *   runtime-evidence completeness
         *   already-computed precedence
         */
        if (!archiveCandidateIndexComplete)
        {
            return
                SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
                    .IncompleteArchiveCandidateIndex;
        }

        if (!runtimeArchiveEvidenceComplete)
        {
            return
                SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
                    .IncompleteRuntimeArchiveEvidence;
        }

        SkyrimRuntimeArchivePrecedenceDecision precedence =
            pathEvidence.ArchivePrecedence;

        if (precedence.HasWinner)
        {
            if (precedence.IsAmbiguous)
            {
                return
                    SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
                        .IndeterminateEvidence;
            }

            return
                SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
                    .LooseUnresolvedWithRuntimeArchiveWinner;
        }

        if (precedence.IsAmbiguous)
        {
            return
                SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
                    .LooseUnresolvedWithAmbiguousArchivePrecedence;
        }

        if (
            precedence.State ==
            SkyrimRuntimeArchivePrecedenceState
                .NoRuntimeEvidencedProvider)
        {
            return
                SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
                    .LooseUnresolvedWithoutRuntimeArchiveProvider;
        }

        /*
         * Example malformed synthetic state:
         * SingleRuntimeEvidencedProvider with no WinningProvider.
         *
         * Production precedence resolution cannot produce that shape.
         */
        return
            SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
                .IndeterminateEvidence;
    }
}
