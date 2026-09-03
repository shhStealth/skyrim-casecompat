namespace CaseCompat.Bethesda.Plugins;

/*
 * One winning ArmorAddon consumer projected from checkpoint-10C path
 * evidence and its checkpoint-10D path-level interpretation.
 *
 * Reference remains the original SkyrimArmorAddonModelReference retained
 * by checkpoint 10C. Its FormKey, EditorId, Field, GivenPath, and
 * DataRelativePath are intentionally not copied into parallel fields.
 *
 * PathInterpretation is shared by reference with every consumer belonging
 * to the same exact checkpoint-10C requested-path group.
 */
public sealed record SkyrimWinningArmorAddonSnapshotConsumerProjection(
    string WinningPluginName,
    int WinningLoadOrderIndex,
    SkyrimArmorAddonModelReference Reference,
    SkyrimArmorAddonSnapshotLoosePathInterpretation PathInterpretation
)
{
    public SkyrimArmorAddonSnapshotLoosePathInterpretationState State =>
        PathInterpretation.State;
}

/*
 * Pure consumer projection over one already-produced checkpoint-10C scan.
 *
 * PathInterpretations contains exactly one checkpoint-10D interpretation
 * for every input path group. Consumers then point back to those exact
 * interpretation objects; checkpoint 10D is not recomputed per consumer.
 *
 * Winner-search completeness remains aggregate inventory/scan evidence and
 * is not folded into the path-local interpretation or copied onto every
 * consumer.
 *
 * This result does not replace EffectiveAssetReferenceFinding and does not
 * infer provider/archive precedence, an overall asset winner, canonical
 * spelling, or repair eligibility.
 */
public sealed record SkyrimWinningArmorAddonSnapshotConsumerProjectionResult(
    SkyrimWinningArmorAddonSnapshotEvidenceScanResult Scan,
    IReadOnlyList<SkyrimArmorAddonSnapshotLoosePathInterpretation>
        PathInterpretations,
    IReadOnlyList<SkyrimWinningArmorAddonSnapshotConsumerProjection>
        Consumers
)
{
    public int PathInterpretationCount =>
        PathInterpretations.Count;

    public int ConsumerCount =>
        Consumers.Count;

    public bool WinnerSearchComplete =>
        Scan.WinnerSearchComplete;
}
