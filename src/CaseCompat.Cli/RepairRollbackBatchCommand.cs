using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

public static class RepairRollbackBatchCommand
{
    private const string BatchManifestName =
        "batch-manifest.json";

    public static int Run(string[] args)
    {
        if (args.Length < 3 ||
            args.Length > 4)
        {
            Console.Error.WriteLine(
                "Error: repair-rollback-batch requires a batch directory, " +
                "Skyrim Data directory, and optional manifest file name."
            );

            Console.Error.WriteLine();

            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine(
                "  casecompat repair-rollback-batch <batch directory> " +
                "<Skyrim Data directory>"
            );
            Console.Error.WriteLine(
                "  casecompat repair-rollback-batch <batch directory> " +
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

        if (!IsValidManifestChildName(manifestChildName))
        {
            Console.Error.WriteLine(
                "Repair-rollback-batch manifest file name must identify " +
                "exactly one direct child and cannot be '.', '..', " +
                "or contain path separators or NUL."
            );

            return 2;
        }

        LinuxNoFollowPathOpenResult batchOpen;

        try
        {
            batchOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    args[1]
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Repair-rollback-batch batch directory open error: " +
                $"{ex.Message}"
            );

            return 3;
        }

        if (
            !batchOpen.Success ||
            batchOpen.OpenedPath is null)
        {
            Console.Error.WriteLine(
                "Repair-rollback-batch batch directory could not be " +
                "opened safely."
            );

            Console.Error.WriteLine(
                batchOpen.Error ??
                batchOpen.State.ToString()
            );

            return 3;
        }

        using LinuxNoFollowPathHandle batchDirectory =
            batchOpen.OpenedPath;

        /*
         * Verify the complete durable batch before crossing into any
         * destructive child rollback authority.
         *
         * This proves exact root topology and every recorded child's
         * PlanId + exact manifest-byte SHA-256.
         *
         * Legacy observational batches are intentionally not executable.
         */
        DataRelativePathRepairBatchCompletionInspection
            completionInspection;

        try
        {
            completionInspection =
                DataRelativePathRepairBatchCompletionInspector.Inspect(
                    batchDirectory,
                    BatchManifestName,
                    manifestChildName,
                    trustedDataRoot
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                "Repair-rollback-batch completion inspection error: " +
                ex.Message
            );

            Console.Error.WriteLine(
                "No recorded child plan was rolled back by this invocation."
            );

            return 4;
        }

        if (!completionInspection.Success)
        {
            WriteCompletionFailure(
                completionInspection
            );

            return 4;
        }

        DataRelativePathRepairBatchManifestRecord manifest =
            completionInspection.Manifest!;

        Console.WriteLine(
            "CaseCompat Repair Batch Rollback"
        );

        Console.WriteLine(
            "================================"
        );

        Console.WriteLine();

        Console.WriteLine(
            $"Batch ID:        {manifest.BatchId}"
        );

        Console.WriteLine(
            $"Data root:       {manifest.DataRoot}"
        );

        Console.WriteLine(
            $"Recorded plans:  {manifest.Children.Count:N0}"
        );

        if (manifest.Children.Count == 0)
        {
            Console.WriteLine();

            Console.WriteLine(
                "Execution state: COMPLETED (NO RECORDED CHILD PLANS)"
            );

            Console.WriteLine(
                "Rolled-back plans: 0"
            );

            Console.WriteLine();

            Console.WriteLine(
                "Rollback result: COMPLETED (NO RECORDED CHILD PLANS)"
            );

            Console.WriteLine(
                "No filesystem rollback operation was executed."
            );

            return 0;
        }

        Console.WriteLine();

        Console.WriteLine(
            "Rolling back recorded plans in reverse durable batch order:"
        );

        int processedCount =
            0;

        int rolledBackPlanCount =
            0;

        int noStartedPlanCount =
            0;

        for (
            int index = manifest.Children.Count - 1;
            index >= 0;
            index--)
        {
            DataRelativePathRepairBatchManifestChild expectedChild =
                manifest.Children[index];

            LinuxOpenChildDirectoryReadOnlyAtResult childOpen;

            try
            {
                /*
                 * Derive child mutation authority from the retained batch
                 * descriptor. Do not reopen a recorded child through a
                 * reconstructed pathname.
                 */
                childOpen =
                    LinuxOpenChildDirectoryReadOnlyAt.Open(
                        batchDirectory,
                        expectedChild.ChildName
                    );
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine();

                Console.Error.WriteLine(
                    $"Repair-rollback-batch could not safely open recorded " +
                    $"child {expectedChild.ChildName}: {ex.Message}"
                );

                WritePartialBatchWarning(
                    processedCount,
                    expectedChild.ChildName,
                    currentChildMayHaveProgress:
                        false
                );

                return 5;
            }

            if (
                !childOpen.Success ||
                childOpen.OpenedDirectory is null)
            {
                Console.Error.WriteLine();

                Console.Error.WriteLine(
                    $"Repair-rollback-batch could not safely open recorded " +
                    $"child {expectedChild.ChildName} for rollback."
                );

                Console.Error.WriteLine(
                    $"Open state (internal): {childOpen.State}"
                );

                if (
                    !string.IsNullOrWhiteSpace(
                        childOpen.Error))
                {
                    Console.Error.WriteLine(
                        $"Error: {childOpen.Error}"
                    );
                }

                WritePartialBatchWarning(
                    processedCount,
                    expectedChild.ChildName,
                    currentChildMayHaveProgress:
                        false
                );

                return 5;
            }

            using LinuxNoFollowPathHandle childDirectory =
                childOpen.OpenedDirectory;

            DataRelativePathRepairPlanRollbackExecution execution;

            try
            {
                /*
                 * Re-bind the durable batch expectation inside the
                 * existing whole-plan rollback lifecycle.
                 *
                 * ExecuteExpectedManifest checks the expectation both
                 * before unnecessary lock side effects and against the
                 * authoritative manifest reread while the shared
                 * per-PlanId execution lock is held.
                 *
                 * All destructive authority remains in the existing
                 * journal/incarnation-aware rollback executor.
                 */
                execution =
                    DataRelativePathRepairPlanRollbackExecutor
                        .ExecuteExpectedManifest(
                            childDirectory,
                            manifestChildName,
                            trustedDataRoot,
                            DateTimeOffset.UtcNow,
                            expectedChild.PlanId,
                            expectedChild.ManifestSha256
                        );
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine();

                Console.Error.WriteLine(
                    $"Repair-rollback-batch execution error for " +
                    $"{expectedChild.ChildName}: {ex.Message}"
                );

                WritePartialBatchWarning(
                    processedCount,
                    expectedChild.ChildName,
                    currentChildMayHaveProgress:
                        true
                );

                return 6;
            }

            if (!execution.Success)
            {
                WriteExecutionFailure(
                    expectedChild,
                    execution
                );

                WritePartialBatchWarning(
                    processedCount,
                    expectedChild.ChildName,
                    currentChildMayHaveProgress:
                        true
                );

                return 6;
            }

            int rolledBackOperations =
                0;

            int notStartedOperations =
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
                    rolledBackOperations++;
                }
                else if (
                    result.State ==
                    DataRelativePathRepairPlanRollbackOperationExecutionState
                        .NotStartedSkipped)
                {
                    notStartedOperations++;
                }
            }

            bool noStartedOperations =
                rolledBackOperations == 0 &&
                notStartedOperations ==
                    execution.OperationResults.Count;

            if (noStartedOperations)
            {
                noStartedPlanCount++;
            }
            else
            {
                rolledBackPlanCount++;
            }

            processedCount++;

            int rollbackStep =
                manifest.Children.Count - index;

            Console.WriteLine(
                $"[{rollbackStep}/{manifest.Children.Count}] " +
                $"{expectedChild.ChildName}: " +
                (
                    noStartedOperations
                        ? "NO STARTED OPERATIONS"
                        : "ROLLED BACK DURABLY"
                )
            );

            Console.WriteLine(
                $"  Plan ID:       {expectedChild.PlanId}"
            );

            Console.WriteLine(
                $"  Rolled back:   {rolledBackOperations}"
            );

            Console.WriteLine(
                $"  Never started: {notStartedOperations}"
            );
        }

