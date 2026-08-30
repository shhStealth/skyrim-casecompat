using Microsoft.Win32.SafeHandles;

namespace CaseCompat.Filesystem.Linux;

public enum LinuxNoFollowPathOpenState
{
    Opened,

    UnsupportedPlatform,
    InvalidRelativePath,

    RootUnavailable,
    RootNotDirectoryOrSymbolicLink,
    RootOpenFailed,

    ComponentUnavailable,
    ComponentNotDirectoryOrSymbolicLink,
    ComponentOpenFailed,

    TargetUnavailable,
    TargetSymbolicLinkRejected,
    TargetOpenFailed
}

public sealed class LinuxNoFollowPathHandle
    : IDisposable
{
    internal LinuxNoFollowPathHandle(
        string rootPath,
        string relativePath,
        string fullPath,
        SafeFileHandle handle)
    {
        RootPath = rootPath;
        RelativePath = relativePath;
        FullPath = fullPath;
        Handle = handle;
    }

    public string RootPath { get; }

    public string RelativePath { get; }

    public string FullPath { get; }

    public SafeFileHandle Handle { get; }

    public void Dispose()
    {
        Handle.Dispose();
    }
}

public sealed record LinuxNoFollowPathOpenResult(
    LinuxNoFollowPathOpenState State,
    string RootPath,
    string RelativePath,
    string? FullPath,
    LinuxNoFollowPathHandle? OpenedPath,
    int? Errno,
    string? Error
)
{
    public bool Success =>
        State ==
            LinuxNoFollowPathOpenState.Opened &&
        OpenedPath is not null;
}
