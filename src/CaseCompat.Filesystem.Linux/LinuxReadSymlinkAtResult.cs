namespace CaseCompat.Filesystem.Linux;

public enum LinuxReadSymlinkAtState
{
    Read,

    UnsupportedPlatform,
    InvalidName,
    InvalidParentHandle,
    ParentNotDirectory,
    ChildUnavailable,
    ChildNotSymbolicLink,
    TargetTooLong,
    ReadFailed
}

public sealed record LinuxReadSymlinkAtResult(
    LinuxReadSymlinkAtState State,
    string ChildName,
    string? Target,
    int? Errno,
    string? Error
)
{
    public bool Success =>
        State ==
            LinuxReadSymlinkAtState.Read &&
        Target is not null;
}
