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

    internal object PublicationGate { get; } =
        new();

    public void Dispose()
    {
        Handle.Dispose();
    }
}
