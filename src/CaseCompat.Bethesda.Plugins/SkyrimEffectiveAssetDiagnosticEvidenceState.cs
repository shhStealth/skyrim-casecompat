namespace CaseCompat.Bethesda.Plugins;

public enum SkyrimEffectiveAssetDiagnosticEvidenceState
{
    IncompleteWinnerSearch,
    IncompleteLooseCandidateSearch,
    IncompleteArchiveCandidateIndex,
    IncompleteRuntimeArchiveEvidence,

    LooseResolvable,

    LooseUnresolvedWithRuntimeArchiveWinner,
    LooseUnresolvedWithAmbiguousArchivePrecedence,

    LooseUnresolvedNoProviderNoEquivalent,
    LooseUnresolvedNoProviderUniqueEquivalent,
    LooseUnresolvedNoProviderAmbiguousEquivalent
}
