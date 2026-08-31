using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public static class
    DataRelativePathRepairDestinationParentLeaseAcquirer
{
    public static
        DataRelativePathRepairDestinationParentLeaseAcquisition
        Acquire(
            string dataRoot,
            DataRelativePathRepairDestinationParentSnapshot
                expectedSnapshot)
    {
        if (string.IsNullOrWhiteSpace(dataRoot))
        {
            throw new ArgumentException(
                "A Data root is required.",
                nameof(dataRoot)
            );
        }

        ArgumentNullException.ThrowIfNull(
            expectedSnapshot
        );

        string fullDataRoot =
            Path.GetFullPath(
                dataRoot
            );

        string fullParentPath;

        try
        {
            fullParentPath =
                Path.GetFullPath(
                    expectedSnapshot.PhysicalPath
                );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathRepairDestinationParentValidationState
                    .InvalidExpectedSnapshot,
                fullDataRoot,
                expectedSnapshot,
                error:
                    ex.Message
            );
        }

        if (
            !ExpectedSnapshotIsValid(
                expectedSnapshot,
                fullParentPath,
                out string? invalidReason
            ))
        {
            return Result(
                DataRelativePathRepairDestinationParentValidationState
                    .InvalidExpectedSnapshot,
                fullDataRoot,
                expectedSnapshot,
                error:
                    invalidReason
            );
        }

        string relativePath =
            Path.GetRelativePath(
                fullDataRoot,
                fullParentPath
            );

        if (
            IsOutsideRoot(
                relativePath
            ))
        {
            return Result(
                DataRelativePathRepairDestinationParentValidationState
                    .ParentOutsideDataRoot,
                fullDataRoot,
                expectedSnapshot,
                error:
                    "The projected destination parent is " +
                    "outside the Data root."
            );
        }

        LinuxNoFollowPathOpenResult openResult =
            relativePath == "."
                ? LinuxNoFollowPath.OpenRootReadOnly(
                    fullDataRoot
                )
                : LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                    fullDataRoot,
                    relativePath
                );

        if (
            !openResult.Success ||
            openResult.OpenedPath is null)
        {
            return Result(
                DataRelativePathRepairDestinationParentValidationState
                    .ParentOpenFailed,
                fullDataRoot,
                expectedSnapshot,
                openState:
                    openResult.State,
                error:
                    openResult.Error
            );
        }

        LinuxNoFollowPathHandle? openedPath =
            openResult.OpenedPath;

        try
        {
            LinuxOpenedDirectorySnapshotResult
                actualSnapshot =
                    LinuxOpenedDirectorySnapshot.Capture(
                        openedPath
                    );

            if (!actualSnapshot.Success)
            {
                return Result(
                    DataRelativePathRepairDestinationParentValidationState
                        .OpenedSnapshotFailed,
                    fullDataRoot,
                    expectedSnapshot,
                    openState:
                        openResult.State,
                    actualSnapshot:
                        actualSnapshot,
                    error:
                        actualSnapshot.Error
                );
            }

            LinuxFileIdentityResult actualIdentity =
                actualSnapshot.Identity!;

            if (
                !expectedSnapshot.Identity
                    .SameObjectAs(
                        actualIdentity
                    ))
            {
                return Result(
                    DataRelativePathRepairDestinationParentValidationState
                        .IdentityChanged,
                    fullDataRoot,
                    expectedSnapshot,
                    openState:
                        openResult.State,
                    actualSnapshot:
                        actualSnapshot,
                    error:
                        "The destination parent now resolves " +
                        "to a different physical directory."
                );
            }

            if (
                actualSnapshot.CasefoldEnabled !=
                expectedSnapshot.CasefoldEnabled)
            {
                return Result(
                    DataRelativePathRepairDestinationParentValidationState
                        .CasefoldChanged,
                    fullDataRoot,
                    expectedSnapshot,
                    openState:
                        openResult.State,
                    actualSnapshot:
                        actualSnapshot,
                    error:
                        "The destination parent's casefold " +
                        "state changed after repair-plan " +
                        "projection."
                );
            }

            /*
             * Capture stronger directory-incarnation evidence from
             * the exact descriptor that the validated lease will
             * retain.
             *
             * This does not change the shared parent-validation
             * contract: file repair may still use a valid parent
             * lease when inode-generation capture is unavailable.
             *
             * Directory journal v2 will require this evidence
             * explicitly before treating the parent as durable
             * mutation authority.
             */
            LinuxOpenedDirectoryIncarnationResult actualIncarnation =
                LinuxOpenedDirectoryIncarnation.Capture(
                    openedPath
                );

            var lease =
                new
                    DataRelativePathRepairValidatedDestinationParentLease(
                        expectedSnapshot,
                        actualSnapshot,
                        actualIncarnation,
                        openedPath
                    );

            openedPath =
                null;

            return Result(
                DataRelativePathRepairDestinationParentValidationState
                    .Matched,
                fullDataRoot,
                expectedSnapshot,
                openState:
                    openResult.State,
                actualSnapshot:
                    actualSnapshot,
                lease:
                    lease
            );
        }
        finally
        {
            openedPath?.Dispose();
        }
    }

    private static bool ExpectedSnapshotIsValid(
        DataRelativePathRepairDestinationParentSnapshot
            snapshot,
        string fullParentPath,
        out string? reason)
    {
        LinuxFileIdentityResult identity =
            snapshot.Identity;

        if (
            !identity.Success ||
            identity.DeviceMajor is null ||
            identity.DeviceMinor is null ||
            identity.Inode is null)
        {
            reason =
                "The projected destination-parent identity " +
                "is incomplete.";

            return false;
        }

        string identityPath;

        try
        {
            identityPath =
                Path.GetFullPath(
                    identity.FullPath
                );
        }
        catch (Exception ex)
        {
            reason =
                "The projected destination-parent identity " +
                "path is invalid: " +
                ex.Message;

            return false;
        }

        if (
            !string.Equals(
                identityPath,
                fullParentPath,
                StringComparison.Ordinal
            ))
        {
            reason =
                "The projected destination-parent path and " +
                "identity path do not match.";

            return false;
        }

        bool rawFlagsCasefold =
            LinuxDirectoryFlags.HasCasefoldFlag(
                snapshot.RawFlags
            );

        if (
            rawFlagsCasefold !=
            snapshot.CasefoldEnabled)
        {
            reason =
                "The projected destination-parent casefold " +
                "state is inconsistent with its raw flags.";

            return false;
        }

        if (snapshot.CasefoldEnabled)
        {
            reason =
                "A repair destination parent must be strict; " +
                "the projected snapshot is casefold-enabled.";

            return false;
        }

        reason =
            null;

        return true;
    }

    private static bool IsOutsideRoot(
        string relativePath)
    {
        return
            Path.IsPathRooted(
                relativePath
            ) ||
            relativePath == ".." ||
            relativePath.StartsWith(
                "../",
                StringComparison.Ordinal
            ) ||
            relativePath.StartsWith(
                "..\\",
                StringComparison.Ordinal
            );
    }

    private static
        DataRelativePathRepairDestinationParentLeaseAcquisition
        Result(
            DataRelativePathRepairDestinationParentValidationState
                state,
            string dataRoot,
            DataRelativePathRepairDestinationParentSnapshot
                expectedSnapshot,
            LinuxNoFollowPathOpenState? openState = null,
            LinuxOpenedDirectorySnapshotResult?
                actualSnapshot = null,
            DataRelativePathRepairValidatedDestinationParentLease?
                lease = null,
            string? error = null)
    {
        var validation =
            new
                DataRelativePathRepairDestinationParentValidation(
                    State:
                        state,
                    DataRoot:
                        dataRoot,
                    ExpectedSnapshot:
                        expectedSnapshot,
                    OpenState:
                        openState,
                    ActualSnapshot:
                        actualSnapshot,
                    Error:
                        error
                );

        return new
            DataRelativePathRepairDestinationParentLeaseAcquisition(
                Validation:
                    validation,
                Lease:
                    lease
            );
    }
}
