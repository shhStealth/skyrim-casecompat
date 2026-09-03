namespace CaseCompat.Bethesda.Plugins;

/*
 * Pure checkpoint-10F classification.
 *
 * No filesystem access, loose-path resolution, namespace lookup,
 * provider/archive precedence, hashing, canonical-spelling selection, or
 * repair decision occurs here.
 *
 * Aggregate winner-search incompleteness dominates every consumer-local
 * checkpoint-10D path state, while the complete path evidence remains
 * retained inside the consumer projection.
 */
public static class SkyrimWinningArmorAddonSnapshotConsumerDiagnosticClassifier
{
    public static SkyrimWinningArmorAddonSnapshotConsumerDiagnosticResult
        Classify(
            SkyrimWinningArmorAddonSnapshotConsumerProjectionResult
                projection)
    {
        ArgumentNullException.ThrowIfNull(
            projection
        );

        if (projection.Scan is null)
        {
            throw new ArgumentException(
                "The checkpoint-10E projection must retain its " +
                "checkpoint-10C scan.",
                nameof(projection)
            );
        }

        if (projection.Consumers is null)
        {
            throw new ArgumentException(
                "The checkpoint-10E projection must retain its consumer " +
                "collection.",
                nameof(projection)
            );
        }

        bool winnerSearchComplete =
            projection.WinnerSearchComplete;

        var diagnostics =
            new List<
                SkyrimWinningArmorAddonSnapshotConsumerDiagnostic
            >(
                projection.Consumers.Count
            );

        foreach (
            SkyrimWinningArmorAddonSnapshotConsumerProjection? consumer
            in projection.Consumers)
        {
            if (consumer is null)
            {
                throw new ArgumentException(
                    "The checkpoint-10E consumer collection must not " +
                    "contain null entries.",
                    nameof(projection)
                );
            }

            diagnostics.Add(
                new SkyrimWinningArmorAddonSnapshotConsumerDiagnostic(
                    Consumer:
                        consumer,
                    State:
                        ClassifyConsumer(
                            winnerSearchComplete,
                            consumer.PathInterpretation
                        )
                )
            );
        }

        return new SkyrimWinningArmorAddonSnapshotConsumerDiagnosticResult(
            Projection:
                projection,
            Diagnostics:
                diagnostics.ToArray()
        );
    }

    private static SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
        ClassifyConsumer(
            bool winnerSearchComplete,
            SkyrimArmorAddonSnapshotLoosePathInterpretation?
                interpretation)
    {
        /*
         * Winner completeness is deliberately first.
         *
         * Even a path that resolved in the observed snapshot cannot be
         * presented as the winning consumer's definitive diagnostic when
         * the winning-record search itself was incomplete.
         */
        if (!winnerSearchComplete)
        {
            return SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                .IncompleteWinnerSearch;
        }

        /*
         * Production checkpoint-10E consumers always retain a checkpoint-10D
         * interpretation. Synthetic malformed input fails closed.
         */
        if (interpretation is null ||
            interpretation.Evidence is null ||
            !interpretation.EvidenceStructureValid ||
            interpretation.InterpretationError is not null ||
            !Enum.IsDefined(
                typeof(
                    SkyrimArmorAddonSnapshotLoosePathInterpretationState
                ),
                interpretation.State
            ))
        {
            return SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                .IndeterminateEvidence;
        }

        return interpretation.State switch
        {
            SkyrimArmorAddonSnapshotLoosePathInterpretationState
                .LooseResolved =>
                    SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                        .LooseResolved,

            SkyrimArmorAddonSnapshotLoosePathInterpretationState
                .LooseUnresolved =>
                    SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                        .LooseUnresolved,

            SkyrimArmorAddonSnapshotLoosePathInterpretationState
                .IndeterminateEvidence =>
                    SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                        .IndeterminateEvidence,

            _ =>
                SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                    .IndeterminateEvidence
        };
    }
}
