using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CaseCompat.Filesystem.Linux;

// Creates a relative symbolic link as one direct child of an already
// retained directory descriptor, pointing at a sibling by bare name
// only - never an absolute path, never a path containing directory
// separators.
//
// This mirrors the same-parent invariant every other rename-flavored
// primitive in this project already relies on: a symlink alias this
// project creates always points at a sibling in its own immediate
// parent, nothing else. Restricting the target to a bare name here,
// at the primitive level, makes that invariant impossible to violate
// by accident from any caller.
public static class LinuxCreateSymlinkAt
{
    private const int EExist = 17;
    private const int ENotDir = 20;
    private const int EBadF = 9;

    [DllImport(
        "libc",
        EntryPoint = "symlinkat",
        SetLastError = true)]
    private static extern int SymlinkAt(
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        string target,
        int newdirfd,
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        string linkpath
    );

    public static LinuxCreateSymlinkAtResult Create(
        ILinuxOpenedHandle parentDirectory,
        string linkName,
        string targetName)
    {
        ArgumentNullException.ThrowIfNull(
            parentDirectory
        );

        if (!IsValidBareName(linkName))
        {
            return Result(
                LinuxCreateSymlinkAtState.InvalidLinkName,
                linkName,
                targetName,
                error:
                    "The link name must identify exactly one direct " +
                    "child and cannot be '.', '..', or contain path " +
                    "separators or NUL."
            );
        }

        if (!IsValidBareName(targetName))
        {
            return Result(
                LinuxCreateSymlinkAtState.InvalidTargetName,
                linkName,
                targetName,
                error:
                    "The alias target must be a bare sibling name and " +
                    "cannot be '.', '..', or contain path separators " +
                    "or NUL."
            );
        }

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                LinuxCreateSymlinkAtState.UnsupportedPlatform,
                linkName,
                targetName,
                error:
                    "Descriptor-relative symlink creation is " +
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
                LinuxCreateSymlinkAtState.InvalidParentHandle,
                linkName,
                targetName,
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

            if (
                SymlinkAt(
                    targetName,
                    parentFd,
                    linkName
                ) == 0)
            {
                return Result(
                    LinuxCreateSymlinkAtState.Created,
                    linkName,
                    targetName
                );
            }

            int errno =
                Marshal.GetLastPInvokeError();

            LinuxCreateSymlinkAtState state =
                errno switch
                {
                    EExist =>
                        LinuxCreateSymlinkAtState.DestinationExists,

                    ENotDir =>
                        LinuxCreateSymlinkAtState.ParentNotDirectory,

                    EBadF =>
                        LinuxCreateSymlinkAtState.InvalidParentHandle,

                    _ =>
                        LinuxCreateSymlinkAtState.CreateFailed
                };

            return Result(
                state,
                linkName,
                targetName,
                errno:
                    errno
            );
        }
        catch (ObjectDisposedException ex)
        {
            return Result(
                LinuxCreateSymlinkAtState.InvalidParentHandle,
                linkName,
                targetName,
                error:
                    ex.Message
            );
        }
        catch (OverflowException ex)
        {
            return Result(
                LinuxCreateSymlinkAtState.InvalidParentHandle,
                linkName,
                targetName,
                error:
                    ex.Message
            );
        }
        catch (DllNotFoundException ex)
        {
            return Result(
                LinuxCreateSymlinkAtState.UnsupportedPlatform,
                linkName,
                targetName,
                error:
                    ex.Message
            );
        }
        catch (EntryPointNotFoundException ex)
        {
            return Result(
                LinuxCreateSymlinkAtState.UnsupportedPlatform,
                linkName,
                targetName,
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

    private static bool IsValidBareName(
        string? name)
    {
        if (
            string.IsNullOrEmpty(
                name
            ) ||
            name is "." or "..")
        {
            return false;
        }

        return
            !name.Contains('/') &&
            !name.Contains('\\') &&
            !name.Contains('\0');
    }

    private static LinuxCreateSymlinkAtResult Result(
        LinuxCreateSymlinkAtState state,
        string linkName,
        string targetName,
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

        return new LinuxCreateSymlinkAtResult(
            State:
                state,
            LinkName:
                linkName,
            TargetName:
                targetName,
            Errno:
                errno,
            Error:
                error
        );
    }
}
