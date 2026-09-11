using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

// Shared batch-apply core used by both the scripted
// targeted-consumer-batch-apply command and the guided wizard, so the
// per-candidate plan/apply pipeline is defined exactly once.
internal static class TargetedConsumerBatchApply
{
    public static TargetedConsumerBatchApplyRunResult Run(
        LinuxNoFollowPathHandle dataRoot,
        LinuxNoFollowPathHandle planDirectory,
        LinuxNoFollowPathHandle journalDirectory,
        LinuxNoFollowPathHandle aliasesDirectory,
        IReadOnlyList<DataRelativePathTargetedConsumerCaseRepairCandidate>
            candidates,
        IReadOnlyList<string?> allWinningRequestedPaths,
        Action<TargetedConsumerBatchApplyItemResult>? onItemCompleted = null)
    {
        int appliedCount =
            0;

        int appliedViaAliasCount =
            0;

        var rejectionCounts =
            new Dictionary<string, int>(
                StringComparer.Ordinal
            );

        var items =
            new List<TargetedConsumerBatchApplyItemResult>(
                candidates.Count
            );

        // Deliberately every winning consumer's requested path, not
        // just this run's mismatched candidates - see
        // DataRelativePathContestedAncestorAnalyzer for why using
        // candidates alone would miss an ancestor an earlier run's
        // already-successful fix still depends on.
        IReadOnlySet<string> contestedAncestorPrefixes =
            DataRelativePathContestedAncestorAnalyzer.Analyze(
                allWinningRequestedPaths
            );

        foreach (
            DataRelativePathTargetedConsumerCaseRepairCandidate candidate
            in candidates)
        {
            (string outcome, string detail, string? destinationPath) =
                ApplyOne(
                    dataRoot,
                    planDirectory,
                    journalDirectory,
                    aliasesDirectory,
                    candidate,
                    contestedAncestorPrefixes
                );

            var item =
                new TargetedConsumerBatchApplyItemResult(
                    candidate,
                    outcome,
                    detail,
                    destinationPath
                );

            items.Add(
                item
            );

            if (outcome == "AppliedDurablyViaAlias")
            {
                appliedCount++;

                appliedViaAliasCount++;
            }
            else if (outcome == "AppliedDurably")
            {
                appliedCount++;
            }
            else
            {
                rejectionCounts[outcome] =
                    rejectionCounts.GetValueOrDefault(
                        outcome
                    ) +
                    1;
            }

            onItemCompleted?.Invoke(
                item
            );
        }

        return new TargetedConsumerBatchApplyRunResult(
            items,
            appliedCount,
            appliedViaAliasCount,
            rejectionCounts
        );
    }

    // A single batch-apply pass can leave some candidates unresolved
    // even though real progress happened: an early candidate's own fix
    // (an alias creation, or a directory rename) can invalidate a later
    // candidate's pre-recorded snapshot from the same pass's initial
    // discovery, or can make a shared ancestor's alias exist by the
    // time a later candidate reaches it. A fresh discovery pass sees
    // current state and picks this up correctly - this repeatedly
    // rescans and reapplies until either nothing is left to do, or two
    // consecutive passes make no further progress at all (a genuine,
    // permanent conflict - e.g. two real directories under both
    // casings - that no amount of rescanning will ever resolve).
    //
    // Discovery is redone from scratch every pass (it is driven by
    // plain path strings, not a retained descriptor, so there is
    // nothing to reuse between passes); the apply-side handles
    // (dataRoot/planDirectory/journalDirectory/aliasesDirectory) are
    // opened once by the caller and reused for every pass, since a
    // directory descriptor always reflects the live, current directory
    // contents - nothing about reusing it across passes risks stale
    // data.
    public const int DefaultMaxConvergencePasses =
        10;

