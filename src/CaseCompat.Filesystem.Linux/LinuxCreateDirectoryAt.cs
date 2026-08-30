using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CaseCompat.Filesystem.Linux;

public static class LinuxCreateDirectoryAt
{
    // Linux errno values used for classification.
    private const int EBadF = 9;
    private const int EExist = 17;
    private const int ENotDir = 20;

    // 0755. The process umask may remove permission bits.
    private const uint Mode0755 = 0x1ED;

    [DllImport(
        "libc",
        EntryPoint = "mkdirat",
        SetLastError = true)]
    private static extern int MkdirAt(
        int dirfd,
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        string pathname,
        uint mode
    );

    public static LinuxCreateDirectoryAtResult Create(
        LinuxNoFollowPathHandle parentDirectory,
        string childName)
    {
        ArgumentNullException.ThrowIfNull(
            parentDirectory
        );

        if (!IsValidChildName(childName))
        {
            return Result(
                LinuxCreateDirectoryAtState
                    .InvalidName,
                childName,
                error:
                    "The directory name must identify exactly " +
                    "one direct child and cannot be '.', '..', " +
                    "or contain path separators or NUL."
            );
        }

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                LinuxCreateDirectoryAtState
                    .UnsupportedPlatform,
                childName,
                error:
                    "Descriptor-relative directory creation is " +
                    "supported on Linux only."
            );
        }

        SafeFileHandle handle =
            parentDirectory.Handle;

        if (
            handle.IsInvalid ||
            handle.IsClosed)
        {
            return Result(
                LinuxCreateDirectoryAtState
                    .InvalidParentHandle,
                childName,
                error:
                    "The parent directory handle is invalid " +
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

            int parentFd =
                checked(
                    (int)handle
                        .DangerousGetHandle()
                        .ToInt64()
                );

            if (
                MkdirAt(
                    parentFd,
                    childName,
                    Mode0755
                ) == 0)
            {
                return Result(
                    LinuxCreateDirectoryAtState
                        .Created,
                    childName
                );
            }

            int errno =
                Marshal.GetLastPInvokeError();

            LinuxCreateDirectoryAtState state =
                errno switch
                {
                    EExist =>
                        LinuxCreateDirectoryAtState
                            .DestinationExists,

                    ENotDir =>
                        LinuxCreateDirectoryAtState
                            .ParentNotDirectory,

                    EBadF =>
                        LinuxCreateDirectoryAtState
                            .InvalidParentHandle,

                    _ =>
                        LinuxCreateDirectoryAtState
                            .CreateFailed
                };

            return Result(
                state,
                childName,
                errno:
                    errno
            );
        }
        catch (
            ObjectDisposedException ex)
        {
            return Result(
                LinuxCreateDirectoryAtState
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
                LinuxCreateDirectoryAtState
                    .InvalidParentHandle,
                childName,
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

    private static LinuxCreateDirectoryAtResult Result(
        LinuxCreateDirectoryAtState state,
        string childName,
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

        return new LinuxCreateDirectoryAtResult(
            State:
                state,
            ChildName:
                childName,
            Errno:
                errno,
            Error:
                error
        );
    }
}
