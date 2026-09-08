using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using System.Text;

public static class TargetedConsumerBatchApplyCommand
{
    public static int Run(string[] args)
    {
        if (args.Length != 9)
        {
            Console.Error.WriteLine(
                "Error: targeted-consumer-batch-apply requires a Data " +
                "root, Plugins.txt, loadorder.txt, Skyrim.ccc, plan " +
                "directory, journal directory, report file path, and " +
                "max candidates."
            );
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "Usage: casecompat targeted-consumer-batch-apply " +
                "<Data root> <Plugins.txt> <loadorder.txt> <Skyrim.ccc> " +
                "<plan directory> <journal directory> <report file path> " +
                "<max candidates>"
            );

            return 2;
        }

        if (
            !int.TryParse(
                args[8],
                out int maxCandidates) ||
            maxCandidates < 1)
        {
            Console.Error.WriteLine(
                $"Error: '{args[8]}' is not a positive integer."
            );

            return 2;
        }

        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult
            discovery;

        try
        {
            discovery =
                TargetedConsumerDiscovery.Discover(
                    dataRoot:
                        args[1],
                    pluginsPath:
                        args[2],
                    loadOrderPath:
                        args[3],
                    cccPath:
                        args[4]
                );
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(
                $"Error: {ex.Message}"
            );

            return 4;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Targeted consumer-candidate discovery error: {ex.Message}"
            );

            return 3;
        }

        if (!discovery.CandidateEvidenceComplete)
        {
            Console.Error.WriteLine(
                "Targeted consumer-candidate discovery was not complete."
            );
            Console.Error.WriteLine(
                $"State: {discovery.State}"
            );

            if (!string.IsNullOrWhiteSpace(
                    discovery.Error))
            {
                Console.Error.WriteLine(
                    $"Error: {discovery.Error}"
                );
            }

            return 5;
        }

        LinuxNoFollowPathOpenResult dataRootOpen;

        try
        {
            dataRootOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    args[1]
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Data root open error: {ex.Message}"
            );

            return 6;
        }

        if (!dataRootOpen.Success)
        {
            Console.Error.WriteLine(
                "Data root could not be opened safely."
            );
            Console.Error.WriteLine(
                dataRootOpen.Error ??
                dataRootOpen.State.ToString()
            );

            return 6;
        }

        using LinuxNoFollowPathHandle dataRoot =
            dataRootOpen.OpenedPath!;

        LinuxNoFollowPathOpenResult planDirectoryOpen;

        try
        {
            planDirectoryOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    args[5]
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Plan directory open error: {ex.Message}"
            );

            return 7;
        }

        if (!planDirectoryOpen.Success)
        {
            Console.Error.WriteLine(
                "Plan directory could not be opened safely."
            );
            Console.Error.WriteLine(
                planDirectoryOpen.Error ??
                planDirectoryOpen.State.ToString()
            );

            return 7;
        }

        using LinuxNoFollowPathHandle planDirectory =
            planDirectoryOpen.OpenedPath!;

        LinuxNoFollowPathOpenResult journalDirectoryOpen;

        try
        {
            journalDirectoryOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    args[6]
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Journal directory open error: {ex.Message}"
            );

            return 8;
        }

        if (!journalDirectoryOpen.Success)
        {
            Console.Error.WriteLine(
                "Journal directory could not be opened safely."
            );
            Console.Error.WriteLine(
                journalDirectoryOpen.Error ??
                journalDirectoryOpen.State.ToString()
            );

            return 8;
        }

        using LinuxNoFollowPathHandle journalDirectory =
            journalDirectoryOpen.OpenedPath!;

        IReadOnlyList<DataRelativePathTargetedConsumerCaseRepairCandidate>
            candidates =
                discovery.Candidates
                    .Take(
                        maxCandidates
                    )
                    .ToArray();

        var report =
            new StringBuilder();

        report.AppendLine(
            "RequestedPath,SourcePath,DestinationPath,Outcome,Detail"
        );

        int appliedCount =
            0;

        var rejectionCounts =
            new Dictionary<string, int>(
                StringComparer.Ordinal
            );

        Console.WriteLine(
            "CaseCompat Targeted Consumer-Case Repair Batch Apply"
        );

        Console.WriteLine(
            "====================================================="
        );

        Console.WriteLine();

        Console.WriteLine(
            $"Candidates available: {discovery.CandidateCount:N0}"
        );

        Console.WriteLine(
            $"Candidates selected:  {candidates.Count:N0}"
        );

        Console.WriteLine();

        int processed =
            0;

        foreach (
            DataRelativePathTargetedConsumerCaseRepairCandidate candidate
            in candidates)
        {
            processed++;

            (string outcome, string detail, string? destinationPath) =
                ApplyOne(
                    dataRoot,
                    planDirectory,
                    journalDirectory,
                    candidate
                );

            report.AppendLine(
                $"{Escape(candidate.AuthoritativeRequestedPath)}," +
                $"{Escape(candidate.SourceSnapshot.PhysicalPath)}," +
                $"{Escape(destinationPath ?? string.Empty)}," +
                $"{Escape(outcome)}," +
                $"{Escape(detail)}"
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

            if (processed % 250 == 0)
            {
                Console.WriteLine(
                    $"Progress: {processed:N0}/{candidates.Count:N0} " +
                    $"processed, {appliedCount:N0} applied"
                );
            }
        }

        File.WriteAllText(
            args[7],
            report.ToString()
        );

        Console.WriteLine();

        Console.WriteLine(
            $"Processed:  {processed:N0}"
        );

        Console.WriteLine(
            $"Applied:    {appliedCount:N0}"
        );

        Console.WriteLine(
            $"Not applied: {processed - appliedCount:N0}"
        );

        if (rejectionCounts.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine(
                "Outcomes other than AppliedDurably:"
            );

            foreach (
                (string outcome, int count)
                in rejectionCounts
                    .OrderByDescending(
                        pair =>
                            pair.Value
                    ))
            {
                Console.WriteLine(
                    $"  {outcome,-32} {count,9:N0}"
                );
            }
        }

        Console.WriteLine();

        Console.WriteLine(
            $"Full per-candidate report: {args[7]}"
        );

        return 0;
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

    private static string Escape(
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
}
