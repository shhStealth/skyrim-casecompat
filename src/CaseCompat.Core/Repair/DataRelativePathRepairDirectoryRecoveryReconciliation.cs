using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairDirectoryRecoveryReconciliationState
{
    AppliedDurably,
    RolledBackDurably,

    InvalidExpectedJournalIdentity,
    NoAutomaticReconciliation,

    DestinationParentValidationFailed,
    NamespaceRevalidationFailed,
    NamespaceChangedBeforeReconciliation,
    DestinationParentSyncFailed,

    JournalTransitionFailed,
    JournalWriteFailed
}

public sealed record
    DataRelativePathRepairDirectoryRecoveryReconciliation(
        DataRelativePathRepairDirectoryRecoveryReconciliationState State,
        DataRelativePathRepairDirectoryRecoveryClassification
            Classification,
        DataRelativePathRepairDestinationParentValidation?
            ParentValidation,
        DataRelativePathRepairDirectoryJournalTransitionResult?
            JournalTransition,
        DataRelativePathRepairDirectoryJournalWriterResult?
            JournalWrite,
        string? Error
    )
{
    public bool Success =>
        State is
            DataRelativePathRepairDirectoryRecoveryReconciliationState
                .AppliedDurably or
            DataRelativePathRepairDirectoryRecoveryReconciliationState
                .RolledBackDurably;
}
