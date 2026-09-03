namespace CaseCompat.Filesystem.Linux;

public enum LinuxOpenChildRegularFileReadOnlyAtState
{
    Opened,

    UnsupportedPlatform,
    InvalidName,
    InvalidParentHandle,
    ParentNotDirectory,

    ChildUnavailable,
    CapabilityOpenFailed,
    ChildNotRegularFile,
    CapabilityIdentityUnavailable,

    ReadableOpenFailed,
    ReadableIdentityUnavailable,
    IdentityMismatch
}

public sealed record LinuxOpenChildRegularFileReadOnlyAtResult(
    LinuxOpenChildRegularFileReadOnlyAtState State,
    string ChildName,
    LinuxOpenedChildHandle? OpenedFile,
    LinuxOpenedFileIdentityResult? Identity,
    int? Errno,
    string? Error
)
{
    public bool Success =>
        State ==
            LinuxOpenChildRegularFileReadOnlyAtState.Opened &&
        OpenedFile is not null &&
        Identity is not null &&
        Identity.Success;
}
