using Microsoft.Win32.SafeHandles;

namespace CaseCompat.Filesystem.Linux;

public sealed class LinuxOpenedChildHandle
    : ILinuxOpenedHandle
{
    internal LinuxOpenedChildHandle(
        string childName,
        SafeFileHandle handle)
    {
        ChildName =
            childName;

        Handle =
            handle;
    }

    public string ChildName { get; }

    public SafeFileHandle Handle { get; }

    public void Dispose()
    {
        Handle.Dispose();
    }
}
