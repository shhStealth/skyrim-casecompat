namespace CaseCompat.Core.Findings;

public static class EffectiveAssetReferenceEvidenceClassifier
{
    public static EffectiveAssetReferenceEvidenceState Classify(
        EffectiveAssetReferenceFinding finding)
    {
        ArgumentNullException.ThrowIfNull(
            finding
        );

        return Classify(
            winnerSearchComplete:
                finding.WinnerSearchComplete,
            linuxResolves:
                finding.LinuxResolves,
            candidateSearchComplete:
                finding.CandidateSearchComplete,
            equivalentCandidateCount:
                finding.EquivalentCandidateCount
        );
    }

    public static EffectiveAssetReferenceEvidenceState Classify(
        bool winnerSearchComplete,
        bool linuxResolves,
        bool candidateSearchComplete,
        int equivalentCandidateCount)
    {
        if (equivalentCandidateCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(equivalentCandidateCount)
            );
        }

        if (!winnerSearchComplete)
        {
            return EffectiveAssetReferenceEvidenceState
                .IncompleteWinnerSearch;
        }

        if (linuxResolves)
        {
            return EffectiveAssetReferenceEvidenceState
                .LinuxResolvable;
        }

        if (!candidateSearchComplete)
        {
            return EffectiveAssetReferenceEvidenceState
                .IncompleteCandidateSearch;
        }

        return equivalentCandidateCount switch
        {
            0 =>
                EffectiveAssetReferenceEvidenceState
                    .UnresolvedNoEquivalent,

            1 =>
                EffectiveAssetReferenceEvidenceState
                    .UnresolvedUniqueEquivalent,

            _ =>
                EffectiveAssetReferenceEvidenceState
                    .UnresolvedAmbiguousEquivalent
        };
    }
}
