namespace CaseCompat.Filesystem.Linux;

public enum LinuxRenameChildAtState
{
    Renamed,

    UnsupportedPlatform,
    InvalidSourceName,
    InvalidDestinationName,
    SameNameSameParent,
    InvalidSourceParentHandle,
    InvalidDestinationParentHandle,
    SourceUnavailable,
    DestinationExists,
    CrossDeviceRenameNotSupported,
    ParentNotDirectory,
    SourceIsDirectoryDestinationIsNot,
    RenameFailed
}

public sealed record LinuxRenameChildAtResult(
    LinuxRenameChildAtState State,
    string SourceChildName,
    string DestinationChildName,
    int? Errno,
    string? Error
)
{
    public bool Success =>
        State ==
            LinuxRenameChildAtState.Renamed;
}