    public static TargetedConsumerBatchApplyConvergenceResult
        RunUntilConverged(
            string dataRoot,
            string pluginsPath,
            string loadOrderPath,
            string cccPath,
            string aliasesDirectoryPath,
            LinuxNoFollowPathHandle dataRootHandle,
            LinuxNoFollowPathHandle planDirectory,
            LinuxNoFollowPathHandle journalDirectory,
            LinuxNoFollowPathHandle aliasesDirectory,
            int maxCandidatesPerPass = int.MaxValue,
            int maxPasses = DefaultMaxConvergencePasses,
            Action<int, int>? onPassStarted = null,
            Action<int, TargetedConsumerBatchApplyItemResult>?
                onItemCompleted = null,
            Action<int, TargetedConsumerBatchApplyRunResult>?
                onPassCompleted = null)
    {
        var passes =
            new List<TargetedConsumerBatchApplyPassResult>();

        int totalAppliedCount =
            0;

        int totalAppliedViaAliasCount =
            0;

        TargetedConsumerBatchApplyPassResult? previousPass =
            null;

        for (
            int passNumber = 1;
            passNumber <= maxPasses;
            passNumber++)
        {
            TargetedConsumerAssetDiscoveryResult discovery =
                TargetedConsumerDiscovery.DiscoverWithAssets(
                    dataRoot:
                        dataRoot,
                    pluginsPath:
                        pluginsPath,
                    loadOrderPath:
                        loadOrderPath,
                    cccPath:
                        cccPath,
                    aliasesDirectoryPath:
                        aliasesDirectoryPath
                );

            if (!discovery.CandidateEvidenceComplete)
            {
                return new(
                    Passes:
                        passes,
                    TotalAppliedCount:
                        totalAppliedCount,
                    TotalAppliedViaAliasCount:
                        totalAppliedViaAliasCount,
                    Converged:
                        false,
                    StoppedDueToNoProgress:
                        false,
                    StoppedDueToPassCap:
                        false,
                    DiscoveryFailed:
                        true,
                    DiscoveryFailureState:
                        discovery.State.ToString(),
                    DiscoveryFailureError:
                        discovery.Error
                );
            }

            if (discovery.CandidateCount == 0)
            {
                return new(
                    Passes:
                        passes,
                    TotalAppliedCount:
                        totalAppliedCount,
                    TotalAppliedViaAliasCount:
                        totalAppliedViaAliasCount,
                    Converged:
                        true,
                    StoppedDueToNoProgress:
                        false,
                    StoppedDueToPassCap:
                        false,
                    DiscoveryFailed:
                        false,
                    DiscoveryFailureState:
                        null,
                    DiscoveryFailureError:
                        null
                );
            }

            onPassStarted?.Invoke(
                passNumber,
                discovery.CandidateCount
            );

            IReadOnlyList<
                DataRelativePathTargetedConsumerCaseRepairCandidate
            > candidates =
                discovery.Candidates
                    .Take(
                        maxCandidatesPerPass
                    )
                    .ToArray();

            TargetedConsumerBatchApplyRunResult result =
                Run(
                    dataRootHandle,
                    planDirectory,
                    journalDirectory,
                    aliasesDirectory,
                    candidates,
                    ExtractWinningRequestedPaths(
                        discovery.Leaves
                    ),
                    item =>
                        onItemCompleted?.Invoke(
                            passNumber,
                            item
                        )
                );

            totalAppliedCount +=
                result.AppliedCount;

            totalAppliedViaAliasCount +=
                result.AppliedViaAliasCount;

            var passResult =
                new TargetedConsumerBatchApplyPassResult(
                    passNumber,
                    candidates.Count,
                    result
                );

            passes.Add(
                passResult
            );

            onPassCompleted?.Invoke(
                passNumber,
                result
            );

            bool fullyCoveredThisPass =
                candidates.Count ==
                discovery.CandidateCount;

            bool everythingAppliedThisPass =
                result.AppliedCount ==
                candidates.Count;

            if (
                fullyCoveredThisPass &&
                everythingAppliedThisPass)
            {
                return new(
                    Passes:
                        passes,
                    TotalAppliedCount:
                        totalAppliedCount,
                    TotalAppliedViaAliasCount:
                        totalAppliedViaAliasCount,
                    Converged:
                        true,
                    StoppedDueToNoProgress:
                        false,
                    StoppedDueToPassCap:
                        false,
                    DiscoveryFailed:
                        false,
                    DiscoveryFailureState:
                        null,
                    DiscoveryFailureError:
                        null
                );
            }

            if (
                previousPass is not null &&
                NoProgressSincePreviousPass(
                    previousPass,
                    passResult
                ))
            {
                return new(
                    Passes:
                        passes,
                    TotalAppliedCount:
                        totalAppliedCount,
                    TotalAppliedViaAliasCount:
                        totalAppliedViaAliasCount,
                    Converged:
                        false,
                    StoppedDueToNoProgress:
                        true,
                    StoppedDueToPassCap:
                        false,
                    DiscoveryFailed:
                        false,
                    DiscoveryFailureState:
                        null,
                    DiscoveryFailureError:
                        null
                );
            }

            previousPass =
                passResult;
        }

        return new(
            Passes:
                passes,
            TotalAppliedCount:
                totalAppliedCount,
            TotalAppliedViaAliasCount:
                totalAppliedViaAliasCount,
            Converged:
                false,
            StoppedDueToNoProgress:
                false,
            StoppedDueToPassCap:
                true,
            DiscoveryFailed:
                false,
            DiscoveryFailureState:
                null,
            DiscoveryFailureError:
                null
        );
    }

