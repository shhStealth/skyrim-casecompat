namespace CaseCompat.Bethesda.Plugins;

/*
 * Consumer-level diagnostic state derived only from checkpoint-10E
 * projection evidence and aggregate winner-search completeness.
 *
 * IncompleteWinnerSearch deliberately outranks every path-local state:
 * when winner discovery was incomplete, CaseCompat cannot claim that the
 * retained consumer belongs to the true winning ArmorAddon population.
 *
 * The underlying checkpoint-10D interpretation is not discarded. It
 * remains available through the retained Consumer projection.
 */
public enum SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
{
    IncompleteWinnerSearch,
    LooseResolved,
    LooseUnresolved,
    IndeterminateEvidence
}

/*
 * One diagnostic classification for one checkpoint-10E consumer.
 *
 * Consumer is retained by reference rather than flattened into duplicate
 * provenance fields. Therefore the original ArmorAddon reference,
 * checkpoint-10D interpretation, checkpoint-10C path evidence, and
 * checkpoint-10A lookup remain recoverable from the source object.
 */
public sealed record SkyrimWinningArmorAddonSnapshotConsumerDiagnostic(
    SkyrimWinningArmorAddonSnapshotConsumerProjection Consumer,
    SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState State
)
{
    public SkyrimArmorAddonSnapshotLoosePathInterpretation
        PathInterpretation =>
            Consumer.PathInterpretation;
}

/*
 * Pure diagnostic classification of one complete checkpoint-10E
 * projection result.
 *
 * Projection remains the authority for aggregate winner-search
 * completeness and for the source consumer collection.
 *
 * This result does not infer provider/archive precedence, an overall
 * asset winner, canonical spelling, or repair eligibility.
 */
public sealed record SkyrimWinningArmorAddonSnapshotConsumerDiagnosticResult(
    SkyrimWinningArmorAddonSnapshotConsumerProjectionResult Projection,
    IReadOnlyList<SkyrimWinningArmorAddonSnapshotConsumerDiagnostic>
        Diagnostics
)
{
    public int DiagnosticCount =>
        Diagnostics.Count;

    public bool WinnerSearchComplete =>
        Projection.WinnerSearchComplete;
}
