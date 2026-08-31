namespace CaseCompat.Filesystem.Linux;

public enum LinuxOpenedDirectoryIncarnationState
{
    Captured,

    UnsupportedPlatform,
    InvalidHandle,
    SnapshotUnavailable,
    NotDirectory,
    GenerationUnavailable
}

public sealed record LinuxOpenedDirectoryIncarnationResult(
    LinuxOpenedDirectoryIncarnationState State,
    string FullPath,
    LinuxOpenedDirectorySnapshotResult? Snapshot,
    LinuxOpenedInodeGenerationResult? GenerationCapture,
    LinuxDirectoryIncarnationIdentity? Identity,
    string? Error
)
{
    public bool Success =>
        State ==
            LinuxOpenedDirectoryIncarnationState.Captured &&
        Identity is not null &&
        Identity.Success;
}
