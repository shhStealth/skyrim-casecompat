using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CaseCompat.Filesystem.Linux;

/*
 * Open exactly one direct child regular file relative to an already
 * retained directory descriptor without first opening an untrusted
 * filesystem object for I/O.
 *
 * Authority sequence:
 *
 * 1. openat(parent, child, O_PATH | O_NOFOLLOW | O_CLOEXEC)
 * 2. prove the retained O_PATH descriptor is a regular file
 * 3. reopen that descriptor through /proc/self/fd for read access
 * 4. prove the readable descriptor names the same physical object
 *
 * The external child pathname is not reopened after step 1.
 *
 * If procfs descriptor reopening is unavailable, this primitive refuses.
 * There is intentionally no pathname fallback.
 */
public static class LinuxOpenChildRegularFileReadOnlyAt
{
    private const int ORdonly =
        0;

    // Linux uapi:
    // #define O_NOFOLLOW 00400000 octal
    private const int ONoFollow =
        0x20000;

    // Linux uapi:
    // #define O_CLOEXEC 02000000 octal
    private const int OCloexec =
        0x80000;

    // Linux uapi:
    // #define O_PATH 010000000 octal
    private const int OPath =
        0x200000;

    private const int ENoEnt =
        2;

    private const int EBadF =
        9;

