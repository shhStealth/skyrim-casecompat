namespace CaseCompat.Bethesda.Plugins;

public static class SkyrimEffectiveAssetProviderEvidenceClassifier
{
    public static SkyrimEffectiveAssetProviderEvidenceState Classify(
        SkyrimEffectiveArmorAddonArchiveCandidateFinding finding,
        bool archiveCandidateIndexComplete,
        bool runtimeArchiveEvidenceComplete)
    {
        ArgumentNullException.ThrowIfNull(
            finding
        );

        return Classify(
            winnerSearchComplete:
                finding.EffectiveFinding.WinnerSearchComplete,
            linuxResolves:
                finding.EffectiveFinding.LinuxResolves,
            archiveCandidateIndexComplete:
                archiveCandidateIndexComplete,
            runtimeArchiveEvidenceComplete:
                runtimeArchiveEvidenceComplete,
            archivePrecedence:
                finding.ArchivePrecedence
        );
    }

    public static SkyrimEffectiveAssetProviderEvidenceState Classify(
        bool winnerSearchComplete,
        bool linuxResolves,
        bool archiveCandidateIndexComplete,
        bool runtimeArchiveEvidenceComplete,
        SkyrimRuntimeArchivePrecedenceDecision archivePrecedence)
    {
        ArgumentNullException.ThrowIfNull(
            archivePrecedence
        );

        if (!winnerSearchComplete)
        {
            return SkyrimEffectiveAssetProviderEvidenceState
                .IncompleteWinnerSearch;
        }

        if (linuxResolves)
        {
            return SkyrimEffectiveAssetProviderEvidenceState
                .LooseResolvable;
        }

        if (!archiveCandidateIndexComplete)
        {
            return SkyrimEffectiveAssetProviderEvidenceState
                .IncompleteArchiveCandidateIndex;
        }

        if (!runtimeArchiveEvidenceComplete)
        {
            return SkyrimEffectiveAssetProviderEvidenceState
                .IncompleteRuntimeArchiveEvidence;
        }

        if (archivePrecedence.HasWinner)
        {
            return SkyrimEffectiveAssetProviderEvidenceState
                .LooseUnresolvedWithRuntimeArchiveWinner;
        }

        if (archivePrecedence.IsAmbiguous)
        {
            return SkyrimEffectiveAssetProviderEvidenceState
                .LooseUnresolvedWithAmbiguousArchivePrecedence;
        }

        if (
            archivePrecedence.State ==
            SkyrimRuntimeArchivePrecedenceState
                .NoRuntimeEvidencedProvider)
        {
            return SkyrimEffectiveAssetProviderEvidenceState
                .LooseUnresolvedWithoutRuntimeArchiveProvider;
        }

        throw new InvalidOperationException(
            "Archive precedence has neither a winner nor an " +
            "recognized unresolved state."
        );
    }
}
