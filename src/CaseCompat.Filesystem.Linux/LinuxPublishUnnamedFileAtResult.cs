namespace CaseCompat.Filesystem.Linux;

public enum LinuxPublishUnnamedFileAtState
{
    Published,

    UnsupportedPlatform,
    InvalidName,
    InvalidSourceHandle,
    InvalidParentHandle,

    DestinationExists,
    DifferentFilesystem,
    ParentNotDirectory,

    SourceDescriptorUnavailable,
    PublicationDenied,
    PublishFailed
}

public sealed record LinuxPublishUnnamedFileAtResult(
    LinuxPublishUnnamedFileAtState State,
    string ChildName,
    int? Errno,
    string? Error
)
{
    public bool Success =>
        State ==
            LinuxPublishUnnamedFileAtState.Published;
}
