namespace CaseCompat.Filesystem.Linux;

public static class LinuxOpenedFileIncarnation
{
    public static LinuxOpenedFileIncarnationResult Capture(
        ILinuxOpenedHandle openedHandle)
    {
        ArgumentNullException.ThrowIfNull(
            openedHandle
        );

        LinuxOpenedFileIdentityResult physicalIdentity =
            LinuxOpenedFileIdentity.Capture(
                openedHandle
            );

        if (
            physicalIdentity.State ==
            LinuxOpenedFileIdentityState.UnsupportedPlatform)
        {
            return Result(
                LinuxOpenedFileIncarnationState
                    .UnsupportedPlatform,
                physicalIdentity:
                    physicalIdentity,
                error:
                    physicalIdentity.Error ??
                    physicalIdentity.State.ToString()
            );
        }

        if (
            physicalIdentity.State ==
            LinuxOpenedFileIdentityState.InvalidHandle)
        {
            return Result(
                LinuxOpenedFileIncarnationState
                    .InvalidHandle,
                physicalIdentity:
                    physicalIdentity,
                error:
                    physicalIdentity.Error ??
                    physicalIdentity.State.ToString()
            );
        }

        if (
            physicalIdentity.State ==
            LinuxOpenedFileIdentityState.NotRegularFile)
        {
            return Result(
                LinuxOpenedFileIncarnationState
                    .NotRegularFile,
                physicalIdentity:
                    physicalIdentity,
                error:
                    physicalIdentity.Error ??
                    "The opened target is not a regular file."
            );
        }

        if (
            !HasCompletePhysicalIdentity(
                physicalIdentity
            ))
        {
            return Result(
                LinuxOpenedFileIncarnationState
                    .IdentityUnavailable,
                physicalIdentity:
                    physicalIdentity,
                error:
                    physicalIdentity.Error ??
                    "Complete opened-file physical identity " +
                    "is unavailable."
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
                    LinuxOpenedInodeGenerationState
                        .UnsupportedPlatform
                    ? LinuxOpenedFileIncarnationState
                        .UnsupportedPlatform
                    : generation.State ==
                        LinuxOpenedInodeGenerationState
                            .InvalidHandle
                        ? LinuxOpenedFileIncarnationState
                            .InvalidHandle
                        : LinuxOpenedFileIncarnationState
                            .GenerationUnavailable,
                physicalIdentity:
                    physicalIdentity,
                generationCapture:
                    generation,
                error:
                    generation.Error ??
                    generation.State.ToString()
            );
        }

        var identity =
            new LinuxFileIncarnationIdentity(
                PhysicalIdentity:
                    physicalIdentity,
                InodeGeneration:
                    generation.Generation!.Value
            );

        return Result(
            LinuxOpenedFileIncarnationState.Captured,
            physicalIdentity:
                physicalIdentity,
            generationCapture:
                generation,
            identity:
                identity
        );
    }

    private static bool HasCompletePhysicalIdentity(
        LinuxOpenedFileIdentityResult identity)
    {
        return
            identity.Success &&
            identity.DeviceMajor is not null &&
            identity.DeviceMinor is not null &&
            identity.Inode is not null &&
            identity.MountId is not null;
    }

    private static LinuxOpenedFileIncarnationResult Result(
        LinuxOpenedFileIncarnationState state,
        LinuxOpenedFileIdentityResult? physicalIdentity = null,
        LinuxOpenedInodeGenerationResult? generationCapture = null,
        LinuxFileIncarnationIdentity? identity = null,
        string? error = null)
    {
        return new(
            State:
                state,
            PhysicalIdentity:
                physicalIdentity,
            GenerationCapture:
                generationCapture,
            Identity:
                identity,
            Error:
                error
        );
    }
}
