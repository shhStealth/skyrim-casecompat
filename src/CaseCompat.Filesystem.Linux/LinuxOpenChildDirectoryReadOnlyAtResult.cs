namespace CaseCompat.Filesystem.Linux;

public enum LinuxOpenChildDirectoryReadOnlyAtState
{
    Opened,

    UnsupportedPlatform,
    InvalidName,
    InvalidParentHandle,

    ChildUnavailable,
    ChildSymbolicLinkRejected,
    NotDirectory,
    ChildOpenFailed
}

public sealed record LinuxOpenChildDirectoryReadOnlyAtResult(
    LinuxOpenChildDirectoryReadOnlyAtState State,
    string ChildName,
    LinuxNoFollowPathHandle? OpenedDirectory,
    int? Errno,
    string? Error)
{
    public bool Success =>
        State ==
            LinuxOpenChildDirectoryReadOnlyAtState.Opened &&
        OpenedDirectory is not null;
}
