using CaseCompat.Core.Repair;

namespace CaseCompat.Bethesda.Plugins;

/*
 * Aggregate authority state for composing independently projected winning
 * ArmorAddon and HeadPart consumer-spelling evidence.
 *
 * Complete requires both winning-record searches and both source projections
 * to be complete, followed by successful generic Core composition.
 *
 * IncompleteWinnerSearch deliberately dominates all source-local evidence.
 * If either winning-record population is incomplete, no source evidence is
 * inspected, salvaged, or composed.
 *
 * IndeterminateConsumerPathEvidence means both winning-record searches were
 * complete, but at least one source could not publish complete consumer-path
 * evidence or the supposedly complete generic evidence was structurally
 * invalid when recomposed by Core.
 *
 * Only Complete may publish composed consumer-spelling evidence.
 */
public enum SkyrimWinningConsumerSpellingEvidenceCompositionState
{
    Complete,
    IncompleteWinnerSearch,
    IndeterminateConsumerPathEvidence
}

/*
 * Read-only Bethesda orchestration result.
 *
 * Both source projection results are retained by reference so their individual
 * provenance, completeness state, and diagnostic errors remain recoverable
 * even when aggregate precedence selects IncompleteWinnerSearch.
 *
 * Evidence is generic Core consumer-spelling authority only. This result grants
 * no filesystem, provider/archive, physical-spelling, content-source, repair,
 * persistence, execution, rollback, or recovery authority.
 */
public sealed record SkyrimWinningConsumerSpellingEvidenceCompositionResult(
    SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult
        ArmorAddonProjection,
    SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult
        HeadPartProjection,
    SkyrimWinningConsumerSpellingEvidenceCompositionState State,
    IReadOnlyList<DataRelativePathAggregateConsumerSpellingEvidence> Evidence,
    string? Error
)
{
    public bool WinnerSearchComplete =>
        ArmorAddonProjection.WinnerSearchComplete &&
        HeadPartProjection.WinnerSearchComplete;

    public bool ConsumerPathEvidenceComplete =>
        State ==
        SkyrimWinningConsumerSpellingEvidenceCompositionState.Complete;

    public int EvidenceCount =>
        Evidence.Count;
}

/*
 * Completeness-aware Bethesda orchestration over the source-independent Core
 * consumer-spelling composer.
 *
 * Precedence:
 *
 *   IncompleteWinnerSearch
 *       > IndeterminateConsumerPathEvidence
 *       > Complete
 *
 * This is intentionally aggregate precedence rather than source precedence.
 * Neither ArmorAddon nor HeadPart wins a spelling disagreement. Only after both
 * source projections are authoritative are their genuine requested spellings
 * passed to Core, which unions and reclassifies them without source priority.
 */
public static class SkyrimWinningConsumerSpellingEvidenceComposer
{
    public static SkyrimWinningConsumerSpellingEvidenceCompositionResult
        Compose(
            SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult
                armorAddonProjection,
            SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult
                headPartProjection)
    {
        ArgumentNullException.ThrowIfNull(
            armorAddonProjection
        );

        ArgumentNullException.ThrowIfNull(
            headPartProjection
        );

        /*
         * Winner-search incompleteness is population-level incompleteness and
         * deliberately outranks every source-local path/evidence condition.
         *
         * Do not dereference either Evidence collection on this branch.
         */
        if (
            !armorAddonProjection.WinnerSearchComplete ||
            !headPartProjection.WinnerSearchComplete)
        {
            return new SkyrimWinningConsumerSpellingEvidenceCompositionResult(
                ArmorAddonProjection:
                    armorAddonProjection,
                HeadPartProjection:
                    headPartProjection,
                State:
                    SkyrimWinningConsumerSpellingEvidenceCompositionState
                        .IncompleteWinnerSearch,
                Evidence:
                    Array.Empty<
                        DataRelativePathAggregateConsumerSpellingEvidence
                    >(),
                Error:
                    null
            );
        }

        /*
         * At this point both winning-record populations are complete. If either
         * source still cannot publish complete consumer-path evidence, partial
         * authority must not be salvaged from the other source.
         */
        if (
            !armorAddonProjection.ConsumerPathEvidenceComplete ||
            !headPartProjection.ConsumerPathEvidenceComplete)
        {
            return Indeterminate(
                armorAddonProjection,
                headPartProjection,
                "One or more complete winning-record searches could not " +
                "establish complete consumer-path evidence."
            );
        }

        try
        {
            IReadOnlyList<
                DataRelativePathAggregateConsumerSpellingEvidence
            > evidence =
                DataRelativePathAggregateConsumerSpellingEvidenceComposer
                    .Compose(
                        new IReadOnlyList<
                            DataRelativePathAggregateConsumerSpellingEvidence
                        >[]
                        {
                            armorAddonProjection.Evidence,
                            headPartProjection.Evidence
                        }
                    );

            return new SkyrimWinningConsumerSpellingEvidenceCompositionResult(
                ArmorAddonProjection:
                    armorAddonProjection,
                HeadPartProjection:
                    headPartProjection,
                State:
                    SkyrimWinningConsumerSpellingEvidenceCompositionState
                        .Complete,
                Evidence:
                    evidence,
                Error:
                    null
            );
        }
        catch (ArgumentException ex)
        {
            /*
             * Public projection result records can be manually constructed.
             * Core revalidation is therefore the final fail-closed boundary
             * before composed consumer authority is published.
             */
            return Indeterminate(
                armorAddonProjection,
                headPartProjection,
                "Generic consumer-spelling composition rejected the source " +
                $"evidence: {ex.Message}"
            );
        }
    }

    private static SkyrimWinningConsumerSpellingEvidenceCompositionResult
        Indeterminate(
            SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult
                armorAddonProjection,
            SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult
                headPartProjection,
            string error)
    {
        return new SkyrimWinningConsumerSpellingEvidenceCompositionResult(
            ArmorAddonProjection:
                armorAddonProjection,
            HeadPartProjection:
                headPartProjection,
            State:
                SkyrimWinningConsumerSpellingEvidenceCompositionState
                    .IndeterminateConsumerPathEvidence,
            Evidence:
                Array.Empty<
                    DataRelativePathAggregateConsumerSpellingEvidence
                >(),
            Error:
                error
        );
    }
}
