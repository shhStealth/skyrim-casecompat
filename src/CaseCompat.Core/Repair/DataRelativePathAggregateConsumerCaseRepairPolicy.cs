namespace CaseCompat.Core.Repair;

// Consumer-authoritative case-repair policy over the two independent evidence
// dimensions established by C4D.
//
// This state surface answers only whether the joined evidence represents a
// candidate for a future casing-repair projection. It does not select source
// bytes, construct a repair plan, authorize mutation, or declare the broader
// physical namespace safe.
//
// In particular:
//
// - UniqueRepresentation + ConsumerCaseMismatch is a candidate only.
// - Equivalent-content multiple representations remain blocked until a later
//   checkpoint establishes explicit source-representation policy.
// - Conflicting-content multiple representations remain rejected.
// - ExactPhysicalSpellingPresent means only that consumer-casing repair is not
//   indicated. It does not erase or bless independent physical topology.
public enum DataRelativePathAggregateConsumerCaseRepairPolicyState
{
    NoConsumerEvidence,
    ConflictingConsumerSpellings,
    ExactPhysicalSpellingPresent,

    UniqueRepresentationConsumerCaseMismatchCandidate,

    EquivalentContentMultipleRepresentationsSourcePolicyRequired,

    ConflictingContentMultipleRepresentationsRejected,

    IndeterminateEvidence
}

// Pure C4E-1 policy classifier.
//
// Inputs are only the already-established C4D physical/content topology state
// and consumer-to-physical spelling state. No filesystem, hashing, source
// selection, plan projection, authorization, persistence, or mutation occurs.
//
// Undefined enum values fail closed as IndeterminateEvidence.
public static class DataRelativePathAggregateConsumerCaseRepairPolicyClassifier
{
    public static DataRelativePathAggregateConsumerCaseRepairPolicyState
        Classify(
            DataRelativePathAggregateLogicalLeafState physicalState,
            DataRelativePathAggregateConsumerPhysicalSpellingState
                spellingState)
    {
        if (!Enum.IsDefined(
                typeof(DataRelativePathAggregateLogicalLeafState),
                physicalState) ||
            !Enum.IsDefined(
                typeof(
                    DataRelativePathAggregateConsumerPhysicalSpellingState
                ),
                spellingState))
        {
            return DataRelativePathAggregateConsumerCaseRepairPolicyState
                .IndeterminateEvidence;
        }

        switch (spellingState)
        {
            case
                DataRelativePathAggregateConsumerPhysicalSpellingState
                    .NoConsumerEvidence:
                return DataRelativePathAggregateConsumerCaseRepairPolicyState
                    .NoConsumerEvidence;

            case
                DataRelativePathAggregateConsumerPhysicalSpellingState
                    .ConflictingConsumerSpellings:
                return DataRelativePathAggregateConsumerCaseRepairPolicyState
                    .ConflictingConsumerSpellings;

            case
                DataRelativePathAggregateConsumerPhysicalSpellingState
                    .ExactPhysicalSpellingPresent:
                return DataRelativePathAggregateConsumerCaseRepairPolicyState
                    .ExactPhysicalSpellingPresent;

            case
                DataRelativePathAggregateConsumerPhysicalSpellingState
                    .ConsumerCaseMismatch:
                return physicalState switch
                {
                    DataRelativePathAggregateLogicalLeafState
                        .UniqueRepresentation =>
                        DataRelativePathAggregateConsumerCaseRepairPolicyState
                            .UniqueRepresentationConsumerCaseMismatchCandidate,

                    DataRelativePathAggregateLogicalLeafState
                        .EquivalentContentMultipleRepresentations =>
                        DataRelativePathAggregateConsumerCaseRepairPolicyState
                            .EquivalentContentMultipleRepresentationsSourcePolicyRequired,

                    DataRelativePathAggregateLogicalLeafState
                        .ConflictingContentMultipleRepresentations =>
                        DataRelativePathAggregateConsumerCaseRepairPolicyState
                            .ConflictingContentMultipleRepresentationsRejected,

                    _ =>
                        DataRelativePathAggregateConsumerCaseRepairPolicyState
                            .IndeterminateEvidence
                };

            default:
                return DataRelativePathAggregateConsumerCaseRepairPolicyState
                    .IndeterminateEvidence;
        }
    }
}
