using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CaseCompat.Filesystem.Linux;

public static class LinuxReplaceOwnedFileAt
{
    private const int EPerm = 1;
    private const int ENoEnt = 2;
    private const int EBadF = 9;
    private const int EAccess = 13;
    private const int EExist = 17;
    private const int EXdev = 18;
    private const int ENotDir = 20;
    private const int EIsDir = 21;
    private const int ERofs = 30;
    private const int ENotEmpty = 39;

    [DllImport(
        "libc",
        EntryPoint = "renameat",
        SetLastError = true)]
    private static extern int RenameAt(
        int olddirfd,
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        string oldpath,
        int newdirfd,
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        string newpath
    );

    public static LinuxReplaceOwnedFileAtResult Replace(
        LinuxNoFollowPathHandle parentDirectory,
        string sourceChildName,
        string destinationChildName,
        LinuxOpenedFileIdentityResult expectedSourceIdentity,
        LinuxOpenedFileIdentityResult expectedDestinationIdentity)
    {
        ArgumentNullException.ThrowIfNull(
            parentDirectory
        );

        ArgumentNullException.ThrowIfNull(
            expectedSourceIdentity
        );

        ArgumentNullException.ThrowIfNull(
            expectedDestinationIdentity
        );

        if (
            !IsValidChildName(sourceChildName) ||
            !IsValidChildName(destinationChildName))
        {
            return Result(
                LinuxReplaceOwnedFileAtState.InvalidName,
                sourceChildName,
                destinationChildName,
                expectedSourceIdentity,
                expectedDestinationIdentity,
                error:
                    "Source and destination must each identify " +
                    "exactly one direct child."
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
                LinuxReplaceOwnedFileAtState.SameName,
                sourceChildName,
                destinationChildName,
                expectedSourceIdentity,
                expectedDestinationIdentity,
                error:
                    "Source and destination names must differ."
            );
        }

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                LinuxReplaceOwnedFileAtState
                    .UnsupportedPlatform,
                sourceChildName,
                destinationChildName,
                expectedSourceIdentity,
                expectedDestinationIdentity,
                error:
                    "Descriptor-relative atomic replacement is " +
                    "supported on Linux only."
            );
        }

        if (!expectedSourceIdentity.Success)
        {
            return Result(
                LinuxReplaceOwnedFileAtState
                    .InvalidExpectedSourceIdentity,
                sourceChildName,
                destinationChildName,
                expectedSourceIdentity,
                expectedDestinationIdentity,
                error:
                    "Replacement requires a successfully captured " +
                    "expected source identity."
            );
        }

        if (!expectedDestinationIdentity.Success)
        {
            return Result(
                LinuxReplaceOwnedFileAtState
                    .InvalidExpectedDestinationIdentity,
                sourceChildName,
                destinationChildName,
                expectedSourceIdentity,
                expectedDestinationIdentity,
                error:
                    "Replacement requires a successfully captured " +
                    "expected destination identity."
            );
        }

        LinuxOpenChildReadOnlyAtResult sourceOpen =
            LinuxOpenChildReadOnlyAt.Open(
                parentDirectory,
                sourceChildName
            );

        if (!sourceOpen.Success)
        {
            return Result(
                MapSourceOpenFailure(
                    sourceOpen.State
                ),
                sourceChildName,
                destinationChildName,
                expectedSourceIdentity,
                expectedDestinationIdentity,
                errno:
                    sourceOpen.Errno,
                error:
                    sourceOpen.Error
            );
        }

        using LinuxOpenedChildHandle source =
            sourceOpen.OpenedChild!;

        LinuxOpenedFileIdentityResult actualSourceIdentity =
            LinuxOpenedFileIdentity.Capture(
                source
            );

        if (!actualSourceIdentity.Success)
        {
            return Result(
                actualSourceIdentity.State ==
                LinuxOpenedFileIdentityState.NotRegularFile
                    ? LinuxReplaceOwnedFileAtState
                        .SourceNotRegularFile
                    : LinuxReplaceOwnedFileAtState
                        .SourceIdentityUnavailable,
                sourceChildName,
                destinationChildName,
                expectedSourceIdentity,
                expectedDestinationIdentity,
                actualSourceIdentity:
                    actualSourceIdentity,
                errno:
                    actualSourceIdentity.Errno,
                error:
                    actualSourceIdentity.Error
            );
        }

        if (
            !expectedSourceIdentity.SameObjectAs(
                actualSourceIdentity
            ))
        {
            return Result(
                LinuxReplaceOwnedFileAtState
                    .SourceIdentityMismatch,
                sourceChildName,
                destinationChildName,
                expectedSourceIdentity,
                expectedDestinationIdentity,
                actualSourceIdentity:
                    actualSourceIdentity,
                error:
                    "The current staging child is not the " +
                    "expected CaseCompat-owned source inode."
            );
        }

        LinuxOpenChildReadOnlyAtResult destinationOpen =
            LinuxOpenChildReadOnlyAt.Open(
                parentDirectory,
                destinationChildName
            );

        if (!destinationOpen.Success)
        {
            return Result(
                MapDestinationOpenFailure(
                    destinationOpen.State
                ),
                sourceChildName,
                destinationChildName,
                expectedSourceIdentity,
                expectedDestinationIdentity,
                actualSourceIdentity:
                    actualSourceIdentity,
                errno:
                    destinationOpen.Errno,
                error:
                    destinationOpen.Error
            );
        }

        using LinuxOpenedChildHandle destination =
            destinationOpen.OpenedChild!;

        LinuxOpenedFileIdentityResult actualDestinationIdentity =
            LinuxOpenedFileIdentity.Capture(
                destination
            );

        if (!actualDestinationIdentity.Success)
        {
            return Result(
                actualDestinationIdentity.State ==
                LinuxOpenedFileIdentityState.NotRegularFile
                    ? LinuxReplaceOwnedFileAtState
                        .DestinationNotRegularFile
                    : LinuxReplaceOwnedFileAtState
                        .DestinationIdentityUnavailable,
                sourceChildName,
                destinationChildName,
                expectedSourceIdentity,
                expectedDestinationIdentity,
                actualSourceIdentity:
                    actualSourceIdentity,
                actualDestinationIdentity:
                    actualDestinationIdentity,
                errno:
                    actualDestinationIdentity.Errno,
                error:
                    actualDestinationIdentity.Error
            );
        }

        if (
            !expectedDestinationIdentity.SameObjectAs(
                actualDestinationIdentity
            ))
        {
            return Result(
                LinuxReplaceOwnedFileAtState
                    .DestinationIdentityMismatch,
                sourceChildName,
                destinationChildName,
                expectedSourceIdentity,
                expectedDestinationIdentity,
                actualSourceIdentity:
                    actualSourceIdentity,
                actualDestinationIdentity:
                    actualDestinationIdentity,
                error:
                    "The current destination child is not the " +
                    "expected CaseCompat-owned destination inode."
            );
        }

        if (
            actualSourceIdentity.SameObjectAs(
                actualDestinationIdentity
            ))
        {
            return Result(
                LinuxReplaceOwnedFileAtState
                    .SourceAndDestinationSameObject,
                sourceChildName,
                destinationChildName,
                expectedSourceIdentity,
                expectedDestinationIdentity,
                actualSourceIdentity:
                    actualSourceIdentity,
                actualDestinationIdentity:
                    actualDestinationIdentity,
                error:
                    "Source and destination already reference " +
                    "the same inode."
            );
        }

        SafeFileHandle parentHandle =
            parentDirectory.Handle;

        if (
            parentHandle.IsInvalid ||
            parentHandle.IsClosed)
        {
            return Result(
                LinuxReplaceOwnedFileAtState
                    .InvalidParentHandle,
                sourceChildName,
                destinationChildName,
                expectedSourceIdentity,
                expectedDestinationIdentity,
                actualSourceIdentity:
                    actualSourceIdentity,
                actualDestinationIdentity:
                    actualDestinationIdentity,
                error:
                    "The parent descriptor became invalid or " +
                    "closed before replacement."
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
             * INTERNAL-FILE SAFETY BOUNDARY
             *
             * renameat() selects source and destination by name.
             * We validate both entries by descriptor immediately
             * before the rename, but a narrow final name race
             * remains.
             *
             * This primitive is intended only for CaseCompat-owned
             * internal files in a controlled journal directory.
             * It must never be used to overwrite Skyrim assets.
             */
            if (
                RenameAt(
                    parentFd,
                    sourceChildName,
                    parentFd,
                    destinationChildName
                ) == 0)
            {
                return Result(
                    LinuxReplaceOwnedFileAtState.Replaced,
                    sourceChildName,
                    destinationChildName,
                    expectedSourceIdentity,
                    expectedDestinationIdentity,
                    actualSourceIdentity:
                        actualSourceIdentity,
                    actualDestinationIdentity:
                        actualDestinationIdentity
                );
            }

            int errno =
                Marshal.GetLastPInvokeError();

            LinuxReplaceOwnedFileAtState state =
                errno switch
                {
                    EBadF =>
                        LinuxReplaceOwnedFileAtState
                            .InvalidParentHandle,

                    ENotDir =>
                        LinuxReplaceOwnedFileAtState
                            .ParentNotDirectory,

                    ENoEnt or
                    EIsDir or
                    EExist or
                    ENotEmpty =>
                        LinuxReplaceOwnedFileAtState
                            .ChildChangedBeforeReplace,

                    EXdev =>
                        LinuxReplaceOwnedFileAtState
                            .DifferentFilesystem,

                    EPerm or
                    EAccess or
                    ERofs =>
                        LinuxReplaceOwnedFileAtState
                            .ReplaceDenied,

                    _ =>
                        LinuxReplaceOwnedFileAtState
                            .ReplaceFailed
                };

            return Result(
                state,
                sourceChildName,
                destinationChildName,
                expectedSourceIdentity,
                expectedDestinationIdentity,
                actualSourceIdentity:
                    actualSourceIdentity,
                actualDestinationIdentity:
                    actualDestinationIdentity,
                errno:
                    errno
            );
        }
        catch (ObjectDisposedException ex)
        {
            return Result(
                LinuxReplaceOwnedFileAtState
                    .InvalidParentHandle,
                sourceChildName,
                destinationChildName,
                expectedSourceIdentity,
                expectedDestinationIdentity,
                actualSourceIdentity:
                    actualSourceIdentity,
                actualDestinationIdentity:
                    actualDestinationIdentity,
                error:
                    ex.Message
            );
        }
        catch (OverflowException ex)
        {
            return Result(
                LinuxReplaceOwnedFileAtState
                    .InvalidParentHandle,
                sourceChildName,
                destinationChildName,
                expectedSourceIdentity,
                expectedDestinationIdentity,
                actualSourceIdentity:
                    actualSourceIdentity,
                actualDestinationIdentity:
                    actualDestinationIdentity,
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

    private static LinuxReplaceOwnedFileAtState
        MapSourceOpenFailure(
            LinuxOpenChildReadOnlyAtState state)
    {
        return state switch
        {
            LinuxOpenChildReadOnlyAtState
                .InvalidParentHandle =>
                    LinuxReplaceOwnedFileAtState
                        .InvalidParentHandle,

            LinuxOpenChildReadOnlyAtState
                .ParentNotDirectory =>
                    LinuxReplaceOwnedFileAtState
                        .ParentNotDirectory,

            LinuxOpenChildReadOnlyAtState
                .ChildUnavailable =>
                    LinuxReplaceOwnedFileAtState
                        .SourceUnavailable,

            LinuxOpenChildReadOnlyAtState
                .ChildSymbolicLinkRejected =>
                    LinuxReplaceOwnedFileAtState
                        .SourceSymbolicLinkRejected,

            _ =>
                LinuxReplaceOwnedFileAtState
                    .SourceOpenFailed
        };
    }

    private static LinuxReplaceOwnedFileAtState
        MapDestinationOpenFailure(
            LinuxOpenChildReadOnlyAtState state)
    {
        return state switch
        {
            LinuxOpenChildReadOnlyAtState
                .InvalidParentHandle =>
                    LinuxReplaceOwnedFileAtState
                        .InvalidParentHandle,

            LinuxOpenChildReadOnlyAtState
                .ParentNotDirectory =>
                    LinuxReplaceOwnedFileAtState
                        .ParentNotDirectory,

            LinuxOpenChildReadOnlyAtState
                .ChildUnavailable =>
                    LinuxReplaceOwnedFileAtState
                        .DestinationUnavailable,

            LinuxOpenChildReadOnlyAtState
                .ChildSymbolicLinkRejected =>
                    LinuxReplaceOwnedFileAtState
                        .DestinationSymbolicLinkRejected,

            _ =>
                LinuxReplaceOwnedFileAtState
                    .DestinationOpenFailed
        };
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

    private static LinuxReplaceOwnedFileAtResult Result(
        LinuxReplaceOwnedFileAtState state,
        string? sourceChildName,
        string? destinationChildName,
        LinuxOpenedFileIdentityResult expectedSourceIdentity,
        LinuxOpenedFileIdentityResult expectedDestinationIdentity,
        LinuxOpenedFileIdentityResult? actualSourceIdentity = null,
        LinuxOpenedFileIdentityResult? actualDestinationIdentity = null,
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

        return new LinuxReplaceOwnedFileAtResult(
            State:
                state,
            SourceChildName:
                sourceChildName ?? string.Empty,
            DestinationChildName:
                destinationChildName ?? string.Empty,
            ExpectedSourceIdentity:
                expectedSourceIdentity,
            ActualSourceIdentity:
                actualSourceIdentity,
            ExpectedDestinationIdentity:
                expectedDestinationIdentity,
            ActualDestinationIdentity:
                actualDestinationIdentity,
            Errno:
                errno,
            Error:
                error
        );
    }
}
