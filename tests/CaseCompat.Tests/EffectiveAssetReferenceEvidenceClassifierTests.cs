using CaseCompat.Core.Findings;
using Xunit;

namespace CaseCompat.Tests;

public sealed class EffectiveAssetReferenceEvidenceClassifierTests
{
    [Fact]
    public void IncompleteWinnerSearch_TakesPriority()
    {
        EffectiveAssetReferenceEvidenceState state =
            EffectiveAssetReferenceEvidenceClassifier.Classify(
                winnerSearchComplete: false,
                linuxResolves: false,
                candidateSearchComplete: true,
                equivalentCandidateCount: 1
            );

        Assert.Equal(
            EffectiveAssetReferenceEvidenceState
                .IncompleteWinnerSearch,
            state
        );
    }

    [Fact]
    public void LinuxResolvable_IgnoresEquivalentCandidateCount()
    {
        EffectiveAssetReferenceEvidenceState state =
            EffectiveAssetReferenceEvidenceClassifier.Classify(
                winnerSearchComplete: true,
                linuxResolves: true,
                candidateSearchComplete: true,
                equivalentCandidateCount: 2
            );

        Assert.Equal(
            EffectiveAssetReferenceEvidenceState
                .LinuxResolvable,
            state
        );
    }

    [Fact]
    public void UnresolvedWithIncompleteCandidateSearch_IsIncomplete()
    {
        EffectiveAssetReferenceEvidenceState state =
            EffectiveAssetReferenceEvidenceClassifier.Classify(
                winnerSearchComplete: true,
                linuxResolves: false,
                candidateSearchComplete: false,
                equivalentCandidateCount: 1
            );

        Assert.Equal(
            EffectiveAssetReferenceEvidenceState
                .IncompleteCandidateSearch,
            state
        );
    }

    [Fact]
    public void UnresolvedWithNoEquivalent_IsClassified()
    {
        EffectiveAssetReferenceEvidenceState state =
            EffectiveAssetReferenceEvidenceClassifier.Classify(
                winnerSearchComplete: true,
                linuxResolves: false,
                candidateSearchComplete: true,
                equivalentCandidateCount: 0
            );

        Assert.Equal(
            EffectiveAssetReferenceEvidenceState
                .UnresolvedNoEquivalent,
            state
        );
    }

    [Fact]
    public void UnresolvedWithUniqueEquivalent_IsClassified()
    {
        EffectiveAssetReferenceEvidenceState state =
            EffectiveAssetReferenceEvidenceClassifier.Classify(
                winnerSearchComplete: true,
                linuxResolves: false,
                candidateSearchComplete: true,
                equivalentCandidateCount: 1
            );

        Assert.Equal(
            EffectiveAssetReferenceEvidenceState
                .UnresolvedUniqueEquivalent,
            state
        );
    }

    [Fact]
    public void UnresolvedWithMultipleEquivalents_IsClassified()
    {
        EffectiveAssetReferenceEvidenceState state =
            EffectiveAssetReferenceEvidenceClassifier.Classify(
                winnerSearchComplete: true,
                linuxResolves: false,
                candidateSearchComplete: true,
                equivalentCandidateCount: 2
            );

        Assert.Equal(
            EffectiveAssetReferenceEvidenceState
                .UnresolvedAmbiguousEquivalent,
            state
        );
    }

    [Fact]
    public void NegativeCandidateCount_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                EffectiveAssetReferenceEvidenceClassifier.Classify(
                    winnerSearchComplete: true,
                    linuxResolves: false,
                    candidateSearchComplete: true,
                    equivalentCandidateCount: -1
                )
        );
    }
}
