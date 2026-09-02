using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairBatchReusedDirectoryRollbackState
{
    RequestedDurably,
    RolledBackDurably,

    InvalidExpectedJournalIdentity,
    LockUnavailable,
    JournalReadFailed,
    JournalIncarnationChanged,
    RecoveryStateNotEligible,
    JournalTransitionFailed,

    DestinationParentValidationFailed,
    FinalOpenFailed,
    FinalIncarnationUnavailable,
    FinalIncarnationMismatch,

    JournalWriteFailed
}

public sealed record
    DataRelativePathRepairBatchReusedDirectoryRollback(
        DataRelativePathRepairBatchReusedDirectoryRollbackState State,
        LinuxExclusiveDirectoryLockState? LockState,
        DataRelativePathRepairDirectoryJournalReaderResult? JournalRead,
        DataRelativePathRepairDirectoryRecoveryClassification? Classification,
        DataRelativePathRepairDestinationParentValidation? ParentValidation,
        LinuxOpenChildReadOnlyAtState? FinalOpenState,
        LinuxOpenedDirectoryIncarnationResult? FinalIncarnation,
        DataRelativePathRepairDirectoryJournalTransitionResult? JournalTransition,
        DataRelativePathRepairDirectoryJournalWriterResult? JournalWrite,
        string? Error
    )
{
    public bool Success =>
        State is
            DataRelativePathRepairBatchReusedDirectoryRollbackState
                .RequestedDurably or
            DataRelativePathRepairBatchReusedDirectoryRollbackState
                .RolledBackDurably;
}
