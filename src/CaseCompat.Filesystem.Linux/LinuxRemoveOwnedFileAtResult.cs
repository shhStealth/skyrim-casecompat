namespace CaseCompat.Filesystem.Linux;

public enum LinuxRemoveOwnedFileAtState
{
    Removed,

    UnsupportedPlatform,
    InvalidName,
    InvalidExpectedIdentity,

    InvalidParentHandle,
    ParentNotDirectory,

    ChildUnavailable,
    ChildSymbolicLinkRejected,
    ChildOpenFailed,

    ChildIdentityUnavailable,
    ChildNotRegularFile,
    IdentityMismatch,

    ChildChangedBeforeRemove,
    RemoveDenied,
    RemoveFailed
}

public sealed record LinuxRemoveOwnedFileAtResult(
    LinuxRemoveOwnedFileAtState State,
    string ChildName,
    LinuxFileIncarnationIdentity ExpectedIdentity,
    LinuxFileIncarnationIdentity? ActualIdentity,
    int? Errno,
    string? Error
)
{
    public bool Success =>
        State ==
            LinuxRemoveOwnedFileAtState.Removed;
}
