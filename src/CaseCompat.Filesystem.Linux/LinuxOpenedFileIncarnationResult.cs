namespace CaseCompat.Filesystem.Linux;

public enum LinuxOpenedFileIncarnationState
{
    Captured,

    UnsupportedPlatform,
    InvalidHandle,
    IdentityUnavailable,
    NotRegularFile,
    GenerationUnavailable
}

public sealed record LinuxOpenedFileIncarnationResult(
    LinuxOpenedFileIncarnationState State,
    LinuxOpenedFileIdentityResult? PhysicalIdentity,
    LinuxOpenedInodeGenerationResult? GenerationCapture,
    LinuxFileIncarnationIdentity? Identity,
    string? Error
)
{
    public bool Success =>
        State ==
            LinuxOpenedFileIncarnationState.Captured &&
        Identity is not null &&
        Identity.Success;
}
