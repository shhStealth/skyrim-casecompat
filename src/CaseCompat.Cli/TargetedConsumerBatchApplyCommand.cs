using CaseCompat.Filesystem.Linux;
using System.Text;

public static class TargetedConsumerBatchApplyCommand
{
    public static int Run(string[] args)
    {
        if (args.Length != 10)
        {
            Console.Error.WriteLine(
                "Error: targeted-consumer-batch-apply requires a Data " +
                "root, Plugins.txt, loadorder.txt, Skyrim.ccc, plan " +
                "directory, journal directory, aliases directory, " +
                "report file path, and max candidates."
            );
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "Usage: casecompat targeted-consumer-batch-apply " +
                "<Data root> <Plugins.txt> <loadorder.txt> <Skyrim.ccc> " +
                "<plan directory> <journal directory> <aliases directory> " +
                "<report file path> <max candidates per pass>"
            );

            return 2;
        }

        if (
            !int.TryParse(
                args[9],
                out int maxCandidatesPerPass) ||
            maxCandidatesPerPass < 1)
        {
            Console.Error.WriteLine(
                $"Error: '{args[9]}' is not a positive integer."
            );

            return 2;
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

        LinuxNoFollowPathOpenResult aliasesDirectoryOpen;

        try
        {
            aliasesDirectoryOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    args[7]
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Aliases directory open error: {ex.Message}"
            );

            return 9;
        }

        if (!aliasesDirectoryOpen.Success)
        {
            Console.Error.WriteLine(
                "Aliases directory could not be opened safely."
            );
            Console.Error.WriteLine(
                aliasesDirectoryOpen.Error ??
                aliasesDirectoryOpen.State.ToString()
            );

            return 9;
        }

        using LinuxNoFollowPathHandle aliasesDirectory =
            aliasesDirectoryOpen.OpenedPath!;

        var report =
            new StringBuilder();

        report.AppendLine(
            "RequestedPath,SourcePath,DestinationPath,Outcome,Detail"
        );

        Console.WriteLine(
            "CaseCompat Targeted Consumer-Case Repair Batch Apply"
        );

        Console.WriteLine(
            "====================================================="
        );

        Console.WriteLine();

        int currentPassCandidateCount =
            0;

        int currentPassProcessed =
            0;

        int currentPassApplied =
            0;

        TargetedConsumerBatchApplyConvergenceResult convergence;

        try
        {
            convergence =
                TargetedConsumerBatchApply.RunUntilConverged(
                    dataRoot:
                        args[1],
                    pluginsPath:
                        args[2],
                    loadOrderPath:
                        args[3],
                    cccPath:
                        args[4],
                    aliasesDirectoryPath:
                        args[7],
                    dataRootHandle:
                        dataRoot,
                    planDirectory:
                        planDirectory,
                    journalDirectory:
                        journalDirectory,
                    aliasesDirectory:
                        aliasesDirectory,
                    maxCandidatesPerPass:
                        maxCandidatesPerPass,
                    onPassStarted:
                        (passNumber, candidateCount) =>
                        {
                            currentPassCandidateCount =
                                candidateCount;

                            currentPassProcessed =
                                0;

                            currentPassApplied =
                                0;

                            Console.WriteLine(
                                $"Pass {passNumber}: " +
                                $"{candidateCount:N0} candidates " +
                                "available, processing up to " +
                                $"{maxCandidatesPerPass:N0}."
                            );
                        },
                    onItemCompleted:
                        (_, item) =>
                        {
                            currentPassProcessed++;

                            if (item.Outcome is
                                "AppliedDurably" or
                                "AppliedDurablyViaAlias")
                            {
                                currentPassApplied++;
                            }

                            report.AppendLine(
                                $"{TargetedConsumerBatchApply.EscapeCsvField(
                                    item.Candidate
                                        .AuthoritativeRequestedPath)}," +
                                $"{TargetedConsumerBatchApply.EscapeCsvField(
                                    item.Candidate.SourceSnapshot
                                        .PhysicalPath)}," +
                                $"{TargetedConsumerBatchApply.EscapeCsvField(
                                    item.DestinationPath ??
                                    string.Empty)}," +
                                $"{TargetedConsumerBatchApply.EscapeCsvField(
                                    item.Outcome)}," +
                                $"{TargetedConsumerBatchApply.EscapeCsvField(
                                    item.Detail)}"
                            );

                            if (currentPassProcessed % 250 == 0)
                            {
                                Console.WriteLine(
                                    $"  Progress: " +
                                    $"{currentPassProcessed:N0}/" +
                                    $"{currentPassCandidateCount:N0} " +
                                    $"processed, {currentPassApplied:N0} " +
                                    "applied"
                                );
                            }
                        }
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

        File.WriteAllText(
            args[8],
            report.ToString()
        );

        if (convergence.DiscoveryFailed)
        {
            Console.Error.WriteLine(
                "A rescan partway through did not complete."
            );
            Console.Error.WriteLine(
                $"State: {convergence.DiscoveryFailureState}"
            );

            if (!string.IsNullOrWhiteSpace(
                    convergence.DiscoveryFailureError))
            {
                Console.Error.WriteLine(
                    $"Error: {convergence.DiscoveryFailureError}"
                );
            }

            Console.WriteLine();

            Console.WriteLine(
                $"Applied so far: {convergence.TotalAppliedCount:N0}"
            );

            Console.WriteLine(
                $"Full per-candidate report: {args[8]}"
            );

            return 5;
        }

        int totalProcessed =
            convergence.Passes
                .Sum(
                    pass =>
                        pass.CandidateCount
                );

        int notApplied =
            convergence.Passes.Count > 0
                ? convergence.Passes[^1].CandidateCount -
                    convergence.Passes[^1].RunResult.AppliedCount
                : 0;

        Console.WriteLine();

        if (convergence.Passes.Count > 1)
        {
            Console.WriteLine(
                $"Completed in {convergence.Passes.Count:N0} passes."
            );
        }

        Console.WriteLine(
            $"Processed:  {totalProcessed:N0}"
        );

        Console.WriteLine(
            $"Applied:    {convergence.TotalAppliedCount:N0}"
        );

        if (convergence.TotalAppliedViaAliasCount > 0)
        {
            Console.WriteLine(
                $"  (of which via alias: " +
                $"{convergence.TotalAppliedViaAliasCount:N0})"
            );
        }

        Console.WriteLine(
            $"Not applied: {notApplied:N0}"
        );

        if (convergence.FinalRejectionCounts.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine(
                "Outcomes other than AppliedDurably (last pass):"
            );

            foreach (
                (string outcome, int count)
                in convergence.FinalRejectionCounts
                    .OrderByDescending(
                        pair =>
                            pair.Value
                    ))
            {
                Console.WriteLine(
                    $"  {outcome,-32} {count,9:N0}"
                );
            }

            if (convergence.StoppedDueToNoProgress)
            {
                Console.WriteLine();

                Console.WriteLine(
                    "Rescanning stopped because the remaining items did " +
                    "not change across two rescans - this looks like a " +
                    "permanent conflict, not a temporary ordering effect."
                );
            }
            else if (convergence.StoppedDueToPassCap)
            {
                Console.WriteLine();

                Console.WriteLine(
                    "Rescanning stopped after " +
                    $"{convergence.Passes.Count:N0} automatic passes. " +
                    "Progress was still being made, so running this " +
                    "command again may resolve more."
                );
            }
        }

        Console.WriteLine();

        Console.WriteLine(
            $"Full per-candidate report: {args[8]}"
        );

        return 0;
    }
}
