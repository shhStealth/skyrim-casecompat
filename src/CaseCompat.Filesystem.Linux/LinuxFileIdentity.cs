using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CaseCompat.Filesystem.Linux;

public sealed record LinuxFileIdentityResult(
    string FullPath,
    uint? DeviceMajor,
    uint? DeviceMinor,
    ulong? Inode,
    uint? LinkCount,
    ulong? MountId,
    string? Error
)
{
    public bool Success => Error is null;

    public bool SameObjectAs(LinuxFileIdentityResult other)
    {
        return Success
            && other.Success
            && DeviceMajor == other.DeviceMajor
            && DeviceMinor == other.DeviceMinor
            && Inode == other.Inode;
    }
}

public static class LinuxFileIdentity
{
    private const int AtFdcwd = -100;
    private const int AtSymlinkNofollow = 0x100;

    private const uint StatxBasicStats = 0x000007ff;
    private const uint StatxMountId = 0x00001000;

    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct StatxBuffer
    {
        [FieldOffset(0)]
        public uint Mask;

        [FieldOffset(16)]
        public uint LinkCount;

        [FieldOffset(32)]
        public ulong Inode;

        [FieldOffset(136)]
        public uint DeviceMajor;

        [FieldOffset(140)]
        public uint DeviceMinor;

        [FieldOffset(144)]
        public ulong MountId;
    }

    [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static extern int Statx(
        int dirfd,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string pathname,
        int flags,
        uint mask,
        out StatxBuffer statxbuf
    );

    public static LinuxFileIdentityResult Inspect(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A path is required.", nameof(path));
        }

        string fullPath = Path.GetFullPath(path);

        if (!OperatingSystem.IsLinux())
        {
            return Failure(
                fullPath,
                "File identity inspection is supported on Linux only."
            );
        }

        int result = Statx(
            AtFdcwd,
            fullPath,
            AtSymlinkNofollow,
            StatxBasicStats | StatxMountId,
            out StatxBuffer buffer
        );

        if (result < 0)
        {
            int errno = Marshal.GetLastPInvokeError();

            return Failure(
                fullPath,
                new Win32Exception(errno).Message
            );
        }

        return new LinuxFileIdentityResult(
            FullPath: fullPath,
            DeviceMajor: buffer.DeviceMajor,
            DeviceMinor: buffer.DeviceMinor,
            Inode: buffer.Inode,
            LinkCount: buffer.LinkCount,
            MountId: buffer.MountId,
            Error: null
        );
    }

    private static LinuxFileIdentityResult Failure(
        string fullPath,
        string error
    )
    {
        return new LinuxFileIdentityResult(
            FullPath: fullPath,
            DeviceMajor: null,
            DeviceMinor: null,
            Inode: null,
            LinkCount: null,
            MountId: null,
            Error: error
        );
    }
}
