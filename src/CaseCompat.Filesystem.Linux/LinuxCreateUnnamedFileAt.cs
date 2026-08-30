using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CaseCompat.Filesystem.Linux;

public static class LinuxCreateUnnamedFileAt
{
    // Linux open(2) flags from asm-generic/fcntl.h.
    private const int ORdwr =
        1 << 1;

    private const int ODirectory =
        1 << 16;

    private const int OCloexec =
        1 << 19;

    private const int OTmpfileInternal =
        1 << 22;

    private const int OTmpfile =
        OTmpfileInternal |
        ODirectory;

    // Linux errno values used for classification.
    private const int ENoEnt = 2;
    private const int EBadF = 9;
    private const int EIsDir = 21;
    private const int ENotDir = 20;
    private const int EOpNotSupp = 95;

    // 0644. The process umask may remove permission bits.
    private const uint Mode0644 =
        0x1A4;

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
        EntryPoint = "close",
        SetLastError = true)]
    private static extern int Close(
        int fd
    );

    public static LinuxCreateUnnamedFileAtResult Create(
        LinuxNoFollowPathHandle parentDirectory)
    {
        ArgumentNullException.ThrowIfNull(
            parentDirectory
        );

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                LinuxCreateUnnamedFileAtState
                    .UnsupportedPlatform,
                error:
                    "Unnamed temporary-file creation is " +
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
                LinuxCreateUnnamedFileAtState
                    .InvalidParentHandle,
                error:
                    "The parent directory handle is invalid " +
                    "or closed."
            );
        }

        bool addedRef =
            false;

        int createdFd =
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

            // "." resolves relative to the already-open dirfd.
            //
            // Do not add O_EXCL here. With O_TMPFILE,
            // O_EXCL prevents the unnamed inode from later
            // being linked into the filesystem.
            createdFd =
                OpenAt(
                    parentFd,
                    ".",
                    ORdwr |
                    OTmpfile |
                    OCloexec,
                    Mode0644
                );

            if (createdFd < 0)
            {
                int errno =
                    Marshal.GetLastPInvokeError();

                LinuxCreateUnnamedFileAtState state =
                    errno switch
                    {
                        EBadF =>
                            LinuxCreateUnnamedFileAtState
                                .InvalidParentHandle,

                        ENotDir =>
                            LinuxCreateUnnamedFileAtState
                                .ParentNotDirectory,

                        // EOPNOTSUPP is the documented
                        // filesystem-level O_TMPFILE failure.
                        //
                        // EISDIR and ENOENT are also documented
                        // when detecting kernels without
                        // O_TMPFILE functionality.
                        EOpNotSupp or
                        EIsDir or
                        ENoEnt =>
                            LinuxCreateUnnamedFileAtState
                                .TmpfileUnsupported,

                        _ =>
                            LinuxCreateUnnamedFileAtState
                                .CreateFailed
                    };

                return Result(
                    state,
                    errno:
                        errno
                );
            }

            var safeHandle =
                new SafeFileHandle(
                    new IntPtr(
                        createdFd
                    ),
                    ownsHandle:
                        true
                );

            createdFd =
                -1;

            var openedFile =
                new LinuxUnnamedFileHandle(
                    safeHandle
                );

            return new LinuxCreateUnnamedFileAtResult(
                State:
                    LinuxCreateUnnamedFileAtState
                        .Created,
                OpenedFile:
                    openedFile,
                Errno:
                    null,
                Error:
                    null
            );
        }
        catch (ObjectDisposedException ex)
        {
            return Result(
                LinuxCreateUnnamedFileAtState
                    .InvalidParentHandle,
                error:
                    ex.Message
            );
        }
        catch (OverflowException ex)
        {
            return Result(
                LinuxCreateUnnamedFileAtState
                    .InvalidParentHandle,
                error:
                    ex.Message
            );
        }
        finally
        {
            if (createdFd >= 0)
            {
                Close(
                    createdFd
                );
            }

            if (addedRef)
            {
                parentHandle.DangerousRelease();
            }
        }
    }

    private static LinuxCreateUnnamedFileAtResult Result(
        LinuxCreateUnnamedFileAtState state,
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

        return new LinuxCreateUnnamedFileAtResult(
            State:
                state,
            OpenedFile:
                null,
            Errno:
                errno,
            Error:
                error
        );
    }
}
