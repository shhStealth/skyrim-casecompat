using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairFileRollbackRequestState
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

public sealed record DataRelativePathRepairFileRollbackRequest(
    DataRelativePathRepairFileRollbackRequestState State,
    LinuxExclusiveDirectoryLockState? LockState,
    DataRelativePathRepairFileJournalReaderResult? JournalRead,
    DataRelativePathRepairFileRecoveryClassification? Classification,
    DataRelativePathRepairFileJournalTransitionResult? JournalTransition,
    DataRelativePathRepairFileJournalWriterResult? JournalWrite,
    string? Error
)
{
    public bool Success =>
        State ==
        DataRelativePathRepairFileRollbackRequestState
            .RequestedDurably;
}
