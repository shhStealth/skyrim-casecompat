using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairFileJournalWriteState
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
    DataRelativePathRepairFileJournalWriterResult(
        DataRelativePathRepairFileJournalWriteState State,
        string JournalChildName,
        string? StagingChildName,
        LinuxOpenedFileIdentityResult? WrittenJournalIdentity,
        bool JournalEntryChanged,
        bool StagingEntryMayRemain,
        string? Error
    )
{
    public bool Success =>
        State is
            DataRelativePathRepairFileJournalWriteState
                .CreatedDurably or
            DataRelativePathRepairFileJournalWriteState
                .ReplacedDurably;
}
