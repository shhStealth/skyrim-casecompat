using CaseCompat.Core.Repair;

namespace CaseCompat.Bethesda.Plugins;

/*
 * Aggregate authority state for composing independently projected winning
 * consumer-spelling evidence from any number of sources (ArmorAddon,
 * HeadPart, and further record/asset kinds as they're added).
 *
 * Complete requires every source's winning-record search and source
 * projection to be complete, followed by successful generic Core
 * composition.
 *
 * IncompleteWinnerSearch deliberately dominates all source-local evidence.
 * If any source's winning-record population is incomplete, no source
 * evidence is inspected, salvaged, or composed.
 *
 * IndeterminateConsumerPathEvidence means every winning-record search was
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
 * Every source's projection result is retained by reference (in Sources) so
 * its individual provenance, completeness state, and diagnostic errors
 * remain recoverable even when aggregate precedence selects
 * IncompleteWinnerSearch. ArmorAddonProjection/HeadPartProjection remain as
 * named lookups into Sources for the two original, most-established call
 * sites - newer sources are only reachable via Sources itself.
 *
 * Evidence is generic Core consumer-spelling authority only. This result grants
 * no filesystem, provider/archive, physical-spelling, content-source, repair,
 * persistence, execution, rollback, or recovery authority.
 */
public sealed record SkyrimWinningConsumerSpellingEvidenceCompositionResult(
    IReadOnlyList<ISkyrimWinningConsumerSpellingEvidenceSource> Sources,
    SkyrimWinningConsumerSpellingEvidenceCompositionState State,
    IReadOnlyList<DataRelativePathAggregateConsumerSpellingEvidence> Evidence,
    string? Error
)
{
    // Retained for the two original sources: most existing call sites (and
    // every test predating the N-source generalization) address ArmorAddon
    // and HeadPart by name rather than by iterating Sources.
    public SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult
        ArmorAddonProjection =>
        (SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult)
            Sources.Single(
                source =>
                    source.SourceName == "ArmorAddon"
            );

    public SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult
        HeadPartProjection =>
        (SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult)
            Sources.Single(
                source =>
                    source.SourceName == "HeadPart"
            );

    public SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjectionResult
        FurnitureProjection =>
        (SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjectionResult)
            Sources.Single(
                source =>
                    source.SourceName == "Furniture"
            );

    public SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjectionResult
        StaticProjection =>
        (SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjectionResult)
            Sources.Single(
                source =>
                    source.SourceName == "Static"
            );

    public SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionResult
        ContainerProjection =>
        (SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionResult)
            Sources.Single(
                source =>
                    source.SourceName == "Container"
            );

    public SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjectionResult
        TreeProjection =>
        (SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjectionResult)
            Sources.Single(
                source =>
                    source.SourceName == "Tree"
            );

    public bool WinnerSearchComplete =>
        Sources.All(
            source =>
                source.WinnerSearchComplete
        );

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
 * No source wins a spelling disagreement over another. Only after every
 * source projection is authoritative are their genuine requested spellings
 * passed to Core, which unions and reclassifies them without source priority.
 */
public static class SkyrimWinningConsumerSpellingEvidenceComposer
{
    public static SkyrimWinningConsumerSpellingEvidenceCompositionResult
        Compose(
            params ISkyrimWinningConsumerSpellingEvidenceSource[] sources)
    {
        ArgumentNullException.ThrowIfNull(
            sources
        );

        if (sources.Length == 0)
        {
            throw new ArgumentException(
                "At least one consumer-spelling evidence source is required.",
                nameof(sources)
            );
        }

        foreach (
            ISkyrimWinningConsumerSpellingEvidenceSource source
            in sources)
        {
            ArgumentNullException.ThrowIfNull(
                source,
                nameof(sources)
            );
        }

        /*
         * Winner-search incompleteness is population-level incompleteness and
         * deliberately outranks every source-local path/evidence condition.
         *
         * Do not dereference any source's Evidence collection on this branch.
         */
        if (sources.Any(
                source =>
                    !source.WinnerSearchComplete))
        {
            return new SkyrimWinningConsumerSpellingEvidenceCompositionResult(
                Sources:
                    sources,
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
         * At this point every source's winning-record population is
         * complete. If any source still cannot publish complete
         * consumer-path evidence, partial authority must not be salvaged
         * from the others.
         */
        if (sources.Any(
                source =>
                    !source.ConsumerPathEvidenceComplete))
        {
            return Indeterminate(
                sources,
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
                        sources
                            .Select(
                                source =>
                                    source.Evidence
                            )
                            .ToArray()
                    );

            return new SkyrimWinningConsumerSpellingEvidenceCompositionResult(
                Sources:
                    sources,
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
                sources,
                "Generic consumer-spelling composition rejected the source " +
                $"evidence: {ex.Message}"
            );
        }
    }

    private static SkyrimWinningConsumerSpellingEvidenceCompositionResult
        Indeterminate(
            IReadOnlyList<ISkyrimWinningConsumerSpellingEvidenceSource>
                sources,
            string error)
    {
        return new SkyrimWinningConsumerSpellingEvidenceCompositionResult(
            Sources:
                sources,
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
