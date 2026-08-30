using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CaseCompat.Filesystem.Linux;

public static class LinuxFsync
{
    [DllImport(
        "libc",
        EntryPoint = "fsync",
        SetLastError = true)]
    private static extern int Fsync(
        int fd
    );

    public static LinuxFsyncResult Sync(
        LinuxNoFollowPathHandle openedPath)
    {
        ArgumentNullException.ThrowIfNull(
            openedPath
        );

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                LinuxFsyncState.UnsupportedPlatform,
                error:
                    "Descriptor fsync is supported on Linux only."
            );
        }

        SafeFileHandle handle =
            openedPath.Handle;

        if (
            handle.IsInvalid ||
            handle.IsClosed)
        {
            return Result(
                LinuxFsyncState.InvalidHandle,
                error:
                    "The descriptor is invalid or closed."
            );
        }

        bool addedRef =
            false;

        try
        {
            handle.DangerousAddRef(
                ref addedRef
            );

            int fd =
                checked(
                    (int)handle
                        .DangerousGetHandle()
                        .ToInt64()
                );

            if (
                Fsync(
                    fd
                ) == 0)
            {
                return Result(
                    LinuxFsyncState.Synced
                );
            }

            int errno =
                Marshal.GetLastPInvokeError();

            return Result(
                LinuxFsyncState.SyncFailed,
                errno:
                    errno
            );
        }
        catch (ObjectDisposedException ex)
        {
            return Result(
                LinuxFsyncState.InvalidHandle,
                error:
                    ex.Message
            );
        }
        catch (OverflowException ex)
        {
            return Result(
                LinuxFsyncState.InvalidHandle,
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

    private static LinuxFsyncResult Result(
        LinuxFsyncState state,
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

        return new LinuxFsyncResult(
            State:
                state,
            Errno:
                errno,
            Error:
                error
        );
    }
}
