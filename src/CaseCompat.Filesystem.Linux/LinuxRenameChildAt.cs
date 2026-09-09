using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CaseCompat.Filesystem.Linux;

// Descriptor-relative, no-clobber rename of exactly one direct child,
// optionally moving it between two open parent directories. Backed by
// renameat2(2) with RENAME_NOREPLACE, so the kernel atomically refuses
// the rename when the destination name already exists - there is no
// separate preflight-then-act race window the caller has to reason
// about.
//
// This is a metadata-only operation: no bytes are read or copied, and
// the child's inode is unchanged by a successful rename. Callers that
// need proof of exactly which physical object ended up at the
// destination should capture a fresh incarnation identity there after
// a successful rename, exactly as they would after any other mutation
// in this pipeline.
public static class LinuxRenameChildAt
{
    // Linux errno values used for classification.
    private const int EPerm = 1;
    private const int ENoEnt = 2;
    private const int EBadF = 9;
    private const int EExist = 17;
    private const int EXdev = 18;
    private const int ENotDir = 20;
    private const int EIsDir = 21;
    private const int EInval = 22;

    // RENAME_NOREPLACE from linux/fs.h.
    private const uint RenameNoReplace = 1;

    [DllImport(
        "libc",
        EntryPoint = "renameat2",
        SetLastError = true)]
    private static extern int RenameAt2(
        int olddirfd,
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        string oldpath,
        int newdirfd,
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        string newpath,
        uint flags
    );

    public static LinuxRenameChildAtResult Rename(
        ILinuxOpenedHandle sourceParent,
        string sourceChildName,
        ILinuxOpenedHandle destinationParent,
        string destinationChildName)
    {
        ArgumentNullException.ThrowIfNull(
            sourceParent
        );

        ArgumentNullException.ThrowIfNull(
            destinationParent
        );

        if (!IsValidChildName(sourceChildName))
        {
            return Result(
                LinuxRenameChildAtState.InvalidSourceName,
                sourceChildName,
                destinationChildName,
                error:
                    "The source name must identify exactly one " +
                    "direct child and cannot be '.', '..', or " +
                    "contain path separators or NUL."
            );
        }

        if (!IsValidChildName(destinationChildName))
        {
            return Result(
                LinuxRenameChildAtState.InvalidDestinationName,
                sourceChildName,
                destinationChildName,
                error:
                    "The destination name must identify exactly one " +
                    "direct child and cannot be '.', '..', or " +
                    "contain path separators or NUL."
            );
        }

        if (
            ReferenceEquals(
                sourceParent,
                destinationParent
            ) &&
            string.Equals(
                sourceChildName,
                destinationChildName,
                StringComparison.Ordinal
            ))
        {
            return Result(
                LinuxRenameChildAtState.SameNameSameParent,
                sourceChildName,
                destinationChildName,
                error:
                    "Source and destination identify the same name " +
                    "in the same parent."
            );
        }

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                LinuxRenameChildAtState.UnsupportedPlatform,
                sourceChildName,
                destinationChildName,
                error:
                    "Descriptor-relative rename is supported on " +
                    "Linux only."
            );
        }

        SafeFileHandle sourceHandle =
            sourceParent.Handle;

        if (
            sourceHandle.IsInvalid ||
            sourceHandle.IsClosed)
        {
            return Result(
                LinuxRenameChildAtState.InvalidSourceParentHandle,
                sourceChildName,
                destinationChildName,
                error:
                    "The source parent directory handle is invalid " +
                    "or closed."
            );
        }

        SafeFileHandle destinationHandle =
            destinationParent.Handle;

        if (
            destinationHandle.IsInvalid ||
            destinationHandle.IsClosed)
        {
            return Result(
                LinuxRenameChildAtState.InvalidDestinationParentHandle,
                sourceChildName,
                destinationChildName,
                error:
                    "The destination parent directory handle is " +
                    "invalid or closed."
            );
        }

        bool sourceRefAdded =
            false;

        bool destinationRefAdded =
            false;

        try
        {
            sourceHandle.DangerousAddRef(
                ref sourceRefAdded
            );

            destinationHandle.DangerousAddRef(
                ref destinationRefAdded
            );

            int sourceFd =
                checked(
                    (int)sourceHandle
                        .DangerousGetHandle()
                        .ToInt64()
                );

            int destinationFd =
                checked(
                    (int)destinationHandle
                        .DangerousGetHandle()
                        .ToInt64()
                );

            int callResult;

            try
            {
                callResult =
                    RenameAt2(
                        sourceFd,
                        sourceChildName,
                        destinationFd,
                        destinationChildName,
                        RenameNoReplace
                    );
            }
            catch (
                Exception ex)
                when (
                    ex is DllNotFoundException or
                    EntryPointNotFoundException)
            {
                return Result(
                    LinuxRenameChildAtState.UnsupportedPlatform,
                    sourceChildName,
                    destinationChildName,
                    error:
                        "renameat2(2) with RENAME_NOREPLACE is not " +
                        "available on this system: " +
                        ex.Message
                );
            }

            if (callResult == 0)
            {
                return Result(
                    LinuxRenameChildAtState.Renamed,
                    sourceChildName,
                    destinationChildName
                );
            }

            int errno =
                Marshal.GetLastPInvokeError();

            LinuxRenameChildAtState state =
                errno switch
                {
                    EExist =>
                        LinuxRenameChildAtState
                            .DestinationExists,

                    ENoEnt =>
                        LinuxRenameChildAtState
                            .SourceUnavailable,

                    EXdev =>
                        LinuxRenameChildAtState
                            .CrossDeviceRenameNotSupported,

                    ENotDir =>
                        LinuxRenameChildAtState
                            .ParentNotDirectory,

                    EIsDir =>
                        LinuxRenameChildAtState
                            .SourceIsDirectoryDestinationIsNot,

                    EInval =>
                        LinuxRenameChildAtState
                            .UnsupportedPlatform,

                    EBadF =>
                        LinuxRenameChildAtState
                            .InvalidSourceParentHandle,

                    EPerm =>
                        LinuxRenameChildAtState
                            .RenameFailed,

                    _ =>
                        LinuxRenameChildAtState
                            .RenameFailed
                };

            return Result(
                state,
                sourceChildName,
                destinationChildName,
                errno:
                    errno
            );
        }
        catch (
            ObjectDisposedException ex)
        {
            return Result(
                LinuxRenameChildAtState.InvalidSourceParentHandle,
                sourceChildName,
                destinationChildName,
                error:
                    ex.Message
            );
        }
        catch (
            OverflowException ex)
        {
            return Result(
                LinuxRenameChildAtState.InvalidSourceParentHandle,
                sourceChildName,
                destinationChildName,
                error:
                    ex.Message
            );
        }
        finally
        {
            if (sourceRefAdded)
            {
                sourceHandle.DangerousRelease();
            }

            if (destinationRefAdded)
            {
                destinationHandle.DangerousRelease();
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

    private static LinuxRenameChildAtResult Result(
        LinuxRenameChildAtState state,
        string sourceChildName,
        string destinationChildName,
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

        return new LinuxRenameChildAtResult(
            State:
                state,
            SourceChildName:
                sourceChildName,
            DestinationChildName:
                destinationChildName,
            Errno:
                errno,
            Error:
                error
        );
    }
}
