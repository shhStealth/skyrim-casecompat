using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CaseCompat.Filesystem.Linux;

/*
 * Open exactly one direct child directory relative to an already
 * retained no-follow directory descriptor.
 *
 * This is deliberately narrower than LinuxOpenChildReadOnlyAt:
 * successful return proves that the exact opened child descriptor
 * was accepted by openat() with O_DIRECTORY and O_NOFOLLOW, and the
 * returned capability is therefore a LinuxNoFollowPathHandle suitable
 * for the existing descriptor-relative mutation primitives.
 *
 * No pathname reopen is performed.
 */
public static class LinuxOpenChildDirectoryReadOnlyAt
{
    private const int ORdonly =
        0;

    private const int ODirectory =
        0x10000;

    private const int ONoFollow =
        0x20000;

    private const int OCloexec =
        0x80000;

    private const int ENoEnt =
        2;

    private const int EBadF =
        9;

    private const int ENotDir =
        20;

    private const int ELoop =
        40;

    [DllImport(
        "libc",
        EntryPoint = "openat",
        SetLastError = true)]
    private static extern int OpenAt(
        int dirfd,
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        string pathname,
        int flags
    );

    [DllImport(
        "libc",
        EntryPoint = "close",
        SetLastError = true)]
    private static extern int Close(
        int fd
    );

    public static LinuxOpenChildDirectoryReadOnlyAtResult Open(
        LinuxNoFollowPathHandle parentDirectory,
        string childName)
    {
        ArgumentNullException.ThrowIfNull(
            parentDirectory
        );

        if (!IsValidChildName(childName))
        {
            return Result(
                LinuxOpenChildDirectoryReadOnlyAtState
                    .InvalidName,
                childName,
                error:
                    "The directory name must identify exactly one " +
                    "direct child and cannot be '.', '..', contain " +
                    "path separators, or contain NUL."
            );
        }

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                LinuxOpenChildDirectoryReadOnlyAtState
                    .UnsupportedPlatform,
                childName,
                error:
                    "Descriptor-relative direct-child directory " +
                    "opening is supported on Linux only."
            );
        }

        SafeFileHandle parentHandle =
            parentDirectory.Handle;

        if (
            parentHandle.IsInvalid ||
            parentHandle.IsClosed)
        {
            return Result(
                LinuxOpenChildDirectoryReadOnlyAtState
                    .InvalidParentHandle,
                childName,
                error:
                    "The parent directory descriptor is invalid " +
                    "or closed."
            );
        }

        /*
         * These strings preserve the existing LinuxNoFollowPathHandle
         * metadata convention only. Filesystem authority comes from the
         * retained descriptor opened below, not from these path strings.
         */
        string relativePath =
            parentDirectory.RelativePath == "."
                ? childName
                : parentDirectory.RelativePath
                    .TrimEnd('/', '\\') +
                    "/" +
                    childName;

        string fullPath =
            Path.GetFullPath(
                Path.Combine(
                    parentDirectory.FullPath,
                    childName
                )
            );

        bool addedRef =
            false;

        int childFd =
            -1;

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

            /*
             * O_DIRECTORY is part of the authority boundary here.
             *
             * LinuxOpenChildReadOnlyAt intentionally opens arbitrary
             * direct children for inspection. This primitive instead
             * establishes a retained directory descriptor that can be
             * passed to existing mutation APIs without reopening the
             * child's external pathname.
             *
             * O_NOFOLLOW rejects a symbolic link at the named child.
             */
            childFd =
                OpenAt(
                    parentFd,
                    childName,
                    ORdonly |
                    ODirectory |
                    ONoFollow |
                    OCloexec
                );

            if (childFd < 0)
            {
                int errno =
                    Marshal.GetLastPInvokeError();

                LinuxOpenChildDirectoryReadOnlyAtState state =
                    errno switch
                    {
                        EBadF =>
                            LinuxOpenChildDirectoryReadOnlyAtState
                                .InvalidParentHandle,

                        ENoEnt =>
                            LinuxOpenChildDirectoryReadOnlyAtState
                                .ChildUnavailable,

                        ELoop =>
                            LinuxOpenChildDirectoryReadOnlyAtState
                                .ChildSymbolicLinkRejected,

                        /*
                         * ENOTDIR may describe either a non-directory
                         * child selected with O_DIRECTORY or an invalid
                         * non-directory parent descriptor. In both cases
                         * no directory capability is returned.
                         */
                        ENotDir =>
                            LinuxOpenChildDirectoryReadOnlyAtState
                                .NotDirectory,

                        _ =>
                            LinuxOpenChildDirectoryReadOnlyAtState
                                .ChildOpenFailed
                    };

                return Result(
                    state,
                    childName,
                    errno:
                        errno
                );
            }

            var safeHandle =
                new SafeFileHandle(
                    new IntPtr(
                        childFd
                    ),
                    ownsHandle:
                        true
                );

            childFd =
                -1;

            var openedDirectory =
                new LinuxNoFollowPathHandle(
                    parentDirectory.RootPath,
                    relativePath,
                    fullPath,
                    safeHandle
                );

            return new(
                State:
                    LinuxOpenChildDirectoryReadOnlyAtState
                        .Opened,
                ChildName:
                    childName,
                OpenedDirectory:
                    openedDirectory,
                Errno:
                    null,
                Error:
                    null
            );
        }
        catch (ObjectDisposedException ex)
        {
            return Result(
                LinuxOpenChildDirectoryReadOnlyAtState
                    .InvalidParentHandle,
                childName,
                error:
                    ex.Message
            );
        }
        catch (OverflowException ex)
        {
            return Result(
                LinuxOpenChildDirectoryReadOnlyAtState
                    .InvalidParentHandle,
                childName,
                error:
                    ex.Message
            );
        }
        finally
        {
            if (childFd >= 0)
            {
                Close(
                    childFd
                );
            }

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

    private static LinuxOpenChildDirectoryReadOnlyAtResult Result(
        LinuxOpenChildDirectoryReadOnlyAtState state,
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

        return new(
            State:
                state,
            ChildName:
                childName ??
                string.Empty,
            OpenedDirectory:
                null,
            Errno:
                errno,
            Error:
                error
        );
    }
}
