using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

// Guided, interactive rollback: detect the same Skyrim install the
// apply wizard would detect, find its journal (same deterministic
// per-Data-root state directory as CaseCompatWizard), and walk every
// still-applied plan back to its original name - without the user
// having to know the journal's on-disk path or any plan ID.
internal static class CaseCompatRollbackWizard
{
    public static int Run(
        TextReader input,
        TextWriter output,
        Func<SkyrimInstallDetectionResult>? detect = null)
    {
        detect ??=
            SkyrimInstallDetection.Detect;

        output.WriteLine(
            "CaseCompat - Roll Back a Previous Repair"
        );

        output.WriteLine(
            "========================================="
        );

        output.WriteLine();

        if (!OperatingSystem.IsLinux())
        {
            output.WriteLine(
                "CaseCompat only fixes issues specific to Linux " +
                "filesystems. There is nothing to roll back on Windows."
            );

            return 0;
        }

        output.WriteLine(
            "This finds the CaseCompat journal for your Skyrim install " +
            "and renames every applied fix back to its original name."
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

        output.WriteLine();

        string journalDirectoryPath =
            Path.Combine(
                CaseCompatStateDirectory.Resolve(
                    dataRoot
                ),
                "journal"
            );

        if (!Directory.Exists(
                journalDirectoryPath))
        {
            output.WriteLine(
                "No CaseCompat journal found for this install. Nothing " +
                "has been applied here, so there is nothing to roll back."
            );

            return 0;
        }

        string[] appliedJournalFiles =
            Directory.GetFiles(
                journalDirectoryPath,
                "*.apply-applied.json"
            );

        if (appliedJournalFiles.Length == 0)
        {
            output.WriteLine(
                "No applied fixes are recorded in this install's " +
                "journal. Nothing to roll back."
            );

            return 0;
        }

        var planIds =
            new List<Guid>();

        foreach (string journalFile in appliedJournalFiles)
        {
            string fileName =
                Path.GetFileName(
                    journalFile
                );

            string planIdText =
                fileName[
                    ..fileName.IndexOf(
                        '.'
                    )
                ];

            if (Guid.TryParse(
                    planIdText,
                    out Guid planId))
            {
                planIds.Add(
                    planId
                );
            }
        }

        output.WriteLine(
            $"Found {planIds.Count:N0} applied fix(es) in this " +
            "install's journal."
        );

        output.WriteLine();

        output.Write(
            $"Roll back {planIds.Count:N0} fix(es) now? (Y/n): "
        );

        if (!CaseCompatWizardPrompts.IsYes(
                input.ReadLine()))
        {
            output.WriteLine(
                "No changes made."
            );

            return 0;
        }

        return RollBackAll(
            output,
            journalDirectoryPath,
            planIds
        );
    }

    private static int RollBackAll(
        TextWriter output,
        string journalDirectoryPath,
        List<Guid> planIds)
    {
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

            return 6;
        }

        using LinuxNoFollowPathHandle journalDirectory =
            journalDirectoryOpen.OpenedPath!;

        output.WriteLine(
            "Rolling back..."
        );

        int processed =
            0;

        int rolledBackSoFar =
            0;

        int alreadyRolledBackSoFar =
            0;

        var failureCounts =
            new Dictionary<
                DataRelativePathTargetedConsumerCaseRepairApplyRollbackState,
                int>();

        foreach (Guid planId in planIds)
        {
            processed++;

            DataRelativePathTargetedConsumerCaseRepairApplyRollbackResult
                rollback =
                    DataRelativePathTargetedConsumerCaseRepairApplyRollback
                        .Rollback(
                            journalDirectory,
                            planId,
                            DateTimeOffset.UtcNow
                        );

            if (rollback.Success)
            {
                rolledBackSoFar++;
            }
            else if (
                rollback.State ==
                DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                    .AlreadyRolledBack)
            {
                alreadyRolledBackSoFar++;
            }
            else
            {
                failureCounts[rollback.State] =
                    failureCounts.GetValueOrDefault(
                        rollback.State
                    ) + 1;
            }

            if (
                processed % 250 == 0 ||
                processed == planIds.Count)
            {
                output.WriteLine(
                    $"  {processed:N0}/{planIds.Count:N0} processed, " +
                    $"{rolledBackSoFar:N0} rolled back"
                );
            }
        }

        output.WriteLine();

        output.WriteLine(
            $"Rolled back:        {rolledBackSoFar:N0}"
        );

        output.WriteLine(
            $"Already rolled back: {alreadyRolledBackSoFar:N0}"
        );

        int failedCount =
            failureCounts.Values.Sum();

        output.WriteLine(
            $"Not rolled back:    {failedCount:N0}"
        );

        if (failureCounts.Count > 0)
        {
            output.WriteLine();

            output.WriteLine(
                "Not rolled back (reasons):"
            );

            foreach (
                (
                    DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                        state,
                    int count
                )
                in failureCounts
                    .OrderByDescending(
                        pair =>
                            pair.Value
                    ))
            {
                output.WriteLine(
                    $"  {state,-32} {count,9:N0}"
                );
            }

            output.WriteLine();

            output.WriteLine(
                "'OriginalLocationOccupied' means something now sits at " +
                "the original name - rollback refuses to overwrite it " +
                "rather than guess which copy is correct."
            );
        }

        return 0;
    }
}
