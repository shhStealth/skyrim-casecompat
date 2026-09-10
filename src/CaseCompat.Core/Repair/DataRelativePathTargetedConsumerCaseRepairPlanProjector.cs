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
    DestinationParentCasefoldNotStrict,
    DestinationParentAmbiguous,
    DirectoryRenameSourceIdentityUnavailable
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
        IReadOnlyList<DataRelativePathRepairDirectoryRenameSource>
            DirectoryRenameSources,
        IReadOnlyList<DataRelativePathRepairAliasSource> AliasSources,
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
    // contestedAncestorPrefixes is a whole-load-order aggregate (see
    // DataRelativePathContestedAncestorAnalyzer) that this single-candidate
    // projection has no way to derive on its own. Callers with visibility
    // into the full discovered candidate set (batch apply, the guided
    // wizard) must compute and pass it; callers that only ever plan one
    // candidate in isolation (a single exact-path plan, or admission's
    // own re-projection of an already-admitted candidate) cannot conflict
    // with anything they didn't already pass through, so the permissive
    // default is correct for them too.
    private static readonly IReadOnlySet<string>
        EmptyContestedAncestorPrefixes =
            new HashSet<string>(
                StringComparer.Ordinal
            );

    public static DataRelativePathTargetedConsumerCaseRepairPlanProjection
        Project(
            LinuxNoFollowPathHandle trustedDataRoot,
            DataRelativePathTargetedConsumerCaseRepairCandidate candidate,
            IReadOnlySet<string>? contestedAncestorPrefixes = null,
            LinuxNoFollowPathHandle? aliasesDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(
            trustedDataRoot
        );

        ArgumentNullException.ThrowIfNull(
            candidate
        );

        contestedAncestorPrefixes ??=
            EmptyContestedAncestorPrefixes;

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
                        sourcePath,
                        contestedAncestorPrefixes,
                        aliasesDirectory
                    );
                }

                if (
                    opened.State ==
                    LinuxOpenChildReadOnlyAtState
                        .ChildSymbolicLinkRejected &&
                    aliasesDirectory is not null &&
                    IsVerifiedKnownAlias(
                        aliasesDirectory,
                        current,
                        currentParentPath,
                        requestedComponent
                    ))
                {
                    // A symlink exists with exactly this requested name,
                    // and it is durably recorded as one this project
                    // itself created, freshly re-verified against its
                    // actual live target rather than trusted from the
                    // registry hint alone. Rather than following it (this
                    // project never traverses through a symlink, even its
                    // own), it is treated exactly as if this component
                    // were still missing: re-entering the same discovery
                    // that built the alias in the first place lets a
                    // later, unrelated candidate reuse it instead of
                    // being refused as a destination conflict.
                    return ProjectMissingSuffix(
                        trustedDataRoot,
                        current,
                        candidate,
                        components,
                        index,
                        currentParentPath,
                        sourcePath,
                        contestedAncestorPrefixes,
                        aliasesDirectory
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

    // A registry hit alone is a hint, never a trusted fact: this also
    // re-reads the symlink's actual current target and requires it to
    // still name exactly the registered target before trusting it for
    // anything, per this project's "always re-derive fresh proof
    // immediately before mutating or trusting" rule.
    private static bool IsVerifiedKnownAlias(
        LinuxNoFollowPathHandle aliasesDirectory,
        ILinuxOpenedHandle parent,
        string parentPath,
        string linkName)
    {
        DataRelativePathRepairAliasRegistryLookupResult lookup =
            DataRelativePathRepairAliasRegistry.TryFind(
                aliasesDirectory,
                parentPath,
                linkName
            );

        if (!lookup.Success)
        {
            return false;
        }

        LinuxReadSymlinkAtResult read =
            LinuxReadSymlinkAt.Read(
                parent,
                linkName
            );

        return
            read.Success &&
            string.Equals(
                read.Target,
                lookup.Record!.TargetName,
                StringComparison.Ordinal
            );
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
            string sourcePath,
            IReadOnlySet<string> contestedAncestorPrefixes,
            LinuxNoFollowPathHandle? aliasesDirectory)
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

        bool recheckIsVerifiedKnownAlias =
            recheck.State ==
                LinuxOpenChildReadOnlyAtState
                    .ChildSymbolicLinkRejected &&
            aliasesDirectory is not null &&
            IsVerifiedKnownAlias(
                aliasesDirectory,
                destinationParent,
                destinationParentPath,
                missingComponent
            );

        if (
            recheck.State !=
                LinuxOpenChildReadOnlyAtState
                    .ChildUnavailable &&
            !recheckIsVerifiedKnownAlias)
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
        IReadOnlyList<DataRelativePathRepairDirectoryRenameSource>
            directoryRenameSources;
        IReadOnlyList<DataRelativePathRepairAliasSource> aliasSources;

        try
        {
            if (!TryBuildOperations(
                    destinationParent,
                    destinationParentPath,
                    requestedComponents,
                    firstMissingIndex,
                    sourcePath,
                    contestedAncestorPrefixes,
                    aliasesDirectory,
                    out operations,
                    out directoryRenameSources,
                    out aliasSources,
                    out DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                        buildFailureState,
                    out string? buildError))
            {
                return Result(
                    candidate,
                    buildFailureState,
                    firstMissingComponentIndex:
                        firstMissingIndex,
                    openedDestinationParentSnapshot:
                        openedSnapshot,
                    destinationParentSnapshot:
                        destinationSnapshot,
                    error:
                        buildError
                );
            }
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
            DirectoryRenameSources:
                directoryRenameSources,
            AliasSources:
                aliasSources,
            Error:
                null
        );
    }

    // Builds the missing suffix as zero or more intermediate directory
    // steps followed by exactly one CreateFile step. Every requested
    // path component still gets exactly one operation - even a segment
    // that turns out to already be exactly correct - so the durable
    // plan's one-operation-per-component shape (relied on by
    // DurablePlan.Validate and the executor's own per-step parent
    // check) never has to special-case a skipped component.
    //
    // Each intermediate step is classified against the real, current
    // filesystem state rather than assumed to need a fresh empty
    // directory:
    //
    //   - The case-insensitive match already has the exact requested
    //     casing: a VerifyExistingDirectory step (SourcePath null).
    //     This is reachable even though the very first requested
    //     component always resolves via the outer loop's own
    //     exact-match traversal (never entering this method at all if
    //     it matches): once a known alias earlier in the path forces
    //     entry into this case-insensitive scan (see the outer loop),
    //     every later segment is resolved here too, including ones
    //     that were never mismatched. A same-name "rename" for one of
    //     those would always collide with the very directory it's
    //     supposed to rename, since source and destination would be
    //     identical - VerifyExistingDirectory instead just re-derives
    //     fresh proof it still exists before continuing.
    //   - No physical child case-insensitively matches the requested
    //     segment: a genuinely new, empty directory (SourcePath null),
    //     exactly as before. Nothing beneath a genuinely new directory
    //     can already exist either, so every deeper segment is also
    //     necessarily new.
    //   - Exactly one physical directory case-insensitively matches, and
    //     nothing about this shared ancestor is contested: that existing,
    //     possibly-populated directory becomes this operation's
    //     SourcePath, to be renamed wholesale rather than shadowed by an
    //     empty new one - see DataRelativePathRepairDirectoryRenameSource.
    //   - Exactly one physical directory case-insensitively matches, but
    //     a different, unrelated candidate's own winning consumer needs
    //     this exact same ancestor under a different casing (a genuinely
    //     contested ancestor - see DataRelativePathContestedAncestorAnalyzer):
    //     renaming would satisfy one side and strand the other, so a
    //     symlink alias is created at the missing casing instead, pointing
    //     at the one real directory - see DataRelativePathRepairAliasSource.
    //     Traversal continues into the real directory exactly as the
    //     rename branch does, so deeper requested segments still resolve
    //     against real, current filesystem state.
    //   - Multiple matches, where exactly one is a real directory and
    //     every other match is a symlink whose live target (freshly
    //     re-read, never trusted from a hint) names that same real
    //     directory and is durably recorded as one this project itself
    //     created: not actually ambiguous, since every extra match
    //     already resolves to the one real object. Collapses to that
    //     single real directory and proceeds exactly as the single-match
    //     case above.
    //   - Anything else (multiple matches that do not collapse this way,
    //     or a match that is not a directory) is refused rather than
    //     guessed at.
    private static bool TryBuildOperations(
        ILinuxOpenedHandle destinationParent,
        string destinationParentPath,
        IReadOnlyList<string> requestedComponents,
        int firstMissingIndex,
        string sourcePath,
        IReadOnlySet<string> contestedAncestorPrefixes,
        LinuxNoFollowPathHandle? aliasesDirectory,
        out IReadOnlyList<DataRelativePathRepairPlanOperation> operations,
        out IReadOnlyList<DataRelativePathRepairDirectoryRenameSource>
            directoryRenameSources,
        out IReadOnlyList<DataRelativePathRepairAliasSource> aliasSources,
        out DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
            failureState,
        out string? error)
    {
        var operationList =
            new List<DataRelativePathRepairPlanOperation>(
                requestedComponents.Count -
                firstMissingIndex
            );

        var renameSourceList =
            new List<DataRelativePathRepairDirectoryRenameSource>();

        var aliasSourceList =
            new List<DataRelativePathRepairAliasSource>();

        operations =
            operationList;

        directoryRenameSources =
            renameSourceList;

        aliasSources =
            aliasSourceList;

        failureState =
            DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                .Projected;

        error =
            null;

        string currentDestinationPath =
            destinationParentPath;

        // The physically-existing parent to keep scanning for an
        // already-populated match, or null once a segment has proven
        // genuinely new (nothing beneath a nonexistent directory can
        // exist either).
        ILinuxOpenedHandle? existingScanParent =
            destinationParent;

        string existingScanParentPath =
            destinationParentPath;

        LinuxOpenedChildHandle? ownedScanParent =
            null;

        try
        {
            for (
                int index = firstMissingIndex;
                index < requestedComponents.Count;
                index++)
            {
                string component =
                    requestedComponents[index];

                currentDestinationPath =
                    Path.GetFullPath(
                        Path.Combine(
                            currentDestinationPath,
                            component
                        )
                    );

                bool isFinal =
                    index ==
                    requestedComponents.Count - 1;

                if (isFinal)
                {
                    operationList.Add(
                        new DataRelativePathRepairPlanOperation(
                            Kind:
                                DataRelativePathRepairPlanOperationKind
                                    .CreateFile,
                            DestinationPath:
                                currentDestinationPath,
                            SourcePath:
                                sourcePath
                        )
                    );

                    break;
                }

                if (existingScanParent is null)
                {
                    operationList.Add(
                        new DataRelativePathRepairPlanOperation(
                            Kind:
                                DataRelativePathRepairPlanOperationKind
                                    .CreateDirectory,
                            DestinationPath:
                                currentDestinationPath,
                            SourcePath:
                                null
                        )
                    );

                    continue;
                }

                LinuxEnumerateDirectoryAtResult enumerated =
                    LinuxEnumerateDirectoryAt.Enumerate(
                        existingScanParent
                    );

                if (!enumerated.Success)
                {
                    failureState =
                        DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                            .DestinationInspectionFailed;

                    error =
                        enumerated.Error ??
                        enumerated.State.ToString();

                    return false;
                }

                string normalizedComponent =
                    component.ToUpperInvariant();

                string[] matches =
                    enumerated.ChildNames
                        .Where(
                            name =>
                                name.ToUpperInvariant() ==
                                normalizedComponent
                        )
                        .ToArray();

                if (matches.Length == 0)
                {
                    operationList.Add(
                        new DataRelativePathRepairPlanOperation(
                            Kind:
                                DataRelativePathRepairPlanOperationKind
                                    .CreateDirectory,
                            DestinationPath:
                                currentDestinationPath,
                            SourcePath:
                                null
                        )
                    );

                    ownedScanParent?.Dispose();

                    ownedScanParent =
                        null;

                    existingScanParent =
                        null;

                    continue;
                }

                if (matches.Length > 1)
                {
                    string? collapsed =
                        TryCollapseAmbiguousMatches(
                            existingScanParent,
                            existingScanParentPath,
                            matches,
                            aliasesDirectory
                        );

                    if (collapsed is null)
                    {
                        failureState =
                            DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                                .DestinationParentAmbiguous;

                        error =
                            $"Multiple physical directory entries beneath " +
                            $"'{existingScanParentPath}' case-insensitively " +
                            $"match the requested segment '{component}'.";

                        return false;
                    }

                    matches =
                        [collapsed];
                }

                string matchedName =
                    matches[0];

                // The matched sibling already has the exact requested
                // casing - nothing to rename and nothing to alias. This
                // is reachable here (as opposed to being handled by the
                // outer exact-match loop before this method is ever
                // called) specifically when a known alias earlier in
                // the path forced entry into this case-insensitive
                // scan: every segment from that point on is resolved
                // here, including ones that were never mismatched at
                // all. A VerifyExistingDirectory operation is emitted
                // (not skipped outright) so the durable plan keeps its
                // one-operation-per-requested-component shape; the
                // executor re-derives fresh proof this still exists
                // immediately before continuing, exactly as every other
                // step here does, rather than trusting this snapshot.
                if (matchedName == component)
                {
                    LinuxInspectChildAtResult exactInspected =
                        LinuxInspectChildAt.Inspect(
                            existingScanParent,
                            matchedName
                        );

                    if (
                        !exactInspected.Success ||
                        exactInspected.Kind !=
                            LinuxChildObjectKind.Directory)
                    {
                        failureState =
                            DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                                .DestinationConflict;

                        error =
                            $"'{matchedName}' beneath " +
                            $"'{existingScanParentPath}' already has the " +
                            "exact requested casing but is not a " +
                            "directory.";

                        return false;
                    }

                    LinuxOpenChildReadOnlyAtResult exactOpen =
                        LinuxOpenChildReadOnlyAt.Open(
                            existingScanParent,
                            matchedName
                        );

                    if (
                        !exactOpen.Success ||
                        exactOpen.OpenedChild is null)
                    {
                        failureState =
                            DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                                .DestinationInspectionFailed;

                        error =
                            exactOpen.Error ??
                            exactOpen.State.ToString();

                        return false;
                    }

                    operationList.Add(
                        new DataRelativePathRepairPlanOperation(
                            Kind:
                                DataRelativePathRepairPlanOperationKind
                                    .VerifyExistingDirectory,
                            DestinationPath:
                                currentDestinationPath,
                            SourcePath:
                                null
                        )
                    );

                    ownedScanParent?.Dispose();

                    ownedScanParent =
                        exactOpen.OpenedChild;

                    existingScanParent =
                        ownedScanParent;

                    existingScanParentPath =
                        Path.GetFullPath(
                            Path.Combine(
                                existingScanParentPath,
                                matchedName
                            )
                        );

                    continue;
                }

                // matchedName is guaranteed to differ from component
                // here - the exact-match case above always continues
                // the loop before reaching this point.
                string accumulatedPrefix =
                    string.Join(
                        '/',
                        requestedComponents.Take(
                            index + 1
                        )
                    );

                bool isContestedAncestor =
                    contestedAncestorPrefixes.Contains(
                        accumulatedPrefix.ToUpperInvariant()
                    );

                LinuxInspectChildAtResult inspected =
                    LinuxInspectChildAt.Inspect(
                        existingScanParent,
                        matchedName
                    );

                if (
                    !inspected.Success ||
                    inspected.Kind !=
                        LinuxChildObjectKind.Directory)
                {
                    failureState =
                        DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                            .DestinationConflict;

                    error =
                        $"'{matchedName}' beneath " +
                        $"'{existingScanParentPath}' case-insensitively " +
                        $"matches the requested segment '{component}' " +
                        "but is not a directory.";

                    return false;
                }

                LinuxOpenChildReadOnlyAtResult matchedOpen =
                    LinuxOpenChildReadOnlyAt.Open(
                        existingScanParent,
                        matchedName
                    );

                if (
                    !matchedOpen.Success ||
                    matchedOpen.OpenedChild is null)
                {
                    failureState =
                        DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                            .DestinationInspectionFailed;

                    error =
                        matchedOpen.Error ??
                        matchedOpen.State.ToString();

                    return false;
                }

                LinuxOpenedChildHandle matchedHandle =
                    matchedOpen.OpenedChild;

                string matchedPhysicalPath =
                    Path.GetFullPath(
                        Path.Combine(
                            existingScanParentPath,
                            matchedName
                        )
                    );

                LinuxOpenedDirectorySnapshotResult matchedSnapshot =
                    LinuxOpenedDirectorySnapshot.Capture(
                        matchedHandle,
                        matchedPhysicalPath
                    );

                LinuxOpenedInodeGenerationResult matchedGeneration =
                    LinuxOpenedInodeGeneration.Capture(
                        matchedHandle
                    );

                if (
                    !matchedSnapshot.Success ||
                    matchedSnapshot.Identity is not
                        LinuxFileIdentityResult matchedIdentity ||
                    !matchedGeneration.Success ||
                    matchedGeneration.Generation is not
                        uint matchedGenerationValue)
                {
                    matchedHandle.Dispose();

                    failureState =
                        DataRelativePathTargetedConsumerCaseRepairPlanProjectionState
                            .DirectoryRenameSourceIdentityUnavailable;

                    error =
                        matchedSnapshot.Error ??
                        matchedGeneration.Error ??
                        "The existing physical directory's identity " +
                        "could not be captured.";

                    return false;
                }

                if (isContestedAncestor)
                {
                    operationList.Add(
                        new DataRelativePathRepairPlanOperation(
                            Kind:
                                DataRelativePathRepairPlanOperationKind
                                    .CreateAliasSymlink,
                            DestinationPath:
                                currentDestinationPath,
                            SourcePath:
                                matchedPhysicalPath
                        )
                    );

                    aliasSourceList.Add(
                        new DataRelativePathRepairAliasSource(
                            DestinationPath:
                                currentDestinationPath,
                            PhysicalPath:
                                matchedPhysicalPath,
                            Identity:
                                matchedIdentity,
                            InodeGeneration:
                                matchedGenerationValue
                        )
                    );
                }
                else
                {
                    operationList.Add(
                        new DataRelativePathRepairPlanOperation(
                            Kind:
                                DataRelativePathRepairPlanOperationKind
                                    .CreateDirectory,
                            DestinationPath:
                                currentDestinationPath,
                            SourcePath:
                                matchedPhysicalPath
                        )
                    );

                    renameSourceList.Add(
                        new DataRelativePathRepairDirectoryRenameSource(
                            DestinationPath:
                                currentDestinationPath,
                            PhysicalPath:
                                matchedPhysicalPath,
                            Identity:
                                matchedIdentity,
                            InodeGeneration:
                                matchedGenerationValue
                        )
                    );
                }

                ownedScanParent?.Dispose();

                ownedScanParent =
                    matchedHandle;

                existingScanParent =
                    matchedHandle;

                existingScanParentPath =
                    matchedPhysicalPath;
            }

            return true;
        }
        finally
        {
            ownedScanParent?.Dispose();
        }
    }

    // Not a trust decision made from names alone: every symlink among
    // the matches must freshly re-read (never assumed from a hint) to
    // the one real object's own current name, AND carry a durable
    // registry record proving this project created it. A single
    // unverifiable or non-matching entry aborts the whole collapse
    // rather than guessing which of several matches is the "right" one.
    private static string? TryCollapseAmbiguousMatches(
        ILinuxOpenedHandle parent,
        string parentPath,
        IReadOnlyList<string> matches,
        LinuxNoFollowPathHandle? aliasesDirectory)
    {
        if (aliasesDirectory is null)
        {
            return null;
        }

        string? realName =
            null;

        var symlinkTargets =
            new List<(string Name, string Target)>();

        foreach (string name in matches)
        {
            LinuxReadSymlinkAtResult read =
                LinuxReadSymlinkAt.Read(
                    parent,
                    name
                );

            if (
                read.State ==
                LinuxReadSymlinkAtState.ChildNotSymbolicLink)
            {
                if (realName is not null)
                {
                    // More than one real object among the matches -
                    // genuinely ambiguous, not just an alias echo.
                    return null;
                }

                realName =
                    name;

                continue;
            }

            if (!read.Success)
            {
                return null;
            }

            symlinkTargets.Add(
                (name, read.Target!)
            );
        }

        if (realName is null)
        {
            return null;
        }

        foreach (
            (string name, string target)
            in symlinkTargets)
        {
            if (
                !string.Equals(
                    target,
                    realName,
                    StringComparison.Ordinal))
            {
                return null;
            }

            DataRelativePathRepairAliasRegistryLookupResult lookup =
                DataRelativePathRepairAliasRegistry.TryFind(
                    aliasesDirectory,
                    parentPath,
                    name
                );

            if (
                !lookup.Success ||
                !string.Equals(
                    lookup.Record!.TargetName,
                    realName,
                    StringComparison.Ordinal))
            {
                return null;
            }
        }

        return realName;
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
            IReadOnlyList<DataRelativePathRepairDirectoryRenameSource>?
                directoryRenameSources = null,
            IReadOnlyList<DataRelativePathRepairAliasSource>?
                aliasSources = null,
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
            DirectoryRenameSources:
                directoryRenameSources ??
                Array.Empty<
                    DataRelativePathRepairDirectoryRenameSource
                >(),
            AliasSources:
                aliasSources ??
                Array.Empty<
                    DataRelativePathRepairAliasSource
                >(),
            Error:
                error
        );
    }
}
