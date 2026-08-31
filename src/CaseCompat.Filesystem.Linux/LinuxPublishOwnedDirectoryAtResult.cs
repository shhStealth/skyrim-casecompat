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
    LinuxFileIdentityResult ExpectedIdentity,
    LinuxFileIdentityResult? HandleIdentity,
    LinuxFileIdentityResult? NamedSourceIdentity,
    int? Errno,
    string? Error
)
{
    public bool Success =>
        State ==
        LinuxPublishOwnedDirectoryAtState.Published;
}
