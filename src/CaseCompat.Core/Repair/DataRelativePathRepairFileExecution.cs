using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairFileExecutionState
{
    AppliedDurably,

    InvalidIntent,
    DataRootMismatch,
    LockUnavailable,

    SourceValidationFailed,
    DestinationParentValidationFailed,
    DestinationInspectionFailed,
    DestinationExists,

    InitialJournalWriteFailed,
    InitialJournalIdentityUnavailable,

    TemporaryFileCreateFailed,
    CopyFailed,
    TemporaryFileSyncFailed,
    PreparedIdentityFailed,

    PreparedTransitionFailed,
    PreparedJournalWriteFailed,
    PreparedJournalIdentityUnavailable,

    PublicationFailed,
    DestinationParentSyncFailed,

    AppliedTransitionFailed,
    AppliedJournalWriteFailed
}

public sealed record DataRelativePathRepairFileExecution(
    DataRelativePathRepairFileExecutionState State,

    LinuxExclusiveDirectoryLockState? LockState,

    DataRelativePathRepairSourceValidation?
        SourceValidation,

    DataRelativePathRepairDestinationParentValidation?
        ParentValidation,

    LinuxOpenChildReadOnlyAtState?
        DestinationOpenState,

    DataRelativePathRepairFileJournalWriterResult?
        InitialJournalWrite,

    LinuxCreateUnnamedFileAtResult?
        TemporaryFileCreate,

    LinuxCopyFileContentsResult?
        CopyResult,

    LinuxFsyncResult?
        TemporaryFileSync,

    LinuxOpenedFileIdentityResult?
        PreparedIdentity,

    DataRelativePathRepairFileJournalTransitionResult?
        PreparedTransition,

    DataRelativePathRepairFileJournalWriterResult?
        PreparedJournalWrite,

    LinuxPublishUnnamedFileAtResult?
        Publication,

    LinuxFsyncResult?
        DestinationParentSync,

    DataRelativePathRepairFileJournalTransitionResult?
        AppliedTransition,

    DataRelativePathRepairFileJournalWriterResult?
        AppliedJournalWrite,

    string? Error
)
{
    public bool Success =>
        State ==
        DataRelativePathRepairFileExecutionState
            .AppliedDurably;
}
