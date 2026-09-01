namespace CaseCompat.Filesystem.Linux;

public enum LinuxEnumerateDirectoryAtState
{
    Enumerated,

    UnsupportedPlatform,
    InvalidDirectoryHandle,
    NotDirectory,

    EnumerationFailed,
    InvalidDirectoryEntry
}

public sealed record LinuxEnumerateDirectoryAtResult(
    LinuxEnumerateDirectoryAtState State,
    IReadOnlyList<string> ChildNames,
    int? Errno,
    string? Error
)
{
    public bool Success =>
        State ==
        LinuxEnumerateDirectoryAtState.Enumerated;
}
