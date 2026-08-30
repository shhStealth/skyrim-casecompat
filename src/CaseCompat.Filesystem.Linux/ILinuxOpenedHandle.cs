using Microsoft.Win32.SafeHandles;

namespace CaseCompat.Filesystem.Linux;

public interface ILinuxOpenedHandle
    : IDisposable
{
    SafeFileHandle Handle { get; }
}