    private const int ENotDir =
        20;

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
        EntryPoint = "open",
        SetLastError = true)]
    private static extern int OpenNative(
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

    public static LinuxOpenChildRegularFileReadOnlyAtResult Open(
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

    public static LinuxOpenChildRegularFileReadOnlyAtResult Open(
        ILinuxOpenedHandle parentDirectory,
        string childName)
    {
        ArgumentNullException.ThrowIfNull(
            parentDirectory
        );

        if (!IsValidChildName(childName))
        {
            return Result(
                LinuxOpenChildRegularFileReadOnlyAtState
                    .InvalidName,
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
                LinuxOpenChildRegularFileReadOnlyAtState
                    .UnsupportedPlatform,
                childName,
                error:
                    "Descriptor-safe regular-file opening is " +
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
                LinuxOpenChildRegularFileReadOnlyAtState
                    .InvalidParentHandle,
                childName,
                error:
                    "The parent directory descriptor is invalid " +
                    "or closed."
            );
        }

        bool parentAddedRef =
            false;

        int capabilityFd =
            -1;

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

            capabilityFd =
                OpenAt(
                    parentFd,
                    childName,
                    OPath |
                    ONoFollow |
                    OCloexec
                );

            if (capabilityFd < 0)
            {
                int errno =
                    Marshal.GetLastPInvokeError();

                LinuxOpenChildRegularFileReadOnlyAtState state =
                    errno switch
                    {
                        EBadF =>
                            LinuxOpenChildRegularFileReadOnlyAtState
                                .InvalidParentHandle,

                        ENotDir =>
                            LinuxOpenChildRegularFileReadOnlyAtState
                                .ParentNotDirectory,

                        ENoEnt =>
                            LinuxOpenChildRegularFileReadOnlyAtState
                                .ChildUnavailable,

                        _ =>
                            LinuxOpenChildRegularFileReadOnlyAtState
                                .CapabilityOpenFailed
                    };

                return Result(
                    state,
                    childName,
                    errno:
                        errno
                );
            }

            var capabilitySafeHandle =
                new SafeFileHandle(
                    new IntPtr(
                        capabilityFd
                    ),
                    ownsHandle:
                        true
                );

            capabilityFd =
                -1;

            using var capability =
                new LinuxOpenedChildHandle(
                    childName,
                    capabilitySafeHandle
                );

            LinuxOpenedFileIdentityResult
                capabilityIdentity =
                    LinuxOpenedFileIdentity.Capture(
                        capability
                    );

            if (
                capabilityIdentity.State ==
                LinuxOpenedFileIdentityState
                    .NotRegularFile)
            {
                return Result(
                    LinuxOpenChildRegularFileReadOnlyAtState
                        .ChildNotRegularFile,
                    childName,
                    error:
                        capabilityIdentity.Error ??
                        "The retained child capability does not " +
                        "refer to a regular file."
                );
            }

            if (!capabilityIdentity.Success)
            {
                return Result(
                    LinuxOpenChildRegularFileReadOnlyAtState
                        .CapabilityIdentityUnavailable,
                    childName,
                    error:
                        capabilityIdentity.Error ??
                        capabilityIdentity.State.ToString()
                );
            }

            SafeFileHandle capabilityHandle =
                capability.Handle;

            bool capabilityAddedRef =
                false;

            int readableFd =
                -1;

            try
            {
                capabilityHandle.DangerousAddRef(
                    ref capabilityAddedRef
                );

                int retainedCapabilityFd =
                    checked(
                        (int)capabilityHandle
                            .DangerousGetHandle()
                            .ToInt64()
                    );

                string procFdPath =
                    $"/proc/self/fd/{retainedCapabilityFd}";

                /*
                 * Following this procfs magic link is intentional.
                 *
                 * It refers to the already retained O_PATH capability.
                 * We do not return to the external child pathname.
                 */
                readableFd =
                    OpenNative(
                        procFdPath,
                        ORdonly |
                        OCloexec
                    );

                if (readableFd < 0)
                {
                    int errno =
                        Marshal.GetLastPInvokeError();

                    return Result(
                        LinuxOpenChildRegularFileReadOnlyAtState
                            .ReadableOpenFailed,
                        childName,
                        errno:
                            errno
                    );
                }

                var readableSafeHandle =
                    new SafeFileHandle(
                        new IntPtr(
                            readableFd
                        ),
                        ownsHandle:
                            true
                    );

                readableFd =
                    -1;

                var openedFile =
                    new LinuxOpenedChildHandle(
                        childName,
                        readableSafeHandle
                    );

                LinuxOpenedFileIdentityResult
                    readableIdentity =
                        LinuxOpenedFileIdentity.Capture(
                            openedFile
                        );

                if (!readableIdentity.Success)
                {
                    openedFile.Dispose();

                    return Result(
                        LinuxOpenChildRegularFileReadOnlyAtState
                            .ReadableIdentityUnavailable,
                        childName,
                        error:
                            readableIdentity.Error ??
                            readableIdentity.State.ToString()
                    );
                }

                if (!capabilityIdentity.SameObjectAs(
                        readableIdentity))
                {
                    openedFile.Dispose();

                    return Result(
                        LinuxOpenChildRegularFileReadOnlyAtState
                            .IdentityMismatch,
                        childName,
                        error:
                            "The readable descriptor does not " +
                            "identify the same physical regular " +
                            "file as the retained O_PATH capability."
                    );
                }

                return new LinuxOpenChildRegularFileReadOnlyAtResult(
                    State:
                        LinuxOpenChildRegularFileReadOnlyAtState
                            .Opened,
                    ChildName:
                        childName,
                    OpenedFile:
                        openedFile,
                    Identity:
                        readableIdentity,
                    Errno:
                        null,
                    Error:
                        null
                );
            }
            finally
            {
                if (readableFd >= 0)
                {
                    Close(
                        readableFd
                    );
                }

                if (capabilityAddedRef)
                {
                    capabilityHandle.DangerousRelease();
                }
            }
        }
        catch (ObjectDisposedException ex)
        {
            return Result(
                LinuxOpenChildRegularFileReadOnlyAtState
                    .InvalidParentHandle,
                childName,
                error:
                    ex.Message
            );
        }
        catch (OverflowException ex)
        {
            return Result(
                LinuxOpenChildRegularFileReadOnlyAtState
                    .InvalidParentHandle,
                childName,
                error:
                    ex.Message
            );
        }
        catch (DllNotFoundException ex)
        {
            return Result(
                LinuxOpenChildRegularFileReadOnlyAtState
                    .UnsupportedPlatform,
                childName,
                error:
                    ex.Message
            );
        }
        catch (EntryPointNotFoundException ex)
        {
            return Result(
                LinuxOpenChildRegularFileReadOnlyAtState
                    .UnsupportedPlatform,
                childName,
                error:
                    ex.Message
            );
        }
        finally
        {
            if (capabilityFd >= 0)
            {
                Close(
                    capabilityFd
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

    private static LinuxOpenChildRegularFileReadOnlyAtResult Result(
        LinuxOpenChildRegularFileReadOnlyAtState state,
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

        return new LinuxOpenChildRegularFileReadOnlyAtResult(
            State:
                state,
            ChildName:
                childName ?? string.Empty,
            OpenedFile:
                null,
            Identity:
                null,
            Errno:
                errno,
            Error:
                error
        );
    }
}
