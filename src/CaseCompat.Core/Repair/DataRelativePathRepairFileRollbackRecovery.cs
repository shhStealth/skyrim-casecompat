using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairFileRollbackRecoveryState
{
    RolledBackDurably,

    LockUnavailable,
    JournalReadFailed,
    RecoveryStateNotEligible,

    JournalTransitionFailed,
    DestinationParentValidationFailed,
    DestinationRevalidationFailed,
    DestinationChangedBeforeRemove,

    RemoveFailed,
    DestinationParentSyncFailed,
    JournalWriteFailed
}

public sealed record DataRelativePathRepairFileRollbackRecovery(
    DataRelativePathRepairFileRollbackRecoveryState State,
    LinuxExclusiveDirectoryLockState? LockState,
    DataRelativePathRepairFileJournalReaderResult? JournalRead,
    DataRelativePathRepairFileRecoveryClassification? Classification,
    DataRelativePathRepairDestinationParentValidation? ParentValidation,
    LinuxRemoveOwnedFileAtResult? RemoveResult,
    DataRelativePathRepairFileJournalTransitionResult? JournalTransition,
    DataRelativePathRepairFileJournalWriterResult? JournalWrite,
    string? Error
)
{
    public bool Success =>
        State ==
        DataRelativePathRepairFileRollbackRecoveryState
            .RolledBackDurably;
}
