using CaseCompat.Core.Repair;

namespace CaseCompat.Bethesda.Plugins;

/*
 * Completeness state for joining winning Bethesda consumer spelling authority
 * to validated aggregate physical/content evidence.
 *
 * IncompleteWinnerSearch and IndeterminateConsumerPathEvidence are inherited
 * directly from the C4D-5B authority boundary. Neither state may be converted
 * into per-leaf NoConsumerEvidence.
 *
 * IndeterminateAggregateEvidence means C4D-5B claimed complete consumer
 * authority, but the aggregate manifest / physical-spelling join rejected the
 * supposedly complete inputs or could not establish its structural invariant.
 *
 * Only Complete may publish joined consumer/physical spelling evidence.
 */
public enum SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionState
{
    Complete,
    IncompleteWinnerSearch,
    IndeterminateConsumerPathEvidence,
    IndeterminateAggregateEvidence
}

/*
 * Read-only Bethesda orchestration result.
 *
 * Manifest and ConsumerSpellingComposition are retained by reference so both
 * physical/content provenance and winning-consumer provenance remain
 * recoverable without flattening either evidence dimension.
 *
 * Evidence is the orthogonal Core C4D-6B relation. This result grants no
 * provider precedence, source selection, repair eligibility, repair planning,
 * persistence, authorization, execution, rollback, or recovery authority.
 */
public sealed record
    SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionResult(
        DataRelativePathAggregateNamespaceManifestRecord Manifest,
        SkyrimWinningConsumerSpellingEvidenceCompositionResult
            ConsumerSpellingComposition,
        SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionState State,
        IReadOnlyList<
            DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingEvidence
        > Evidence,
        string? Error
    )
{
    public bool ConsumerPhysicalSpellingEvidenceComplete =>
        State ==
        SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionState.Complete;

    public int EvidenceCount =>
        Evidence.Count;
}

/*
 * Completeness-aware Bethesda orchestration over the pure Core C4D-6B join.
 *
 * C4D-5B remains the consumer-authority boundary:
 *
 *   IncompleteWinnerSearch
 *       -> publish no aggregate relation.
 *
 *   IndeterminateConsumerPathEvidence
 *       -> publish no aggregate relation.
 *
 *   Complete
 *       -> and only then pass its generic Core evidence into C4D-6B.
 *
 * The non-complete branches deliberately do not inspect the C4D-5B Evidence
 * collection and do not validate/project the manifest. This prevents an
 * incomplete consumer population from being misrepresented as
 * NoConsumerEvidence for physical leaves.
 *
 * A supposedly complete join that Core rejects fails closed as
 * IndeterminateAggregateEvidence.
 */
public static class SkyrimWinningConsumerPhysicalSpellingEvidenceProjector
{
    public static SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionResult
        Project(
            DataRelativePathAggregateNamespaceManifestRecord manifest,
            SkyrimWinningConsumerSpellingEvidenceCompositionResult
                consumerSpellingComposition)
    {
        ArgumentNullException.ThrowIfNull(
            manifest
        );

        ArgumentNullException.ThrowIfNull(
            consumerSpellingComposition
        );

        switch (consumerSpellingComposition.State)
        {
            case
                SkyrimWinningConsumerSpellingEvidenceCompositionState
                    .IncompleteWinnerSearch:
                return Terminal(
                    manifest,
                    consumerSpellingComposition,
                    SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionState
                        .IncompleteWinnerSearch,
                    consumerSpellingComposition.Error
                );

            case
                SkyrimWinningConsumerSpellingEvidenceCompositionState
                    .IndeterminateConsumerPathEvidence:
                return Terminal(
                    manifest,
                    consumerSpellingComposition,
                    SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionState
                        .IndeterminateConsumerPathEvidence,
                    consumerSpellingComposition.Error
                );

            case
                SkyrimWinningConsumerSpellingEvidenceCompositionState
                    .Complete:
                break;

            default:
                return Terminal(
                    manifest,
                    consumerSpellingComposition,
                    SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionState
                        .IndeterminateAggregateEvidence,
                    "The consumer-spelling composition has an unrecognized " +
                    $"state value: {consumerSpellingComposition.State}."
                );
        }

        try
        {
            IReadOnlyList<
                DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingEvidence
            > evidence =
                DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingProjector
                    .Project(
                        manifest,
                        consumerSpellingComposition.Evidence
                    );

            return new
                SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionResult(
                    Manifest:
                        manifest,
                    ConsumerSpellingComposition:
                        consumerSpellingComposition,
                    State:
                        SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionState
                            .Complete,
                    Evidence:
                        evidence,
                    Error:
                        null
                );
        }
        catch (ArgumentException ex)
        {
            return IndeterminateAggregate(
                manifest,
                consumerSpellingComposition,
                ex
            );
        }
        catch (InvalidOperationException ex)
        {
            return IndeterminateAggregate(
                manifest,
                consumerSpellingComposition,
                ex
            );
        }
    }

    private static
        SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionResult
        IndeterminateAggregate(
            DataRelativePathAggregateNamespaceManifestRecord manifest,
            SkyrimWinningConsumerSpellingEvidenceCompositionResult
                consumerSpellingComposition,
            Exception exception)
    {
        return Terminal(
            manifest,
            consumerSpellingComposition,
            SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionState
                .IndeterminateAggregateEvidence,
            "Aggregate consumer/physical spelling projection rejected the " +
            $"supposedly complete evidence: {exception.Message}"
        );
    }

    private static
        SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionResult
        Terminal(
            DataRelativePathAggregateNamespaceManifestRecord manifest,
            SkyrimWinningConsumerSpellingEvidenceCompositionResult
                consumerSpellingComposition,
            SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionState state,
            string? error)
    {
        return new
            SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionResult(
                Manifest:
                    manifest,
                ConsumerSpellingComposition:
                    consumerSpellingComposition,
                State:
                    state,
                Evidence:
                    Array.Empty<
                        DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingEvidence
                    >(),
                Error:
                    error
            );
    }
}