    // Two passes made no progress at all - same candidates presented,
    // same number applied, same rejection reasons in the same
    // quantities - so continuing would only repeat this forever. This
    // is the signature of a genuine, permanent conflict (most commonly
    // DestinationParentAmbiguous: two real directories already exist
    // under both casings) rather than a transient, same-run cascade
    // effect, which a fresh pass already would have cleared.
    // Internal (not private) specifically so tests can exercise this
    // stopping-condition logic directly with hand-built pass results,
    // without needing a full plugin/load-order fixture to drive
    // RunUntilConverged's own real discovery end to end.
    internal static bool NoProgressSincePreviousPass(
        TargetedConsumerBatchApplyPassResult previous,
        TargetedConsumerBatchApplyPassResult current)
    {
        if (
            previous.CandidateCount !=
                current.CandidateCount ||
            previous.RunResult.AppliedCount !=
                current.RunResult.AppliedCount ||
            previous.RunResult.RejectionCounts.Count !=
                current.RunResult.RejectionCounts.Count)
        {
            return false;
        }

        foreach (
            (string outcome, int count)
            in previous.RunResult.RejectionCounts)
        {
            if (
                !current.RunResult.RejectionCounts.TryGetValue(
                    outcome,
                    out int currentCount) ||
                currentCount != count)
            {
                return false;
            }
        }

        return true;
    }

    // Every winning consumer's own authoritative requested path across
    // the whole scan - not just the subset that turned into a
    // mismatched candidate. Feed this, not just this run's candidates,
    // to contested-ancestor analysis: a consumer whose file already
    // sits exactly where it requires still pins that ancestor's
    // current casing as load-bearing, even though it is not itself a
    // candidate.
    public static IReadOnlyList<string?> ExtractWinningRequestedPaths(
        IReadOnlyList<SkyrimWinningTargetedConsumerCaseRepairLeafProjection>
            leaves)
    {
        return leaves
            .Where(
                leaf =>
                    leaf.ConsumerSpelling.State ==
                    DataRelativePathAggregateConsumerSpellingState
                        .UniqueConsumerSpelling
            )
            .Select(
                leaf =>
                    leaf.ConsumerSpelling.AuthoritativeRequestedPath
            )
            .ToArray();
    }

    public static string EscapeCsvField(
        string value)
    {
        if (
            value.Contains(',') ||
            value.Contains('"') ||
            value.Contains('\n'))
        {
            return
                "\"" +
                value.Replace(
                    "\"",
                    "\"\""
                ) +
                "\"";
        }

        return value;
    }

