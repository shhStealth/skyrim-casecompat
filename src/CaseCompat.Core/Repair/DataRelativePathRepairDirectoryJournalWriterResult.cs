using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairDirectoryJournalWriteState
{
    CreatedDurably,
    ReplacedDurably,

    InvalidRecord,
    InvalidJournalName,
    InvalidInitialRevision,
    InvalidReplacementRevision,
    InvalidExpectedCurrentIdentity,

    SerializationFailed,
    TemporaryFileCreateFailed,
    WriteFailed,
    TemporaryFileSyncFailed,

    JournalAlreadyExists,
    InitialPublishFailed,

    CurrentJournalOpenFailed,
    CurrentJournalIdentityFailed,
    CurrentJournalIdentityMismatch,

    StagingPublishFailed,
    ReplacementFailed,
    StagingCleanupFailed,

    DirectorySyncFailed
}

public sealed record
    DataRelativePathRepairDirectoryJournalWriterResult(
        DataRelativePathRepairDirectoryJournalWriteState State,
        string JournalChildName,
        string? StagingChildName,
        LinuxOpenedFileIncarnationResult? WrittenJournalIncarnation,
        bool JournalEntryChanged,
        bool StagingEntryMayRemain,
        string? Error
    )
{
    public LinuxOpenedFileIdentityResult? WrittenJournalIdentity =>
        WrittenJournalIncarnation?.PhysicalIdentity;

    public LinuxFileIncarnationIdentity?
        WrittenJournalIncarnationIdentity =>
            WrittenJournalIncarnation?.Identity;

    public bool Success =>
        State is
            DataRelativePathRepairDirectoryJournalWriteState
                .CreatedDurably or
            DataRelativePathRepairDirectoryJournalWriteState
                .ReplacedDurably;
}
