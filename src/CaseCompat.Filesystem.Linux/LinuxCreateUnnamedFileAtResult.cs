namespace CaseCompat.Filesystem.Linux;

public enum LinuxCreateUnnamedFileAtState
{
    Created,

    UnsupportedPlatform,
    InvalidParentHandle,
    ParentNotDirectory,

    TmpfileUnsupported,
    CreateFailed
}

public sealed record LinuxCreateUnnamedFileAtResult(
    LinuxCreateUnnamedFileAtState State,
    LinuxUnnamedFileHandle? OpenedFile,
    int? Errno,
    string? Error
)
{
    public bool Success =>
        State ==
            LinuxCreateUnnamedFileAtState.Created &&
        OpenedFile is not null;
}
