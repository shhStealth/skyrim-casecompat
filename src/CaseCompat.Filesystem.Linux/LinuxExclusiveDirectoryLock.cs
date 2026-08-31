using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CaseCompat.Filesystem.Linux;

public static class LinuxExclusiveDirectoryLock
{
    private const int OReadOnly =
        0;

    // Linux:
    // O_DIRECTORY = 00200000 octal
    private const int ODirectory =
        0x10000;

    // Linux:
    // O_NOFOLLOW = 00400000 octal
    private const int ONoFollow =
        0x20000;

    // Linux:
    // O_CLOEXEC = 02000000 octal
    private const int OCloseOnExec =
        0x80000;

    private const int LockExclusive =
        2;

    private const int LockNonBlocking =
        4;

    private const int EIntr =
        4;

    private const int EBadF =
        9;

    private const int EWouldBlock =
        11;

    private const int ENotDir =
        20;

    private const int ENoLck =
        37;

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
        EntryPoint = "flock",
        SetLastError = true)]
    private static extern int Flock(
        int fd,
        int operation
    );

    public static LinuxExclusiveDirectoryLockResult Acquire(
        LinuxNoFollowPathHandle parentDirectory)
    {
        ArgumentNullException.ThrowIfNull(
            parentDirectory
        );

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                LinuxExclusiveDirectoryLockState
                    .UnsupportedPlatform,
                error:
                    "Descriptor-backed advisory locking is " +
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
                LinuxExclusiveDirectoryLockState
                    .InvalidParentHandle,
                error:
                    "The directory descriptor is invalid or closed."
            );
        }

        bool parentRef =
            false;

        SafeFileHandle? lockHandle =
            null;

        try
        {
            parentHandle.DangerousAddRef(
                ref parentRef
            );

            int parentFd =
                checked(
                    (int)parentHandle
                        .DangerousGetHandle()
                        .ToInt64()
                );

            /*
             * Open "." relative to the already-open descriptor.
             *
             * This gives the lock lease its own open file
             * description for the exact same physical directory.
             * Pathname replacement of the directory after the
             * caller opened it cannot redirect this operation.
             */
            int lockFd =
                OpenAt(
                    parentFd,
                    ".",
                    OReadOnly |
                    ODirectory |
                    ONoFollow |
                    OCloseOnExec
                );

            if (lockFd < 0)
            {
                int errno =
                    Marshal.GetLastPInvokeError();

                LinuxExclusiveDirectoryLockState state =
                    errno switch
                    {
                        EBadF =>
                            LinuxExclusiveDirectoryLockState
                                .InvalidParentHandle,

                        ENotDir =>
                            LinuxExclusiveDirectoryLockState
                                .ParentNotDirectory,

                        _ =>
                            LinuxExclusiveDirectoryLockState
                                .LockDescriptorOpenFailed
                    };

                return Result(
                    state,
                    errno:
                        errno
                );
            }

            lockHandle =
                new SafeFileHandle(
                    (IntPtr)lockFd,
                    ownsHandle:
                        true
                );

            if (
                Flock(
                    lockFd,
                    LockExclusive |
                    LockNonBlocking
                ) == 0)
            {
                LinuxExclusiveDirectoryLockLease lease =
                    new(
                        lockHandle
                    );

                lockHandle =
                    null;

                return Result(
                    LinuxExclusiveDirectoryLockState
                        .Acquired,
                    lease:
                        lease
                );
            }

            int lockErrno =
                Marshal.GetLastPInvokeError();

            LinuxExclusiveDirectoryLockState lockState =
                lockErrno switch
                {
                    EWouldBlock =>
                        LinuxExclusiveDirectoryLockState
                            .AlreadyLocked,

                    EIntr =>
                        LinuxExclusiveDirectoryLockState
                            .LockInterrupted,

                    ENoLck =>
                        LinuxExclusiveDirectoryLockState
                            .LockResourceUnavailable,

                    EBadF =>
                        LinuxExclusiveDirectoryLockState
                            .LockDescriptorOpenFailed,

                    _ =>
                        LinuxExclusiveDirectoryLockState
                            .LockFailed
                };

            return Result(
                lockState,
                errno:
                    lockErrno
            );
        }
        catch (ObjectDisposedException ex)
        {
            return Result(
                LinuxExclusiveDirectoryLockState
                    .InvalidParentHandle,
                error:
                    ex.Message
            );
        }
        catch (OverflowException ex)
        {
            return Result(
                LinuxExclusiveDirectoryLockState
                    .InvalidParentHandle,
                error:
                    ex.Message
            );
        }
        finally
        {
            lockHandle?.Dispose();

            if (parentRef)
            {
                parentHandle.DangerousRelease();
            }
        }
    }

    private static LinuxExclusiveDirectoryLockResult Result(
        LinuxExclusiveDirectoryLockState state,
        LinuxExclusiveDirectoryLockLease? lease = null,
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

        return new LinuxExclusiveDirectoryLockResult(
            State:
                state,
            Lease:
                lease,
            Errno:
                errno,
            Error:
                error
        );
    }
}
