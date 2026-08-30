namespace CaseCompat.Filesystem.Linux;

public enum LinuxFsyncState
{
    Synced,

    UnsupportedPlatform,
    InvalidHandle,
    SyncFailed
}

public sealed record LinuxFsyncResult(
    LinuxFsyncState State,
    int? Errno,
    string? Error
)
{
    public bool Success =>
        State ==
            LinuxFsyncState.Synced;
}
