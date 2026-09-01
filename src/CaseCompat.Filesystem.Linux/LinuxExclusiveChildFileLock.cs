using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CaseCompat.Filesystem.Linux;

/*
 * Persistent descriptor-relative advisory lock on one direct child file.
 *
 * The child is intentionally NOT unlinked when the lease is released.
 * All cooperating users of the same child name therefore continue to
 * flock the same persistent inode.
 *
 * This is a cooperating-process mutex. It is not intended to defend
 * against a hostile same-user process that deliberately unlinks or
 * replaces the lock entry while another process is running.
 */
public static class LinuxExclusiveChildFileLock
{
    // Linux open(2) flags.
    private const int OReadOnly =
        0;

    private const int OCreat =
        0x40;

    private const int ONonBlock =
        0x800;

    private const int ONoFollow =
        0x20000;

    private const int OCloseOnExec =
        0x80000;

    // 0600. The process umask may remove permission bits.
    private const uint Mode0600 =
        0x180;

    private const int LockExclusive =
        2;

    private const int LockNonBlocking =
        4;

    // Linux errno values used for classification.
    private const int EIntr =
        4;

    private const int EBadF =
        9;

    // EWOULDBLOCK == EAGAIN on Linux.
    private const int EWouldBlock =
        11;

    private const int EIsDir =
        21;

    private const int ENotDir =
        20;

