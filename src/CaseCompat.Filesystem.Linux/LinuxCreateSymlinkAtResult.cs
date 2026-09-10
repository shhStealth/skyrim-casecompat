namespace CaseCompat.Filesystem.Linux;

public enum LinuxCreateSymlinkAtState
{
    Created,

    UnsupportedPlatform,
    InvalidLinkName,
    InvalidTargetName,
    InvalidParentHandle,
    ParentNotDirectory,
    DestinationExists,
    CreateFailed
}

public sealed record LinuxCreateSymlinkAtResult(
    LinuxCreateSymlinkAtState State,
    string LinkName,
    string TargetName,
    int? Errno,
    string? Error
)
{
    public bool Success =>
        State ==
            LinuxCreateSymlinkAtState.Created;
}
