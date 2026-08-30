using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CaseCompat.Filesystem.Linux;

public static class LinuxOpenedFileIdentity
{
    private const int AtEmptyPath =
        0x1000;

    private const uint StatxBasicStats =
        0x000007ff;

    private const uint StatxMountId =
        0x00001000;

    private const ushort SIfmt =
        0xF000;

    private const ushort SIfreg =
        0x8000;

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

    public static LinuxOpenedFileIdentityResult Capture(
        ILinuxOpenedHandle openedFile)
    {
        ArgumentNullException.ThrowIfNull(
            openedFile
        );

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                LinuxOpenedFileIdentityState
                    .UnsupportedPlatform,
                error:
                    "Opened-file identity capture is " +
                    "supported on Linux only."
            );
        }

        SafeFileHandle handle =
            openedFile.Handle;

        if (
            handle.IsInvalid ||
            handle.IsClosed)
        {
            return Result(
                LinuxOpenedFileIdentityState
                    .InvalidHandle,
                error:
                    "The opened file descriptor is invalid " +
                    "or closed."
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
                    LinuxOpenedFileIdentityState
                        .MetadataUnavailable,
                    errno:
                        errno
                );
            }

            if (
                (metadata.Mode & SIfmt) !=
                SIfreg)
            {
                return Result(
                    LinuxOpenedFileIdentityState
                        .NotRegularFile,
                    error:
                        "The opened descriptor does not " +
                        "refer to a regular file."
                );
            }

            return new LinuxOpenedFileIdentityResult(
                State:
                    LinuxOpenedFileIdentityState
                        .Captured,
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
                Errno:
                    null,
                Error:
                    null
            );
        }
        catch (ObjectDisposedException ex)
        {
            return Result(
                LinuxOpenedFileIdentityState
                    .InvalidHandle,
                error:
                    ex.Message
            );
        }
        catch (OverflowException ex)
        {
            return Result(
                LinuxOpenedFileIdentityState
                    .InvalidHandle,
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

    private static LinuxOpenedFileIdentityResult Result(
        LinuxOpenedFileIdentityState state,
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

        return new LinuxOpenedFileIdentityResult(
            State:
                state,
            DeviceMajor:
                null,
            DeviceMinor:
                null,
            Inode:
                null,
            LinkCount:
                null,
            MountId:
                null,
            Errno:
                errno,
            Error:
                error
        );
    }
}
