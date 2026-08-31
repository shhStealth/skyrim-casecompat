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
        LinuxFileIncarnationIdentity expectedSourceIncarnation,
        LinuxFileIncarnationIdentity expectedDestinationIncarnation)
    {
        ArgumentNullException.ThrowIfNull(
            parentDirectory
        );

        ArgumentNullException.ThrowIfNull(
            expectedSourceIncarnation
        );

        ArgumentNullException.ThrowIfNull(
            expectedDestinationIncarnation
        );

        if (
            !IsValidChildName(sourceChildName) ||
            !IsValidChildName(destinationChildName))
        {
            return Result(
                LinuxReplaceOwnedFileAtState.InvalidName,
                sourceChildName,
                destinationChildName,
                expectedSourceIncarnation,
                expectedDestinationIncarnation,
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
                expectedSourceIncarnation,
                expectedDestinationIncarnation,
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
                expectedSourceIncarnation,
                expectedDestinationIncarnation,
                error:
                    "Descriptor-relative atomic replacement is " +
                    "supported on Linux only."
            );
        }

        if (!expectedSourceIncarnation.Success)
        {
            return Result(
                LinuxReplaceOwnedFileAtState
                    .InvalidExpectedSourceIdentity,
                sourceChildName,
                destinationChildName,
                expectedSourceIncarnation,
                expectedDestinationIncarnation,
                error:
                    "Replacement requires a successfully captured " +
                    "expected source identity."
            );
        }

        if (!expectedDestinationIncarnation.Success)
        {
            return Result(
                LinuxReplaceOwnedFileAtState
                    .InvalidExpectedDestinationIdentity,
                sourceChildName,
                destinationChildName,
                expectedSourceIncarnation,
                expectedDestinationIncarnation,
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
                expectedSourceIncarnation,
                expectedDestinationIncarnation,
                errno:
                    sourceOpen.Errno,
                error:
                    sourceOpen.Error
            );
        }

        using LinuxOpenedChildHandle source =
            sourceOpen.OpenedChild!;

        LinuxOpenedFileIncarnationResult actualSourceIncarnation =
            LinuxOpenedFileIncarnation.Capture(
                source
            );

        if (!actualSourceIncarnation.Success)
        {
            return Result(
                actualSourceIncarnation.PhysicalIdentity?.State ==
                LinuxOpenedFileIdentityState.NotRegularFile
                    ? LinuxReplaceOwnedFileAtState
                        .SourceNotRegularFile
                    : LinuxReplaceOwnedFileAtState
                        .SourceIdentityUnavailable,
                sourceChildName,
                destinationChildName,
                expectedSourceIncarnation,
                expectedDestinationIncarnation,
                actualSourceIncarnation:
                    actualSourceIncarnation,
                errno:
                    actualSourceIncarnation.PhysicalIdentity?.Errno,
                error:
                    actualSourceIncarnation.Error
            );
        }

        if (
            !expectedSourceIncarnation.SameIncarnationAs(
                actualSourceIncarnation.Identity!
            ))
        {
            return Result(
                LinuxReplaceOwnedFileAtState
                    .SourceIdentityMismatch,
                sourceChildName,
                destinationChildName,
                expectedSourceIncarnation,
                expectedDestinationIncarnation,
                actualSourceIncarnation:
                    actualSourceIncarnation,
                error:
                    "The current staging child is not the " +
                    "expected CaseCompat-owned source incarnation."
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
                expectedSourceIncarnation,
                expectedDestinationIncarnation,
                actualSourceIncarnation:
                    actualSourceIncarnation,
                errno:
                    destinationOpen.Errno,
                error:
                    destinationOpen.Error
            );
        }

        using LinuxOpenedChildHandle destination =
            destinationOpen.OpenedChild!;

        LinuxOpenedFileIncarnationResult actualDestinationIncarnation =
            LinuxOpenedFileIncarnation.Capture(
                destination
            );

        if (!actualDestinationIncarnation.Success)
        {
            return Result(
                actualDestinationIncarnation.PhysicalIdentity?.State ==
                LinuxOpenedFileIdentityState.NotRegularFile
                    ? LinuxReplaceOwnedFileAtState
                        .DestinationNotRegularFile
                    : LinuxReplaceOwnedFileAtState
                        .DestinationIdentityUnavailable,
                sourceChildName,
                destinationChildName,
                expectedSourceIncarnation,
                expectedDestinationIncarnation,
                actualSourceIncarnation:
                    actualSourceIncarnation,
                actualDestinationIncarnation:
                    actualDestinationIncarnation,
                errno:
                    actualDestinationIncarnation.PhysicalIdentity?.Errno,
                error:
                    actualDestinationIncarnation.Error
            );
        }

        if (
            !expectedDestinationIncarnation.SameIncarnationAs(
                actualDestinationIncarnation.Identity!
            ))
        {
            return Result(
                LinuxReplaceOwnedFileAtState
                    .DestinationIdentityMismatch,
                sourceChildName,
                destinationChildName,
                expectedSourceIncarnation,
                expectedDestinationIncarnation,
                actualSourceIncarnation:
                    actualSourceIncarnation,
                actualDestinationIncarnation:
                    actualDestinationIncarnation,
                error:
                    "The current destination child is not the " +
                    "expected CaseCompat-owned destination incarnation."
            );
        }

        if (
            actualSourceIncarnation.Identity!.PhysicalIdentity
                .SameObjectAs(
                    actualDestinationIncarnation.Identity!
                        .PhysicalIdentity
                ))
        {
            return Result(
                LinuxReplaceOwnedFileAtState
                    .SourceAndDestinationSameObject,
                sourceChildName,
                destinationChildName,
                expectedSourceIncarnation,
                expectedDestinationIncarnation,
                actualSourceIncarnation:
                    actualSourceIncarnation,
                actualDestinationIncarnation:
                    actualDestinationIncarnation,
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
                expectedSourceIncarnation,
                expectedDestinationIncarnation,
                actualSourceIncarnation:
                    actualSourceIncarnation,
                actualDestinationIncarnation:
                    actualDestinationIncarnation,
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
                    expectedSourceIncarnation,
                    expectedDestinationIncarnation,
                    actualSourceIncarnation:
                        actualSourceIncarnation,
                    actualDestinationIncarnation:
                        actualDestinationIncarnation
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
                expectedSourceIncarnation,
                expectedDestinationIncarnation,
                actualSourceIncarnation:
                    actualSourceIncarnation,
                actualDestinationIncarnation:
                    actualDestinationIncarnation,
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
                expectedSourceIncarnation,
                expectedDestinationIncarnation,
                actualSourceIncarnation:
                    actualSourceIncarnation,
                actualDestinationIncarnation:
                    actualDestinationIncarnation,
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
                expectedSourceIncarnation,
                expectedDestinationIncarnation,
                actualSourceIncarnation:
                    actualSourceIncarnation,
                actualDestinationIncarnation:
                    actualDestinationIncarnation,
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
        LinuxFileIncarnationIdentity expectedSourceIncarnation,
        LinuxFileIncarnationIdentity expectedDestinationIncarnation,
        LinuxOpenedFileIncarnationResult? actualSourceIncarnation = null,
        LinuxOpenedFileIncarnationResult? actualDestinationIncarnation = null,
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
            ExpectedSourceIncarnation:
                expectedSourceIncarnation,
            ActualSourceIncarnation:
                actualSourceIncarnation,
            ExpectedDestinationIncarnation:
                expectedDestinationIncarnation,
            ActualDestinationIncarnation:
                actualDestinationIncarnation,
            Errno:
                errno,
            Error:
                error
        );
    }
}
