using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using System.Text;

// Guided, interactive entry point for end users: detect the Skyrim
// install, confirm the handful of inputs the pipeline needs, scan for
// case-mismatch fixes, and apply them - all without the user having to
// know any of the individual targeted-consumer-* commands.
internal static class CaseCompatWizard
{
    public static int Run(
        TextReader input,
        TextWriter output,
        Func<SkyrimInstallDetectionResult>? detect = null)
    {
        detect ??=
            SkyrimInstallDetection.Detect;

        output.WriteLine(
            "CaseCompat - Skyrim Case-Sensitivity Repair"
        );

        output.WriteLine(
            "============================================"
        );

        output.WriteLine();

        if (!OperatingSystem.IsLinux())
        {
            output.WriteLine(
                "CaseCompat only fixes issues specific to Linux " +
                "filesystems. No changes are needed on Windows."
            );

            return 0;
        }

        output.WriteLine(
            "This will scan your Skyrim install for files whose case " +
            "doesn't match what your mods request, and offer to fix them."
        );

        output.WriteLine();

        SkyrimInstallDetectionResult detected =
            detect();

        if (!string.IsNullOrWhiteSpace(
                detected.Note))
        {
            output.WriteLine(
                detected.Note
            );

            output.WriteLine();
        }

        string? dataRoot =
            CaseCompatWizardPrompts.ResolvePath(
                input,
                output,
                label:
                    "Skyrim Data folder",
                detected:
                    detected.DataRoot,
                validate:
                    Directory.Exists,
                invalidMessage:
                    "That folder does not exist."
            );

        if (dataRoot is null)
        {
            return 130;
        }

        string? pluginsPath =
            CaseCompatWizardPrompts.ResolvePath(
                input,
                output,
                label:
                    "Plugins.txt",
                detected:
                    detected.PluginsPath,
                validate:
                    File.Exists,
                invalidMessage:
                    "That file does not exist."
            );

        if (pluginsPath is null)
        {
            return 130;
        }

        string? loadOrderPath =
            CaseCompatWizardPrompts.ResolvePath(
                input,
                output,
                label:
                    "loadorder.txt",
                detected:
                    detected.LoadOrderPath,
                validate:
                    File.Exists,
                invalidMessage:
                    "That file does not exist."
            );

        if (loadOrderPath is null)
        {
            return 130;
        }

        string? cccPath =
            CaseCompatWizardPrompts.ResolvePath(
                input,
                output,
                label:
                    "Skyrim.ccc",
                detected:
                    detected.SkyrimCccPath,
                validate:
                    File.Exists,
                invalidMessage:
                    "That file does not exist."
            );

        if (cccPath is null)
        {
            return 130;
        }

        output.WriteLine();

        output.WriteLine(
            "Scanning your load order for case-sensitivity issues..."
        );

        output.WriteLine(
            "This can take several minutes for large mod lists."
        );

        output.WriteLine();

        // Computed here (rather than only later, in ApplyAll) so a
        // second or later scan against this same install recognizes
        // aliases an earlier run already created - without it, the scan
        // would see a real directory plus its alias as an unresolved
        // conflict rather than an already-fixed path. This is only a
        // path computation; the directory need not exist yet, and
        // Discover degrades gracefully if it doesn't (the common case
        // for a genuinely first run).
        string aliasesDirectoryPathForScan =
            Path.Combine(
                CaseCompatStateDirectory.Resolve(
                    dataRoot
                ),
                "aliases"
            );

        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult
            discoveryResult;

        try
        {
            discoveryResult =
                TargetedConsumerDiscovery.Discover(
                    dataRoot:
                        dataRoot,
                    pluginsPath:
                        pluginsPath,
                    loadOrderPath:
                        loadOrderPath,
                    cccPath:
                        cccPath,
                    aliasesDirectoryPath:
                        aliasesDirectoryPathForScan
                );
        }
        catch (InvalidOperationException ex)
        {
            output.WriteLine(
                $"Error: {ex.Message}"
            );

            return 4;
        }
        catch (Exception ex)
        {
            output.WriteLine(
                $"Scan failed: {ex.Message}"
            );

            return 3;
        }

        if (!discoveryResult.CandidateEvidenceComplete)
        {
            output.WriteLine(
                "Scan did not complete."
            );

            output.WriteLine(
                $"State: {discoveryResult.State}"
            );

            if (!string.IsNullOrWhiteSpace(
                    discoveryResult.Error))
            {
                output.WriteLine(
                    $"Error: {discoveryResult.Error}"
                );
            }

            return 5;
        }

        output.WriteLine(
            $"Scan complete: {discoveryResult.CandidateCount:N0} " +
            "potential fixes found."
        );

        output.WriteLine();

        if (discoveryResult.CandidateCount == 0)
        {
            output.WriteLine(
                "Nothing to fix - your install looks fine."
            );

            return 0;
        }

        output.Write(
            $"Apply {discoveryResult.CandidateCount:N0} fixes now? " +
            "(Y/n): "
        );

        if (!CaseCompatWizardPrompts.IsYes(
                input.ReadLine()))
        {
            output.WriteLine(
                "No changes made."
            );

            return 0;
        }

        return ApplyAll(
            output,
            dataRoot,
            discoveryResult.Candidates,
            discoveryResult.Leaves
        );
    }

