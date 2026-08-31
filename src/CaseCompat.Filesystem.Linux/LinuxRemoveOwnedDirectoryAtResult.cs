namespace CaseCompat.Filesystem.Linux;

public enum LinuxRemoveOwnedDirectoryAtState
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
    ChildNotDirectory,
    IdentityMismatch,

    ChildChangedBeforeRemove,
    DirectoryNotEmpty,
    DirectoryBusy,
    RemoveDenied,
    RemoveFailed
}

public sealed record LinuxRemoveOwnedDirectoryAtResult(
    LinuxRemoveOwnedDirectoryAtState State,
    string ChildName,
    LinuxDirectoryIncarnationIdentity ExpectedIdentity,
    LinuxDirectoryIncarnationIdentity? ActualIdentity,
    int? Errno,
    string? Error
)
{
    public bool Success =>
        State ==
        LinuxRemoveOwnedDirectoryAtState.Removed;
}
