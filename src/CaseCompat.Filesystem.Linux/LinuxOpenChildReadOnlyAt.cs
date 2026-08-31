using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CaseCompat.Filesystem.Linux;

public static class LinuxOpenChildReadOnlyAt
{
    private const int ORdonly =
        0;

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

    public static LinuxOpenChildReadOnlyAtResult Open(
        LinuxNoFollowPathHandle parentDirectory,
        string childName)
    {
        ArgumentNullException.ThrowIfNull(
            parentDirectory
        );

        return Open(
            (ILinuxOpenedHandle)parentDirectory,
            childName
        );
    }

    public static LinuxOpenChildReadOnlyAtResult Open(
        ILinuxOpenedHandle parentDirectory,
        string childName)
    {
        ArgumentNullException.ThrowIfNull(
            parentDirectory
        );

        if (!IsValidChildName(childName))
        {
            return Result(
                LinuxOpenChildReadOnlyAtState
                    .InvalidName,
                childName,
                error:
                    "The child name must identify exactly one " +
                    "direct child and cannot be '.', '..', " +
                    "or contain path separators or NUL."
            );
        }

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                LinuxOpenChildReadOnlyAtState
                    .UnsupportedPlatform,
                childName,
                error:
                    "Descriptor-relative child opening is " +
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
                LinuxOpenChildReadOnlyAtState
                    .InvalidParentHandle,
                childName,
                error:
                    "The parent directory descriptor is " +
                    "invalid or closed."
            );
        }

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

            childFd =
                OpenAt(
                    parentFd,
                    childName,
                    ORdonly |
                    ONoFollow |
                    OCloexec
                );

            if (childFd < 0)
            {
                int errno =
                    Marshal.GetLastPInvokeError();

                LinuxOpenChildReadOnlyAtState state =
                    errno switch
                    {
                        EBadF =>
                            LinuxOpenChildReadOnlyAtState
                                .InvalidParentHandle,

                        ENotDir =>
                            LinuxOpenChildReadOnlyAtState
                                .ParentNotDirectory,

                        ENoEnt =>
                            LinuxOpenChildReadOnlyAtState
                                .ChildUnavailable,

                        ELoop =>
                            LinuxOpenChildReadOnlyAtState
                                .ChildSymbolicLinkRejected,

                        _ =>
                            LinuxOpenChildReadOnlyAtState
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

            var openedChild =
                new LinuxOpenedChildHandle(
                    childName,
                    safeHandle
                );

            return new LinuxOpenChildReadOnlyAtResult(
                State:
                    LinuxOpenChildReadOnlyAtState
                        .Opened,
                ChildName:
                    childName,
                OpenedChild:
                    openedChild,
                Errno:
                    null,
                Error:
                    null
            );
        }
        catch (ObjectDisposedException ex)
        {
            return Result(
                LinuxOpenChildReadOnlyAtState
                    .InvalidParentHandle,
                childName,
                error:
                    ex.Message
            );
        }
        catch (OverflowException ex)
        {
            return Result(
                LinuxOpenChildReadOnlyAtState
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

    private static LinuxOpenChildReadOnlyAtResult Result(
        LinuxOpenChildReadOnlyAtState state,
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

        return new LinuxOpenChildReadOnlyAtResult(
            State:
                state,
            ChildName:
                childName ?? string.Empty,
            OpenedChild:
                null,
            Errno:
                errno,
            Error:
                error
        );
    }
}
