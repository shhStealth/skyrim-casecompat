using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

public static class RepairApplyCommand
{
    public static int Run(string[] args)
    {
        if (args.Length != 4)
        {
            Console.Error.WriteLine(
                "Error: repair-apply requires a journal directory, " +
                "manifest file name, and Skyrim Data directory."
            );
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "Usage: casecompat repair-apply <journal directory> " +
                "<manifest file name> <Skyrim Data directory>"
            );

            return 2;
        }

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

        DataRelativePathRepairPlanForwardExecution execution;

        try
        {
            /*
             * The CLI deliberately delegates the entire mutating
             * lifecycle to the hardened whole-plan executor.
             *
             * It does not reconstruct a plan from the live filesystem
             * and does not call directory/file executors directly.
             */
            execution =
                DataRelativePathRepairPlanForwardExecutor.Execute(
                    journalDirectory,
                    args[2],
                    args[3],
                    DateTimeOffset.UtcNow
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Repair apply execution error: {ex.Message}"
            );
            Console.Error.WriteLine();
            WriteFailureWarning();

            return 4;
        }

        if (!execution.Success)
        {
            Console.Error.WriteLine(
                "Repair apply did not reach whole-plan durable success."
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
                    DataRelativePathRepairPlanForwardOperationExecution
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

        DataRelativePathRepairPlanManifestRecord? appliedManifest =
            execution.ManifestRead?.Manifest;

        Console.WriteLine(
            "CaseCompat Repair Apply"
        );
        Console.WriteLine(
            "======================="
        );
        Console.WriteLine();

        if (appliedManifest is not null)
        {
            Console.WriteLine(
                $"Plan ID:          {appliedManifest.PlanId}"
            );
            Console.WriteLine(
                $"Data root:        {appliedManifest.DataRoot}"
            );
            Console.WriteLine(
                $"Requested path:   {appliedManifest.RequestedPath}"
            );
        }

        Console.WriteLine(
            "Execution state:  APPLIED DURABLY"
        );
        Console.WriteLine(
            $"Operation count:  {execution.OperationResults.Count}"
        );

        Console.WriteLine();
        Console.WriteLine(
            "Operation results:"
        );

        foreach (
            DataRelativePathRepairPlanForwardOperationExecution result
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
        Console.WriteLine(
            "Repair result: APPLIED DURABLY"
        );
        Console.WriteLine(
            "All plan operations were applied durably."
        );
        Console.WriteLine(
            "Run repair-status to independently inspect the persisted " +
            "operation journals."
        );

        return 0;
    }

    private static string FormatOperation(
        DataRelativePathRepairPlanForwardOperationExecutionState state)
    {
        return state switch
        {
            DataRelativePathRepairPlanForwardOperationExecutionState
                .AppliedDurably =>
                    "APPLIED DURABLY",

            _ =>
                state.ToString()
        };
    }

    private static void WriteFailureWarning()
    {
        Console.Error.WriteLine(
            "IMPORTANT: do not assume the plan is unchanged."
        );
        Console.Error.WriteLine(
            "A failed repair-apply may have made durable journal or " +
            "filesystem progress before the failure was reported."
        );
        Console.Error.WriteLine(
            "Inspect the plan with repair-status before retrying or " +
            "attempting rollback."
        );
    }
}
