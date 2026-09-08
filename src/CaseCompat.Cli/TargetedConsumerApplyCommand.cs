using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

public static class TargetedConsumerApplyCommand
{
    public static int Run(string[] args)
    {
        if (args.Length != 5)
        {
            Console.Error.WriteLine(
                "Error: targeted-consumer-apply requires a Data root, " +
                "plan directory, plan file name, and journal directory."
            );
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "Usage: casecompat targeted-consumer-apply <Data root> " +
                "<plan directory> <plan file name> <journal directory>"
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

            return 3;
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

            return 3;
        }

        using LinuxNoFollowPathHandle dataRoot =
            dataRootOpen.OpenedPath!;

        LinuxNoFollowPathOpenResult planDirectoryOpen;

        try
        {
            planDirectoryOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    args[2]
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Plan directory open error: {ex.Message}"
            );

            return 4;
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

            return 4;
        }

        using LinuxNoFollowPathHandle planDirectory =
            planDirectoryOpen.OpenedPath!;

        DataRelativePathTargetedConsumerCaseRepairDurablePlanReaderResult
            planRead;

        try
        {
            planRead =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanReader
                    .Read(
                        planDirectory,
                        args[3]
                    );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Plan read error: {ex.Message}"
            );

            return 5;
        }

        if (!planRead.Success)
        {
            Console.Error.WriteLine(
                "Targeted durable plan could not be read."
            );
            Console.Error.WriteLine(
                $"Read state: {planRead.State}"
            );

            if (!string.IsNullOrWhiteSpace(
                    planRead.Error))
            {
                Console.Error.WriteLine(
                    $"Error:      {planRead.Error}"
                );
            }

            return 5;
        }

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            planRead.Plan!;

        LinuxNoFollowPathOpenResult journalDirectoryOpen;

        try
        {
            journalDirectoryOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    args[4]
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Journal directory open error: {ex.Message}"
            );

            return 6;
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

            return 6;
        }

        using LinuxNoFollowPathHandle journalDirectory =
            journalDirectoryOpen.OpenedPath!;

        DataRelativePathTargetedConsumerCaseRepairApplyExecution execution;

        try
        {
            execution =
                DataRelativePathTargetedConsumerCaseRepairApplyExecutor
                    .Execute(
                        dataRoot,
                        journalDirectory,
                        plan,
                        DateTimeOffset.UtcNow
                    );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Targeted apply execution error: {ex.Message}"
            );

            return 7;
        }

        Console.WriteLine(
            "CaseCompat Targeted Consumer-Case Repair Apply"
        );

        Console.WriteLine(
            "==============================================="
        );

        Console.WriteLine();

        Console.WriteLine(
            $"Plan ID:          {plan.PlanId}"
        );

        Console.WriteLine(
            $"Requested path:   {plan.RequestedPath}"
        );

        Console.WriteLine(
            $"Destination:      {execution.DestinationPath}"
        );

        Console.WriteLine(
            $"Execution state:  {execution.State}"
        );

        Console.WriteLine(
            $"Intent journal:   " +
            $"{execution.IntentJournalChildName ?? "(not written)"}"
        );

        Console.WriteLine(
            $"Prepared journal: " +
            $"{execution.PreparedJournalChildName ?? "(not written)"}"
        );

        Console.WriteLine(
            $"Applied journal:  " +
            $"{execution.AppliedJournalChildName ?? "(not written)"}"
        );

        Console.WriteLine(
            $"Journal directory:{journalDirectory.FullPath}"
        );

        if (!string.IsNullOrWhiteSpace(
                execution.Error))
        {
            Console.WriteLine(
                $"Error:            {execution.Error}"
            );
        }

        Console.WriteLine();

        if (execution.Success)
        {
            Console.WriteLine(
                "Repair operation executed: YES"
            );
            Console.WriteLine(
                "Durable apply journal:     Intent -> Prepared -> Applied"
            );
            Console.WriteLine();
            Console.WriteLine(
                $"To undo, run: casecompat targeted-consumer-rollback " +
                $"{journalDirectory.FullPath} {plan.PlanId}"
            );

            return 0;
        }

        Console.WriteLine(
            "Repair operation executed: NO"
        );

        bool possiblyPublished =
            execution.PreparedJournalChildName is not null;

        if (possiblyPublished)
        {
            Console.WriteLine(
                "IMPORTANT: execution reached the Prepared phase or " +
                "later, so the destination file may already be " +
                "published on disk even though the Applied journal was " +
                "not durably written."
            );
            Console.WriteLine(
                $"Inspect '{execution.DestinationPath}' and the journal " +
                "directory directly before retrying."
            );
        }
        else
        {
            Console.WriteLine(
                "Execution stopped before the destination was created; " +
                "the Data tree was not mutated."
            );
        }

        return 8;
    }
}
