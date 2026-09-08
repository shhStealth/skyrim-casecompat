using CaseCompat.Core.Analysis;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

// Read-only planning state for one already source-bound,
// consumer-authoritative targeted repair candidate.
//
// This projection establishes only the destination operation shape.
// It grants no persistence, batch, authorization, execution, rollback,
// recovery, or mutation authority.
public enum DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
{
    Projected,

    InvalidCandidateEvidence,
    InvalidRequestedPath,
    DataRootMismatch,

    DestinationInspectionFailed,
    DestinationConflict,
    DestinationParentSnapshotFailed,
    DestinationParentCasefoldNotStrict
}

// Manifest-independent destination plan shape.
//
// Source authority remains entirely with Candidate. SourceSnapshot is not
// reacquired, rehashed, or reselected here.
//
// FirstMissingComponentIndex identifies the first exact requested component
// proven absent beneath the retained destination-parent descriptor.
//
// Operations contain the requested missing suffix only:
// zero or more CreateDirectory operations followed by one CreateFile.
public sealed record
    DataRelativePathTargetedConsumerCaseRepairPlanProjection(
        DataRelativePathTargetedConsumerCaseRepairCandidate Candidate,
        DataRelativePathTargetedConsumerCaseRepairPlanProjectionState State,
        int? FirstMissingComponentIndex,
        LinuxOpenedDirectorySnapshotResult? OpenedDestinationParentSnapshot,
        DataRelativePathRepairDestinationParentSnapshot?
            DestinationParentSnapshot,
        IReadOnlyList<DataRelativePathRepairPlanOperation> Operations,
        string? Error
    )
{
    public bool HasPlan =>
        State ==
            DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                .Projected &&
        FirstMissingComponentIndex is not null &&
        DestinationParentSnapshot is not null &&
        Operations.Count > 0;
}

