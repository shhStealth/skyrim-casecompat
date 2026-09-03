using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CaseCompat.Filesystem.Linux;

/*
 * Inspect exactly one direct child relative to an already retained
 * directory descriptor.
 *
 * statx() is called with AT_SYMLINK_NOFOLLOW, so this reports metadata
 * for the named child itself and never follows a final symbolic link.
 *
 * No child file or directory is opened by this primitive.
 */
public static class LinuxInspectChildAt
{
    private const int AtSymlinkNofollow =
        0x100;

    private const uint StatxBasicStats =
        0x000007ff;

    private const uint StatxMountId =
        0x00001000;

    private const ushort SIfmt =
        0xF000;

    private const ushort SIfdir =
        0x4000;

    private const ushort SIfreg =
        0x8000;

    private const ushort SIfLnk =
        0xA000;

    private const int ENoEnt =
        2;

    private const int EBadF =
        9;

    private const int ENotDir =
        20;

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

    public static LinuxInspectChildAtResult Inspect(
        ILinuxOpenedHandle parentDirectory,
        string childName)
    {
        ArgumentNullException.ThrowIfNull(
            parentDirectory
        );

        if (!IsValidChildName(childName))
        {
            return Result(
                LinuxInspectChildAtState.InvalidName,
                childName,
                error:
                    "The child name must identify exactly one " +
                    "direct child and cannot be '.', '..', " +
                    "contain path separators, or contain NUL."
            );
        }

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                LinuxInspectChildAtState
                    .UnsupportedPlatform,
                childName,
                error:
                    "Descriptor-relative child inspection is " +
                    "supported on Linux only."
            );
        }

        SafeFileHandle parentHandle =
            parentDirectory.Handle;

        if (
            parentHandle.IsInvalid ||
            parentHandle.IsClosed)
        {
            return Result(
                LinuxInspectChildAtState
                    .InvalidParentHandle,
                childName,
                error:
                    "The parent directory descriptor is " +
                    "invalid or closed."
            );
        }

        bool addedRef =
            false;

        try
        {
            parentHandle.DangerousAddRef(
                ref addedRef
            );

            int parentFd =
                checked(
                    (int)parentHandle
                        .DangerousGetHandle()
                        .ToInt64()
                );

            if (
                Statx(
                    parentFd,
                    childName,
                    AtSymlinkNofollow,
                    StatxBasicStats |
                    StatxMountId,
                    out StatxBuffer metadata
                ) < 0)
            {
                int errno =
                    Marshal.GetLastPInvokeError();

                LinuxInspectChildAtState state =
                    errno switch
                    {
                        EBadF =>
                            LinuxInspectChildAtState
                                .InvalidParentHandle,

                        ENotDir =>
                            LinuxInspectChildAtState
                                .ParentNotDirectory,

                        ENoEnt =>
                            LinuxInspectChildAtState
                                .ChildUnavailable,

                        _ =>
                            LinuxInspectChildAtState
                                .MetadataUnavailable
                    };

                return Result(
                    state,
                    childName,
                    errno:
                        errno
                );
            }

            if (
                (metadata.Mask & StatxMountId) !=
                StatxMountId)
            {
                return Result(
                    LinuxInspectChildAtState
                        .MetadataUnavailable,
                    childName,
                    error:
                        "The child mount identity is unavailable."
                );
            }

            ushort objectType =
                (ushort)(
                    metadata.Mode &
                    SIfmt
                );

            LinuxChildObjectKind kind =
                objectType switch
                {
                    SIfdir =>
                        LinuxChildObjectKind.Directory,

                    SIfreg =>
                        LinuxChildObjectKind.RegularFile,

                    SIfLnk =>
                        LinuxChildObjectKind.SymbolicLink,

                    _ =>
                        LinuxChildObjectKind.Other
                };

            return new LinuxInspectChildAtResult(
                State:
                    LinuxInspectChildAtState
                        .Inspected,
                ChildName:
                    childName,
                Kind:
                    kind,
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
                LinuxInspectChildAtState
                    .InvalidParentHandle,
                childName,
                error:
                    ex.Message
            );
        }
        catch (OverflowException ex)
        {
            return Result(
                LinuxInspectChildAtState
                    .InvalidParentHandle,
                childName,
                error:
                    ex.Message
            );
        }
        catch (DllNotFoundException ex)
        {
            return Result(
                LinuxInspectChildAtState
                    .UnsupportedPlatform,
                childName,
                error:
                    ex.Message
            );
        }
        catch (EntryPointNotFoundException ex)
        {
            return Result(
                LinuxInspectChildAtState
                    .UnsupportedPlatform,
                childName,
                error:
                    ex.Message
            );
        }
        finally
        {
            if (addedRef)
            {
                parentHandle.DangerousRelease();
            }
        }
    }

    private static bool IsValidChildName(
        string? childName)
    {
        if (
            string.IsNullOrEmpty(
                childName
            ) ||
            childName is "." or "..")
        {
            return false;
        }

        return
            !childName.Contains('/') &&
            !childName.Contains('\\') &&
            !childName.Contains('\0');
    }

    private static LinuxInspectChildAtResult Result(
        LinuxInspectChildAtState state,
        string? childName,
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

        return new LinuxInspectChildAtResult(
            State:
                state,
            ChildName:
                childName ??
                string.Empty,
            Kind:
                null,
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
