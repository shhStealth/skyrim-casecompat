namespace CaseCompat.Filesystem.Linux;

public enum LinuxOpenedFileSnapshotState
{
    Captured,

    UnsupportedPlatform,
    InvalidHandle,

    MetadataUnavailable,
    NotRegularFile,
    SizeUnavailable,
    HashFailed,
    SizeChangedDuringHash
}

public sealed record LinuxOpenedFileSnapshotResult(
    LinuxOpenedFileSnapshotState State,
    string FullPath,
    LinuxFileIdentityResult? Identity,
    long? Size,
    string? Sha256,
    int? Errno,
    string? Error
)
{
    public bool Success =>
        State ==
            LinuxOpenedFileSnapshotState.Captured &&
        Identity is not null &&
        Size is not null &&
        Sha256 is not null;
}
