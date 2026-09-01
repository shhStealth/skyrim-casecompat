using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

public static class RepairRollbackCommand
{
    public static int Run(string[] args)
    {
        if (args.Length < 3 ||
            args.Length > 4)
        {
            Console.Error.WriteLine(
                "Error: repair-rollback requires a journal directory, " +
                "Skyrim Data directory, and optional manifest file name."
            );
            Console.Error.WriteLine();
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine(
                "  casecompat repair-rollback <journal directory> " +
                "<Skyrim Data directory>"
            );
            Console.Error.WriteLine(
                "  casecompat repair-rollback <journal directory> " +
                "<manifest file name> <Skyrim Data directory>"
            );

            return 2;
        }

        string manifestChildName =
            args.Length == 4
                ? args[2]
                : RepairCliDefaults.PlanManifestChildName;

        string trustedDataRoot =
            args.Length == 4
                ? args[3]
                : args[2];

        LinuxNoFollowPathOpenResult journalOpen;

        try
        {
            journalOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    args[1]
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Journal directory open error: {ex.Message}"
            );

            return 3;
        }

        if (
            !journalOpen.Success ||
            journalOpen.OpenedPath is null)
        {
            Console.Error.WriteLine(
                "Journal directory could not be opened safely."
            );
            Console.Error.WriteLine(
                journalOpen.Error ??
                journalOpen.State.ToString()
            );

            return 3;
        }

        using LinuxNoFollowPathHandle journalDirectory =
            journalOpen.OpenedPath;

        DataRelativePathRepairPlanRollbackExecution execution;

        try
        {
            /*
             * The CLI delegates the entire destructive rollback
             * lifecycle to the hardened whole-plan executor.
             *
             * It does not infer ownership from live filesystem paths,
             * reconstruct the plan, or perform low-level removals.
             */
            execution =
                DataRelativePathRepairPlanRollbackExecutor.Execute(
                    journalDirectory,
                    manifestChildName,
                    trustedDataRoot,
                    DateTimeOffset.UtcNow
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Repair rollback execution error: {ex.Message}"
            );
            Console.Error.WriteLine();
            WriteFailureWarning();

            return 4;
        }

        if (!execution.Success)
        {
            Console.Error.WriteLine(
                "Repair rollback did not reach whole-plan durable success."
            );
            Console.Error.WriteLine(
                $"Execution state (internal): {execution.State}"
            );

            if (
                execution.ManifestRead?.Manifest is
                    DataRelativePathRepairPlanManifestRecord manifest)
            {
                Console.Error.WriteLine(
                    $"Plan ID: {manifest.PlanId}"
                );
            }

            if (!string.IsNullOrWhiteSpace(
                    execution.Error))
            {
                Console.Error.WriteLine(
                    $"Error: {execution.Error}"
                );
            }

            if (execution.OperationResults.Count > 0)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(
                    "Operation results:"
                );

                foreach (
                    DataRelativePathRepairPlanRollbackOperationExecution
                        result
                    in execution.OperationResults)
                {
                    Console.Error.WriteLine(
                        $"[{result.Index}] {result.Kind}: " +
                        $"{result.State} (internal state)"
                    );
                    Console.Error.WriteLine(
                        $"  Journal: {result.JournalChildName}"
                    );

                    if (!string.IsNullOrWhiteSpace(
                            result.Error))
                    {
                        Console.Error.WriteLine(
                            $"  Error: {result.Error}"
                        );
                    }
                }
            }

            Console.Error.WriteLine();
            WriteFailureWarning();

            return 4;
        }

        DataRelativePathRepairPlanManifestRecord? rolledBackManifest =
            execution.ManifestRead?.Manifest;

        int rolledBackCount =
            0;

        int notStartedCount =
            0;

        foreach (
            DataRelativePathRepairPlanRollbackOperationExecution result
            in execution.OperationResults)
        {
            if (
                result.State ==
                DataRelativePathRepairPlanRollbackOperationExecutionState
                    .RolledBackDurably)
            {
                rolledBackCount++;
            }
            else if (
                result.State ==
                DataRelativePathRepairPlanRollbackOperationExecutionState
                    .NotStartedSkipped)
            {
                notStartedCount++;
            }
        }

        Console.WriteLine(
            "CaseCompat Repair Rollback"
        );
        Console.WriteLine(
            "=========================="
        );
        Console.WriteLine();

        if (rolledBackManifest is not null)
        {
            Console.WriteLine(
                $"Plan ID:          {rolledBackManifest.PlanId}"
            );
            Console.WriteLine(
                $"Data root:        {rolledBackManifest.DataRoot}"
            );
            Console.WriteLine(
                $"Requested path:   {rolledBackManifest.RequestedPath}"
            );
        }

        bool noStartedOperations =
            rolledBackCount == 0 &&
            notStartedCount ==
                execution.OperationResults.Count;

        Console.WriteLine(
            noStartedOperations
                ? "Execution state:  COMPLETED (NO STARTED OPERATIONS)"
                : "Execution state:  ROLLED BACK DURABLY"
        );
        Console.WriteLine(
            $"Operation count:  {execution.OperationResults.Count}"
        );
        Console.WriteLine(
            $"Rolled back:      {rolledBackCount}"
        );
        Console.WriteLine(
            $"Never started:    {notStartedCount}"
        );

        Console.WriteLine();
        Console.WriteLine(
            "Operation results:"
        );

        foreach (
            DataRelativePathRepairPlanRollbackOperationExecution result
            in execution.OperationResults)
        {
            Console.WriteLine(
                $"[{result.Index}] {result.Kind}: " +
                $"{FormatOperation(result.State)}"
            );
            Console.WriteLine(
                $"  Journal: {result.JournalChildName}"
            );
        }

        Console.WriteLine();

        if (noStartedOperations)
        {
            Console.WriteLine(
                "Rollback result: NO STARTED OPERATIONS"
            );
            Console.WriteLine(
                "The rollback executor completed successfully, but no " +
                "operation journal existed to roll back."
            );
            Console.WriteLine(
                "repair-status will continue to report this plan as " +
                "NOT STARTED."
            );
        }
        else
        {
            Console.WriteLine(
                "Rollback result: ROLLED BACK DURABLY"
            );
            Console.WriteLine(
                "All started plan operations were rolled back durably."
            );

            if (notStartedCount > 0)
            {
                Console.WriteLine(
                    "Untouched suffix operations were not started, " +
                    "so no rollback was needed."
                );
            }

            Console.WriteLine(
                "Run repair-status to independently inspect the " +
                "persisted rollback journals."
            );
        }

        return 0;
    }

    private static string FormatOperation(
        DataRelativePathRepairPlanRollbackOperationExecutionState state)
    {
        return state switch
        {
            DataRelativePathRepairPlanRollbackOperationExecutionState
                .RolledBackDurably =>
                    "ROLLED BACK DURABLY",

            DataRelativePathRepairPlanRollbackOperationExecutionState
                .NotStartedSkipped =>
                    "NOT STARTED (NO ROLLBACK NEEDED)",

            _ =>
                state.ToString()
        };
    }

    private static void WriteFailureWarning()
    {
        Console.Error.WriteLine(
            "IMPORTANT: do not assume rollback made no changes."
        );
        Console.Error.WriteLine(
            "A failed repair-rollback may already have durably removed " +
            "some CaseCompat-owned filesystem objects or advanced " +
            "rollback journals before the failure was reported."
        );
        Console.Error.WriteLine(
            "Inspect the plan with repair-status before retrying " +
            "rollback or attempting repair-apply."
        );
    }
}
