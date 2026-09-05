using CaseCompat.Core.Resolution;
using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;

namespace CaseCompat.Core.Repair;

public static class DataRelativePathRepairPlanProjector
{
    /*
     * Standalone projection is authoritative only when every safety
     * condition owned by one independent plan is satisfied, including the
     * case-variant source-branch coverage rule.
     */
    public static DataRelativePathRepairPlanProjection Project(
        DataRelativePathResolution resolution)
    {
        return ProjectCore(
            resolution,
            requireStandaloneBranchCoverage:
                true,
            allowLeafOnlyAlternateBranch:
                true,
            allowAggregateAlternateBranch:
                false
        );
    }

    /*
     * Produce the technically projectable form used by batch-wide
     * namespace-coverage authorization.
     *
     * IMPORTANT:
     *
     * This result is NOT independently authorized for persistence or
     * execution. A caller must prove complete, consistent aggregate
     * namespace coverage for the immutable batch before treating this
     * candidate as an authorized batch repair plan.
     *
     * repair-plan-batch consumes this candidate form only as part of the
     * complete aggregate coverage decision.
     */
    public static DataRelativePathRepairPlanProjection
        ProjectBatchCandidate(
            DataRelativePathResolution resolution)
    {
        return ProjectCore(
            resolution,
            requireStandaloneBranchCoverage:
                false,
            allowLeafOnlyAlternateBranch:
                false,
            allowAggregateAlternateBranch:
                false
        );
    }

    /*
     * Produce a technically projectable alternate-physical-branch form
     * intended only for a future aggregate namespace-coverage policy.
     *
     * IMPORTANT:
     *
     * This entry point grants no standalone persistence or execution
     * authority. It deliberately does not alter Project() or
     * ProjectBatchCandidate().
     *
     * A caller must establish complete recursive aggregate namespace
     * coverage before any result from this method may become durable
     * repair authority.
     */
    public static DataRelativePathRepairPlanProjection
        ProjectAggregateAlternateBranchBatchCandidate(
            DataRelativePathResolution resolution)
    {
        return ProjectCore(
            resolution,
            requireStandaloneBranchCoverage:
                false,
            allowLeafOnlyAlternateBranch:
                false,
            allowAggregateAlternateBranch:
                true
        );
    }

    private static DataRelativePathRepairPlanProjection ProjectCore(
        DataRelativePathResolution resolution,
        bool requireStandaloneBranchCoverage,
        bool allowLeafOnlyAlternateBranch,
        bool allowAggregateAlternateBranch)
    {
        ArgumentNullException.ThrowIfNull(
            resolution
        );

        DataRelativePathCaseMismatchTopologyState topologyState =
            DataRelativePathCaseMismatchTopologyClassifier.Classify(
                resolution
            );

        bool directStrictCaseMismatch =
            topologyState ==
            DataRelativePathCaseMismatchTopologyState
                .DirectStrictCaseMismatch;

        /*
         * CandidateBranchesBeforeFailure normally remains blocked.
         *
         * There is one narrower standalone repair shape that does not
         * create or redirect any directory hierarchy:
         *
         * - the requested traversal reaches its existing destination
         *   parent;
         * - failure occurs only at the final file component;
         * - the resolver has already proven exactly one Windows-equivalent
         *   physical source candidate;
         * - projection therefore needs only one CreateFile operation.
         *
         * Batch projection deliberately does not admit this topology yet.
         * Durable aggregate authorization still models schema-v2 direct
         * strict mismatches and must evolve separately.
         */
        bool leafOnlyAlternateBranch =
            allowLeafOnlyAlternateBranch &&
            topologyState ==
                DataRelativePathCaseMismatchTopologyState
                    .CandidateBranchesBeforeFailure &&
            resolution.FailedComponentIndex is int
                alternateFailedIndex &&
            alternateFailedIndex >= 0 &&
            alternateFailedIndex ==
                SplitComponents(
                    resolution.RequestedPath
                ).Length - 1;

        bool aggregateAlternateBranch =
            allowAggregateAlternateBranch &&
            topologyState ==
                DataRelativePathCaseMismatchTopologyState
                    .CandidateBranchesBeforeFailure &&
            resolution.FailedComponentIndex is int
                aggregateFailedIndex &&
            aggregateFailedIndex >= 0 &&
            aggregateFailedIndex <
                SplitComponents(
                    resolution.RequestedPath
                ).Length - 1;

        if (
            !directStrictCaseMismatch &&
            !leafOnlyAlternateBranch &&
            !aggregateAlternateBranch)
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

        LinuxNoFollowPathOpenResult parentOpen =
            parentRelative == "."
                ? LinuxNoFollowPath.OpenRootReadOnly(
                    dataRoot
                )
                : LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                    dataRoot,
                    parentRelative
                );

        if (
            !parentOpen.Success ||
            parentOpen.OpenedPath is null)
        {
            return Result(
                resolution,
                topologyState,
                DataRelativePathRepairPlanProjectionState
                    .DestinationParentOpenFailed,
                sourceSnapshot,
                error:
                    parentOpen.Error ??
                    "Unable to open the destination parent " +
                    $"without following symlinks ({parentOpen.State})."
            );
        }

