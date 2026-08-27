namespace CaseCompat.Filesystem.Linux;

public sealed record DirectoryProbeResult(
    string RequestedPath,
    string FullPath,
    bool Exists,
    bool IsLinux
);