        Console.WriteLine();

        if (rolledBackPlanCount == 0)
        {
            Console.WriteLine(
                "Execution state: COMPLETED (NO STARTED CHILD PLANS)"
            );
        }
        else
        {
            Console.WriteLine(
                "Execution state: BATCH ROLLED BACK DURABLY"
            );
        }

        Console.WriteLine(
            $"Processed plans:    {processedCount:N0}"
        );

        Console.WriteLine(
            $"Rolled-back plans:  {rolledBackPlanCount:N0}"
        );

        Console.WriteLine(
            $"Never-started plans:{noStartedPlanCount,3:N0}"
        );

        Console.WriteLine();

        if (rolledBackPlanCount == 0)
        {
            Console.WriteLine(
                "Rollback result: COMPLETED (NO STARTED CHILD PLANS)"
            );

            Console.WriteLine(
                "Every recorded child plan was already not started or " +
                "required no rollback work."
            );
        }
        else
        {
            Console.WriteLine(
                "Rollback result: BATCH ROLLED BACK DURABLY"
            );

            Console.WriteLine(
                "Every started recorded child plan processed by this " +
                "invocation reached whole-plan durable rollback success."
            );
        }

        Console.WriteLine(
            "Child plans were processed in reverse durable batch order."
        );

        Console.WriteLine(
            "Each child retains its own durable rollback journals and " +
            "execution lock."
        );

