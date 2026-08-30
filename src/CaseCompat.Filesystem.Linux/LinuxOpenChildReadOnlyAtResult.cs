namespace CaseCompat.Filesystem.Linux;

public enum LinuxOpenChildReadOnlyAtState
{
    Opened,

    UnsupportedPlatform,
    InvalidName,
    InvalidParentHandle,
    ParentNotDirectory,

    ChildUnavailable,
    ChildSymbolicLinkRejected,
    ChildOpenFailed
}

public sealed record LinuxOpenChildReadOnlyAtResult(
    LinuxOpenChildReadOnlyAtState State,
    string ChildName,
    LinuxOpenedChildHandle? OpenedChild,
    int? Errno,
    string? Error
)
{
    public bool Success =>
        State ==
            LinuxOpenChildReadOnlyAtState.Opened &&
        OpenedChild is not null;
}
