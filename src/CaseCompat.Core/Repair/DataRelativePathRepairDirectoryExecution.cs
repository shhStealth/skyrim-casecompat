using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairDirectoryExecutionState
{
    AppliedDurably,

    LockUnavailable,
    DestinationParentValidationFailed,
    DestinationParentIncarnationUnavailable,

    IntentTransitionFailed,

    DestinationExists,
    DestinationInspectionFailed,

    InitialJournalWriteFailed,

    IntentRecoveryFailed,
    ForwardRecoveryFailed
}

public sealed record DataRelativePathRepairDirectoryExecution(
    DataRelativePathRepairDirectoryExecutionState State,

    LinuxExclusiveDirectoryLockState?
        InitialLockState,

    DataRelativePathRepairDestinationParentValidation?
        ParentValidation,

    LinuxOpenChildReadOnlyAtState?
        DestinationOpenState,

    DataRelativePathRepairDirectoryJournalTransitionResult?
        IntentTransition,

    DataRelativePathRepairDirectoryJournalWriterResult?
        InitialJournalWrite,

    DataRelativePathRepairDirectoryIntentRecovery?
        IntentRecovery,

    DataRelativePathRepairDirectoryForwardRecovery?
        ForwardRecovery,

    string? Error
)
{
    public bool Success =>
        State ==
        DataRelativePathRepairDirectoryExecutionState
            .AppliedDurably;
}