        LinuxOpenedDirectorySnapshotResult
            openedParentSnapshot;

        using (parentOpen.OpenedPath)
        {
            openedParentSnapshot =
                LinuxOpenedDirectorySnapshot.Capture(
                    parentOpen.OpenedPath
                );
        }

        if (
            !openedParentSnapshot.Success ||
            openedParentSnapshot.Identity is not
                LinuxFileIdentityResult parentIdentity ||
            openedParentSnapshot.CasefoldEnabled is not
                bool parentCasefoldEnabled ||
            openedParentSnapshot.RawFlags is not
                long parentRawFlags)
        {
            return Result(
                resolution,
                topologyState,
                DataRelativePathRepairPlanProjectionState
                    .DestinationParentSnapshotFailed,
                sourceSnapshot,
                error:
                    openedParentSnapshot.Error ??
                    "The opened destination parent snapshot " +
                    "was incomplete."
            );
        }

        if (parentCasefoldEnabled)
        {
            return Result(
                resolution,
                topologyState,
                DataRelativePathRepairPlanProjectionState
                    .DestinationParentCasefoldNotStrict,
                sourceSnapshot,
                error:
                    "The destination parent is currently " +
                    "casefold-enabled, but direct strict case " +
                    "mismatch projection requires a strict parent."
            );
        }

        var destinationParentSnapshot =
            new DataRelativePathRepairDestinationParentSnapshot(
                PhysicalPath:
                    expectedParent,
                Identity:
                    parentIdentity,
                CasefoldEnabled:
                    parentCasefoldEnabled,
                RawFlags:
                    parentRawFlags
            );

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

        if (requireStandaloneBranchCoverage)
        {
            /*
             * A direct strict mismatch can have one already-proven physical
             * candidate whose spelling differs at the failed component.
             *
             * Creating a sparse parallel requested hierarchy is only safe
             * when the existing physical branch from that mismatched
             * directory down to the source is a single-entry chain. If any
             * directory contains another entry, creating the requested
             * parallel hierarchy would strand that untargeted content in
             * the old branch and can make it unreachable to Skyrim.
             *
             * Do not perform a fresh case-insensitive lookup here. The
             * topology classifier has already proven that sourcePath is the
             * single equivalent candidate and that its component at
             * failedIndex is the failed step's single equivalent physical
             * name.
             */
            string candidateRelative =
                Path.GetRelativePath(
                    dataRoot,
                    sourcePath
                );

            string[] candidateComponents =
                SplitComponents(
                    candidateRelative
                );

            if (
                Path.IsPathRooted(
                    candidateRelative
                ) ||
                candidateComponents.Any(component =>
                    component == ".."
                ) ||
                candidateComponents.Length !=
                    requestedComponents.Length)
            {
                return Result(
                    resolution,
                    topologyState,
                    DataRelativePathRepairPlanProjectionState
                        .ProjectionInvariantViolation,
                    sourceSnapshot,
                    error:
                        "The proven equivalent physical candidate no " +
                        "longer has the requested path shape."
                );
            }

            try
            {
                for (
                    int index = failedIndex;
                    index < candidateComponents.Length - 1;
                    index++)
                {
                    string existingPhysicalDirectory =
                        Path.Combine(
                            new[] { dataRoot }
                                .Concat(
                                    candidateComponents
                                        .Take(index + 1)
                                )
                                .ToArray()
                        );

                    string expectedPhysicalChildName =
                        candidateComponents[index + 1];

                    string[] entries =
                        Directory
                            .EnumerateFileSystemEntries(
                                existingPhysicalDirectory
                            )
                            .ToArray();

                    if (
                        entries.Length != 1 ||
                        !string.Equals(
                            Path.GetFileName(
                                entries[0]
                            ),
                            expectedPhysicalChildName,
                            StringComparison.Ordinal
                        ))
                    {
                        return Result(
                            resolution,
                            topologyState,
                            DataRelativePathRepairPlanProjectionState
                                .DestinationConflict,
                            sourceSnapshot,
                            error:
                                "The existing case-variant physical branch contains " +
                                "untargeted content that would be stranded by a " +
                                "sparse parallel repair hierarchy."
                        );
                    }
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
            operations,
            destinationParentSnapshot:
                destinationParentSnapshot
        );
    }

    private static DataRelativePathRepairPlanProjection Result(
        DataRelativePathResolution resolution,
        DataRelativePathCaseMismatchTopologyState topologyState,
        DataRelativePathRepairPlanProjectionState state,
        DataRelativePathRepairSourceSnapshot? sourceSnapshot = null,
        IReadOnlyList<DataRelativePathRepairPlanOperation>? operations = null,
        DataRelativePathRepairDestinationParentSnapshot?
            destinationParentSnapshot = null,
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
            DestinationParentSnapshot:
                destinationParentSnapshot,
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
