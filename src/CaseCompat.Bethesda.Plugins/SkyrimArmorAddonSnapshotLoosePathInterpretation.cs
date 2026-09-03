using CaseCompat.Core.Analysis;

namespace CaseCompat.Bethesda.Plugins;

/*
 * Coarse claim that may safely be made from one checkpoint-10C
 * path-evidence group.
 *
 * LooseUnresolved means the requested regular-file path is definitely
 * not resolved by the snapshot. It intentionally does not mean that
 * the physical name is simply absent; Missing, NotDirectory, and
 * NotFile remain distinguishable through Evidence.Lookup.State.
 *
 * IndeterminateEvidence means no definite loose-resolution claim may
 * be made from the available snapshot/composition evidence.
 */
public enum SkyrimArmorAddonSnapshotLoosePathInterpretationState
{
    LooseResolved,
    LooseUnresolved,
    IndeterminateEvidence
}

/*
 * Pure interpretation of one checkpoint-10C requested-path group.
 *
 * The complete source evidence is retained so the coarse state never
 * erases the original checkpoint-10B-B or checkpoint-10A state.
 *
 * Winner-search completeness remains owned by the enclosing checkpoint-10C
 * scan result and is intentionally not folded into this path-local claim.
 */
public sealed record SkyrimArmorAddonSnapshotLoosePathInterpretation(
    SkyrimWinningArmorAddonSnapshotPathEvidence Evidence,
    SkyrimArmorAddonSnapshotLoosePathInterpretationState State,
    bool EvidenceStructureValid,
    string? InterpretationError
)
{
    public bool Definitive =>
        EvidenceStructureValid &&
        State !=
            SkyrimArmorAddonSnapshotLoosePathInterpretationState
                .IndeterminateEvidence;

    public bool LooseResolves =>
        EvidenceStructureValid &&
        State ==
            SkyrimArmorAddonSnapshotLoosePathInterpretationState
                .LooseResolved;

    public WindowsNamespaceSnapshotFileLookupState? SnapshotState =>
        Evidence.Lookup?.State;
}
