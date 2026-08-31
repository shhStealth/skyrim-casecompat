using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CaseCompat.Filesystem.Linux;

public static class LinuxOpenedDirectorySnapshot
{
    // Linux uapi:
    // #define AT_EMPTY_PATH 0x1000
    private const int AtEmptyPath = 0x1000;

    private const uint StatxBasicStats = 0x000007ff;
    private const uint StatxMountId = 0x00001000;

    // Linux inode type bits.
    private const ushort SIfmt = 0xF000;
    private const ushort SIfdir = 0x4000;

    // Linux:
    // #define FS_IOC_GETFLAGS _IOR('f', 1, long)
    //
    // On 64-bit Linux:
    // _IOR('f', 1, 8) = 0x80086601
    private const ulong FsIocGetFlags = 0x80086601UL;

    [StructLayout(
        LayoutKind.Explicit,
        Size = 256)]
    private struct StatxBuffer
    {
        [FieldOffset(0)]
        public uint Mask;

        [FieldOffset(16)]
        public uint LinkCount;

        [FieldOffset(28)]
        public ushort Mode;

        [FieldOffset(32)]
        public ulong Inode;

        [FieldOffset(136)]
        public uint DeviceMajor;

        [FieldOffset(140)]
        public uint DeviceMinor;

        [FieldOffset(144)]
        public ulong MountId;
    }

    [DllImport(
        "libc",
        EntryPoint = "statx",
        SetLastError = true)]
    private static extern int Statx(
        int dirfd,
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        string pathname,
        int flags,
        uint mask,
        out StatxBuffer statxbuf
    );

    [DllImport(
        "libc",
        EntryPoint = "ioctl",
        SetLastError = true)]
    private static extern int Ioctl(
        int fd,
        ulong request,
        ref long value
    );

    public static LinuxOpenedDirectorySnapshotResult Capture(
        LinuxNoFollowPathHandle openedPath)
    {
        ArgumentNullException.ThrowIfNull(
            openedPath
        );

        return Capture(
            openedPath,
            openedPath.FullPath
        );
    }

    public static LinuxOpenedDirectorySnapshotResult Capture(
        ILinuxOpenedHandle openedHandle,
        string displayPath)
    {
        ArgumentNullException.ThrowIfNull(
            openedHandle
        );

        if (string.IsNullOrWhiteSpace(displayPath))
        {
            throw new ArgumentException(
                "A diagnostic display path is required.",
                nameof(displayPath)
            );
        }

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                LinuxOpenedDirectorySnapshotState
                    .UnsupportedPlatform,
                displayPath,
                error:
                    "Opened-directory snapshotting is " +
                    "supported on Linux only."
            );
        }

        SafeFileHandle handle =
            openedHandle.Handle;

        if (
            handle.IsInvalid ||
            handle.IsClosed)
        {
            return Result(
                LinuxOpenedDirectorySnapshotState
                    .InvalidHandle,
                displayPath,
                error:
                    "The opened directory handle is " +
                    "invalid or closed."
            );
        }

        bool addedRef =
            false;

        try
        {
            handle.DangerousAddRef(
                ref addedRef
            );

            int fd =
                checked(
                    (int)handle
                        .DangerousGetHandle()
                        .ToInt64()
                );

            if (
                Statx(
                    fd,
                    string.Empty,
                    AtEmptyPath,
                    StatxBasicStats |
                    StatxMountId,
                    out StatxBuffer metadata
                ) < 0)
            {
                int errno =
                    Marshal.GetLastPInvokeError();

                return Result(
                    LinuxOpenedDirectorySnapshotState
                        .MetadataUnavailable,
                    displayPath,
                    errno:
                        errno
                );
            }

            if (
                (metadata.Mode & SIfmt) !=
                SIfdir)
            {
                return Result(
                    LinuxOpenedDirectorySnapshotState
                        .NotDirectory,
                    displayPath,
                    error:
                        "The opened target is not a directory."
                );
            }

            var identity =
                new LinuxFileIdentityResult(
                    FullPath:
                        displayPath,
                    DeviceMajor:
                        metadata.DeviceMajor,
                    DeviceMinor:
                        metadata.DeviceMinor,
                    Inode:
                        metadata.Inode,
                    LinkCount:
                        metadata.LinkCount,
                    MountId:
                        metadata.MountId,
                    Error:
                        null
                );

            long flags =
                0;

            if (
                Ioctl(
                    fd,
                    FsIocGetFlags,
                    ref flags
                ) < 0)
            {
                int errno =
                    Marshal.GetLastPInvokeError();

                return Result(
                    LinuxOpenedDirectorySnapshotState
                        .FlagsUnavailable,
                    displayPath,
                    identity:
                        identity,
                    errno:
                        errno
                );
            }

            return new LinuxOpenedDirectorySnapshotResult(
                State:
                    LinuxOpenedDirectorySnapshotState
                        .Captured,
                FullPath:
                    displayPath,
                Identity:
                    identity,
                CasefoldEnabled:
                    LinuxDirectoryFlags
                        .HasCasefoldFlag(
                            flags
                        ),
                RawFlags:
                    flags,
                Errno:
                    null,
                Error:
                    null
            );
        }
        catch (
            ObjectDisposedException ex)
        {
            return Result(
                LinuxOpenedDirectorySnapshotState
                    .InvalidHandle,
                displayPath,
                error:
                    ex.Message
            );
        }
        catch (
            OverflowException ex)
        {
            return Result(
                LinuxOpenedDirectorySnapshotState
                    .InvalidHandle,
                displayPath,
                error:
                    ex.Message
            );
        }
        finally
        {
            if (addedRef)
            {
                handle.DangerousRelease();
            }
        }
    }

    private static LinuxOpenedDirectorySnapshotResult Result(
        LinuxOpenedDirectorySnapshotState state,
        string fullPath,
        LinuxFileIdentityResult? identity = null,
        int? errno = null,
        string? error = null)
    {
        if (
            error is null &&
            errno is int value)
        {
            error =
                new Win32Exception(
                    value
                ).Message;
        }

        return new LinuxOpenedDirectorySnapshotResult(
            State:
                state,
            FullPath:
                fullPath,
            Identity:
                identity,
            CasefoldEnabled:
                null,
            RawFlags:
                null,
            Errno:
                errno,
            Error:
                error
        );
    }
}