// Consumer-authoritative targeted destination planning.
//
// Deliberately absent:
// - DataRelativePathResolver
// - DataRelativePathCaseMismatchTopologyClassifier
// - EquivalentPhysicalCandidates
// - plan-manifest schemas
// - aggregate namespace manifests / sidecars
// - source-generation binding
// - persistence
// - mutation
//
// Algorithm:
//
// 1. Validate that the C4E-4B source snapshot belongs beneath this exact
//    retained Data root and is Windows-logically equivalent to the
//    authoritative consumer path.
// 2. Traverse the exact requested spelling from the retained Data descriptor.
// 3. The first LinuxOpenChildReadOnlyAtState.ChildUnavailable is the only
//    accepted proof of a missing exact requested component.
// 4. Capture the current parent snapshot from that same already-open
//    descriptor and require a strict parent.
// 5. Recheck the missing child on that same descriptor after snapshot capture.
// 6. Project the missing requested suffix as directories plus one final file.
//
// A source freshness binder belongs to a later authorization/admission layer.
public static class
    DataRelativePathTargetedConsumerCaseRepairPlanProjector
{
    public static DataRelativePathTargetedConsumerCaseRepairPlanProjection
        Project(
            LinuxNoFollowPathHandle trustedDataRoot,
            DataRelativePathTargetedConsumerCaseRepairCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(
            trustedDataRoot
        );

        ArgumentNullException.ThrowIfNull(
            candidate
        );

        if (!TryValidateCandidate(
                trustedDataRoot,
                candidate,
                out string dataRoot,
                out string requestedPath,
                out string sourcePath,
                out string? validationError,
                out DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                    validationState))
        {
            return Result(
                candidate,
                validationState,
                error:
                    validationError
            );
        }

        string[] components =
            requestedPath.Split('/');

        ILinuxOpenedHandle current =
            trustedDataRoot;

        LinuxOpenedChildHandle? ownedCurrent =
            null;

        string currentParentPath =
            dataRoot;

        try
        {
            for (
                int index = 0;
                index < components.Length;
                index++)
            {
                string requestedComponent =
                    components[index];

                LinuxOpenChildReadOnlyAtResult opened =
                    LinuxOpenChildReadOnlyAt.Open(
                        current,
                        requestedComponent
                    );

                if (
                    opened.State ==
                    LinuxOpenChildReadOnlyAtState
                        .ChildUnavailable)
                {
                    return ProjectMissingSuffix(
                        trustedDataRoot,
                        current,
                        candidate,
                        components,
                        index,
                        currentParentPath,
                        sourcePath
                    );
                }

                if (!opened.Success ||
                    opened.OpenedChild is null)
                {
                    DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                        state =
                            opened.State is
                                LinuxOpenChildReadOnlyAtState
                                    .ChildSymbolicLinkRejected or
                                LinuxOpenChildReadOnlyAtState
                                    .ParentNotDirectory
                                ? DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                                    .DestinationConflict
                                : DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                                    .DestinationInspectionFailed;

                    return Result(
                        candidate,
                        state,
                        error:
                            opened.Error ??
                            opened.State.ToString()
                    );
                }

                LinuxOpenedChildHandle next =
                    opened.OpenedChild;

                bool finalComponent =
                    index ==
                    components.Length - 1;

                if (finalComponent)
                {
                    next.Dispose();

                    return Result(
                        candidate,
                        DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                            .DestinationConflict,
                        error:
                            "The exact authoritative requested destination " +
                            "already exists."
                    );
                }

                ownedCurrent?.Dispose();

                ownedCurrent =
                    next;

                current =
                    next;

                try
                {
                    currentParentPath =
                        Path.GetFullPath(
                            Path.Combine(
                                currentParentPath,
                                requestedComponent
                            )
                        );
                }
                catch (
                    Exception ex)
                    when (
                        ex is ArgumentException or
                        NotSupportedException or
                        PathTooLongException)
                {
                    return Result(
                        candidate,
                        DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                            .InvalidRequestedPath,
                        error:
                            "The exact destination prefix cannot be " +
                            $"recomposed: {ex.Message}"
                    );
                }
            }

            return Result(
                candidate,
                DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                    .DestinationInspectionFailed,
                error:
                    "Destination traversal ended without either an exact " +
                    "destination or a proven missing component."
            );
        }
        finally
        {
            ownedCurrent?.Dispose();
        }
    }

    private static
        DataRelativePathTargetedConsumerCaseRepairPlanProjection
        ProjectMissingSuffix(
            LinuxNoFollowPathHandle trustedDataRoot,
            ILinuxOpenedHandle destinationParent,
            DataRelativePathTargetedConsumerCaseRepairCandidate candidate,
            IReadOnlyList<string> requestedComponents,
            int firstMissingIndex,
            string destinationParentPath,
            string sourcePath)
    {
        LinuxOpenedDirectorySnapshotResult openedSnapshot =
            LinuxOpenedDirectorySnapshot.Capture(
                destinationParent,
                destinationParentPath
            );

        if (
            !openedSnapshot.Success ||
            openedSnapshot.Identity is not
                LinuxFileIdentityResult identity ||
            openedSnapshot.CasefoldEnabled is not
                bool casefoldEnabled ||
            openedSnapshot.RawFlags is not
                long rawFlags)
        {
            return Result(
                candidate,
                DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                    .DestinationParentSnapshotFailed,
                firstMissingComponentIndex:
                    firstMissingIndex,
                openedDestinationParentSnapshot:
                    openedSnapshot,
                error:
                    openedSnapshot.Error ??
                    openedSnapshot.State.ToString()
            );
        }

        if (casefoldEnabled)
        {
            return Result(
                candidate,
                DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                    .DestinationParentCasefoldNotStrict,
                firstMissingComponentIndex:
                    firstMissingIndex,
                openedDestinationParentSnapshot:
                    openedSnapshot,
                error:
                    "The exact requested destination is missing beneath a " +
                    "casefold-enabled parent; a parallel case repair branch " +
                    "must not be planned there."
            );
        }

        var destinationSnapshot =
            new DataRelativePathRepairDestinationParentSnapshot(
                PhysicalPath:
                    destinationParentPath,
                Identity:
                    identity,
                CasefoldEnabled:
                    false,
                RawFlags:
                    rawFlags
            );

        string missingComponent =
            requestedComponents[
                firstMissingIndex
            ];

        LinuxOpenChildReadOnlyAtResult recheck =
            LinuxOpenChildReadOnlyAt.Open(
                destinationParent,
                missingComponent
            );

        if (
            recheck.State !=
            LinuxOpenChildReadOnlyAtState
                .ChildUnavailable)
        {
            if (recheck.Success &&
                recheck.OpenedChild is not null)
            {
                recheck.OpenedChild.Dispose();

                return Result(
                    candidate,
                    DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                        .DestinationConflict,
                    firstMissingComponentIndex:
                        firstMissingIndex,
                    openedDestinationParentSnapshot:
                        openedSnapshot,
                    destinationParentSnapshot:
                        destinationSnapshot,
                    error:
                        "The requested destination component appeared while " +
                        "the targeted plan shape was being captured."
                );
            }

            DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                state =
                    recheck.State is
                        LinuxOpenChildReadOnlyAtState
                            .ChildSymbolicLinkRejected or
                        LinuxOpenChildReadOnlyAtState
                            .ParentNotDirectory
                        ? DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                            .DestinationConflict
                        : DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                            .DestinationInspectionFailed;

            return Result(
                candidate,
                state,
                firstMissingComponentIndex:
                    firstMissingIndex,
                openedDestinationParentSnapshot:
                    openedSnapshot,
                destinationParentSnapshot:
                    destinationSnapshot,
                error:
                    recheck.Error ??
                    recheck.State.ToString()
            );
        }

        IReadOnlyList<DataRelativePathRepairPlanOperation> operations;

        try
        {
            operations =
                BuildOperations(
                    destinationParentPath,
                    requestedComponents,
                    firstMissingIndex,
                    sourcePath
                );
        }
        catch (
            Exception ex)
            when (
                ex is ArgumentException or
                NotSupportedException or
                PathTooLongException)
        {
            return Result(
                candidate,
                DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                    .InvalidRequestedPath,
                firstMissingComponentIndex:
                    firstMissingIndex,
                openedDestinationParentSnapshot:
                    openedSnapshot,
                destinationParentSnapshot:
                    destinationSnapshot,
                error:
                    "The requested missing suffix cannot be projected: " +
                    ex.Message
            );
        }

        return new(
            Candidate:
                candidate,
            State:
                DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                    .Projected,
            FirstMissingComponentIndex:
                firstMissingIndex,
            OpenedDestinationParentSnapshot:
                openedSnapshot,
            DestinationParentSnapshot:
                destinationSnapshot,
            Operations:
                operations,
            Error:
                null
        );
    }

    private static IReadOnlyList<DataRelativePathRepairPlanOperation>
        BuildOperations(
            string destinationParentPath,
            IReadOnlyList<string> requestedComponents,
            int firstMissingIndex,
            string sourcePath)
    {
        var operations =
            new List<DataRelativePathRepairPlanOperation>(
                requestedComponents.Count -
                firstMissingIndex
            );

        string current =
            destinationParentPath;

        for (
            int index = firstMissingIndex;
            index < requestedComponents.Count;
            index++)
        {
            current =
                Path.GetFullPath(
                    Path.Combine(
                        current,
                        requestedComponents[index]
                    )
                );

            bool final =
                index ==
                requestedComponents.Count - 1;

            operations.Add(
                new DataRelativePathRepairPlanOperation(
                    Kind:
                        final
                            ? DataRelativePathRepairPlanOperationKind
                                .CreateFile
                            : DataRelativePathRepairPlanOperationKind
                                .CreateDirectory,
                    DestinationPath:
                        current,
                    SourcePath:
                        final
                            ? sourcePath
                            : null
                )
            );
        }

        return operations.ToArray();
    }

    private static bool TryValidateCandidate(
        LinuxNoFollowPathHandle trustedDataRoot,
        DataRelativePathTargetedConsumerCaseRepairCandidate candidate,
        out string dataRoot,
        out string requestedPath,
        out string sourcePath,
        out string? error,
        out DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
            failureState)
    {
        dataRoot =
            string.Empty;

        requestedPath =
            string.Empty;

        sourcePath =
            string.Empty;

        error =
            null;

        failureState =
            DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                .InvalidCandidateEvidence;

        try
        {
            string normalizedRoot =
                Path.GetFullPath(
                    trustedDataRoot.RootPath
                );

            if (!string.Equals(
                    normalizedRoot,
                    trustedDataRoot.RootPath,
                    StringComparison.Ordinal))
            {
                error =
                    "The retained Data-root path is not canonical.";

                failureState =
                    DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                        .DataRootMismatch;

                return false;
            }

            dataRoot =
                normalizedRoot;

            requestedPath =
                candidate.AuthoritativeRequestedPath;

            if (!TryValidateRequestedPath(
                    requestedPath,
                    out string[] requestedComponents))
            {
                error =
                    "The authoritative consumer requested path is not a " +
                    "canonical slash-separated Data-relative file path.";

                failureState =
                    DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                        .InvalidRequestedPath;

                return false;
            }

            sourcePath =
                Path.GetFullPath(
                    candidate.SourceSnapshot.PhysicalPath
                );

            if (!string.Equals(
                    sourcePath,
                    candidate.SourceSnapshot.PhysicalPath,
                    StringComparison.Ordinal))
            {
                error =
                    "The candidate source snapshot path is not canonical.";

                return false;
            }

            string sourceRelative =
                Path.GetRelativePath(
                    dataRoot,
                    sourcePath
                );

            if (IsOutsideRoot(
                    sourceRelative) ||
                sourceRelative == ".")
            {
                error =
                    "The C4E-4B source snapshot does not belong to the " +
                    "retained Data root.";

                failureState =
                    DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                        .DataRootMismatch;

                return false;
            }

            sourceRelative =
                sourceRelative.Replace(
                    '\\',
                    '/'
                );

            if (!TryValidateRequestedPath(
                    sourceRelative,
                    out string[] sourceComponents) ||
                sourceComponents.Length !=
                    requestedComponents.Length)
            {
                error =
                    "The candidate source is not a valid case-equivalent " +
                    "Data-relative representation of the consumer path.";

                return false;
            }

            if (
                WindowsLogicalPath.FromRelativePath(
                    sourceRelative) !=
                WindowsLogicalPath.FromRelativePath(
                    requestedPath))
            {
                error =
                    "The candidate source and authoritative requested path " +
                    "are not Windows-logically equivalent.";

                return false;
            }

            string representationRelative =
                candidate.SourceRepresentation.RelativePath;

            if (!TryValidateRequestedPath(
                    representationRelative,
                    out _))
            {
                error =
                    "The candidate source representation has an invalid " +
                    "Data-relative path.";

                return false;
            }

            string recomposedSource =
                Path.GetFullPath(
                    Path.Combine(
                        dataRoot,
                        representationRelative.Replace(
                            '/',
                            Path.DirectorySeparatorChar
                        )
                    )
                );

            if (!string.Equals(
                    recomposedSource,
                    sourcePath,
                    StringComparison.Ordinal))
            {
                error =
                    "The candidate source representation does not exactly " +
                    "recompose to its source snapshot physical path.";

                return false;
            }

            return true;
        }
        catch (
            Exception ex)
            when (
                ex is ArgumentException or
                InvalidOperationException or
                NotSupportedException or
                PathTooLongException or
                NullReferenceException)
        {
            error =
                "The C4E-4B candidate evidence is malformed: " +
                ex.Message;

            return false;
        }
    }

    private static bool TryValidateRequestedPath(
        string? path,
        out string[] components)
    {
        components =
            Array.Empty<string>();

        if (
            string.IsNullOrWhiteSpace(
                path) ||
            Path.IsPathRooted(
                path) ||
            path.StartsWith(
                '\\') ||
            path.Contains(
                '\\') ||
            path.Contains(
                '\0'))
        {
            return false;
        }

        string[] split =
            path.Split(
                '/',
                StringSplitOptions.None
            );

        if (
            split.Length == 0 ||
            split.Any(component =>
                string.IsNullOrEmpty(
                    component) ||
                component is "." or ".."))
        {
            return false;
        }

        components =
            split;

        return true;
    }

    private static bool IsOutsideRoot(
        string relativePath)
    {
        return
            Path.IsPathRooted(
                relativePath) ||
            relativePath == ".." ||
            relativePath.StartsWith(
                "../",
                StringComparison.Ordinal) ||
            relativePath.StartsWith(
                "..\\",
                StringComparison.Ordinal);
    }

    private static
        DataRelativePathTargetedConsumerCaseRepairPlanProjection
        Result(
            DataRelativePathTargetedConsumerCaseRepairCandidate candidate,
            DataRelativePathTargetedConsumerCaseRepairPlanProjectionState state,
            int? firstMissingComponentIndex = null,
            LinuxOpenedDirectorySnapshotResult?
                openedDestinationParentSnapshot = null,
            DataRelativePathRepairDestinationParentSnapshot?
                destinationParentSnapshot = null,
            IReadOnlyList<DataRelativePathRepairPlanOperation>? operations = null,
            string? error = null)
    {
        return new(
            Candidate:
                candidate,
            State:
                state,
            FirstMissingComponentIndex:
                firstMissingComponentIndex,
            OpenedDestinationParentSnapshot:
                openedDestinationParentSnapshot,
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
}
