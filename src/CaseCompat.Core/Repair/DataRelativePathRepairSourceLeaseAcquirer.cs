using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public static class DataRelativePathRepairSourceLeaseAcquirer
{
    public static DataRelativePathRepairSourceLeaseAcquisition
        Acquire(
            string dataRoot,
            DataRelativePathRepairSourceSnapshot
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

        string fullSourcePath;

        try
        {
            fullSourcePath =
                Path.GetFullPath(
                    expectedSnapshot.PhysicalPath
                );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathRepairSourceValidationState
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
                fullSourcePath,
                out string? invalidReason
            ))
        {
            return Result(
                DataRelativePathRepairSourceValidationState
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
                fullSourcePath
            );

        if (
            IsOutsideRoot(
                relativePath
            ))
        {
            return Result(
                DataRelativePathRepairSourceValidationState
                    .SourceOutsideDataRoot,
                fullDataRoot,
                expectedSnapshot,
                error:
                    "The projected source is outside " +
                    "the Data root."
            );
        }

        LinuxNoFollowPathOpenResult openResult =
            LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                fullDataRoot,
                relativePath
            );

        if (
            !openResult.Success ||
            openResult.OpenedPath is null)
        {
            return Result(
                DataRelativePathRepairSourceValidationState
                    .SourceOpenFailed,
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
            LinuxOpenedFileSnapshotResult actualSnapshot =
                LinuxOpenedFileSnapshot.Capture(
                    openedPath
                );

            if (!actualSnapshot.Success)
            {
                return Result(
                    DataRelativePathRepairSourceValidationState
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
                    DataRelativePathRepairSourceValidationState
                        .IdentityChanged,
                    fullDataRoot,
                    expectedSnapshot,
                    openState:
                        openResult.State,
                    actualSnapshot:
                        actualSnapshot,
                    error:
                        "The source now resolves to a " +
                        "different physical file."
                );
            }

            if (
                actualSnapshot.Size !=
                expectedSnapshot.Size)
            {
                return Result(
                    DataRelativePathRepairSourceValidationState
                        .SizeChanged,
                    fullDataRoot,
                    expectedSnapshot,
                    openState:
                        openResult.State,
                    actualSnapshot:
                        actualSnapshot,
                    error:
                        "The source file size changed " +
                        "after repair-plan projection."
                );
            }

            if (
                !string.Equals(
                    actualSnapshot.Sha256,
                    expectedSnapshot.Sha256,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return Result(
                    DataRelativePathRepairSourceValidationState
                        .HashChanged,
                    fullDataRoot,
                    expectedSnapshot,
                    openState:
                        openResult.State,
                    actualSnapshot:
                        actualSnapshot,
                    error:
                        "The source file content changed " +
                        "after repair-plan projection."
                );
            }

            var lease =
                new DataRelativePathRepairValidatedSourceLease(
                    expectedSnapshot,
                    actualSnapshot,
                    openedPath
                );

            openedPath =
                null;

            return Result(
                DataRelativePathRepairSourceValidationState
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
        DataRelativePathRepairSourceSnapshot snapshot,
        string fullSourcePath,
        out string? reason)
    {
        if (
            snapshot.Size < 0)
        {
            reason =
                "The projected source size is invalid.";

            return false;
        }

        LinuxFileIdentityResult identity =
            snapshot.Identity;

        if (
            !identity.Success ||
            identity.DeviceMajor is null ||
            identity.DeviceMinor is null ||
            identity.Inode is null)
        {
            reason =
                "The projected source identity is incomplete.";

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
                "The projected identity path is invalid: " +
                ex.Message;

            return false;
        }

        if (
            !string.Equals(
                identityPath,
                fullSourcePath,
                StringComparison.Ordinal
            ))
        {
            reason =
                "The projected source path and identity " +
                "path do not match.";

            return false;
        }

        if (
            !IsSha256(
                snapshot.Sha256
            ))
        {
            reason =
                "The projected source SHA-256 is invalid.";

            return false;
        }

        reason =
            null;

        return true;
    }

    private static bool IsSha256(
        string value)
    {
        if (
            value.Length != 64)
        {
            return false;
        }

        foreach (
            char character
            in value)
        {
            bool hexadecimal =
                character is >= '0' and <= '9' ||
                character is >= 'A' and <= 'F' ||
                character is >= 'a' and <= 'f';

            if (!hexadecimal)
            {
                return false;
            }
        }

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
        DataRelativePathRepairSourceLeaseAcquisition
        Result(
            DataRelativePathRepairSourceValidationState state,
            string dataRoot,
            DataRelativePathRepairSourceSnapshot
                expectedSnapshot,
            LinuxNoFollowPathOpenState? openState = null,
            LinuxOpenedFileSnapshotResult? actualSnapshot = null,
            DataRelativePathRepairValidatedSourceLease?
                lease = null,
            string? error = null)
    {
        var validation =
            new DataRelativePathRepairSourceValidation(
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

        return new DataRelativePathRepairSourceLeaseAcquisition(
            Validation:
                validation,
            Lease:
                lease
        );
    }
}
