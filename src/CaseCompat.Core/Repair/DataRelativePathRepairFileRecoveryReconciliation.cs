using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairFileRecoveryReconciliationState
{
    AppliedDurably,
    RolledBackDurably,

    InvalidExpectedJournalIdentity,
    NoAutomaticReconciliation,

    DestinationParentValidationFailed,
    DestinationRevalidationFailed,
    DestinationChangedBeforeReconciliation,
    DestinationParentSyncFailed,

    JournalTransitionFailed,
    JournalWriteFailed
}

public sealed record
    DataRelativePathRepairFileRecoveryReconciliation(
        DataRelativePathRepairFileRecoveryReconciliationState State,
        DataRelativePathRepairFileRecoveryClassification Classification,
        DataRelativePathRepairDestinationParentValidation?
            ParentValidation,
        DataRelativePathRepairFileJournalTransitionResult?
            JournalTransition,
        DataRelativePathRepairFileJournalWriterResult?
            JournalWrite,
        string? Error
    )
{
    public bool Success =>
        State is
            DataRelativePathRepairFileRecoveryReconciliationState
                .AppliedDurably or
            DataRelativePathRepairFileRecoveryReconciliationState
                .RolledBackDurably;
}
