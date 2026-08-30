namespace CaseCompat.Filesystem.Linux;

public enum LinuxOpenedDirectorySnapshotState
{
    Captured,

    UnsupportedPlatform,
    InvalidHandle,

    MetadataUnavailable,
    NotDirectory,
    FlagsUnavailable
}

public sealed record LinuxOpenedDirectorySnapshotResult(
    LinuxOpenedDirectorySnapshotState State,
    string FullPath,
    LinuxFileIdentityResult? Identity,
    bool? CasefoldEnabled,
    long? RawFlags,
    int? Errno,
    string? Error
)
{
    public bool Success =>
        State ==
            LinuxOpenedDirectorySnapshotState.Captured &&
        Identity is not null &&
        CasefoldEnabled is not null &&
        RawFlags is not null;
}
