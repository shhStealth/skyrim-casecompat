using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CaseCompat.Filesystem.Linux;

public static class LinuxOpenedInodeGeneration
{
    /*
     * Linux UAPI:
     *
     *   #define FS_IOC_GETVERSION _IOR('v', 1, long)
     *
     * x86_64 Linux therefore encodes this as 0x80087601.
     *
     * ext4 returns i_generation through this ioctl.
     * The ioctl payload is machine-long-sized, while the ext4
     * generation value occupies the low 32 bits.
     */
    private const ulong FsIocGetVersion =
        0x80087601UL;

    [DllImport(
        "libc",
        EntryPoint = "ioctl",
        SetLastError = true)]
    private static extern int Ioctl(
        int fd,
        ulong request,
        ref long value
    );

    public static LinuxOpenedInodeGenerationResult Capture(
        ILinuxOpenedHandle openedHandle)
    {
        ArgumentNullException.ThrowIfNull(
            openedHandle
        );

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                LinuxOpenedInodeGenerationState
                    .UnsupportedPlatform,
                error:
                    "Opened inode generation capture is " +
                    "supported on Linux only."
            );
        }

        SafeFileHandle handle =
            openedHandle.Handle;

        if (
            handle.IsInvalid ||
            handle.IsClosed)
        {
            return Result(
                LinuxOpenedInodeGenerationState
                    .InvalidHandle,
                error:
                    "The opened filesystem handle is invalid " +
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

            int fd =
                checked(
                    (int)handle
                        .DangerousGetHandle()
                        .ToInt64()
                );

            long rawGeneration =
                0;

            if (
                Ioctl(
                    fd,
                    FsIocGetVersion,
                    ref rawGeneration
                ) < 0)
            {
                int errno =
                    Marshal.GetLastPInvokeError();

                return Result(
                    LinuxOpenedInodeGenerationState
                        .GenerationUnavailable,
                    errno:
                        errno
                );
            }

            uint generation =
                unchecked(
                    (uint)rawGeneration
                );

            return Result(
                LinuxOpenedInodeGenerationState.Captured,
                generation:
                    generation
            );
        }
        catch (ObjectDisposedException ex)
        {
            return Result(
                LinuxOpenedInodeGenerationState.InvalidHandle,
                error:
                    ex.Message
            );
        }
        catch (OverflowException ex)
        {
            return Result(
                LinuxOpenedInodeGenerationState.InvalidHandle,
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

    private static LinuxOpenedInodeGenerationResult Result(
        LinuxOpenedInodeGenerationState state,
        uint? generation = null,
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
            Generation:
                generation,
            Errno:
                errno,
            Error:
                error
        );
    }
}
