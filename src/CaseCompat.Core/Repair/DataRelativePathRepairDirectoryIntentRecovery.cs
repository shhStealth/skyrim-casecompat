using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairDirectoryIntentRecoveryState
{
    PreparedDurably,

    LockUnavailable,
    JournalReadFailed,
    RecoveryStateNotEligible,

    DestinationParentValidationFailed,
    NamespaceRevalidationFailed,
    NamespaceChangedBeforePreparation,

    PreparationFailed,
    PreparedTransitionFailed,
    PreparedJournalWriteFailed
}

public sealed record
    DataRelativePathRepairDirectoryIntentRecovery(
        DataRelativePathRepairDirectoryIntentRecoveryState State,
        LinuxExclusiveDirectoryLockState? LockState,
        DataRelativePathRepairDirectoryJournalReaderResult?
            JournalRead,
        DataRelativePathRepairDirectoryRecoveryClassification?
            Classification,
        DataRelativePathRepairDestinationParentValidation?
            ParentValidation,
        string? FreshStagingChildName,
        LinuxPrepareOwnedDirectoryAtResult?
            Preparation,
        DataRelativePathRepairDirectoryJournalTransitionResult?
            PreparedTransition,
        DataRelativePathRepairDirectoryJournalWriterResult?
            PreparedJournalWrite,
        bool UnjournaledStagingEntryMayRemain,
        string? Error
    )
{
    public bool Success =>
        State ==
        DataRelativePathRepairDirectoryIntentRecoveryState
            .PreparedDurably;
}
