using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairDirectoryRollbackRecoveryState
{
    RolledBackDurably,

    LockUnavailable,
    JournalReadFailed,
    InvalidExpectedJournalIdentity,
    JournalIncarnationChanged,
    RecoveryStateNotEligible,

    JournalTransitionFailed,
    DestinationParentValidationFailed,
    NamespaceRevalidationFailed,
    NamespaceChangedBeforeRemove,

    RemoveFailed,
    DestinationParentSyncFailed,
    JournalWriteFailed
}

public sealed record
    DataRelativePathRepairDirectoryRollbackRecovery(
        DataRelativePathRepairDirectoryRollbackRecoveryState State,
        LinuxExclusiveDirectoryLockState? LockState,
        DataRelativePathRepairDirectoryJournalReaderResult?
            JournalRead,
        DataRelativePathRepairDirectoryRecoveryClassification?
            Classification,
        DataRelativePathRepairDestinationParentValidation?
            ParentValidation,
        LinuxOpenChildReadOnlyAtState?
            StagingOpenState,
        LinuxOpenChildReadOnlyAtState?
            FinalOpenState,
        LinuxOpenedDirectorySnapshotResult?
            FinalSnapshot,
        LinuxRemoveOwnedDirectoryAtResult?
            RemoveResult,
        LinuxFsyncResult?
            DestinationParentSync,
        DataRelativePathRepairDirectoryJournalTransitionResult?
            JournalTransition,
        DataRelativePathRepairDirectoryJournalWriterResult?
            JournalWrite,
        string? Error
    )
{
    public bool Success =>
        State ==
        DataRelativePathRepairDirectoryRollbackRecoveryState
            .RolledBackDurably;
}
