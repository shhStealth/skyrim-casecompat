using Microsoft.Win32.SafeHandles;

namespace CaseCompat.Filesystem.Linux;

public sealed class LinuxUnnamedFileHandle
    : ILinuxOpenedHandle
{
    internal LinuxUnnamedFileHandle(
        SafeFileHandle handle)
    {
        Handle =
            handle;
    }

    public SafeFileHandle Handle { get; }

    public void Dispose()
    {
        Handle.Dispose();
    }
}
