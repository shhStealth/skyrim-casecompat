using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace CaseCompat.Filesystem.Linux;

// Reads the raw target of exactly one direct child, relative to an
// already retained directory descriptor. The child must itself be a
// symbolic link; nothing is followed or resolved beyond this one
// readlinkat() call, and the returned target is returned exactly as
// stored - never combined with any path, never re-opened.
//
// This is the only primitive in this project that reads through a
// symbolic link at all, and it deliberately only ever reads the raw
// target string. Deciding whether that target is trustworthy (does it
// match a durable, previously-recorded alias?) is entirely the
// caller's responsibility - this primitive carries no opinion about
// that.
public static class LinuxReadSymlinkAt
{
    private const int ENoEnt = 2;
    private const int EBadF = 9;
    private const int EInval = 22;
    private const int ENotDir = 20;

    private const int BufferSize = 4096;

    [DllImport(
        "libc",
        EntryPoint = "readlinkat",
        SetLastError = true)]
    private static extern int ReadLinkAt(
        int dirfd,
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        string pathname,
        byte[] buf,
        int bufsiz
    );

    public static LinuxReadSymlinkAtResult Read(
        ILinuxOpenedHandle parentDirectory,
        string childName)
    {
        ArgumentNullException.ThrowIfNull(
            parentDirectory
        );

        if (!IsValidChildName(childName))
        {
            return Result(
                LinuxReadSymlinkAtState.InvalidName,
                childName,
                error:
                    "The child name must identify exactly one direct " +
                    "child and cannot be '.', '..', or contain path " +
                    "separators or NUL."
            );
        }

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                LinuxReadSymlinkAtState.UnsupportedPlatform,
                childName,
                error:
                    "Descriptor-relative symlink reading is " +
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
                LinuxReadSymlinkAtState.InvalidParentHandle,
                childName,
                error:
                    "The parent directory handle is invalid or closed."
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

            byte[] buffer =
                new byte[BufferSize];

            int written =
                ReadLinkAt(
                    parentFd,
                    childName,
                    buffer,
                    buffer.Length
                );

            if (written < 0)
            {
                int errno =
                    Marshal.GetLastPInvokeError();

                LinuxReadSymlinkAtState state =
                    errno switch
                    {
                        ENoEnt =>
                            LinuxReadSymlinkAtState.ChildUnavailable,

                        EInval =>
                            LinuxReadSymlinkAtState
                                .ChildNotSymbolicLink,

                        EBadF =>
                            LinuxReadSymlinkAtState.InvalidParentHandle,

                        ENotDir =>
                            LinuxReadSymlinkAtState.ParentNotDirectory,

                        _ =>
                            LinuxReadSymlinkAtState.ReadFailed
                    };

                return Result(
                    state,
                    childName,
                    errno:
                        errno
                );
            }

            if (written >= buffer.Length)
            {
                return Result(
                    LinuxReadSymlinkAtState.TargetTooLong,
                    childName,
                    error:
                        "The symbolic link target may have been " +
                        "truncated."
                );
            }

            string target =
                Encoding.UTF8.GetString(
                    buffer,
                    0,
                    written
                );

            return new LinuxReadSymlinkAtResult(
                State:
                    LinuxReadSymlinkAtState.Read,
                ChildName:
                    childName,
                Target:
                    target,
                Errno:
                    null,
                Error:
                    null
            );
        }
        catch (ObjectDisposedException ex)
        {
            return Result(
                LinuxReadSymlinkAtState.InvalidParentHandle,
                childName,
                error:
                    ex.Message
            );
        }
        catch (OverflowException ex)
        {
            return Result(
                LinuxReadSymlinkAtState.InvalidParentHandle,
                childName,
                error:
                    ex.Message
            );
        }
        catch (DllNotFoundException ex)
        {
            return Result(
                LinuxReadSymlinkAtState.UnsupportedPlatform,
                childName,
                error:
                    ex.Message
            );
        }
        catch (EntryPointNotFoundException ex)
        {
            return Result(
                LinuxReadSymlinkAtState.UnsupportedPlatform,
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

    private static LinuxReadSymlinkAtResult Result(
        LinuxReadSymlinkAtState state,
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

        return new LinuxReadSymlinkAtResult(
            State:
                state,
            ChildName:
                childName,
            Target:
                null,
            Errno:
                errno,
            Error:
                error
        );
    }
}
