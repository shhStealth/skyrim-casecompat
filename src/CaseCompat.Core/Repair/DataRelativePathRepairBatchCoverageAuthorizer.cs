using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairBatchCoverageDecisionState
{
    Authorized,

    InvalidCandidate,
    InvalidCandidateShape,
    DuplicateSourceCoverage,

    ConflictingRequestedNamespace,

    PhysicalBranchOpenFailed,
    PhysicalBranchEnumerationFailed,
    IncompletePhysicalCoverage
}

public sealed record
    DataRelativePathRepairBatchCoverageDecision(
        int CandidateIndex,
        DataRelativePathRepairBatchCoverageDecisionState State,
        string? Error)
{
    public bool Authorized =>
        State ==
            DataRelativePathRepairBatchCoverageDecisionState
                .Authorized;
}

public sealed record
    DataRelativePathRepairBatchCoverageAuthorization(
        IReadOnlyList<
            DataRelativePathRepairBatchCoverageDecision
        > Decisions)
{
    public int AuthorizedCount =>
        Decisions.Count(
            decision =>
                decision.Authorized
        );

    public int RejectedCount =>
        Decisions.Count -
        AuthorizedCount;

    public bool AllAuthorized =>
        RejectedCount == 0;
}

/*
 * Authorize technically projectable batch candidates against the complete
 * physical namespace that would be split by their requested directory
 * spelling.
 *
 * This class grants no persistence or execution authority by itself.
 *
 * repair-plan-batch consumes projection-derived decisions before
 * publishing a coverage-authorized schema-v2 batch.
 *
 * repair-apply-batch consumes persisted-manifest-derived decisions as
 * fresh filesystem proof before publishing durable batch-wide apply
 * authorization.
 *
 * Safety properties:
 *
 * - every candidate must already be a ProjectBatchCandidate() success;
 * - every existing entry beneath an affected physical case-variant branch
 *   must be represented by the candidate set for that branch;
 * - one physical child name may map to only one exact requested spelling;
 * - one physical source file may be covered only once;
 * - recursive traversal is descriptor-relative;
 * - directories are opened with O_DIRECTORY | O_NOFOLLOW;
 * - enumeration failures or unexpected topology fail closed.
 */
