using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CaseCompat.Filesystem.Linux;

public static class LinuxRemoveOwnedDirectoryAt
{
    private const int EPerm =
        1;

    private const int ENoEnt =
        2;

    private const int EBadF =
        9;

    private const int EAccess =
        13;

    private const int EBusy =
        16;

    private const int EExist =
        17;

    private const int ENotDir =
        20;

    private const int ERofs =
        30;

    private const int ENotEmpty =
        39;

    /*
     * Linux AT_REMOVEDIR from fcntl.h.
     */
    private const int AtRemoveDir =
        0x200;

    [DllImport(
        "libc",
        EntryPoint = "unlinkat",
        SetLastError = true)]
    private static extern int UnlinkAt(
        int dirfd,
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        string pathname,
        int flags
    );

    public static LinuxRemoveOwnedDirectoryAtResult Remove(
        LinuxNoFollowPathHandle parentDirectory,
        string childName,
        LinuxFileIdentityResult expectedIdentity)
    {
        ArgumentNullException.ThrowIfNull(
            parentDirectory
        );

        ArgumentNullException.ThrowIfNull(
            expectedIdentity
        );

        if (!IsValidChildName(childName))
        {
            return Result(
                LinuxRemoveOwnedDirectoryAtState
                    .InvalidName,
                childName,
                expectedIdentity,
                error:
                    "The rollback name must identify exactly " +
                    "one direct child and cannot be '.', '..', " +
                    "or contain path separators or NUL."
            );
        }

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                LinuxRemoveOwnedDirectoryAtState
                    .UnsupportedPlatform,
                childName,
                expectedIdentity,
                error:
                    "Descriptor-relative directory rollback is " +
                    "supported on Linux only."
            );
        }

        if (!HasCompleteIdentity(expectedIdentity))
        {
            return Result(
                LinuxRemoveOwnedDirectoryAtState
                    .InvalidExpectedIdentity,
                childName,
                expectedIdentity,
                error:
                    "Directory rollback requires a complete " +
                    "descriptor-captured identity including " +
                    "device, inode, and mount ID."
            );
        }

        LinuxOpenChildReadOnlyAtResult opened =
            LinuxOpenChildReadOnlyAt.Open(
                parentDirectory,
                childName
            );

        if (!opened.Success)
        {
            LinuxRemoveOwnedDirectoryAtState state =
                opened.State switch
                {
                    LinuxOpenChildReadOnlyAtState
                        .InvalidParentHandle =>
                            LinuxRemoveOwnedDirectoryAtState
                                .InvalidParentHandle,

                    LinuxOpenChildReadOnlyAtState
                        .ParentNotDirectory =>
                            LinuxRemoveOwnedDirectoryAtState
                                .ParentNotDirectory,

                    LinuxOpenChildReadOnlyAtState
                        .ChildUnavailable =>
                            LinuxRemoveOwnedDirectoryAtState
                                .ChildUnavailable,

                    LinuxOpenChildReadOnlyAtState
                        .ChildSymbolicLinkRejected =>
                            LinuxRemoveOwnedDirectoryAtState
                                .ChildSymbolicLinkRejected,

                    LinuxOpenChildReadOnlyAtState
                        .UnsupportedPlatform =>
                            LinuxRemoveOwnedDirectoryAtState
                                .UnsupportedPlatform,

                    _ =>
                        LinuxRemoveOwnedDirectoryAtState
                            .ChildOpenFailed
                };

            return Result(
                state,
                childName,
                expectedIdentity,
                errno:
                    opened.Errno,
                error:
                    opened.Error
            );
        }

        using LinuxOpenedChildHandle child =
            opened.OpenedChild!;

        string displayPath =
            Path.GetFullPath(
                Path.Combine(
                    parentDirectory.FullPath,
                    childName
                )
            );

        LinuxOpenedDirectorySnapshotResult snapshot =
            LinuxOpenedDirectorySnapshot.Capture(
                child,
                displayPath
            );

        if (
            snapshot.State ==
            LinuxOpenedDirectorySnapshotState
                .NotDirectory)
        {
            return Result(
                LinuxRemoveOwnedDirectoryAtState
                    .ChildNotDirectory,
                childName,
                expectedIdentity,
                error:
                    snapshot.Error ??
                    "The current child is not a directory."
            );
        }

        /*
         * FlagsUnavailable is acceptable here.
         *
         * statx has already proved the opened target is a
         * directory and captured its physical identity. Directory
         * rollback does not depend on casefold/ioctl flags.
         */
        bool identityUsable =
            snapshot.Identity is not null &&
            HasCompleteIdentity(
                snapshot.Identity
            ) &&
            (
                snapshot.State ==
                LinuxOpenedDirectorySnapshotState.Captured ||
                snapshot.State ==
                LinuxOpenedDirectorySnapshotState.FlagsUnavailable
            );

        if (!identityUsable)
        {
            return Result(
                LinuxRemoveOwnedDirectoryAtState
                    .ChildIdentityUnavailable,
                childName,
                expectedIdentity,
                actualIdentity:
                    snapshot.Identity,
                errno:
                    snapshot.Errno,
                error:
                    snapshot.Error ??
                    snapshot.State.ToString()
            );
        }

        LinuxFileIdentityResult actualIdentity =
            snapshot.Identity!;

        if (
            !SameDirectoryObject(
                expectedIdentity,
                actualIdentity
            ))
        {
            return Result(
                LinuxRemoveOwnedDirectoryAtState
                    .IdentityMismatch,
                childName,
                expectedIdentity,
                actualIdentity:
                    actualIdentity,
                error:
                    "The current child does not have the " +
                    "physical identity of the directory " +
                    "CaseCompat created."
            );
        }

        SafeFileHandle parentHandle =
            parentDirectory.Handle;

        if (
            parentHandle.IsInvalid ||
            parentHandle.IsClosed)
        {
            return Result(
                LinuxRemoveOwnedDirectoryAtState
                    .InvalidParentHandle,
                childName,
                expectedIdentity,
                actualIdentity:
                    actualIdentity,
                error:
                    "The destination-parent descriptor became " +
                    "invalid or closed before directory rollback."
            );
        }

        bool addedRef =
            false;

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

            /*
             * IMPORTANT SAFETY LIMITATION
             *
             * unlinkat(..., AT_REMOVEDIR) still removes a
             * directory entry by name. Linux provides no ordinary
             * unprivileged "remove this exact opened directory fd"
             * operation.
             *
             * We therefore:
             *
             * 1. open the exact direct child with O_NOFOLLOW;
             * 2. prove from that descriptor that it is a directory;
             * 3. verify its complete physical identity;
             * 4. perform exactly one unlinkat(AT_REMOVEDIR).
             *
             * The kernel itself is the final emptiness gate:
             * non-empty directories are refused atomically.
             *
             * There remains a narrow race in which another process
             * with sufficient access could replace the directory
             * entry between identity validation and unlinkat().
             *
             * Never retry a failed removal here.
             */
            if (
                UnlinkAt(
                    parentFd,
                    childName,
                    AtRemoveDir
                ) == 0)
            {
                return Result(
                    LinuxRemoveOwnedDirectoryAtState
                        .Removed,
                    childName,
                    expectedIdentity,
                    actualIdentity:
                        actualIdentity
                );
            }

            int errno =
                Marshal.GetLastPInvokeError();

            LinuxRemoveOwnedDirectoryAtState removeState =
                errno switch
                {
                    EBadF =>
                        LinuxRemoveOwnedDirectoryAtState
                            .InvalidParentHandle,

                    /*
                     * The direct child was proven to be the
                     * expected directory immediately before the
                     * syscall. ENOENT/ENOTDIR therefore indicate
                     * that the named entry changed in the narrow
                     * verification-to-removal race.
                     */
                    ENoEnt or ENotDir =>
                        LinuxRemoveOwnedDirectoryAtState
                            .ChildChangedBeforeRemove,

                    /*
                     * Linux normally reports ENOTEMPTY; some
                     * implementations/filesystems may report
                     * EEXIST for a non-empty directory.
                     */
                    ENotEmpty or EExist =>
                        LinuxRemoveOwnedDirectoryAtState
                            .DirectoryNotEmpty,

                    EBusy =>
                        LinuxRemoveOwnedDirectoryAtState
                            .DirectoryBusy,

                    EPerm or
                    EAccess or
                    ERofs =>
                        LinuxRemoveOwnedDirectoryAtState
                            .RemoveDenied,

                    _ =>
                        LinuxRemoveOwnedDirectoryAtState
                            .RemoveFailed
                };

            return Result(
                removeState,
                childName,
                expectedIdentity,
                actualIdentity:
                    actualIdentity,
                errno:
                    errno
            );
        }
        catch (ObjectDisposedException ex)
        {
            return Result(
                LinuxRemoveOwnedDirectoryAtState
                    .InvalidParentHandle,
                childName,
                expectedIdentity,
                actualIdentity:
                    actualIdentity,
                error:
                    ex.Message
            );
        }
        catch (OverflowException ex)
        {
            return Result(
                LinuxRemoveOwnedDirectoryAtState
                    .InvalidParentHandle,
                childName,
                expectedIdentity,
                actualIdentity:
                    actualIdentity,
                error:
                    ex.Message
            );
        }
        finally
        {
            if (addedRef)
            {
                parentHandle.DangerousRelease();
            }
        }
    }

    private static bool SameDirectoryObject(
        LinuxFileIdentityResult expected,
        LinuxFileIdentityResult actual)
    {
        return
            HasCompleteIdentity(expected) &&
            HasCompleteIdentity(actual) &&
            expected.DeviceMajor ==
                actual.DeviceMajor &&
            expected.DeviceMinor ==
                actual.DeviceMinor &&
            expected.Inode ==
                actual.Inode &&
            expected.MountId ==
                actual.MountId;
    }

    private static bool HasCompleteIdentity(
        LinuxFileIdentityResult identity)
    {
        return
            identity.Success &&
            identity.DeviceMajor is not null &&
            identity.DeviceMinor is not null &&
            identity.Inode is not null &&
            identity.MountId is not null;
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

    private static LinuxRemoveOwnedDirectoryAtResult Result(
        LinuxRemoveOwnedDirectoryAtState state,
        string? childName,
        LinuxFileIdentityResult expectedIdentity,
        LinuxFileIdentityResult? actualIdentity = null,
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

        return new LinuxRemoveOwnedDirectoryAtResult(
            State:
                state,
            ChildName:
                childName ?? string.Empty,
            ExpectedIdentity:
                expectedIdentity,
            ActualIdentity:
                actualIdentity,
            Errno:
                errno,
            Error:
                error
        );
    }
}
