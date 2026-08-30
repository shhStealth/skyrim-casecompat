namespace CaseCompat.Filesystem.Linux;

public enum LinuxReplaceOwnedFileAtState
{
    Replaced,

    UnsupportedPlatform,
    InvalidName,
    SameName,
    InvalidExpectedSourceIdentity,
    InvalidExpectedDestinationIdentity,

    InvalidParentHandle,
    ParentNotDirectory,

    SourceUnavailable,
    SourceSymbolicLinkRejected,
    SourceOpenFailed,
    SourceIdentityUnavailable,
    SourceNotRegularFile,
    SourceIdentityMismatch,

    DestinationUnavailable,
    DestinationSymbolicLinkRejected,
    DestinationOpenFailed,
    DestinationIdentityUnavailable,
    DestinationNotRegularFile,
    DestinationIdentityMismatch,

    SourceAndDestinationSameObject,
    ChildChangedBeforeReplace,
    DifferentFilesystem,
    ReplaceDenied,
    ReplaceFailed
}

public sealed record LinuxReplaceOwnedFileAtResult(
    LinuxReplaceOwnedFileAtState State,
    string SourceChildName,
    string DestinationChildName,
    LinuxOpenedFileIdentityResult ExpectedSourceIdentity,
    LinuxOpenedFileIdentityResult? ActualSourceIdentity,
    LinuxOpenedFileIdentityResult ExpectedDestinationIdentity,
    LinuxOpenedFileIdentityResult? ActualDestinationIdentity,
    int? Errno,
    string? Error
)
{
    public bool Success =>
        State ==
            LinuxReplaceOwnedFileAtState.Replaced;
}