    private const int ENoLck =
        37;

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
        int flags,
        uint mode
    );

    [DllImport(
        "libc",
        EntryPoint = "flock",
        SetLastError = true)]
    private static extern int Flock(
        int fd,
        int operation
    );

    [DllImport(
        "libc",
        EntryPoint = "close",
        SetLastError = true)]
    private static extern int Close(
        int fd
    );

    public static LinuxExclusiveChildFileLockResult Acquire(
        LinuxNoFollowPathHandle parentDirectory,
        string childName)
    {
        ArgumentNullException.ThrowIfNull(
            parentDirectory
        );

        if (!IsValidChildName(childName))
        {
            return Result(
                LinuxExclusiveChildFileLockState
                    .InvalidName,
                childName,
                error:
                    "The lock name must identify exactly one direct " +
                    "child and cannot be '.', '..', contain path " +
                    "separators, or contain NUL."
            );
        }

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                LinuxExclusiveChildFileLockState
                    .UnsupportedPlatform,
                childName,
                error:
                    "Descriptor-relative child-file locking is " +
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
                LinuxExclusiveChildFileLockState
                    .InvalidParentHandle,
                childName,
                error:
                    "The parent directory descriptor is invalid " +
                    "or closed."
            );
        }

        bool parentAddedRef =
            false;

        int rawFd =
            -1;

        LinuxOpenedChildHandle? openedChild =
            null;

        try
        {
            parentHandle.DangerousAddRef(
                ref parentAddedRef
            );

            int parentFd =
                checked(
                    (int)parentHandle
                        .DangerousGetHandle()
                        .ToInt64()
                );

            /*
             * O_NONBLOCK is intentional.
             *
             * If an unexpected FIFO occupies the lock name, opening it
             * must not block before the exact opened descriptor can be
             * rejected as a non-regular file.
             *
             * O_NOFOLLOW rejects a symbolic link at the lock name.
             */
            rawFd =
                OpenAt(
                    parentFd,
                    childName,
                    OReadOnly |
                    OCreat |
                    ONonBlock |
                    ONoFollow |
                    OCloseOnExec,
                    Mode0600
                );

            if (rawFd < 0)
            {
                int errno =
                    Marshal.GetLastPInvokeError();

                LinuxExclusiveChildFileLockState state =
                    errno switch
                    {
                        EBadF =>
                            LinuxExclusiveChildFileLockState
                                .InvalidParentHandle,

                        ENotDir =>
                            LinuxExclusiveChildFileLockState
                                .ParentNotDirectory,

                        ELoop =>
                            LinuxExclusiveChildFileLockState
                                .ChildSymbolicLinkRejected,

                        EIsDir =>
                            LinuxExclusiveChildFileLockState
                                .ChildNotRegularFile,

                        _ =>
                            LinuxExclusiveChildFileLockState
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
                        rawFd
                    ),
                    ownsHandle:
                        true
                );

            rawFd =
                -1;

            openedChild =
                new LinuxOpenedChildHandle(
                    childName,
                    safeHandle
                );

            /*
             * Validate the exact descriptor before flocking it.
             *
             * This rejects directories, FIFOs, devices, sockets, and
             * other non-regular objects without trusting the pathname.
             */
            LinuxOpenedFileIdentityResult identity =
                LinuxOpenedFileIdentity.Capture(
                    openedChild
                );

            if (!identity.Success)
            {
                return Result(
                    identity.State ==
                    LinuxOpenedFileIdentityState
                        .NotRegularFile
                        ? LinuxExclusiveChildFileLockState
                            .ChildNotRegularFile
                        : LinuxExclusiveChildFileLockState
                            .ChildIdentityUnavailable,
                    childName,
                    openedIdentity:
                        identity,
                    errno:
                        identity.Errno,
                    error:
                        identity.Error ??
                        identity.State.ToString()
                );
            }

            SafeFileHandle lockHandle =
                openedChild.Handle;

            bool lockAddedRef =
                false;

            try
            {
                lockHandle.DangerousAddRef(
                    ref lockAddedRef
                );

                int lockFd =
                    checked(
                        (int)lockHandle
                            .DangerousGetHandle()
                            .ToInt64()
                    );

                while (true)
                {
                    if (
                        Flock(
                            lockFd,
                            LockExclusive |
                            LockNonBlocking
                        ) == 0)
                    {
                        break;
                    }

                    int errno =
                        Marshal.GetLastPInvokeError();

                    if (errno == EIntr)
                    {
                        continue;
                    }

                    LinuxExclusiveChildFileLockState state =
                        errno switch
                        {
                            EWouldBlock =>
                                LinuxExclusiveChildFileLockState
                                    .AlreadyLocked,

                            ENoLck =>
                                LinuxExclusiveChildFileLockState
                                    .LockTableUnavailable,

                            EBadF =>
                                LinuxExclusiveChildFileLockState
                                    .ChildOpenFailed,

                            _ =>
                                LinuxExclusiveChildFileLockState
                                    .LockFailed
                        };

                    return Result(
                        state,
                        childName,
                        openedIdentity:
                            identity,
                        errno:
                            errno
                    );
                }
            }
            finally
            {
                if (lockAddedRef)
                {
                    lockHandle.DangerousRelease();
                }
            }

            var lease =
                new LinuxExclusiveChildFileLockLease(
                    childName,
                    openedChild
                );

            /*
             * Ownership of the descriptor transfers to the lease.
             * Closing that descriptor releases flock().
             *
             * The filesystem entry itself is intentionally retained.
             */
            openedChild =
                null;

            return new(
                State:
                    LinuxExclusiveChildFileLockState
                        .Locked,
                ChildName:
                    childName,
                OpenedIdentity:
                    identity,
                Lease:
                    lease,
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
                LinuxExclusiveChildFileLockState
                    .InvalidParentHandle,
                childName,
                error:
                    ex.Message
            );
        }
        catch (
            OverflowException ex)
        {
            return Result(
                LinuxExclusiveChildFileLockState
                    .InvalidParentHandle,
                childName,
                error:
                    ex.Message
            );
        }
        finally
        {
            openedChild?.Dispose();

            if (rawFd >= 0)
            {
                Close(
                    rawFd
                );
            }

            if (parentAddedRef)
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

    private static LinuxExclusiveChildFileLockResult Result(
        LinuxExclusiveChildFileLockState state,
        string? childName,
        LinuxOpenedFileIdentityResult? openedIdentity = null,
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
            OpenedIdentity:
                openedIdentity,
            Lease:
                null,
            Errno:
                errno,
            Error:
                error
        );
    }
}