    private static int ApplyAll(
        TextWriter output,
        string dataRoot,
        IReadOnlyList<DataRelativePathTargetedConsumerCaseRepairCandidate>
            candidates,
        IReadOnlyList<SkyrimWinningTargetedConsumerCaseRepairLeafProjection>
            leaves)
    {
        string stateDirectory =
            CaseCompatStateDirectory.Resolve(
                dataRoot
            );

        string planDirectoryPath =
            Path.Combine(
                stateDirectory,
                "plans"
            );

        string journalDirectoryPath =
            Path.Combine(
                stateDirectory,
                "journal"
            );

        string aliasesDirectoryPath =
            Path.Combine(
                stateDirectory,
                "aliases"
            );

        Directory.CreateDirectory(
            planDirectoryPath
        );

        Directory.CreateDirectory(
            journalDirectoryPath
        );

        Directory.CreateDirectory(
            aliasesDirectoryPath
        );

        string reportPath =
            Path.Combine(
                stateDirectory,
                $"report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv"
            );

        LinuxNoFollowPathOpenResult dataRootOpen =
            LinuxNoFollowPath.OpenRootReadOnly(
                dataRoot
            );

        if (!dataRootOpen.Success)
        {
            output.WriteLine(
                "The Data folder could not be opened safely."
            );

            output.WriteLine(
                dataRootOpen.Error ??
                dataRootOpen.State.ToString()
            );

            return 6;
        }

        using LinuxNoFollowPathHandle dataRootHandle =
            dataRootOpen.OpenedPath!;

        LinuxNoFollowPathOpenResult planDirectoryOpen =
            LinuxNoFollowPath.OpenRootReadOnly(
                planDirectoryPath
            );

        if (!planDirectoryOpen.Success)
        {
            output.WriteLine(
                "The plan directory could not be opened safely."
            );

            output.WriteLine(
                planDirectoryOpen.Error ??
                planDirectoryOpen.State.ToString()
            );

            return 7;
        }

        using LinuxNoFollowPathHandle planDirectoryHandle =
            planDirectoryOpen.OpenedPath!;

        LinuxNoFollowPathOpenResult journalDirectoryOpen =
            LinuxNoFollowPath.OpenRootReadOnly(
                journalDirectoryPath
            );

        if (!journalDirectoryOpen.Success)
        {
            output.WriteLine(
                "The journal directory could not be opened safely."
            );

            output.WriteLine(
                journalDirectoryOpen.Error ??
                journalDirectoryOpen.State.ToString()
            );

            return 8;
        }

        using LinuxNoFollowPathHandle journalDirectoryHandle =
            journalDirectoryOpen.OpenedPath!;

        LinuxNoFollowPathOpenResult aliasesDirectoryOpen =
            LinuxNoFollowPath.OpenRootReadOnly(
                aliasesDirectoryPath
            );

        if (!aliasesDirectoryOpen.Success)
        {
            output.WriteLine(
                "The aliases directory could not be opened safely."
            );

            output.WriteLine(
                aliasesDirectoryOpen.Error ??
                aliasesDirectoryOpen.State.ToString()
            );

            return 9;
        }

        using LinuxNoFollowPathHandle aliasesDirectoryHandle =
            aliasesDirectoryOpen.OpenedPath!;

        var report =
            new StringBuilder();

        report.AppendLine(
            "RequestedPath,SourcePath,DestinationPath,Outcome,Detail"
        );

        int processed =
            0;

        int appliedSoFar =
            0;

        output.WriteLine(
            "Applying fixes..."
        );

        TargetedConsumerBatchApplyRunResult result =
            TargetedConsumerBatchApply.Run(
                dataRootHandle,
                planDirectoryHandle,
                journalDirectoryHandle,
                aliasesDirectoryHandle,
                candidates,
                TargetedConsumerBatchApply.ExtractWinningRequestedPaths(
                    leaves
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

                    if (
                        processed % 250 == 0 ||
                        processed == candidates.Count)
                    {
                        output.WriteLine(
                            $"  {processed:N0}/{candidates.Count:N0} " +
                            $"processed, {appliedSoFar:N0} applied"
                        );
                    }
                }
            );

        File.WriteAllText(
            reportPath,
            report.ToString()
        );

        output.WriteLine();

        output.WriteLine(
            $"Applied:     {result.AppliedCount:N0}"
        );

        if (result.AppliedViaAliasCount > 0)
        {
            output.WriteLine(
                $"  (of which via alias: " +
                $"{result.AppliedViaAliasCount:N0})"
            );

            output.WriteLine(
                "  An 'alias' fix means two or more of your mods " +
                "disagreed about the correct case for a shared folder. " +
                "Rather than renaming it - which would have broken " +
                "whichever mod needed the other casing - CaseCompat " +
                "created a second name pointing at the same folder, so " +
                "both mods can find it."
            );
        }

        output.WriteLine(
            $"Not applied: {candidates.Count - result.AppliedCount:N0}"
        );

        if (result.RejectionCounts.Count > 0)
        {
            output.WriteLine();

            output.WriteLine(
                "Not applied (reasons):"
            );

            foreach (
                (string outcome, int count)
                in result.RejectionCounts
                    .OrderByDescending(
                        pair =>
                            pair.Value
                    ))
            {
                output.WriteLine(
                    $"  {outcome,-32} {count,9:N0}"
                );
            }

            output.WriteLine();

            output.WriteLine(
                "'PlanRejected:DestinationConflict' is normal: your " +
                "filesystem already resolves those paths correctly, so " +
                "no change was needed."
            );

            if (result.RejectionCounts.ContainsKey(
                    "PlanRejected:DestinationParentAmbiguous"))
            {
                output.WriteLine();

                output.WriteLine(
                    "'PlanRejected:DestinationParentAmbiguous' means a " +
                    "shared folder already physically exists under both " +
                    "casings your mods disagree about (not just one real " +
                    "folder with an alias - two genuinely separate " +
                    "folders). CaseCompat will not guess which one is " +
                    "correct, so these were left untouched."
                );
            }
        }

        output.WriteLine();

        output.WriteLine(
            $"Full report: {reportPath}"
        );

        output.WriteLine(
            $"Journal (for troubleshooting or rollback): " +
            $"{journalDirectoryPath}"
        );

        return 0;
    }
}
