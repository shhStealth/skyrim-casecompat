using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CaseCompat.Filesystem.Linux;

public static class LinuxDirectoryFlags
{
    // Linux uapi:
    // #define FS_CASEFOLD_FL 0x40000000
    public const long FsCasefoldFlag = 0x40000000L;

    // Linux:
    // #define FS_IOC_GETFLAGS _IOR('f', 1, long)
    //
    // On 64-bit Linux:
    // _IOR('f', 1, 8) = 0x80086601
    private const ulong FsIocGetFlags = 0x80086601UL;

    // Linux open(2) flags.
    private const int ORdonly = 0;
    private const int ODirectory = 0x10000;
    private const int ONoFollow = 0x20000;
    private const int OCloexec = 0x80000;

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int Open(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string pathname,
        int flags
    );

    [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    private static extern int Ioctl(
        int fd,
        ulong request,
        ref long value
    );

    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    private static extern int Close(int fd);

    public static DirectoryCasefoldResult Inspect(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A directory path is required.", nameof(path));
        }

        string fullPath = Path.GetFullPath(path);

        if (!Directory.Exists(fullPath))
        {
            return new DirectoryCasefoldResult(
                FullPath: fullPath,
                Exists: false,
                CasefoldEnabled: null,
                RawFlags: null,
                Error: null
            );
        }

        if (!OperatingSystem.IsLinux())
        {
            return new DirectoryCasefoldResult(
                FullPath: fullPath,
                Exists: true,
                CasefoldEnabled: null,
                RawFlags: null,
                Error: "Directory flag inspection is supported on Linux only."
            );
        }

        int fd = Open(
            fullPath,
            ORdonly | ODirectory | ONoFollow | OCloexec
        );

        if (fd < 0)
        {
            int errno = Marshal.GetLastPInvokeError();

            return new DirectoryCasefoldResult(
                FullPath: fullPath,
                Exists: true,
                CasefoldEnabled: null,
                RawFlags: null,
                Error: new Win32Exception(errno).Message
            );
        }

        try
        {
            long flags = 0;

            if (Ioctl(fd, FsIocGetFlags, ref flags) < 0)
            {
                int errno = Marshal.GetLastPInvokeError();

                return new DirectoryCasefoldResult(
                    FullPath: fullPath,
                    Exists: true,
                    CasefoldEnabled: null,
                    RawFlags: null,
                    Error: new Win32Exception(errno).Message
                );
            }

            return new DirectoryCasefoldResult(
                FullPath: fullPath,
                Exists: true,
                CasefoldEnabled: HasCasefoldFlag(flags),
                RawFlags: flags,
                Error: null
            );
        }
        finally
        {
            Close(fd);
        }
    }

    public static bool HasCasefoldFlag(long flags)
    {
        return (flags & FsCasefoldFlag) != 0;
    }
}