        Console.WriteLine(
            "This command does not claim a batch-wide atomic filesystem " +
            "transaction."
        );

        return 0;
    }

    private static void WriteCompletionFailure(
        DataRelativePathRepairBatchCompletionInspection inspection)
    {
        Console.Error.WriteLine(
            "Repair batch rollback was refused before child rollback."
        );

        Console.Error.WriteLine(
            $"Completion state (internal): {inspection.State}"
        );

        if (
            inspection.State ==
            DataRelativePathRepairBatchCompletionInspectionState
                .ManifestUnavailable)
        {
            Console.Error.WriteLine(
                "A durable batch-manifest.json completion record is " +
                "required for batch rollback."
            );

            Console.Error.WriteLine(
                "Legacy observational batches cannot be rolled back as " +
                "a batch."
            );
        }

        if (
            !string.IsNullOrWhiteSpace(
                inspection.FailedChildName))
        {
            Console.Error.WriteLine(
                $"Child: {inspection.FailedChildName}"
            );
        }

        if (
            !string.IsNullOrWhiteSpace(
                inspection.Error))
        {
            Console.Error.WriteLine(
                $"Error: {inspection.Error}"
            );
        }

        Console.Error.WriteLine(
            "No recorded child plan was rolled back by this invocation."
        );
    }

    private static void WriteExecutionFailure(
        DataRelativePathRepairBatchManifestChild expectedChild,
        DataRelativePathRepairPlanRollbackExecution execution)
    {
        Console.Error.WriteLine();

        Console.Error.WriteLine(
            $"Repair-rollback-batch child {expectedChild.ChildName} did " +
            "not reach whole-plan rollback success."
        );

        Console.Error.WriteLine(
            $"Execution state (internal): {execution.State}"
        );

        Console.Error.WriteLine(
            $"Expected Plan ID: {expectedChild.PlanId}"
        );

        if (
            execution.ManifestRead?.Manifest is
                DataRelativePathRepairPlanManifestRecord observedManifest)
        {
            Console.Error.WriteLine(
                $"Observed Plan ID: {observedManifest.PlanId}"
            );
        }

        if (
            !string.IsNullOrWhiteSpace(
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
                DataRelativePathRepairPlanRollbackOperationExecution result
                in execution.OperationResults)
            {
                Console.Error.WriteLine(
                    $"[{result.Index}] {result.Kind}: " +
                    $"{result.State} (internal state)"
                );

                Console.Error.WriteLine(
                    $"  Journal: {result.JournalChildName}"
                );

                if (
                    !string.IsNullOrWhiteSpace(
                        result.Error))
                {
                    Console.Error.WriteLine(
                        $"  Error: {result.Error}"
                    );
                }
            }
        }
    }

    private static void WritePartialBatchWarning(
        int processedCount,
        string failedChildName,
        bool currentChildMayHaveProgress)
    {
        Console.Error.WriteLine();

        Console.Error.WriteLine(
            "IMPORTANT: do not assume the batch remains fully applied."
        );

        Console.Error.WriteLine(
            $"{processedCount:N0} recorded child plan(s) earlier in " +
            "reverse rollback order completed successfully in this " +
            "invocation."
        );

        if (currentChildMayHaveProgress)
        {
            Console.Error.WriteLine(
                $"The failing child {failedChildName} may also have made " +
                "durable rollback-journal or filesystem progress before " +
                "the failure was reported."
            );
        }
        else
        {
            Console.Error.WriteLine(
                $"The failing child {failedChildName} was not rolled back " +
                "by this invocation."
            );
        }

        Console.Error.WriteLine(
            "Remaining earlier recorded child plans were not attempted " +
            "by this invocation."
        );

        Console.Error.WriteLine(
            "Inspect the batch with repair-status-batch and inspect the " +
            "failing child with repair-status before retrying."
        );

        Console.Error.WriteLine(
            "No automatic re-apply or compensating forward execution was " +
            "attempted."
        );
    }

    private static bool IsValidManifestChildName(
        string? childName)
    {
        if (
            string.IsNullOrWhiteSpace(
                childName))
        {
            return false;
        }

        if (
            childName == "." ||
            childName == "..")
        {
            return false;
        }

        return
            childName.IndexOf(
                '\0'
            ) < 0 &&
            childName.IndexOf(
                '/'
            ) < 0 &&
            childName.IndexOf(
                '\\'
            ) < 0;
    }
}
