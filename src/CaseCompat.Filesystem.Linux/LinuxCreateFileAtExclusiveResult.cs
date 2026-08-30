namespace CaseCompat.Filesystem.Linux;

public enum LinuxCreateFileAtExclusiveState
{
    Created,

    UnsupportedPlatform,
    InvalidName,
    InvalidParentHandle,
    ParentNotDirectory,
    DestinationExists,
    CreateFailed
}

public sealed record LinuxCreateFileAtExclusiveResult(
    LinuxCreateFileAtExclusiveState State,
    string ChildName,
    LinuxNoFollowPathHandle? OpenedPath,
    int? Errno,
    string? Error
)
{
    public bool Success =>
        State ==
            LinuxCreateFileAtExclusiveState.Created &&
        OpenedPath is not null;
}
