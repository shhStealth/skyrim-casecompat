using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairFileJournalReadState
{
    Loaded,

    UnsupportedPlatform,
    InvalidJournalName,

    InvalidJournalDirectoryHandle,
    JournalDirectoryNotDirectory,

    JournalUnavailable,
    JournalSymbolicLinkRejected,
    JournalOpenFailed,

    JournalIdentityFailed,
    JournalNotRegularFile,

    JournalLengthUnavailable,
    JournalTooLarge,

    ReadFailed,
    UnexpectedEndOfFile,
    LengthChangedDuringRead,

    DeserializeFailed,
    InvalidRecord
}

public sealed record
    DataRelativePathRepairFileJournalReaderResult(
        DataRelativePathRepairFileJournalReadState State,
        string JournalChildName,
        DataRelativePathRepairFileJournalRecord? Record,
        LinuxOpenedFileIdentityResult? JournalIdentity,
        long? Length,
        string? Error
    )
{
    public bool Success =>
        State ==
            DataRelativePathRepairFileJournalReadState.Loaded &&
        Record is not null &&
        JournalIdentity is not null &&
        JournalIdentity.Success;
}
