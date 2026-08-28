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
                equivalentCandidateCount: 2
            );

        Assert.Equal(
            EffectiveAssetReferenceEvidenceState
                .LinuxResolvable,
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
                    equivalentCandidateCount: -1
                )
        );
    }
}
