using CaseCompat.Core.Resolution;
using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;

namespace CaseCompat.Core.Repair;

public static class DataRelativePathRepairPlanProjector
{
    public static DataRelativePathRepairPlanProjection Project(
        DataRelativePathResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(
            resolution
        );

        DataRelativePathCaseMismatchTopologyState topologyState =
            DataRelativePathCaseMismatchTopologyClassifier.Classify(
                resolution
            );

        if (
            topologyState !=
            DataRelativePathCaseMismatchTopologyState
                .DirectStrictCaseMismatch)
        {
            return Result(
                resolution,
                topologyState,
                DataRelativePathRepairPlanProjectionState
                    .NotDirectStrictCaseMismatch
            );
        }

        if (
            resolution
                .EquivalentPhysicalCandidates
                .Count != 1)
        {
            return Result(
                resolution,
                topologyState,
                DataRelativePathRepairPlanProjectionState
                    .ProjectionInvariantViolation,
                error:
                    "Direct strict case mismatch requires " +
                    "exactly one equivalent physical candidate."
            );
        }

        string sourcePath =
            Path.GetFullPath(
                resolution
                    .EquivalentPhysicalCandidates[0]
            );

        FileAttributes sourceAttributes;

        try
        {
            sourceAttributes =
                File.GetAttributes(
                    sourcePath
                );
        }
        catch (Exception ex)
        {
            return Result(
                resolution,
                topologyState,
                DataRelativePathRepairPlanProjectionState
                    .SourceUnavailable,
                error:
                    ex.Message
            );
        }

        if (
            (sourceAttributes &
             FileAttributes.ReparsePoint) != 0)
        {
            return Result(
                resolution,
                topologyState,
                DataRelativePathRepairPlanProjectionState
                    .SourceSymbolicLinkRejected
            );
        }

        if (
            (sourceAttributes &
             FileAttributes.Directory) != 0)
        {
            return Result(
                resolution,
                topologyState,
                DataRelativePathRepairPlanProjectionState
                    .SourceNotFile
            );
        }

        LinuxFileIdentityResult identity =
            LinuxFileIdentity.Inspect(
                sourcePath
            );

        if (!identity.Success)
        {
            return Result(
                resolution,
                topologyState,
                DataRelativePathRepairPlanProjectionState
                    .SourceIdentityUnavailable,
                error:
                    identity.Error
            );
        }

        long sourceSize;
        string sourceHash;

        try
        {
            sourceSize =
                new FileInfo(
                    sourcePath
                ).Length;

            sourceHash =
                ComputeSha256(
                    sourcePath
                );
        }
        catch (Exception ex)
        {
            return Result(
                resolution,
                topologyState,
                DataRelativePathRepairPlanProjectionState
                    .SourceSnapshotFailed,
                error:
                    ex.Message
            );
        }

        var sourceSnapshot =
            new DataRelativePathRepairSourceSnapshot(
                PhysicalPath:
                    sourcePath,
                Size:
                    sourceSize,
                Sha256:
                    sourceHash,
                Identity:
                    identity
            );

        if (
            resolution.FailedComponentIndex is not int
                failedIndex)
        {
            return Result(
                resolution,
                topologyState,
                DataRelativePathRepairPlanProjectionState
                    .ProjectionInvariantViolation,
                sourceSnapshot,
                error:
                    "Direct strict case mismatch requires " +
                    "a failed component index."
            );
        }

        PathResolutionStep[] failedSteps =
            resolution.Steps
                .Where(step =>
                    step.ComponentIndex ==
                    failedIndex
                )
                .ToArray();

        if (failedSteps.Length != 1)
        {
            return Result(
                resolution,
                topologyState,
                DataRelativePathRepairPlanProjectionState
                    .ProjectionInvariantViolation,
                sourceSnapshot,
                error:
                    "Direct strict case mismatch requires " +
                    "exactly one failed resolution step."
            );
        }

        PathResolutionStep failedStep =
            failedSteps[0];

        string dataRoot =
            Path.GetFullPath(
                resolution.DataRoot
            );

        string existingParent =
            dataRoot;

        for (
            int index = 0;
            index < failedIndex;
            index++)
        {
            PathResolutionStep[] matchingSteps =
                resolution.Steps
                    .Where(step =>
                        step.ComponentIndex ==
                        index
                    )
                    .ToArray();

            if (
                matchingSteps.Length != 1 ||
                string.IsNullOrEmpty(
                    matchingSteps[0]
                        .SelectedPhysicalName
                ))
            {
                return Result(
                    resolution,
                    topologyState,
                    DataRelativePathRepairPlanProjectionState
                        .ExistingHierarchyChanged,
                    sourceSnapshot
                );
            }

            string expectedChild =
                Path.Combine(
                    existingParent,
                    matchingSteps[0]
                        .SelectedPhysicalName!
                );

            FileAttributes attributes;

            try
            {
                attributes =
                    File.GetAttributes(
                        expectedChild
                    );
            }
            catch (Exception ex)
            {
                return Result(
                    resolution,
                    topologyState,
                    DataRelativePathRepairPlanProjectionState
                        .ExistingHierarchyChanged,
                    sourceSnapshot,
                    error:
                        ex.Message
                );
            }

            if (
                (attributes &
                 FileAttributes.ReparsePoint) != 0 ||
                (attributes &
                 FileAttributes.Directory) == 0)
            {
                return Result(
                    resolution,
                    topologyState,
                    DataRelativePathRepairPlanProjectionState
                        .ExistingHierarchyChanged,
                    sourceSnapshot
                );
            }

            existingParent =
                expectedChild;
        }

        string expectedParent =
            Path.GetFullPath(
                failedStep.ParentPhysicalPath
            );

        if (
            !string.Equals(
                Path.GetFullPath(
                    existingParent
                ),
                expectedParent,
                StringComparison.Ordinal
            ))
        {
            return Result(
                resolution,
                topologyState,
                DataRelativePathRepairPlanProjectionState
                    .ExistingHierarchyChanged,
                sourceSnapshot
            );
        }

        string parentRelative =
            Path.GetRelativePath(
                dataRoot,
                expectedParent
            );

        if (
            Path.IsPathRooted(
                parentRelative
            ) ||
            SplitComponents(
                parentRelative
            ).Any(component =>
                component == ".."
            ))
        {
            return Result(
                resolution,
                topologyState,
                DataRelativePathRepairPlanProjectionState
                    .DestinationParentOutsideDataRoot,
                sourceSnapshot
            );
        }

        FileAttributes parentAttributes;

        try
        {
            parentAttributes =
                File.GetAttributes(
                    expectedParent
                );
        }
        catch (Exception ex)
        {
            return Result(
                resolution,
                topologyState,
                DataRelativePathRepairPlanProjectionState
                    .DestinationParentUnavailable,
                sourceSnapshot,
                error:
                    ex.Message
            );
        }

        if (
            (parentAttributes &
             FileAttributes.ReparsePoint) != 0)
        {
            return Result(
                resolution,
                topologyState,
                DataRelativePathRepairPlanProjectionState
                    .DestinationParentSymbolicLinkRejected,
                sourceSnapshot
            );
        }

        if (
            (parentAttributes &
             FileAttributes.Directory) == 0)
        {
            return Result(
                resolution,
                topologyState,
                DataRelativePathRepairPlanProjectionState
                    .DestinationParentNotDirectory,
                sourceSnapshot
            );
        }

        string[] requestedComponents =
            SplitComponents(
                resolution.RequestedPath
            );

        string failedComponent =
            requestedComponents[
                failedIndex
            ];

        try
        {
            bool exactDestinationExists =
                Directory
                    .EnumerateFileSystemEntries(
                        expectedParent
                    )
                    .Any(path =>
                        string.Equals(
                            Path.GetFileName(
                                path
                            ),
                            failedComponent,
                            StringComparison.Ordinal
                        )
                    );

            if (exactDestinationExists)
            {
                return Result(
                    resolution,
                    topologyState,
                    DataRelativePathRepairPlanProjectionState
                        .DestinationConflict,
                    sourceSnapshot
                );
            }
        }
        catch (Exception ex)
        {
            return Result(
                resolution,
                topologyState,
                DataRelativePathRepairPlanProjectionState
                    .DestinationInspectionFailed,
                sourceSnapshot,
                error:
                    ex.Message
            );
        }

        var operations =
            new List<
                DataRelativePathRepairPlanOperation
            >();

        string projectedParent =
            expectedParent;

        for (
            int index = failedIndex;
            index < requestedComponents.Length - 1;
            index++)
        {
            projectedParent =
                Path.Combine(
                    projectedParent,
                    requestedComponents[index]
                );

            operations.Add(
                new DataRelativePathRepairPlanOperation(
                    Kind:
                        DataRelativePathRepairPlanOperationKind
                            .CreateDirectory,
                    DestinationPath:
                        projectedParent,
                    SourcePath:
                        null
                )
            );
        }

        string destinationFile =
            Path.Combine(
                projectedParent,
                requestedComponents[^1]
            );

        operations.Add(
            new DataRelativePathRepairPlanOperation(
                Kind:
                    DataRelativePathRepairPlanOperationKind
                        .CreateFile,
                DestinationPath:
                    destinationFile,
                SourcePath:
                    sourcePath
            )
        );

        return Result(
            resolution,
            topologyState,
            DataRelativePathRepairPlanProjectionState
                .Projected,
            sourceSnapshot,
            operations
        );
    }

    private static DataRelativePathRepairPlanProjection Result(
        DataRelativePathResolution resolution,
        DataRelativePathCaseMismatchTopologyState topologyState,
        DataRelativePathRepairPlanProjectionState state,
        DataRelativePathRepairSourceSnapshot? sourceSnapshot = null,
        IReadOnlyList<DataRelativePathRepairPlanOperation>? operations = null,
        string? error = null)
    {
        return new DataRelativePathRepairPlanProjection(
            State:
                state,
            TopologyState:
                topologyState,
            Resolution:
                resolution,
            SourceSnapshot:
                sourceSnapshot,
            Operations:
                operations ??
                Array.Empty<
                    DataRelativePathRepairPlanOperation
                >(),
            Error:
                error
        );
    }

    private static string ComputeSha256(
        string path)
    {
        using FileStream stream =
            new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read
            );

        byte[] hash =
            SHA256.HashData(
                stream
            );

        return Convert.ToHexString(
            hash
        );
    }

    private static string[] SplitComponents(
        string path)
    {
        return path
            .Replace(
                '\\',
                '/'
            )
            .Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries
            );
    }
}
