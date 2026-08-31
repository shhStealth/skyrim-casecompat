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
        LinuxOpenedFileIncarnationResult? JournalIncarnation,
        long? Length,
        string? Error
    )
{
    public LinuxOpenedFileIdentityResult? JournalIdentity =>
        JournalIncarnation?.PhysicalIdentity;

    public LinuxFileIncarnationIdentity? JournalIncarnationIdentity =>
        JournalIncarnation?.Identity;

    public bool Success =>
        State ==
            DataRelativePathRepairFileJournalReadState.Loaded &&
        Record is not null &&
        JournalIncarnation is not null &&
        JournalIncarnation.Success;
}
