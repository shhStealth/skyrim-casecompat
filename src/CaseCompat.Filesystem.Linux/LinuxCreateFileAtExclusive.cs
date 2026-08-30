using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CaseCompat.Filesystem.Linux;

public static class LinuxCreateFileAtExclusive
{
    // Linux open(2) flags.
    private const int ORdwr = 2;
    private const int OCreat = 0x40;
    private const int OExcl = 0x80;
    private const int ONoFollow = 0x20000;
    private const int OCloexec = 0x80000;

    // Linux errno values used for classification.
    private const int EBadF = 9;
    private const int EExist = 17;
    private const int ENotDir = 20;

    // 0644. The process umask may remove permission bits.
    private const uint Mode0644 = 0x1A4;

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

    public static LinuxCreateFileAtExclusiveResult Create(
        LinuxNoFollowPathHandle parentDirectory,
        string childName)
    {
        ArgumentNullException.ThrowIfNull(
            parentDirectory
        );

        if (!IsValidChildName(childName))
        {
            return Result(
                LinuxCreateFileAtExclusiveState
                    .InvalidName,
                childName,
                error:
                    "The file name must identify exactly " +
                    "one direct child and cannot be '.', '..', " +
                    "or contain path separators or NUL."
            );
        }

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                LinuxCreateFileAtExclusiveState
                    .UnsupportedPlatform,
                childName,
                error:
                    "Descriptor-relative exclusive file " +
                    "creation is supported on Linux only."
            );
        }

        SafeFileHandle parentHandle =
            parentDirectory.Handle;

        if (
            parentHandle.IsInvalid ||
            parentHandle.IsClosed)
        {
            return Result(
                LinuxCreateFileAtExclusiveState
                    .InvalidParentHandle,
                childName,
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

            createdFd =
                OpenAt(
                    parentFd,
                    childName,
                    ORdwr |
                    OCreat |
                    OExcl |
                    ONoFollow |
                    OCloexec,
                    Mode0644
                );

            if (createdFd < 0)
            {
                int errno =
                    Marshal.GetLastPInvokeError();

                LinuxCreateFileAtExclusiveState state =
                    errno switch
                    {
                        EExist =>
                            LinuxCreateFileAtExclusiveState
                                .DestinationExists,

                        ENotDir =>
                            LinuxCreateFileAtExclusiveState
                                .ParentNotDirectory,

                        EBadF =>
                            LinuxCreateFileAtExclusiveState
                                .InvalidParentHandle,

                        _ =>
                            LinuxCreateFileAtExclusiveState
                                .CreateFailed
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
                        createdFd
                    ),
                    ownsHandle:
                        true
                );

            createdFd =
                -1;

            string relativePath =
                parentDirectory.RelativePath == "."
                    ? childName
                    : parentDirectory.RelativePath
                        .TrimEnd('/') +
                        "/" +
                        childName;

            string fullPath =
                Path.GetFullPath(
                    Path.Combine(
                        parentDirectory.FullPath,
                        childName
                    )
                );

            var openedPath =
                new LinuxNoFollowPathHandle(
                    parentDirectory.RootPath,
                    relativePath,
                    fullPath,
                    safeHandle
                );

            return new LinuxCreateFileAtExclusiveResult(
                State:
                    LinuxCreateFileAtExclusiveState
                        .Created,
                ChildName:
                    childName,
                OpenedPath:
                    openedPath,
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
                LinuxCreateFileAtExclusiveState
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
                LinuxCreateFileAtExclusiveState
                    .InvalidParentHandle,
                childName,
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

    private static LinuxCreateFileAtExclusiveResult Result(
        LinuxCreateFileAtExclusiveState state,
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

        return new LinuxCreateFileAtExclusiveResult(
            State:
                state,
            ChildName:
                childName ?? string.Empty,
            OpenedPath:
                null,
            Errno:
                errno,
            Error:
                error
        );
    }
}
