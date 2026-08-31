namespace CaseCompat.Filesystem.Linux;

public enum LinuxOpenedInodeGenerationState
{
    Captured,

    UnsupportedPlatform,
    InvalidHandle,
    GenerationUnavailable
}

public sealed record LinuxOpenedInodeGenerationResult(
    LinuxOpenedInodeGenerationState State,
    uint? Generation,
    int? Errno,
    string? Error
)
{
    public bool Success =>
        State ==
            LinuxOpenedInodeGenerationState.Captured &&
        Generation is not null;
}
