using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CaseCompat.Filesystem.Linux;

public static class LinuxNoFollowPath
{
    // Linux open(2) flags.
    private const int ORdonly = 0;
    private const int ODirectory = 0x10000;
    private const int ONoFollow = 0x20000;
    private const int OCloexec = 0x80000;

    // Linux errno values used only for classification.
    private const int ENoEnt = 2;
    private const int ENotDir = 20;
    private const int ELoop = 40;

    [DllImport(
        "libc",
        EntryPoint = "open",
        SetLastError = true)]
    private static extern int Open(
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        string pathname,
        int flags
    );

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

    public static LinuxNoFollowPathOpenResult
        OpenReadOnlyUnderRoot(
            string rootPath,
            string relativePath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException(
                "A root path is required.",
                nameof(rootPath)
            );
        }

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException(
                "A relative path is required.",
                nameof(relativePath)
            );
        }

        string root =
            Path.GetFullPath(
                rootPath
            );

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                LinuxNoFollowPathOpenState
                    .UnsupportedPlatform,
                root,
                relativePath,
                error:
                    "No-follow path opening is " +
                    "supported on Linux only."
            );
        }

        string[]? components =
            SplitRelativePath(
                relativePath
            );

        if (components is null)
        {
            return Result(
                LinuxNoFollowPathOpenState
                    .InvalidRelativePath,
                root,
                relativePath,
                error:
                    "The target must be a Data-relative " +
                    "path without traversal components."
            );
        }

        string normalizedRelative =
            string.Join(
                '/',
                components
            );

        string displayFullPath =
            Path.GetFullPath(
                Path.Combine(
                    root,
                    Path.Combine(
                        components
                    )
                )
            );

        int parentFd =
            Open(
                root,
                ORdonly |
                ODirectory |
                ONoFollow |
                OCloexec
            );

        if (parentFd < 0)
        {
            int errno =
                Marshal.GetLastPInvokeError();

            return RootFailure(
                root,
                normalizedRelative,
                displayFullPath,
                errno
            );
        }

        try
        {
            for (
                int index = 0;
                index < components.Length - 1;
                index++)
            {
                int childFd =
                    OpenAt(
                        parentFd,
                        components[index],
                        ORdonly |
                        ODirectory |
                        ONoFollow |
                        OCloexec
                    );

                if (childFd < 0)
                {
                    int errno =
                        Marshal.GetLastPInvokeError();

                    return ComponentFailure(
                        root,
                        normalizedRelative,
                        displayFullPath,
                        errno
                    );
                }

                Close(
                    parentFd
                );

                parentFd =
                    childFd;
            }

            int targetFd =
                OpenAt(
                    parentFd,
                    components[^1],
                    ORdonly |
                    ONoFollow |
                    OCloexec
                );

            if (targetFd < 0)
            {
                int errno =
                    Marshal.GetLastPInvokeError();

                return TargetFailure(
                    root,
                    normalizedRelative,
                    displayFullPath,
                    errno
                );
            }

            var handle =
                new SafeFileHandle(
                    new IntPtr(
                        targetFd
                    ),
                    ownsHandle:
                        true
                );

            return new LinuxNoFollowPathOpenResult(
                State:
                    LinuxNoFollowPathOpenState
                        .Opened,
                RootPath:
                    root,
                RelativePath:
                    normalizedRelative,
                FullPath:
                    displayFullPath,
                OpenedPath:
                    new LinuxNoFollowPathHandle(
                        root,
                        normalizedRelative,
                        displayFullPath,
                        handle
                    ),
                Errno:
                    null,
                Error:
                    null
            );
        }
        finally
        {
            Close(
                parentFd
            );
        }
    }

    private static string[]? SplitRelativePath(
        string path)
    {
        if (
            Path.IsPathRooted(
                path
            ) ||
            path.StartsWith(
                '\\'
            ) ||
            (
                path.Length >= 2 &&
                char.IsLetter(
                    path[0]
                ) &&
                path[1] == ':'
            ))
        {
            return null;
        }

        string[] components =
            path.Split(
                ['/', '\\'],
                StringSplitOptions
                    .RemoveEmptyEntries
            );

        if (
            components.Length == 0 ||
            components.Any(component =>
                component is "." or ".."
            ))
        {
            return null;
        }

        return components;
    }

    private static LinuxNoFollowPathOpenResult
        RootFailure(
            string root,
            string relativePath,
            string fullPath,
            int errno)
    {
        LinuxNoFollowPathOpenState state =
            errno switch
            {
                ENoEnt =>
                    LinuxNoFollowPathOpenState
                        .RootUnavailable,

                ELoop or ENotDir =>
                    LinuxNoFollowPathOpenState
                        .RootNotDirectoryOrSymbolicLink,

                _ =>
                    LinuxNoFollowPathOpenState
                        .RootOpenFailed
            };

        return Result(
            state,
            root,
            relativePath,
            fullPath,
            errno
        );
    }

    private static LinuxNoFollowPathOpenResult
        ComponentFailure(
            string root,
            string relativePath,
            string fullPath,
            int errno)
    {
        LinuxNoFollowPathOpenState state =
            errno switch
            {
                ENoEnt =>
                    LinuxNoFollowPathOpenState
                        .ComponentUnavailable,

                ELoop or ENotDir =>
                    LinuxNoFollowPathOpenState
                        .ComponentNotDirectoryOrSymbolicLink,

                _ =>
                    LinuxNoFollowPathOpenState
                        .ComponentOpenFailed
            };

        return Result(
            state,
            root,
            relativePath,
            fullPath,
            errno
        );
    }

    private static LinuxNoFollowPathOpenResult
        TargetFailure(
            string root,
            string relativePath,
            string fullPath,
            int errno)
    {
        LinuxNoFollowPathOpenState state =
            errno switch
            {
                ENoEnt =>
                    LinuxNoFollowPathOpenState
                        .TargetUnavailable,

                ELoop =>
                    LinuxNoFollowPathOpenState
                        .TargetSymbolicLinkRejected,

                _ =>
                    LinuxNoFollowPathOpenState
                        .TargetOpenFailed
            };

        return Result(
            state,
            root,
            relativePath,
            fullPath,
            errno
        );
    }

    private static LinuxNoFollowPathOpenResult Result(
        LinuxNoFollowPathOpenState state,
        string root,
        string relativePath,
        string? fullPath = null,
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

        return new LinuxNoFollowPathOpenResult(
            State:
                state,
            RootPath:
                root,
            RelativePath:
                relativePath,
            FullPath:
                fullPath,
            OpenedPath:
                null,
            Errno:
                errno,
            Error:
                error
        );
    }
}