public static class
    DataRelativePathRepairBatchCoverageAuthorizer
{
    public static
        DataRelativePathRepairBatchCoverageAuthorization
        Authorize(
            IReadOnlyList<
                DataRelativePathRepairPlanProjection
            > candidates)
    {
        ArgumentNullException.ThrowIfNull(
            candidates
        );

        var decisions =
            new DataRelativePathRepairBatchCoverageDecision?[
                candidates.Count
            ];

        var infos =
            new List<CandidateInfo>(
                candidates.Count
            );

        var sourceClaims =
            new List<SourceClaim>(
                candidates.Count
            );

        for (
            int index = 0;
            index < candidates.Count;
            index++)
        {
            DataRelativePathRepairPlanProjection candidate =
                candidates[index];

            if (
                candidate is null ||
                !candidate.HasPlan ||
                candidate.SourceSnapshot is null ||
                candidate.Resolution.FailedComponentIndex
                    is not int failedIndex)
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchCoverageDecisionState
                            .InvalidCandidate,
                        "Batch coverage authorization requires a " +
                        "successful projected candidate with source " +
                        "snapshot and failed-component evidence."
                    );

                continue;
            }

            string dataRoot;
            string sourcePath;
            string sourceRelative;

            try
            {
                dataRoot =
                    Path.GetFullPath(
                        candidate.Resolution.DataRoot
                    );

                sourcePath =
                    Path.GetFullPath(
                        candidate.SourceSnapshot
                            .PhysicalPath
                    );

                sourceRelative =
                    Path.GetRelativePath(
                        dataRoot,
                        sourcePath
                    );
            }
            catch (Exception ex)
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchCoverageDecisionState
                            .InvalidCandidateShape,
                        ex.Message
                    );

                continue;
            }

            string[] physicalComponents =
                SplitComponents(
                    sourceRelative
                );

            string[] requestedComponents =
                SplitComponents(
                    candidate.Resolution.RequestedPath
                );

            if (
                Path.IsPathRooted(
                    sourceRelative
                ) ||
                IsOutsideRelativePath(
                    sourceRelative
                ) ||
                physicalComponents.Length == 0 ||
                physicalComponents.Length !=
                    requestedComponents.Length ||
                failedIndex < 0 ||
                failedIndex >=
                    physicalComponents.Length)
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchCoverageDecisionState
                            .InvalidCandidateShape,
                        "The candidate source and requested paths do " +
                        "not describe one valid Data-relative path shape."
                    );

                continue;
            }

            sourceClaims.Add(
                new SourceClaim(
                    CandidateIndex:
                        index,
                    PhysicalPath:
                        sourcePath
                )
            );

            if (
                failedIndex ==
                    physicalComponents.Length - 1)
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchCoverageDecisionState
                            .Authorized
                    );

                continue;
            }

            string branchKey =
                BuildBranchKey(
                    dataRoot,
                    physicalComponents,
                    failedIndex
                );

            infos.Add(
                new CandidateInfo(
                    CandidateIndex:
                        index,
                    DataRoot:
                        dataRoot,
                    PhysicalComponents:
                        physicalComponents,
                    RequestedComponents:
                        requestedComponents,
                    FailedIndex:
                        failedIndex,
                    BranchKey:
                        branchKey
                )
            );
        }

        return AuthorizeNormalized(
            decisions,
            infos,
            sourceClaims
        );
    }

    /*
     * Reconstruct the same normalized namespace-coverage claims from
     * independently validated durable schema-v2 child manifests.
     *
     * This method grants no mutation authority. A mutating caller must
     * separately authenticate durable batch membership and exact manifest
     * bytes before relying on these claims.
     */
    public static
        DataRelativePathRepairBatchCoverageAuthorization
        AuthorizePersistedManifests(
            IReadOnlyList<
                DataRelativePathRepairPlanManifestRecord
            > manifests)
    {
        ArgumentNullException.ThrowIfNull(
            manifests
        );

        var decisions =
            new DataRelativePathRepairBatchCoverageDecision?[
                manifests.Count
            ];

        var infos =
            new List<CandidateInfo>(
                manifests.Count
            );

        var sourceClaims =
            new List<SourceClaim>(
                manifests.Count
            );

        for (
            int index = 0;
            index < manifests.Count;
            index++)
        {
            DataRelativePathRepairPlanManifestRecord? manifest =
                manifests[index];

            if (manifest is null)
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchCoverageDecisionState
                            .InvalidCandidate,
                        "Persisted batch coverage requires a non-null " +
                        "plan manifest."
                    );

                continue;
            }

            string? validationError;

            try
            {
                validationError =
                    DataRelativePathRepairPlanManifest.Validate(
                        manifest
                    );
            }
            catch (Exception ex)
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchCoverageDecisionState
                            .InvalidCandidate,
                        ex.Message
                    );

                continue;
            }

            if (validationError is not null)
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchCoverageDecisionState
                            .InvalidCandidate,
                        validationError
                    );

                continue;
            }

            if (
                manifest.SchemaVersion !=
                    DataRelativePathRepairPlanManifestRecord
                        .SchemaVersion2 ||
                manifest.ResolvedPrefixSteps is null)
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchCoverageDecisionState
                            .InvalidCandidate,
                        "Persisted aggregate namespace coverage requires " +
                        "a valid schema-v2 plan manifest with durable " +
                        "resolved-prefix evidence."
                    );

                continue;
            }

            string dataRoot;
            string sourcePath;
            string sourceRelative;

            try
            {
                dataRoot =
                    Path.GetFullPath(
                        manifest.DataRoot
                    );

                sourcePath =
                    Path.GetFullPath(
                        manifest.SourceSnapshot.PhysicalPath
                    );

                sourceRelative =
                    Path.GetRelativePath(
                        dataRoot,
                        sourcePath
                    );
            }
            catch (Exception ex)
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchCoverageDecisionState
                            .InvalidCandidateShape,
                        ex.Message
                    );

                continue;
            }

            string[] physicalComponents =
                SplitComponents(
                    sourceRelative
                );

            string[] requestedComponents =
                SplitComponents(
                    manifest.RequestedPath
                );

            int failedIndex =
                manifest.ResolvedPrefixSteps.Count;

            if (
                Path.IsPathRooted(
                    sourceRelative
                ) ||
                IsOutsideRelativePath(
                    sourceRelative
                ) ||
                physicalComponents.Length == 0 ||
                physicalComponents.Length !=
                    requestedComponents.Length ||
                failedIndex < 0 ||
                failedIndex >=
                    physicalComponents.Length)
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchCoverageDecisionState
                            .InvalidCandidateShape,
                        "The persisted source and requested paths do not " +
                        "describe one valid Data-relative path shape."
                    );

                continue;
            }

            bool prefixMatchesSource =
                true;

            for (
                int componentIndex = 0;
                componentIndex < failedIndex;
                componentIndex++)
            {
                DataRelativePathRepairPlanResolvedPrefixStep step =
                    manifest.ResolvedPrefixSteps[
                        componentIndex
                    ];

                if (
                    !string.Equals(
                        physicalComponents[
                            componentIndex
                        ],
                        step.SelectedPhysicalName,
                        StringComparison.Ordinal))
                {
                    prefixMatchesSource =
                        false;

                    break;
                }
            }

            if (!prefixMatchesSource)
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchCoverageDecisionState
                            .InvalidCandidateShape,
                        "The persisted physical source does not reproduce " +
                        "the durable resolved-prefix hierarchy."
                    );

                continue;
            }

            /*
             * ResolvedPrefixSteps.Count is the strict mismatch index.
             *
             * The source and requested suffix may contain additional case
             * differences below that first strict mismatch, so compare every
             * remaining component using Skyrim/Windows logical-path semantics.
             *
             * The failed component itself must differ ordinally; otherwise
             * the durable manifest does not describe the direct strict-case
             * boundary represented by the schema-v2 prefix.
             */
            if (
                string.Equals(
                    physicalComponents[
                        failedIndex
                    ],
                    requestedComponents[
                        failedIndex
                    ],
                    StringComparison.Ordinal))
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchCoverageDecisionState
                            .InvalidCandidateShape,
                        "The persisted failed component is ordinally exact " +
                        "and therefore does not describe a strict case " +
                        "mismatch."
                    );

                continue;
            }

            bool suffixLogicallyEquivalent =
                true;

            for (
                int componentIndex = failedIndex;
                componentIndex <
                    physicalComponents.Length;
                componentIndex++)
            {
                if (
                    !AreWindowsLogicallyEquivalentComponents(
                        physicalComponents[
                            componentIndex
                        ],
                        requestedComponents[
                            componentIndex
                        ]))
                {
                    suffixLogicallyEquivalent =
                        false;

                    break;
                }
            }

            if (!suffixLogicallyEquivalent)
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchCoverageDecisionState
                            .InvalidCandidateShape,
                        "The persisted physical source and requested path " +
                        "are not Windows-logically equivalent from the " +
                        "strict mismatch through the source leaf."
                    );

                continue;
            }

            sourceClaims.Add(
                new SourceClaim(
                    CandidateIndex:
                        index,
                    PhysicalPath:
                        sourcePath
                )
            );

            if (
                failedIndex ==
                    physicalComponents.Length - 1)
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchCoverageDecisionState
                            .Authorized
                    );

                continue;
            }

            string branchKey =
                BuildBranchKey(
                    dataRoot,
                    physicalComponents,
                    failedIndex
                );

            infos.Add(
                new CandidateInfo(
                    CandidateIndex:
                        index,
                    DataRoot:
                        dataRoot,
                    PhysicalComponents:
                        physicalComponents,
                    RequestedComponents:
                        requestedComponents,
                    FailedIndex:
                        failedIndex,
                    BranchKey:
                        branchKey
                )
            );
        }

        return AuthorizeNormalized(
            decisions,
            infos,
            sourceClaims
        );
    }

    /*
     * Reconstruct coverage-policy-v2 claims from independently validated
     * schema-v4 aggregate alternate-branch child manifests.
     *
     * This method grants no persistence or mutation authority.
     *
     * The existing AuthorizePersistedManifests() method remains the
     * unchanged schema-v2/policy-v1 reconstruction contract.
     */
    public static
        DataRelativePathRepairBatchCoverageAuthorization
        AuthorizeAggregateAlternateBranchPersistedManifests(
            IReadOnlyList<
                DataRelativePathRepairPlanManifestRecord
            > manifests)
    {
        ArgumentNullException.ThrowIfNull(
            manifests
        );

        var decisions =
            new DataRelativePathRepairBatchCoverageDecision?[
                manifests.Count
            ];

        var infos =
            new List<CandidateInfo>(
                manifests.Count
            );

        var sourceClaims =
            new List<SourceClaim>(
                manifests.Count
            );

        for (
            int index = 0;
            index < manifests.Count;
            index++)
        {
            DataRelativePathRepairPlanManifestRecord? manifest =
                manifests[index];

            if (manifest is null)
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchCoverageDecisionState
                            .InvalidCandidate,
                        "Persisted batch coverage requires a non-null " +
                        "plan manifest."
                    );

                continue;
            }

            string? validationError;

            try
            {
                validationError =
                    DataRelativePathRepairPlanManifest.Validate(
                        manifest
                    );
            }
            catch (Exception ex)
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchCoverageDecisionState
                            .InvalidCandidate,
                        ex.Message
                    );

                continue;
            }

            if (validationError is not null)
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchCoverageDecisionState
                            .InvalidCandidate,
                        validationError
                    );

                continue;
            }

            if (
                manifest.SchemaVersion !=
                    DataRelativePathRepairPlanManifestRecord
                        .SchemaVersion4 ||
                manifest.ResolvedPrefixSteps is null)
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchCoverageDecisionState
                            .InvalidCandidate,
                        "Persisted aggregate namespace coverage requires " +
                        "a valid schema-v4 aggregate alternate-branch plan manifest with durable " +
                        "resolved-prefix evidence."
                    );

                continue;
            }

            string dataRoot;
            string sourcePath;
            string sourceRelative;

            try
            {
                dataRoot =
                    Path.GetFullPath(
                        manifest.DataRoot
                    );

                sourcePath =
                    Path.GetFullPath(
                        manifest.SourceSnapshot.PhysicalPath
                    );

                sourceRelative =
                    Path.GetRelativePath(
                        dataRoot,
                        sourcePath
                    );
            }
            catch (Exception ex)
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchCoverageDecisionState
                            .InvalidCandidateShape,
                        ex.Message
                    );

                continue;
            }

            string[] physicalComponents =
                SplitComponents(
                    sourceRelative
                );

            string[] requestedComponents =
                SplitComponents(
                    manifest.RequestedPath
                );

            int failedIndex =
                manifest.ResolvedPrefixSteps.Count;

            if (
                Path.IsPathRooted(
                    sourceRelative
                ) ||
                IsOutsideRelativePath(
                    sourceRelative
                ) ||
                physicalComponents.Length == 0 ||
                physicalComponents.Length !=
                    requestedComponents.Length ||
                failedIndex < 0 ||
                failedIndex >=
                    physicalComponents.Length)
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchCoverageDecisionState
                            .InvalidCandidateShape,
                        "The persisted source and requested paths do not " +
                        "describe one valid Data-relative path shape."
                    );

                continue;
            }

            /*
             * Schema-v4 validation has already proved that:
             *
             * - the complete physical source is Windows-logically
             *   equivalent to the requested path;
             * - the source physically diverged from the persisted
             *   destination prefix before the first created component;
             * - ResolvedPrefixSteps.Count is the first missing requested
             *   destination component.
             *
             * Policy-v2 therefore must not require the physical source
             * prefix to reproduce the requested destination prefix, and
             * failedIndex must not be reinterpreted as a direct strict-case
             * mismatch.
             *
             * The shared recursive coverage tail is rooted in the actual
             * physical source branch below.
             */
            bool suffixLogicallyEquivalent =
                true;

            for (
                int componentIndex = failedIndex;
                componentIndex <
                    physicalComponents.Length;
                componentIndex++)
            {
                if (
                    !AreWindowsLogicallyEquivalentComponents(
                        physicalComponents[
                            componentIndex
                        ],
                        requestedComponents[
                            componentIndex
                        ]))
                {
                    suffixLogicallyEquivalent =
                        false;

                    break;
                }
            }

            if (!suffixLogicallyEquivalent)
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchCoverageDecisionState
                            .InvalidCandidateShape,
                        "The persisted physical source and requested path " +
                        "are not Windows-logically equivalent from the " +
                        "strict mismatch through the source leaf."
                    );

                continue;
            }

            sourceClaims.Add(
                new SourceClaim(
                    CandidateIndex:
                        index,
                    PhysicalPath:
                        sourcePath
                )
            );

            if (
                failedIndex ==
                    physicalComponents.Length - 1)
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchCoverageDecisionState
                            .Authorized
                    );

                continue;
            }

            string branchKey =
                BuildBranchKey(
                    dataRoot,
                    physicalComponents,
                    failedIndex
                );

            infos.Add(
                new CandidateInfo(
                    CandidateIndex:
                        index,
                    DataRoot:
                        dataRoot,
                    PhysicalComponents:
                        physicalComponents,
                    RequestedComponents:
                        requestedComponents,
                    FailedIndex:
                        failedIndex,
                    BranchKey:
                        branchKey
                )
            );
        }

        return AuthorizeNormalized(
            decisions,
            infos,
            sourceClaims
        );
    }

    /*
     * One authorization tail is shared by projection-derived and
     * persisted-manifest-derived claims. All duplicate-source, branch
     * grouping, namespace-spelling, descriptor-relative traversal, and
     * recursive full-coverage decisions therefore remain identical.
     */
    private static
        DataRelativePathRepairBatchCoverageAuthorization
        AuthorizeNormalized(
            DataRelativePathRepairBatchCoverageDecision?[]
                decisions,
            IReadOnlyList<CandidateInfo> infos,
            IReadOnlyList<SourceClaim> sourceClaims)
    {
        HashSet<int> duplicateCandidateIndexes =
            sourceClaims
                .GroupBy(
                    claim =>
                        claim.PhysicalPath,
                    StringComparer.Ordinal
                )
                .Where(group =>
                    group.Count() > 1
                )
                .SelectMany(group =>
                    group.Select(claim =>
                        claim.CandidateIndex
                    )
                )
                .ToHashSet();

        foreach (
            int duplicateIndex
            in duplicateCandidateIndexes)
        {
            decisions[duplicateIndex] =
                Decision(
                    duplicateIndex,
                    DataRelativePathRepairBatchCoverageDecisionState
                        .DuplicateSourceCoverage,
                    "The same physical source file is represented by " +
                    "more than one batch candidate."
                );
        }

        CandidateInfo[] eligibleInfos =
            infos
                .Where(info =>
                    !duplicateCandidateIndexes.Contains(
                        info.CandidateIndex
                    )
                )
                .ToArray();

        foreach (
            IGrouping<string, CandidateInfo> group
            in eligibleInfos.GroupBy(
                info =>
                    info.BranchKey,
                StringComparer.Ordinal
            ))
        {
            CandidateInfo[] branchCandidates =
                group
                    .OrderBy(info =>
                        info.CandidateIndex
                    )
                    .ToArray();

            ValidationOutcome outcome =
                ValidateBranch(
                    branchCandidates
                );

            foreach (
                CandidateInfo info
                in branchCandidates)
            {
                decisions[info.CandidateIndex] =
                    Decision(
                        info.CandidateIndex,
                        outcome.State,
                        outcome.Error
                    );
            }
        }

        for (
            int index = 0;
            index < decisions.Length;
            index++)
        {
            decisions[index] ??=
                Decision(
                    index,
                    DataRelativePathRepairBatchCoverageDecisionState
                        .InvalidCandidate,
                    "The candidate did not reach a terminal batch " +
                    "coverage authorization state."
                );
        }

        return new(
            Decisions:
                decisions
                    .Select(decision =>
                        decision!
                    )
                    .ToArray()
        );
    }


    private static ValidationOutcome ValidateBranch(
        IReadOnlyList<CandidateInfo> candidates)
    {
        if (candidates.Count == 0)
        {
            return Outcome(
                DataRelativePathRepairBatchCoverageDecisionState
                    .InvalidCandidate,
                "A physical branch requires at least one candidate."
            );
        }

        CandidateInfo first =
            candidates[0];

        if (
            candidates.Any(candidate =>
                !string.Equals(
                    candidate.DataRoot,
                    first.DataRoot,
                    StringComparison.Ordinal
                ) ||
                candidate.FailedIndex !=
                    first.FailedIndex))
        {
            return Outcome(
                DataRelativePathRepairBatchCoverageDecisionState
                    .InvalidCandidateShape,
                "Candidates grouped into one physical branch do not " +
                "share one Data root and failed-component depth."
            );
        }

        string[] requestedBranchNames =
            candidates
                .Select(candidate =>
                    candidate.RequestedComponents[
                        candidate.FailedIndex
                    ]
                )
                .Distinct(
                    StringComparer.Ordinal
                )
                .ToArray();

        if (requestedBranchNames.Length != 1)
        {
            return Outcome(
                DataRelativePathRepairBatchCoverageDecisionState
                    .ConflictingRequestedNamespace,
                "The same physical case-variant directory is mapped " +
                "to more than one requested spelling."
            );
        }

        LinuxNoFollowPathOpenResult rootOpen =
            LinuxNoFollowPath.OpenRootReadOnly(
                first.DataRoot
            );

        if (
            !rootOpen.Success ||
            rootOpen.OpenedPath is null)
        {
            return Outcome(
                DataRelativePathRepairBatchCoverageDecisionState
                    .PhysicalBranchOpenFailed,
                rootOpen.Error ??
                $"Unable to open the Data root safely ({rootOpen.State})."
            );
        }

        using LinuxNoFollowPathHandle dataRoot =
            rootOpen.OpenedPath;

        var openedDirectories =
            new List<
                LinuxNoFollowPathHandle
            >();

        try
        {
            LinuxNoFollowPathHandle current =
                dataRoot;

            for (
                int componentIndex = 0;
                componentIndex <=
                    first.FailedIndex;
                componentIndex++)
            {
                string physicalComponent =
                    first.PhysicalComponents[
                        componentIndex
                    ];

                LinuxOpenChildDirectoryReadOnlyAtResult opened =
                    LinuxOpenChildDirectoryReadOnlyAt.Open(
                        current,
                        physicalComponent
                    );

                if (
                    !opened.Success ||
                    opened.OpenedDirectory is null)
                {
                    return Outcome(
                        DataRelativePathRepairBatchCoverageDecisionState
                            .PhysicalBranchOpenFailed,
                        "The physical case-variant branch could not " +
                        "be opened descriptor-relatively at " +
                        $"'{physicalComponent}' ({opened.State}): " +
                        (
                            opened.Error ??
                            "no additional error"
                        )
                    );
                }

                current =
                    opened.OpenedDirectory;

                openedDirectories.Add(
                    current
                );
            }

            return ValidateDirectory(
                current,
                candidates,
                first.FailedIndex + 1
            );
        }
        finally
        {
            for (
                int index =
                    openedDirectories.Count - 1;
                index >= 0;
                index--)
            {
                openedDirectories[index]
                    .Dispose();
            }
        }
    }

    private static ValidationOutcome ValidateDirectory(
        LinuxNoFollowPathHandle directory,
        IReadOnlyList<CandidateInfo> candidates,
        int componentIndex)
    {
        LinuxEnumerateDirectoryAtResult enumeration =
            LinuxEnumerateDirectoryAt.Enumerate(
                directory
            );

        if (!enumeration.Success)
        {
            return Outcome(
                DataRelativePathRepairBatchCoverageDecisionState
                    .PhysicalBranchEnumerationFailed,
                "The physical case-variant branch could not be " +
                "enumerated descriptor-relatively " +
                $"({enumeration.State}): " +
                (
                    enumeration.Error ??
                    "no additional error"
                )
            );
        }

        string[] expectedPhysicalChildren =
            candidates
                .Select(candidate =>
                    candidate.PhysicalComponents[
                        componentIndex
                    ]
                )
                .Distinct(
                    StringComparer.Ordinal
                )
                .OrderBy(
                    name =>
                        name,
                    StringComparer.Ordinal
                )
                .ToArray();

        string[] actualPhysicalChildren =
            enumeration
                .ChildNames
                .OrderBy(
                    name =>
                        name,
                    StringComparer.Ordinal
                )
                .ToArray();

        if (
            !expectedPhysicalChildren.SequenceEqual(
                actualPhysicalChildren,
                StringComparer.Ordinal
            ))
        {
            return Outcome(
                DataRelativePathRepairBatchCoverageDecisionState
                    .IncompletePhysicalCoverage,
                "The candidate set does not cover every existing " +
                "entry in the physical case-variant branch. " +
                $"Expected candidate children: " +
                $"[{string.Join(", ", expectedPhysicalChildren)}]. " +
                $"Observed physical children: " +
                $"[{string.Join(", ", actualPhysicalChildren)}]."
            );
        }

        foreach (
            string physicalChild
            in actualPhysicalChildren)
        {
            CandidateInfo[] childCandidates =
                candidates
                    .Where(candidate =>
                        string.Equals(
                            candidate.PhysicalComponents[
                                componentIndex
                            ],
                            physicalChild,
                            StringComparison.Ordinal
                        )
                    )
                    .ToArray();

            string[] requestedChildNames =
                childCandidates
                    .Select(candidate =>
                        candidate.RequestedComponents[
                            componentIndex
                        ]
                    )
                    .Distinct(
                        StringComparer.Ordinal
                    )
                    .ToArray();

            if (requestedChildNames.Length != 1)
            {
                return Outcome(
                    DataRelativePathRepairBatchCoverageDecisionState
                        .ConflictingRequestedNamespace,
                    $"Physical child '{physicalChild}' is mapped " +
                    "to more than one exact requested spelling."
                );
            }

            bool anyEndsHere =
                childCandidates.Any(candidate =>
                    componentIndex ==
                        candidate.PhysicalComponents.Length - 1
                );

            bool allEndHere =
                childCandidates.All(candidate =>
                    componentIndex ==
                        candidate.PhysicalComponents.Length - 1
                );

            if (anyEndsHere)
            {
                if (
                    !allEndHere ||
                    childCandidates.Length != 1)
                {
                    return Outcome(
                        DataRelativePathRepairBatchCoverageDecisionState
                            .DuplicateSourceCoverage,
                        $"Physical leaf '{physicalChild}' is covered " +
                        "by more than one candidate shape."
                    );
                }

                /*
                 * ProjectBatchCandidate already proved this exact source
                 * path was a regular source file and captured its source
                 * snapshot. No arbitrary leaf pathname is reopened here.
                 */
                continue;
            }

            LinuxOpenChildDirectoryReadOnlyAtResult opened =
                LinuxOpenChildDirectoryReadOnlyAt.Open(
                    directory,
                    physicalChild
                );

            if (
                !opened.Success ||
                opened.OpenedDirectory is null)
            {
                return Outcome(
                    DataRelativePathRepairBatchCoverageDecisionState
                        .PhysicalBranchOpenFailed,
                    $"Expected physical directory '{physicalChild}' " +
                    "could not be opened descriptor-relatively " +
                    $"({opened.State}): " +
                    (
                        opened.Error ??
                        "no additional error"
                    )
                );
            }

            using LinuxNoFollowPathHandle childDirectory =
                opened.OpenedDirectory;

            ValidationOutcome childOutcome =
                ValidateDirectory(
                    childDirectory,
                    childCandidates,
                    componentIndex + 1
                );

            if (
                childOutcome.State !=
                DataRelativePathRepairBatchCoverageDecisionState
                    .Authorized)
            {
                return childOutcome;
            }
        }

        return Outcome(
            DataRelativePathRepairBatchCoverageDecisionState
                .Authorized
        );
    }

    private static bool
        AreWindowsLogicallyEquivalentComponents(
            string physicalComponent,
            string requestedComponent)
    {
        try
        {
            return
                CaseCompat.Core.Analysis.WindowsLogicalPath
                    .FromRelativePath(
                        physicalComponent
                    ) ==
                CaseCompat.Core.Analysis.WindowsLogicalPath
                    .FromRelativePath(
                        requestedComponent
                    );
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string BuildBranchKey(
        string dataRoot,
        IReadOnlyList<string> physicalComponents,
        int failedIndex)
    {
        return
            dataRoot +
            '\u001f' +
            string.Join(
                '\u001f',
                physicalComponents.Take(
                    failedIndex + 1
                )
            );
    }

    private static bool IsOutsideRelativePath(
        string relativePath)
    {
        string normalized =
            relativePath.Replace(
                '\\',
                '/'
            );

        return
            normalized == ".." ||
            normalized.StartsWith(
                "../",
                StringComparison.Ordinal
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

    private static
        DataRelativePathRepairBatchCoverageDecision
        Decision(
            int candidateIndex,
            DataRelativePathRepairBatchCoverageDecisionState state,
            string? error = null)
    {
        return new(
            CandidateIndex:
                candidateIndex,
            State:
                state,
            Error:
                error
        );
    }

    private static ValidationOutcome Outcome(
        DataRelativePathRepairBatchCoverageDecisionState state,
        string? error = null)
    {
        return new(
            State:
                state,
            Error:
                error
        );
    }

    private sealed record SourceClaim(
        int CandidateIndex,
        string PhysicalPath
    );

    private sealed record CandidateInfo(
        int CandidateIndex,
        string DataRoot,
        string[] PhysicalComponents,
        string[] RequestedComponents,
        int FailedIndex,
        string BranchKey
    );

    private sealed record ValidationOutcome(
        DataRelativePathRepairBatchCoverageDecisionState State,
        string? Error
    );
}