    private static
        (string Outcome, string Detail, string? DestinationPath)
        ApplyOne(
            LinuxNoFollowPathHandle dataRoot,
            LinuxNoFollowPathHandle planDirectory,
            LinuxNoFollowPathHandle journalDirectory,
            LinuxNoFollowPathHandle aliasesDirectory,
            DataRelativePathTargetedConsumerCaseRepairCandidate candidate,
            IReadOnlySet<string> contestedAncestorPrefixes)
    {
        DataRelativePathTargetedConsumerCaseRepairPlanProjection projection;

        try
        {
            projection =
                DataRelativePathTargetedConsumerCaseRepairPlanProjector
                    .Project(
                        dataRoot,
                        candidate,
                        contestedAncestorPrefixes,
                        aliasesDirectory
                    );
        }
        catch (Exception ex)
        {
            return (
                "PlanProjectionError",
                ex.Message,
                null
            );
        }

        if (!projection.HasPlan)
        {
            return (
                $"PlanRejected:{projection.State}",
                projection.Error ??
                projection.State.ToString(),
                null
            );
        }

        Guid planId =
            Guid.NewGuid();

        DataRelativePathTargetedConsumerCaseRepairDurablePlanCreation
            creation;

        try
        {
            creation =
                DataRelativePathTargetedConsumerCaseRepairDurablePlan.Create(
                    dataRoot,
                    planId,
                    DateTimeOffset.UtcNow,
                    projection,
                    contestedAncestorPrefixes,
                    aliasesDirectory
                );
        }
        catch (Exception ex)
        {
            return (
                "DurablePlanCreationError",
                ex.Message,
                null
            );
        }

        if (!creation.Success)
        {
            return (
                $"DurablePlanRejected:{creation.State}",
                creation.Error ??
                creation.State.ToString(),
                null
            );
        }

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            creation.Record!;

        string destinationPath =
            plan.Operations[^1].DestinationPath;

        string planChildName =
            $"{planId:N}.plan.json";

        DataRelativePathTargetedConsumerCaseRepairDurablePlanWriterResult
            write =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanWriter
                    .CreateInitial(
                        planDirectory,
                        planChildName,
                        plan
                    );

        if (!write.Success)
        {
            return (
                $"PlanWriteFailed:{write.State}",
                write.Error ??
                write.State.ToString(),
                destinationPath
            );
        }

        DataRelativePathTargetedConsumerCaseRepairDurablePlanReaderResult
            verify =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanReader
                    .Read(
                        planDirectory,
                        planChildName
                    );

        if (
            !verify.Success ||
            verify.Plan is null ||
            verify.Plan.PlanId != plan.PlanId)
        {
            return (
                $"PlanVerifyFailed:{verify.State}",
                verify.Error ??
                verify.State.ToString(),
                destinationPath
            );
        }

        DataRelativePathTargetedConsumerCaseRepairApplyExecution execution;

        try
        {
            execution =
                DataRelativePathTargetedConsumerCaseRepairApplyExecutor
                    .Execute(
                        dataRoot,
                        journalDirectory,
                        verify.Plan,
                        DateTimeOffset.UtcNow,
                        aliasesDirectory
                    );
        }
        catch (Exception ex)
        {
            return (
                "ApplyExecutionError",
                ex.Message,
                destinationPath
            );
        }

        if (!execution.Success)
        {
            return (
                execution.State.ToString(),
                execution.Error ??
                execution.State.ToString(),
                destinationPath
            );
        }

        bool appliedViaAlias =
            plan.Operations
                .Any(
                    op =>
                        op.Kind ==
                        DataRelativePathRepairPlanOperationKind
                            .CreateAliasSymlink
                );

        return (
            appliedViaAlias
                ? "AppliedDurablyViaAlias"
                : "AppliedDurably",
            string.Empty,
            destinationPath
        );
    }
}

internal sealed record TargetedConsumerBatchApplyItemResult(
    DataRelativePathTargetedConsumerCaseRepairCandidate Candidate,
    string Outcome,
    string Detail,
    string? DestinationPath);

internal sealed record TargetedConsumerBatchApplyRunResult(
    IReadOnlyList<TargetedConsumerBatchApplyItemResult> Items,
    int AppliedCount,
    int AppliedViaAliasCount,
    IReadOnlyDictionary<string, int> RejectionCounts);

internal sealed record TargetedConsumerBatchApplyPassResult(
    int PassNumber,
    int CandidateCount,
    TargetedConsumerBatchApplyRunResult RunResult);

internal sealed record TargetedConsumerBatchApplyConvergenceResult(
    IReadOnlyList<TargetedConsumerBatchApplyPassResult> Passes,
    int TotalAppliedCount,
    int TotalAppliedViaAliasCount,
    bool Converged,
    bool StoppedDueToNoProgress,
    bool StoppedDueToPassCap,
    bool DiscoveryFailed,
    string? DiscoveryFailureState,
    string? DiscoveryFailureError)
{
    // The last pass's own rejection counts, if any passes ran at all -
    // what a caller should show as "still not applied" once the loop
    // has stopped (converged, stuck, or capped).
    public IReadOnlyDictionary<string, int> FinalRejectionCounts =>
        Passes.Count > 0
            ? Passes[^1].RunResult.RejectionCounts
            : new Dictionary<string, int>(
                StringComparer.Ordinal
            );
}
