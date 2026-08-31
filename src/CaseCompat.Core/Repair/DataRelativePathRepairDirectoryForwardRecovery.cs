using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairDirectoryForwardRecoveryState
{
    AppliedDurably,

    LockUnavailable,
    JournalReadFailed,
    RecoveryStateNotEligible,

    DestinationParentValidationFailed,
    StagingRevalidationFailed,
    NamespaceChangedBeforePublication,

    PublicationFailed,
    DestinationParentSyncFailed,

    AppliedTransitionFailed,
    AppliedJournalWriteFailed
}

public sealed record
    DataRelativePathRepairDirectoryForwardRecovery(
        DataRelativePathRepairDirectoryForwardRecoveryState State,
        LinuxExclusiveDirectoryLockState? LockState,
        DataRelativePathRepairDirectoryJournalReaderResult?
            JournalRead,
        DataRelativePathRepairDirectoryRecoveryClassification?
            Classification,
        DataRelativePathRepairDestinationParentValidation?
            ParentValidation,
        LinuxOpenChildReadOnlyAtState?
            StagingOpenState,
        LinuxOpenedDirectorySnapshotResult?
            StagingSnapshot,
        LinuxOpenChildReadOnlyAtState?
            FinalOpenState,
        LinuxPublishOwnedDirectoryAtResult?
            Publication,
        LinuxFsyncResult?
            DestinationParentSync,
        DataRelativePathRepairDirectoryJournalTransitionResult?
            AppliedTransition,
        DataRelativePathRepairDirectoryJournalWriterResult?
            AppliedJournalWrite,
        string? Error
    )
{
    public bool Success =>
        State ==
        DataRelativePathRepairDirectoryForwardRecoveryState
            .AppliedDurably;
}
