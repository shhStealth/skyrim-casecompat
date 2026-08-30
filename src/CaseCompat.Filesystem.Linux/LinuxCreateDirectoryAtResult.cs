namespace CaseCompat.Filesystem.Linux;

public enum LinuxCreateDirectoryAtState
{
    Created,

    UnsupportedPlatform,
    InvalidName,
    InvalidParentHandle,
    ParentNotDirectory,
    DestinationExists,
    CreateFailed
}

public sealed record LinuxCreateDirectoryAtResult(
    LinuxCreateDirectoryAtState State,
    string ChildName,
    int? Errno,
    string? Error
)
{
    public bool Success =>
        State ==
            LinuxCreateDirectoryAtState.Created;
}
