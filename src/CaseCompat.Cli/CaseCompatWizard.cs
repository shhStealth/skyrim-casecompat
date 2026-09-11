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

        // Computed here (rather than only later, in ApplyAll) so this
        // first scan already recognizes aliases an earlier RUN of the
        // wizard created - without it, the scan would see a real
        // directory plus its alias as an unresolved conflict rather
        // than an already-fixed path. This is only a path computation;
        // the directory need not exist yet, and Discover degrades
        // gracefully if it doesn't (the common case for a genuinely
        // first run).
        string aliasesDirectoryPathForScan =
            Path.Combine(
                CaseCompatStateDirectory.Resolve(
                    dataRoot
                ),
                "aliases"
            );

        TargetedConsumerAssetDiscoveryResult discoveryResult;

        try
        {
            discoveryResult =
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
            pluginsPath,
            loadOrderPath,
            cccPath
        );
    }

    private static int ApplyAll(
        TextWriter output,
        string dataRoot,
        string pluginsPath,
        string loadOrderPath,
        string cccPath)
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
                        dataRoot,
                    pluginsPath:
                        pluginsPath,
                    loadOrderPath:
                        loadOrderPath,
                    cccPath:
                        cccPath,
                    aliasesDirectoryPath:
                        aliasesDirectoryPath,
                    dataRootHandle:
                        dataRootHandle,
                    planDirectory:
                        planDirectoryHandle,
                    journalDirectory:
                        journalDirectoryHandle,
                    aliasesDirectory:
                        aliasesDirectoryHandle,
                    onPassStarted:
                        (passNumber, candidateCount) =>
                        {
                            currentPassCandidateCount =
                                candidateCount;

                            currentPassProcessed =
                                0;

                            currentPassApplied =
                                0;

                            if (passNumber > 1)
                            {
                                output.WriteLine();

                                output.WriteLine(
                                    $"Rescanning for remaining work " +
                                    $"(pass {passNumber})..."
                                );

                                output.WriteLine(
                                    $"Found {candidateCount:N0} " +
                                    "remaining potential fixes."
                                );
                            }

                            output.WriteLine(
                                "Applying fixes..."
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

                            if (
                                currentPassProcessed % 250 == 0 ||
                                currentPassProcessed ==
                                    currentPassCandidateCount)
                            {
                                output.WriteLine(
                                    $"  {currentPassProcessed:N0}/" +
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

        File.WriteAllText(
            reportPath,
            report.ToString()
        );

        if (convergence.DiscoveryFailed)
        {
            output.WriteLine();

            output.WriteLine(
                "A rescan partway through did not complete."
            );

            output.WriteLine(
                $"State: {convergence.DiscoveryFailureState}"
            );

            if (!string.IsNullOrWhiteSpace(
                    convergence.DiscoveryFailureError))
            {
                output.WriteLine(
                    $"Error: {convergence.DiscoveryFailureError}"
                );
            }

            output.WriteLine();

            output.WriteLine(
                $"Applied so far: {convergence.TotalAppliedCount:N0}"
            );

            output.WriteLine(
                $"Full report: {reportPath}"
            );

            return 5;
        }

        int notApplied =
            convergence.Passes.Count > 0
                ? convergence.Passes[^1].CandidateCount -
                    convergence.Passes[^1].RunResult.AppliedCount
                : 0;

        output.WriteLine();

        if (convergence.Passes.Count > 1)
        {
            output.WriteLine(
                $"Completed in {convergence.Passes.Count:N0} passes."
            );
        }

        output.WriteLine(
            $"Applied:     {convergence.TotalAppliedCount:N0}"
        );

        if (convergence.TotalAppliedViaAliasCount > 0)
        {
            output.WriteLine(
                $"  (of which via alias: " +
                $"{convergence.TotalAppliedViaAliasCount:N0})"
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
            $"Not applied: {notApplied:N0}"
        );

        if (convergence.FinalRejectionCounts.Count > 0)
        {
            output.WriteLine();

            output.WriteLine(
                "Not applied (reasons):"
            );

            foreach (
                (string outcome, int count)
                in convergence.FinalRejectionCounts
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

            if (convergence.FinalRejectionCounts.ContainsKey(
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

            if (convergence.StoppedDueToNoProgress)
            {
                output.WriteLine();

                output.WriteLine(
                    "Rescanning stopped because the remaining items did " +
                    "not change across two rescans - this looks like a " +
                    "permanent conflict CaseCompat cannot resolve " +
                    "automatically, not a temporary ordering effect."
                );
            }
            else if (convergence.StoppedDueToPassCap)
            {
                output.WriteLine();

                output.WriteLine(
                    "Rescanning stopped after " +
                    $"{convergence.Passes.Count:N0} automatic passes. " +
                    "Progress was still being made, so running the " +
                    "wizard again may resolve more."
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
