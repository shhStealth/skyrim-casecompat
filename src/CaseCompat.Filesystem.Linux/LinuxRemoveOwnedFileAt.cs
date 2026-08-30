using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CaseCompat.Filesystem.Linux;

public static class LinuxRemoveOwnedFileAt
{
    private const int EPerm =
        1;

    private const int ENoEnt =
        2;

    private const int EBadF =
        9;

    private const int EAccess =
        13;

    private const int ENotDir =
        20;

    private const int EIsDir =
        21;

    private const int ERofs =
        30;

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

    public static LinuxRemoveOwnedFileAtResult Remove(
        LinuxNoFollowPathHandle parentDirectory,
        string childName,
        LinuxOpenedFileIdentityResult expectedIdentity)
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
                LinuxRemoveOwnedFileAtState
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
                LinuxRemoveOwnedFileAtState
                    .UnsupportedPlatform,
                childName,
                expectedIdentity,
                error:
                    "Descriptor-relative rollback is supported " +
                    "on Linux only."
            );
        }

        if (!expectedIdentity.Success)
        {
            return Result(
                LinuxRemoveOwnedFileAtState
                    .InvalidExpectedIdentity,
                childName,
                expectedIdentity,
                error:
                    "Rollback requires a successfully captured " +
                    "identity for the file CaseCompat published."
            );
        }

        LinuxOpenChildReadOnlyAtResult opened =
            LinuxOpenChildReadOnlyAt.Open(
                parentDirectory,
                childName
            );

        if (!opened.Success)
        {
            LinuxRemoveOwnedFileAtState state =
                opened.State switch
                {
                    LinuxOpenChildReadOnlyAtState
                        .InvalidParentHandle =>
                            LinuxRemoveOwnedFileAtState
                                .InvalidParentHandle,

                    LinuxOpenChildReadOnlyAtState
                        .ParentNotDirectory =>
                            LinuxRemoveOwnedFileAtState
                                .ParentNotDirectory,

                    LinuxOpenChildReadOnlyAtState
                        .ChildUnavailable =>
                            LinuxRemoveOwnedFileAtState
                                .ChildUnavailable,

                    LinuxOpenChildReadOnlyAtState
                        .ChildSymbolicLinkRejected =>
                            LinuxRemoveOwnedFileAtState
                                .ChildSymbolicLinkRejected,

                    LinuxOpenChildReadOnlyAtState
                        .UnsupportedPlatform =>
                            LinuxRemoveOwnedFileAtState
                                .UnsupportedPlatform,

                    _ =>
                        LinuxRemoveOwnedFileAtState
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

        LinuxOpenedFileIdentityResult actualIdentity =
            LinuxOpenedFileIdentity.Capture(
                child
            );

        if (!actualIdentity.Success)
        {
            LinuxRemoveOwnedFileAtState state =
                actualIdentity.State ==
                LinuxOpenedFileIdentityState
                    .NotRegularFile
                    ? LinuxRemoveOwnedFileAtState
                        .ChildNotRegularFile
                    : LinuxRemoveOwnedFileAtState
                        .ChildIdentityUnavailable;

            return Result(
                state,
                childName,
                expectedIdentity,
                actualIdentity:
                    actualIdentity,
                errno:
                    actualIdentity.Errno,
                error:
                    actualIdentity.Error
            );
        }

        if (
            !expectedIdentity.SameObjectAs(
                actualIdentity
            ))
        {
            return Result(
                LinuxRemoveOwnedFileAtState
                    .IdentityMismatch,
                childName,
                expectedIdentity,
                actualIdentity:
                    actualIdentity,
                error:
                    "The current child does not have the " +
                    "identity of the file CaseCompat published."
            );
        }

        SafeFileHandle parentHandle =
            parentDirectory.Handle;

        if (
            parentHandle.IsInvalid ||
            parentHandle.IsClosed)
        {
            return Result(
                LinuxRemoveOwnedFileAtState
                    .InvalidParentHandle,
                childName,
                expectedIdentity,
                actualIdentity:
                    actualIdentity,
                error:
                    "The destination-parent descriptor became " +
                    "invalid or closed before rollback."
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
             * unlinkat() removes a directory entry by name.
             * Linux provides no ordinary unprivileged
             * "unlink this exact open regular-file fd" form.
             *
             * We therefore:
             *
             * 1. open the direct child beneath the already-open
             *    parent with O_NOFOLLOW;
             * 2. verify its descriptor identity;
             * 3. perform exactly one unlinkat() immediately.
             *
             * There remains a narrow race in which another
             * process with sufficient access could replace the
             * directory entry between steps 2 and 3.
             *
             * Do not retry an unlink failure here.
             */
            if (
                UnlinkAt(
                    parentFd,
                    childName,
                    flags:
                        0
                ) == 0)
            {
                return Result(
                    LinuxRemoveOwnedFileAtState
                        .Removed,
                    childName,
                    expectedIdentity,
                    actualIdentity:
                        actualIdentity
                );
            }

            int errno =
                Marshal.GetLastPInvokeError();

            LinuxRemoveOwnedFileAtState removeState =
                errno switch
                {
                    EBadF =>
                        LinuxRemoveOwnedFileAtState
                            .InvalidParentHandle,

                    ENotDir =>
                        LinuxRemoveOwnedFileAtState
                            .ParentNotDirectory,

                    // The child passed identity validation
                    // immediately before unlinkat(). These
                    // errors therefore indicate that the
                    // directory entry changed in the meantime.
                    ENoEnt or EIsDir =>
                        LinuxRemoveOwnedFileAtState
                            .ChildChangedBeforeRemove,

                    EPerm or
                    EAccess or
                    ERofs =>
                        LinuxRemoveOwnedFileAtState
                            .RemoveDenied,

                    _ =>
                        LinuxRemoveOwnedFileAtState
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
                LinuxRemoveOwnedFileAtState
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
                LinuxRemoveOwnedFileAtState
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

    private static LinuxRemoveOwnedFileAtResult Result(
        LinuxRemoveOwnedFileAtState state,
        string? childName,
        LinuxOpenedFileIdentityResult expectedIdentity,
        LinuxOpenedFileIdentityResult? actualIdentity = null,
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

        return new LinuxRemoveOwnedFileAtResult(
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
