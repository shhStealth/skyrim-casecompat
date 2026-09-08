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
        IReadOnlyList<DataRelativePathTargetedConsumerCaseRepairCandidate>
            candidates,
        Action<TargetedConsumerBatchApplyItemResult>? onItemCompleted = null)
    {
        int appliedCount =
            0;

        var rejectionCounts =
            new Dictionary<string, int>(
                StringComparer.Ordinal
            );

        var items =
            new List<TargetedConsumerBatchApplyItemResult>(
                candidates.Count
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
                    candidate
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

            if (outcome == "AppliedDurably")
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
            rejectionCounts
        );
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
            DataRelativePathTargetedConsumerCaseRepairCandidate candidate)
    {
        DataRelativePathTargetedConsumerCaseRepairPlanProjection projection;

        try
        {
            projection =
                DataRelativePathTargetedConsumerCaseRepairPlanProjector
                    .Project(
                        dataRoot,
                        candidate
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
                    projection
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
                        DateTimeOffset.UtcNow
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

        return (
            "AppliedDurably",
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
    IReadOnlyDictionary<string, int> RejectionCounts);
