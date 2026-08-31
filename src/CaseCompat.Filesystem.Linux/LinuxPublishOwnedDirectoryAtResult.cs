namespace CaseCompat.Filesystem.Linux;

public enum LinuxPublishOwnedDirectoryAtState
{
    Published,

    UnsupportedPlatform,
    NoReplaceUnsupported,

    InvalidName,
    SameName,
    InvalidExpectedIdentity,

    InvalidParentHandle,
    InvalidSourceHandle,
    ParentNotDirectory,

    SourceUnavailable,
    SourceSymbolicLinkRejected,
    SourceOpenFailed,
    SourceNotDirectory,
    SourceIdentityUnavailable,
    SourceIdentityMismatch,

    DestinationExists,
    ChildChangedBeforePublish,
    PublicationDenied,
    PublishFailed
}

public sealed record LinuxPublishOwnedDirectoryAtResult(
    LinuxPublishOwnedDirectoryAtState State,
    string SourceChildName,
    string DestinationChildName,
    LinuxDirectoryIncarnationIdentity ExpectedIdentity,
    LinuxDirectoryIncarnationIdentity? HandleIdentity,
    LinuxDirectoryIncarnationIdentity? NamedSourceIdentity,
    int? Errno,
    string? Error
)
{
    public bool Success =>
        State ==
        LinuxPublishOwnedDirectoryAtState.Published;
}
