namespace CaseCompat.Filesystem.Linux;

public enum LinuxPrepareOwnedDirectoryAtState
{
    PreparedDurably,

    UnsupportedPlatform,
    InvalidName,
    InvalidParentHandle,
    ParentNotDirectory,

    StagingAlreadyExists,
    CreateFailed,

    StagingUnavailableAfterCreate,
    StagingSymbolicLinkRejected,
    StagingOpenFailed,

    StagingNotDirectory,
    StagingSnapshotFailed,

    ParentSyncFailed
}

public sealed record LinuxPrepareOwnedDirectoryAtResult(
    LinuxPrepareOwnedDirectoryAtState State,
    string StagingChildName,
    LinuxCreateDirectoryAtResult? CreateResult,
    LinuxOpenChildReadOnlyAtResult? OpenResult,
    LinuxOpenedDirectorySnapshotResult? Snapshot,
    LinuxFsyncResult? ParentSync,
    LinuxPreparedOwnedDirectoryLease? Lease,
    bool StagingEntryChanged,
    bool StagingEntryMayRemain,
    string? Error
)
{
    public bool Success =>
        State ==
            LinuxPrepareOwnedDirectoryAtState.PreparedDurably &&
        Lease is not null;
}
