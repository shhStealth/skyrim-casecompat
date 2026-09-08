using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;
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
            ResolvePath(
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
            ResolvePath(
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
            ResolvePath(
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
            ResolvePath(
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
                        cccPath
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

        if (!IsYes(
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
            discoveryResult.Candidates
        );
    }

    private static int ApplyAll(
        TextWriter output,
        string dataRoot,
        IReadOnlyList<DataRelativePathTargetedConsumerCaseRepairCandidate>
            candidates)
    {
        string stateDirectory =
            GetStateDirectory(
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

        Directory.CreateDirectory(
            planDirectoryPath
        );

        Directory.CreateDirectory(
            journalDirectoryPath
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
                candidates,
                item =>
                {
                    processed++;

                    if (item.Outcome == "AppliedDurably")
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

    private static string? ResolvePath(
        TextReader input,
        TextWriter output,
        string label,
        string? detected,
        Func<string, bool> validate,
        string invalidMessage)
    {
        if (
            detected is not null &&
            validate(
                detected))
        {
            output.Write(
                $"{label} [auto-detected]: {detected}\nUse this? (Y/n): "
            );

            if (IsYes(
                    input.ReadLine()))
            {
                return detected;
            }
        }

        while (true)
        {
            output.Write(
                $"Enter path for {label} (leave empty to cancel): "
            );

            string? entered =
                input.ReadLine();

            if (string.IsNullOrWhiteSpace(
                    entered))
            {
                output.WriteLine(
                    "Cancelled."
                );

                return null;
            }

            string trimmed =
                entered.Trim();

            if (!validate(
                    trimmed))
            {
                output.WriteLine(
                    invalidMessage
                );

                continue;
            }

            return trimmed;
        }
    }

    private static bool IsYes(
        string? response)
    {
        if (string.IsNullOrWhiteSpace(
                response))
        {
            return true;
        }

        char first =
            response.Trim()[0];

        return
            first != 'n' &&
            first != 'N';
    }

    private static string GetStateDirectory(
        string dataRoot)
    {
        string? xdgDataHome =
            Environment.GetEnvironmentVariable(
                "XDG_DATA_HOME"
            );

        string baseDirectory =
            string.IsNullOrWhiteSpace(
                xdgDataHome)
                ? Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.UserProfile
                    ),
                    ".local",
                    "share"
                )
                : xdgDataHome;

        string hash =
            Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        dataRoot
                    )
                )
            )[..16].ToLowerInvariant();

        return Path.Combine(
            baseDirectory,
            "CaseCompat",
            "installs",
            hash
        );
    }
}
