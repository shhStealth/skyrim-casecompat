using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairFileForwardRecoveryState
{
    AppliedDurably,

    LockUnavailable,
    JournalReadFailed,
    InvalidExpectedJournalIdentity,
    JournalIncarnationChanged,
    RecoveryStateNotEligible,

    SourceValidationFailed,
    DestinationParentValidationFailed,
    DestinationRevalidationFailed,
    DestinationChangedBeforePreparation,

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

public sealed record DataRelativePathRepairFileForwardRecovery(
    DataRelativePathRepairFileForwardRecoveryState State,
    LinuxExclusiveDirectoryLockState? LockState,
    DataRelativePathRepairFileJournalReaderResult? JournalRead,
    DataRelativePathRepairFileRecoveryClassification? Classification,
    DataRelativePathRepairSourceValidation? SourceValidation,
    DataRelativePathRepairDestinationParentValidation? ParentValidation,
    LinuxOpenChildReadOnlyAtState? DestinationOpenState,
    LinuxCreateUnnamedFileAtResult? TemporaryFileCreate,
    LinuxCopyFileContentsResult? CopyResult,
    LinuxFsyncResult? TemporaryFileSync,
    LinuxOpenedFileIdentityResult? PreparedIdentity,
    DataRelativePathRepairFileJournalTransitionResult? PreparedTransition,
    DataRelativePathRepairFileJournalWriterResult? PreparedJournalWrite,
    LinuxPublishUnnamedFileAtResult? Publication,
    LinuxFsyncResult? DestinationParentSync,
    DataRelativePathRepairFileJournalTransitionResult? AppliedTransition,
    DataRelativePathRepairFileJournalWriterResult? AppliedJournalWrite,
    string? Error
)
{
    public bool Success =>
        State ==
        DataRelativePathRepairFileForwardRecoveryState
            .AppliedDurably;
}
