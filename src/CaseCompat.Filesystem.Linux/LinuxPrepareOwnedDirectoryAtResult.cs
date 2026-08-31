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
    StagingGenerationUnavailable,

    ParentSyncFailed
}

public sealed record LinuxPrepareOwnedDirectoryAtResult(
    LinuxPrepareOwnedDirectoryAtState State,
    string StagingChildName,
    LinuxCreateDirectoryAtResult? CreateResult,
    LinuxOpenChildReadOnlyAtResult? OpenResult,
    LinuxOpenedDirectorySnapshotResult? Snapshot,
    LinuxOpenedDirectoryIncarnationResult? Incarnation,
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
        Lease is not null &&
        Incarnation is not null &&
        Incarnation.Success;
}
