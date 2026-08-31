using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairDirectoryReprepareRecoveryState
{
    RepreparedDurably,

    LockUnavailable,
    JournalReadFailed,
    InvalidExpectedJournalIdentity,
    JournalIncarnationChanged,
    RecoveryStateNotEligible,

    DestinationParentValidationFailed,
    NamespaceRevalidationFailed,
    NamespaceChangedBeforePreparation,

    PreparationFailed,
    ReprepareTransitionFailed,
    RepreparedJournalWriteFailed
}

public sealed record
    DataRelativePathRepairDirectoryReprepareRecovery(
        DataRelativePathRepairDirectoryReprepareRecoveryState State,
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
            ReprepareTransition,
        DataRelativePathRepairDirectoryJournalWriterResult?
            RepreparedJournalWrite,
        bool UnjournaledStagingEntryMayRemain,
        string? Error
    )
{
    public bool Success =>
        State ==
        DataRelativePathRepairDirectoryReprepareRecoveryState
            .RepreparedDurably;
}
