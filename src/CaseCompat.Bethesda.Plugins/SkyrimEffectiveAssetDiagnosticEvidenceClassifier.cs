using CaseCompat.Core.Findings;

namespace CaseCompat.Bethesda.Plugins;

public static class SkyrimEffectiveAssetDiagnosticEvidenceClassifier
{
    public static SkyrimEffectiveAssetDiagnosticEvidenceState Classify(
        SkyrimEffectiveArmorAddonArchiveCandidateFinding finding,
        bool archiveCandidateIndexComplete,
        bool runtimeArchiveEvidenceComplete)
    {
        ArgumentNullException.ThrowIfNull(
            finding
        );

        EffectiveAssetReferenceEvidenceState looseState =
            finding.LooseEvidenceState;

        if (
            looseState ==
            EffectiveAssetReferenceEvidenceState
                .IncompleteWinnerSearch)
        {
            return SkyrimEffectiveAssetDiagnosticEvidenceState
                .IncompleteWinnerSearch;
        }

        if (
            looseState ==
            EffectiveAssetReferenceEvidenceState
                .LinuxResolvable)
        {
            return SkyrimEffectiveAssetDiagnosticEvidenceState
                .LooseResolvable;
        }

        if (!archiveCandidateIndexComplete)
        {
            return SkyrimEffectiveAssetDiagnosticEvidenceState
                .IncompleteArchiveCandidateIndex;
        }

        if (!runtimeArchiveEvidenceComplete)
        {
            return SkyrimEffectiveAssetDiagnosticEvidenceState
                .IncompleteRuntimeArchiveEvidence;
        }

        if (finding.ArchivePrecedence.HasWinner)
        {
            return SkyrimEffectiveAssetDiagnosticEvidenceState
                .LooseUnresolvedWithRuntimeArchiveWinner;
        }

        if (finding.ArchivePrecedence.IsAmbiguous)
        {
            return SkyrimEffectiveAssetDiagnosticEvidenceState
                .LooseUnresolvedWithAmbiguousArchivePrecedence;
        }

        if (
            finding.ArchivePrecedence.State !=
            SkyrimRuntimeArchivePrecedenceState
                .NoRuntimeEvidencedProvider)
        {
            throw new InvalidOperationException(
                "Archive precedence has neither a winner, ambiguity, " +
                "nor the no-provider state."
            );
        }

        if (
            looseState ==
            EffectiveAssetReferenceEvidenceState
                .IncompleteCandidateSearch)
        {
            return SkyrimEffectiveAssetDiagnosticEvidenceState
                .IncompleteLooseCandidateSearch;
        }

        return looseState switch
        {
            EffectiveAssetReferenceEvidenceState
                .UnresolvedNoEquivalent =>
                    SkyrimEffectiveAssetDiagnosticEvidenceState
                        .LooseUnresolvedNoProviderNoEquivalent,

            EffectiveAssetReferenceEvidenceState
                .UnresolvedUniqueEquivalent =>
                    SkyrimEffectiveAssetDiagnosticEvidenceState
                        .LooseUnresolvedNoProviderUniqueEquivalent,

            EffectiveAssetReferenceEvidenceState
                .UnresolvedAmbiguousEquivalent =>
                    SkyrimEffectiveAssetDiagnosticEvidenceState
                        .LooseUnresolvedNoProviderAmbiguousEquivalent,

            _ =>
                throw new InvalidOperationException(
                    $"Unexpected loose evidence state: {looseState}."
                )
        };
    }
}
