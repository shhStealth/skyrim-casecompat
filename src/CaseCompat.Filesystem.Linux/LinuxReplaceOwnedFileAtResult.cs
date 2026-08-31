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
    LinuxFileIncarnationIdentity ExpectedSourceIncarnation,
    LinuxOpenedFileIncarnationResult? ActualSourceIncarnation,
    LinuxFileIncarnationIdentity ExpectedDestinationIncarnation,
    LinuxOpenedFileIncarnationResult? ActualDestinationIncarnation,
    int? Errno,
    string? Error
)
{
    public LinuxOpenedFileIdentityResult ExpectedSourceIdentity =>
        ExpectedSourceIncarnation.PhysicalIdentity;

    public LinuxOpenedFileIdentityResult? ActualSourceIdentity =>
        ActualSourceIncarnation?.PhysicalIdentity;

    public LinuxOpenedFileIdentityResult ExpectedDestinationIdentity =>
        ExpectedDestinationIncarnation.PhysicalIdentity;

    public LinuxOpenedFileIdentityResult? ActualDestinationIdentity =>
        ActualDestinationIncarnation?.PhysicalIdentity;

    public bool Success =>
        State ==
            LinuxReplaceOwnedFileAtState.Replaced;
}
