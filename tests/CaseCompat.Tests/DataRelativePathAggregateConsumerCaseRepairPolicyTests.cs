using CaseCompat.Core.Repair;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathAggregateConsumerCaseRepairPolicyTests
{
    [Fact]
    public void Classify_NoConsumerEvidence_IsNotCandidate()
    {
        DataRelativePathAggregateConsumerCaseRepairPolicyState state =
            DataRelativePathAggregateConsumerCaseRepairPolicyClassifier
                .Classify(
                    DataRelativePathAggregateLogicalLeafState
                        .UniqueRepresentation,
                    DataRelativePathAggregateConsumerPhysicalSpellingState
                        .NoConsumerEvidence
                );

        Assert.Equal(
            DataRelativePathAggregateConsumerCaseRepairPolicyState
                .NoConsumerEvidence,
            state
        );
    }

    [Fact]
    public void Classify_ConflictingConsumerSpellings_IsNotCandidate()
    {
        DataRelativePathAggregateConsumerCaseRepairPolicyState state =
            DataRelativePathAggregateConsumerCaseRepairPolicyClassifier
                .Classify(
                    DataRelativePathAggregateLogicalLeafState
                        .UniqueRepresentation,
                    DataRelativePathAggregateConsumerPhysicalSpellingState
                        .ConflictingConsumerSpellings
                );

        Assert.Equal(
            DataRelativePathAggregateConsumerCaseRepairPolicyState
                .ConflictingConsumerSpellings,
            state
        );
    }

    [Fact]
    public void Classify_ExactPhysicalSpellingPresent_IsNotCandidate()
    {
        DataRelativePathAggregateConsumerCaseRepairPolicyState state =
            DataRelativePathAggregateConsumerCaseRepairPolicyClassifier
                .Classify(
                    DataRelativePathAggregateLogicalLeafState
                        .UniqueRepresentation,
                    DataRelativePathAggregateConsumerPhysicalSpellingState
                        .ExactPhysicalSpellingPresent
                );

        Assert.Equal(
            DataRelativePathAggregateConsumerCaseRepairPolicyState
                .ExactPhysicalSpellingPresent,
            state
        );
    }

    [Fact]
    public void
        Classify_UniqueRepresentationConsumerCaseMismatch_IsCandidate()
    {
        DataRelativePathAggregateConsumerCaseRepairPolicyState state =
            DataRelativePathAggregateConsumerCaseRepairPolicyClassifier
                .Classify(
                    DataRelativePathAggregateLogicalLeafState
                        .UniqueRepresentation,
                    DataRelativePathAggregateConsumerPhysicalSpellingState
                        .ConsumerCaseMismatch
                );

        Assert.Equal(
            DataRelativePathAggregateConsumerCaseRepairPolicyState
                .UniqueRepresentationConsumerCaseMismatchCandidate,
            state
        );
    }

    [Fact]
    public void
        Classify_EquivalentContentConsumerCaseMismatch_RequiresSourcePolicy()
    {
        DataRelativePathAggregateConsumerCaseRepairPolicyState state =
            DataRelativePathAggregateConsumerCaseRepairPolicyClassifier
                .Classify(
                    DataRelativePathAggregateLogicalLeafState
                        .EquivalentContentMultipleRepresentations,
                    DataRelativePathAggregateConsumerPhysicalSpellingState
                        .ConsumerCaseMismatch
                );

        Assert.Equal(
            DataRelativePathAggregateConsumerCaseRepairPolicyState
                .EquivalentContentMultipleRepresentationsSourcePolicyRequired,
            state
        );
    }

    [Fact]
    public void
        Classify_ConflictingContentConsumerCaseMismatch_IsRejected()
    {
        DataRelativePathAggregateConsumerCaseRepairPolicyState state =
            DataRelativePathAggregateConsumerCaseRepairPolicyClassifier
                .Classify(
                    DataRelativePathAggregateLogicalLeafState
                        .ConflictingContentMultipleRepresentations,
                    DataRelativePathAggregateConsumerPhysicalSpellingState
                        .ConsumerCaseMismatch
                );

        Assert.Equal(
            DataRelativePathAggregateConsumerCaseRepairPolicyState
                .ConflictingContentMultipleRepresentationsRejected,
            state
        );
    }

    [Fact]
    public void
        Classify_ExactPhysicalSpellingDoesNotEraseConflictingPhysicalTopology()
    {
        DataRelativePathAggregateConsumerCaseRepairPolicyState state =
            DataRelativePathAggregateConsumerCaseRepairPolicyClassifier
                .Classify(
                    DataRelativePathAggregateLogicalLeafState
                        .ConflictingContentMultipleRepresentations,
                    DataRelativePathAggregateConsumerPhysicalSpellingState
                        .ExactPhysicalSpellingPresent
                );

        Assert.Equal(
            DataRelativePathAggregateConsumerCaseRepairPolicyState
                .ExactPhysicalSpellingPresent,
            state
        );

        Assert.Equal(
            DataRelativePathAggregateLogicalLeafState
                .ConflictingContentMultipleRepresentations,
            DataRelativePathAggregateLogicalLeafState
                .ConflictingContentMultipleRepresentations
        );
    }

    [Fact]
    public void Classify_UndefinedEvidenceStatesFailClosed()
    {
        Assert.Equal(
            DataRelativePathAggregateConsumerCaseRepairPolicyState
                .IndeterminateEvidence,
            DataRelativePathAggregateConsumerCaseRepairPolicyClassifier
                .Classify(
                    (DataRelativePathAggregateLogicalLeafState)999,
                    DataRelativePathAggregateConsumerPhysicalSpellingState
                        .ConsumerCaseMismatch
                )
        );

        Assert.Equal(
            DataRelativePathAggregateConsumerCaseRepairPolicyState
                .IndeterminateEvidence,
            DataRelativePathAggregateConsumerCaseRepairPolicyClassifier
                .Classify(
                    DataRelativePathAggregateLogicalLeafState
                        .UniqueRepresentation,
                    (DataRelativePathAggregateConsumerPhysicalSpellingState)999
                )
        );
    }
}
