using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Repair;
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
                "<report file path> <max candidates>"
            );

            return 2;
        }

        if (
            !int.TryParse(
                args[9],
                out int maxCandidates) ||
            maxCandidates < 1)
        {
            Console.Error.WriteLine(
                $"Error: '{args[9]}' is not a positive integer."
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
                        args[4],
                    aliasesDirectoryPath:
                        args[7]
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

        int appliedSoFar =
            0;

        TargetedConsumerBatchApplyRunResult result =
            TargetedConsumerBatchApply.Run(
                dataRoot,
                planDirectory,
                journalDirectory,
                aliasesDirectory,
                candidates,
                TargetedConsumerBatchApply.ExtractWinningRequestedPaths(
                    discovery.Leaves
                ),
                item =>
                {
                    processed++;

                    if (item.Outcome is
                        "AppliedDurably" or
                        "AppliedDurablyViaAlias")
                    {
                        appliedSoFar++;
                    }

                    report.AppendLine(
                        $"{TargetedConsumerBatchApply.EscapeCsvField(
                            item.Candidate.AuthoritativeRequestedPath)}," +
                        $"{TargetedConsumerBatchApply.EscapeCsvField(
                            item.Candidate.SourceSnapshot.PhysicalPath)}," +
                        $"{TargetedConsumerBatchApply.EscapeCsvField(
                            item.DestinationPath ?? string.Empty)}," +
                        $"{TargetedConsumerBatchApply.EscapeCsvField(
                            item.Outcome)}," +
                        $"{TargetedConsumerBatchApply.EscapeCsvField(
                            item.Detail)}"
                    );

                    if (processed % 250 == 0)
                    {
                        Console.WriteLine(
                            $"Progress: {processed:N0}/" +
                            $"{candidates.Count:N0} processed, " +
                            $"{appliedSoFar:N0} applied"
                        );
                    }
                }
            );

        File.WriteAllText(
            args[8],
            report.ToString()
        );

        Console.WriteLine();

        Console.WriteLine(
            $"Processed:  {processed:N0}"
        );

        Console.WriteLine(
            $"Applied:    {result.AppliedCount:N0}"
        );

        if (result.AppliedViaAliasCount > 0)
        {
            Console.WriteLine(
                $"  (of which via alias: " +
                $"{result.AppliedViaAliasCount:N0})"
            );
        }

        Console.WriteLine(
            $"Not applied: {processed - result.AppliedCount:N0}"
        );

        if (result.RejectionCounts.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine(
                "Outcomes other than AppliedDurably:"
            );

            foreach (
                (string outcome, int count)
                in result.RejectionCounts
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
            $"Full per-candidate report: {args[8]}"
        );

        return 0;
    }
}
