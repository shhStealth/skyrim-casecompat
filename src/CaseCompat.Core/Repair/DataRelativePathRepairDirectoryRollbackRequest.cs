using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairDirectoryRollbackRequestState
{
    RequestedDurably,

    LockUnavailable,
    JournalReadFailed,
    InvalidExpectedJournalIdentity,
    JournalIncarnationChanged,
    RecoveryStateNotEligible,
    JournalTransitionFailed,
    JournalWriteFailed
}

public sealed record DataRelativePathRepairDirectoryRollbackRequest(
    DataRelativePathRepairDirectoryRollbackRequestState State,
    LinuxExclusiveDirectoryLockState? LockState,
    DataRelativePathRepairDirectoryJournalReaderResult? JournalRead,
    DataRelativePathRepairDirectoryRecoveryClassification? Classification,
    DataRelativePathRepairDirectoryJournalTransitionResult? JournalTransition,
    DataRelativePathRepairDirectoryJournalWriterResult? JournalWrite,
    string? Error
)
{
    public bool Success =>
        State ==
        DataRelativePathRepairDirectoryRollbackRequestState
            .RequestedDurably;
}
