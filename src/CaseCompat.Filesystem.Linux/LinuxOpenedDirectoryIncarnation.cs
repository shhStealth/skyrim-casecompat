namespace CaseCompat.Filesystem.Linux;

public static class LinuxOpenedDirectoryIncarnation
{
    public static LinuxOpenedDirectoryIncarnationResult Capture(
        LinuxNoFollowPathHandle openedPath)
    {
        ArgumentNullException.ThrowIfNull(
            openedPath
        );

        return Capture(
            openedPath,
            openedPath.FullPath
        );
    }

    public static LinuxOpenedDirectoryIncarnationResult Capture(
        ILinuxOpenedHandle openedHandle,
        string displayPath)
    {
        ArgumentNullException.ThrowIfNull(
            openedHandle
        );

        if (string.IsNullOrWhiteSpace(displayPath))
        {
            throw new ArgumentException(
                "A diagnostic display path is required.",
                nameof(displayPath)
            );
        }

        LinuxOpenedDirectorySnapshotResult snapshot =
            LinuxOpenedDirectorySnapshot.Capture(
                openedHandle,
                displayPath
            );

        if (
            snapshot.State ==
            LinuxOpenedDirectorySnapshotState.UnsupportedPlatform)
        {
            return Result(
                LinuxOpenedDirectoryIncarnationState
                    .UnsupportedPlatform,
                displayPath,
                snapshot:
                    snapshot,
                error:
                    snapshot.Error ??
                    snapshot.State.ToString()
            );
        }

        if (
            snapshot.State ==
            LinuxOpenedDirectorySnapshotState.InvalidHandle)
        {
            return Result(
                LinuxOpenedDirectoryIncarnationState
                    .InvalidHandle,
                displayPath,
                snapshot:
                    snapshot,
                error:
                    snapshot.Error ??
                    snapshot.State.ToString()
            );
        }

        if (
            snapshot.State ==
            LinuxOpenedDirectorySnapshotState.NotDirectory)
        {
            return Result(
                LinuxOpenedDirectoryIncarnationState
                    .NotDirectory,
                displayPath,
                snapshot:
                    snapshot,
                error:
                    snapshot.Error ??
                    "The opened target is not a directory."
            );
        }

        /*
         * FS_IOC_GETFLAGS is unrelated to incarnation identity.
         *
         * Therefore FlagsUnavailable remains usable when statx
         * already captured complete physical directory identity.
         */
        bool physicalIdentityUsable =
            snapshot.Identity is not null &&
            HasCompletePhysicalIdentity(
                snapshot.Identity
            ) &&
            (
                snapshot.State ==
                    LinuxOpenedDirectorySnapshotState.Captured ||
                snapshot.State ==
                    LinuxOpenedDirectorySnapshotState.FlagsUnavailable
            );

        if (!physicalIdentityUsable)
        {
            return Result(
                LinuxOpenedDirectoryIncarnationState
                    .SnapshotUnavailable,
                displayPath,
                snapshot:
                    snapshot,
                error:
                    snapshot.Error ??
                    snapshot.State.ToString()
            );
        }

        LinuxOpenedInodeGenerationResult generation =
            LinuxOpenedInodeGeneration.Capture(
                openedHandle
            );

        if (!generation.Success)
        {
            return Result(
                generation.State ==
                    LinuxOpenedInodeGenerationState.UnsupportedPlatform
                    ? LinuxOpenedDirectoryIncarnationState
                        .UnsupportedPlatform
                    : generation.State ==
                        LinuxOpenedInodeGenerationState.InvalidHandle
                        ? LinuxOpenedDirectoryIncarnationState
                            .InvalidHandle
                        : LinuxOpenedDirectoryIncarnationState
                            .GenerationUnavailable,
                displayPath,
                snapshot:
                    snapshot,
                generationCapture:
                    generation,
                error:
                    generation.Error ??
                    generation.State.ToString()
            );
        }

        var identity =
            new LinuxDirectoryIncarnationIdentity(
                PhysicalIdentity:
                    snapshot.Identity!,
                InodeGeneration:
                    generation.Generation!.Value
            );

        return Result(
            LinuxOpenedDirectoryIncarnationState.Captured,
            displayPath,
            snapshot:
                snapshot,
            generationCapture:
                generation,
            identity:
                identity
        );
    }

    private static bool HasCompletePhysicalIdentity(
        LinuxFileIdentityResult identity)
    {
        return
            identity.Success &&
            identity.DeviceMajor is not null &&
            identity.DeviceMinor is not null &&
            identity.Inode is not null &&
            identity.MountId is not null;
    }

    private static LinuxOpenedDirectoryIncarnationResult Result(
        LinuxOpenedDirectoryIncarnationState state,
        string fullPath,
        LinuxOpenedDirectorySnapshotResult? snapshot = null,
        LinuxOpenedInodeGenerationResult? generationCapture = null,
        LinuxDirectoryIncarnationIdentity? identity = null,
        string? error = null)
    {
        return new(
            State:
                state,
            FullPath:
                fullPath,
            Snapshot:
                snapshot,
            GenerationCapture:
                generationCapture,
            Identity:
                identity,
            Error:
                error
        );
    }
}
