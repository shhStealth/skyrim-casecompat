using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CaseCompat.Filesystem.Linux;

public static class LinuxPublishOwnedDirectoryAt
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

    private const int EIsDir =
        21;

    private const int ERofs =
        30;

    private const int ENoSys =
        38;

    private const int ENotEmpty =
        39;

    private const int EOpNotSupp =
        95;

    /*
     * Linux RENAME_NOREPLACE from linux/fs.h.
     */
    private const uint RenameNoReplace =
        1U;

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

    public static LinuxPublishOwnedDirectoryAtResult Publish(
        ILinuxOpenedHandle parentDirectory,
        string sourceChildName,
        string destinationChildName,
        LinuxOpenedChildHandle sourceDirectory,
        LinuxFileIdentityResult expectedIdentity)
    {
        ArgumentNullException.ThrowIfNull(
            parentDirectory
        );

        ArgumentNullException.ThrowIfNull(
            sourceDirectory
        );

        ArgumentNullException.ThrowIfNull(
            expectedIdentity
        );

        if (
            !IsValidChildName(sourceChildName) ||
            !IsValidChildName(destinationChildName))
        {
            return Result(
                LinuxPublishOwnedDirectoryAtState.InvalidName,
                sourceChildName,
                destinationChildName,
                expectedIdentity,
                error:
                    "Source and destination names must each " +
                    "identify exactly one direct child."
            );
        }

        if (
            string.Equals(
                sourceChildName,
                destinationChildName,
                StringComparison.Ordinal
            ))
        {
            return Result(
                LinuxPublishOwnedDirectoryAtState.SameName,
                sourceChildName,
                destinationChildName,
                expectedIdentity,
                error:
                    "Source and destination names must differ."
            );
        }

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                LinuxPublishOwnedDirectoryAtState
                    .UnsupportedPlatform,
                sourceChildName,
                destinationChildName,
                expectedIdentity,
                error:
                    "No-overwrite directory publication is " +
                    "supported on Linux only."
            );
        }

        if (!HasCompleteIdentity(expectedIdentity))
        {
            return Result(
                LinuxPublishOwnedDirectoryAtState
                    .InvalidExpectedIdentity,
                sourceChildName,
                destinationChildName,
                expectedIdentity,
                error:
                    "Directory publication requires a complete " +
                    "descriptor-captured identity including " +
                    "device, inode, and mount ID."
            );
        }

        /*
         * First prove that the long-lived descriptor supplied by
         * the caller is the directory whose identity was recorded
         * during preparation.
         */
        LinuxOpenedDirectorySnapshotResult handleSnapshot =
            LinuxOpenedDirectorySnapshot.Capture(
                sourceDirectory,
                sourceChildName
            );

        if (
            handleSnapshot.State ==
            LinuxOpenedDirectorySnapshotState.NotDirectory)
        {
            return Result(
                LinuxPublishOwnedDirectoryAtState.SourceNotDirectory,
                sourceChildName,
                destinationChildName,
                expectedIdentity,
                error:
                    handleSnapshot.Error ??
                    "The prepared source descriptor is not a " +
                    "directory."
            );
        }

        bool handleIdentityUsable =
            handleSnapshot.Identity is not null &&
            HasCompleteIdentity(
                handleSnapshot.Identity
            ) &&
            (
                handleSnapshot.State ==
                    LinuxOpenedDirectorySnapshotState.Captured ||
                handleSnapshot.State ==
                    LinuxOpenedDirectorySnapshotState.FlagsUnavailable
            );

        if (!handleIdentityUsable)
        {
            return Result(
                LinuxPublishOwnedDirectoryAtState
                    .InvalidSourceHandle,
                sourceChildName,
                destinationChildName,
                expectedIdentity,
                handleIdentity:
                    handleSnapshot.Identity,
                errno:
                    handleSnapshot.Errno,
                error:
                    handleSnapshot.Error ??
                    handleSnapshot.State.ToString()
            );
        }

        LinuxFileIdentityResult handleIdentity =
            handleSnapshot.Identity!;

        if (
            !SameDirectoryObject(
                expectedIdentity,
                handleIdentity
            ))
        {
            return Result(
                LinuxPublishOwnedDirectoryAtState
                    .SourceIdentityMismatch,
                sourceChildName,
                destinationChildName,
                expectedIdentity,
                handleIdentity:
                    handleIdentity,
                error:
                    "The prepared source descriptor no longer " +
                    "matches the expected CaseCompat-owned " +
                    "directory identity."
            );
        }

        /*
         * renameat2 still selects the source entry by name.
         *
         * Reopen that exact name beneath the already-open parent
         * with O_NOFOLLOW and prove it still names the same
         * directory as the long-lived prepared descriptor.
         */
        LinuxOpenChildReadOnlyAtResult namedOpen =
            LinuxOpenChildReadOnlyAt.Open(
                parentDirectory,
                sourceChildName
            );

        if (!namedOpen.Success)
        {
            LinuxPublishOwnedDirectoryAtState state =
                namedOpen.State switch
                {
                    LinuxOpenChildReadOnlyAtState
                        .InvalidParentHandle =>
                            LinuxPublishOwnedDirectoryAtState
                                .InvalidParentHandle,

                    LinuxOpenChildReadOnlyAtState
                        .ParentNotDirectory =>
                            LinuxPublishOwnedDirectoryAtState
                                .ParentNotDirectory,

                    LinuxOpenChildReadOnlyAtState
                        .ChildUnavailable =>
                            LinuxPublishOwnedDirectoryAtState
                                .SourceUnavailable,

                    LinuxOpenChildReadOnlyAtState
                        .ChildSymbolicLinkRejected =>
                            LinuxPublishOwnedDirectoryAtState
                                .SourceSymbolicLinkRejected,

                    LinuxOpenChildReadOnlyAtState
                        .UnsupportedPlatform =>
                            LinuxPublishOwnedDirectoryAtState
                                .UnsupportedPlatform,

                    _ =>
                        LinuxPublishOwnedDirectoryAtState
                            .SourceOpenFailed
                };

            return Result(
                state,
                sourceChildName,
                destinationChildName,
                expectedIdentity,
                handleIdentity:
                    handleIdentity,
                errno:
                    namedOpen.Errno,
                error:
                    namedOpen.Error
            );
        }

        using LinuxOpenedChildHandle namedSource =
            namedOpen.OpenedChild!;

        LinuxOpenedDirectorySnapshotResult namedSnapshot =
            LinuxOpenedDirectorySnapshot.Capture(
                namedSource,
                sourceChildName
            );

        if (
            namedSnapshot.State ==
            LinuxOpenedDirectorySnapshotState.NotDirectory)
        {
            return Result(
                LinuxPublishOwnedDirectoryAtState.SourceNotDirectory,
                sourceChildName,
                destinationChildName,
                expectedIdentity,
                handleIdentity:
                    handleIdentity,
                error:
                    namedSnapshot.Error ??
                    "The named staging source is no longer a " +
                    "directory."
            );
        }

        bool namedIdentityUsable =
            namedSnapshot.Identity is not null &&
            HasCompleteIdentity(
                namedSnapshot.Identity
            ) &&
            (
                namedSnapshot.State ==
                    LinuxOpenedDirectorySnapshotState.Captured ||
                namedSnapshot.State ==
                    LinuxOpenedDirectorySnapshotState.FlagsUnavailable
            );

        if (!namedIdentityUsable)
        {
            return Result(
                LinuxPublishOwnedDirectoryAtState
                    .SourceIdentityUnavailable,
                sourceChildName,
                destinationChildName,
                expectedIdentity,
                handleIdentity:
                    handleIdentity,
                namedSourceIdentity:
                    namedSnapshot.Identity,
                errno:
                    namedSnapshot.Errno,
                error:
                    namedSnapshot.Error ??
                    namedSnapshot.State.ToString()
            );
        }

        LinuxFileIdentityResult namedIdentity =
            namedSnapshot.Identity!;

        if (
            !SameDirectoryObject(
                expectedIdentity,
                namedIdentity
            ) ||
            !SameDirectoryObject(
                handleIdentity,
                namedIdentity
            ))
        {
            return Result(
                LinuxPublishOwnedDirectoryAtState
                    .SourceIdentityMismatch,
                sourceChildName,
                destinationChildName,
                expectedIdentity,
                handleIdentity:
                    handleIdentity,
                namedSourceIdentity:
                    namedIdentity,
                error:
                    "The staging name no longer identifies the " +
                    "prepared CaseCompat-owned directory."
            );
        }

        SafeFileHandle parentHandle =
            parentDirectory.Handle;

        if (
            parentHandle.IsInvalid ||
            parentHandle.IsClosed)
        {
            return Result(
                LinuxPublishOwnedDirectoryAtState
                    .InvalidParentHandle,
                sourceChildName,
                destinationChildName,
                expectedIdentity,
                handleIdentity:
                    handleIdentity,
                namedSourceIdentity:
                    namedIdentity,
                error:
                    "The parent descriptor became invalid or " +
                    "closed before publication."
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
             * PUBLIC-ASSET SAFETY BOUNDARY
             *
             * RENAME_NOREPLACE is essential here. Unlike the
             * internal journal replacement primitive, this
             * operation must never overwrite or merge with an
             * existing Skyrim-visible destination.
             *
             * The source name was identity-checked immediately
             * above, but renameat2 still selects it by name.
             * Therefore a narrow final source-name race remains.
             *
             * Perform exactly one renameat2 attempt. Never retry
             * automatically after failure.
             */
            if (
                RenameAt2(
                    parentFd,
                    sourceChildName,
                    parentFd,
                    destinationChildName,
                    RenameNoReplace
                ) == 0)
            {
                return Result(
                    LinuxPublishOwnedDirectoryAtState.Published,
                    sourceChildName,
                    destinationChildName,
                    expectedIdentity,
                    handleIdentity:
                        handleIdentity,
                    namedSourceIdentity:
                        namedIdentity
                );
            }

            int errno =
                Marshal.GetLastPInvokeError();

            LinuxPublishOwnedDirectoryAtState state =
                errno switch
                {
                    EExist =>
                        LinuxPublishOwnedDirectoryAtState
                            .DestinationExists,

                    /*
                     * The source name was proven immediately
                     * before renameat2. These errors therefore
                     * conservatively mean namespace state changed
                     * before publication completed.
                     */
                    ENoEnt or
                    ENotDir or
                    EIsDir or
                    ENotEmpty =>
                        LinuxPublishOwnedDirectoryAtState
                            .ChildChangedBeforePublish,

                    /*
                     * ENOSYS means the running kernel lacks the
                     * operation. EOPNOTSUPP is accepted
                     * defensively for filesystems that report it.
                     *
                     * Linux commonly reports EINVAL when the
                     * filesystem does not support the requested
                     * renameat2 flag.
                     */
                    ENoSys or
                    EOpNotSupp =>
                        LinuxPublishOwnedDirectoryAtState
                            .NoReplaceUnsupported,

                    EBusy or
                    EPerm or
                    EAccess or
                    ERofs =>
                        LinuxPublishOwnedDirectoryAtState
                            .PublicationDenied,

                    _ =>
                        LinuxPublishOwnedDirectoryAtState
                            .PublishFailed
                };

            /*
             * EINVAL is intentionally handled separately because
             * it can indicate unsupported RENAME_NOREPLACE.
             */
            if (errno == 22)
            {
                state =
                    LinuxPublishOwnedDirectoryAtState
                        .NoReplaceUnsupported;
            }

            return Result(
                state,
                sourceChildName,
                destinationChildName,
                expectedIdentity,
                handleIdentity:
                    handleIdentity,
                namedSourceIdentity:
                    namedIdentity,
                errno:
                    errno
            );
        }
        catch (EntryPointNotFoundException ex)
        {
            return Result(
                LinuxPublishOwnedDirectoryAtState
                    .NoReplaceUnsupported,
                sourceChildName,
                destinationChildName,
                expectedIdentity,
                handleIdentity:
                    handleIdentity,
                namedSourceIdentity:
                    namedIdentity,
                error:
                    ex.Message
            );
        }
        catch (ObjectDisposedException ex)
        {
            return Result(
                LinuxPublishOwnedDirectoryAtState
                    .InvalidParentHandle,
                sourceChildName,
                destinationChildName,
                expectedIdentity,
                handleIdentity:
                    handleIdentity,
                namedSourceIdentity:
                    namedIdentity,
                error:
                    ex.Message
            );
        }
        catch (OverflowException ex)
        {
            return Result(
                LinuxPublishOwnedDirectoryAtState
                    .InvalidParentHandle,
                sourceChildName,
                destinationChildName,
                expectedIdentity,
                handleIdentity:
                    handleIdentity,
                namedSourceIdentity:
                    namedIdentity,
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
        LinuxFileIdentityResult left,
        LinuxFileIdentityResult right)
    {
        return
            HasCompleteIdentity(left) &&
            HasCompleteIdentity(right) &&
            left.DeviceMajor ==
                right.DeviceMajor &&
            left.DeviceMinor ==
                right.DeviceMinor &&
            left.Inode ==
                right.Inode &&
            left.MountId ==
                right.MountId;
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

    private static LinuxPublishOwnedDirectoryAtResult Result(
        LinuxPublishOwnedDirectoryAtState state,
        string? sourceChildName,
        string? destinationChildName,
        LinuxFileIdentityResult expectedIdentity,
        LinuxFileIdentityResult? handleIdentity = null,
        LinuxFileIdentityResult? namedSourceIdentity = null,
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

        return new LinuxPublishOwnedDirectoryAtResult(
            State:
                state,
            SourceChildName:
                sourceChildName ?? string.Empty,
            DestinationChildName:
                destinationChildName ?? string.Empty,
            ExpectedIdentity:
                expectedIdentity,
            HandleIdentity:
                handleIdentity,
            NamedSourceIdentity:
                namedSourceIdentity,
            Errno:
                errno,
            Error:
                error
        );
    }
}
