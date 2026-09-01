using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

public static class RepairApplyBatchCommand
{
    private const string BatchManifestName =
        "batch-manifest.json";

    public static int Run(string[] args)
    {
        if (args.Length != 4)
        {
            Console.Error.WriteLine(
                "Error: repair-apply-batch requires a batch directory, " +
                "manifest file name, and Skyrim Data directory."
            );

            Console.Error.WriteLine();

            Console.Error.WriteLine(
                "Usage: casecompat repair-apply-batch <batch directory> " +
                "<manifest file name> <Skyrim Data directory>"
            );

            return 2;
        }

        if (!IsValidManifestChildName(args[2]))
        {
            Console.Error.WriteLine(
                "Repair-apply-batch manifest file name must identify " +
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
                $"Repair-apply-batch batch directory open error: " +
                $"{ex.Message}"
            );

            return 3;
        }

        if (
            !batchOpen.Success ||
            batchOpen.OpenedPath is null)
        {
            Console.Error.WriteLine(
                "Repair-apply-batch batch directory could not be " +
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
         * Before any child mutation is attempted, verify the entire
         * durable completed batch from the retained batch descriptor.
         *
         * This proves exact root topology and every recorded child's
         * PlanId + exact manifest-byte SHA-256.
         *
         * Legacy batches without batch-manifest.json are intentionally
         * not executable through this command.
         */
        DataRelativePathRepairBatchCompletionInspection
            completionInspection;

        try
        {
            completionInspection =
                DataRelativePathRepairBatchCompletionInspector.Inspect(
                    batchDirectory,
                    BatchManifestName,
                    args[2],
                    args[3]
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                "Repair-apply-batch completion inspection error: " +
                ex.Message
            );

            Console.Error.WriteLine(
                "No recorded child plan was executed by this invocation."
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
            "CaseCompat Repair Batch Apply"
        );

        Console.WriteLine(
            "============================="
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

        /*
         * A durable completed zero-child batch is valid.
         *
         * Its completion manifest and exact topology were verified above,
         * so this is a successful no-op rather than an incomplete batch.
         */
        if (manifest.Children.Count == 0)
        {
            Console.WriteLine();

            Console.WriteLine(
                "Execution state: COMPLETED (NO RECORDED CHILD PLANS)"
            );

            Console.WriteLine(
                "Applied plans:   0"
            );

            Console.WriteLine();

            Console.WriteLine(
                "Repair result: COMPLETED (NO RECORDED CHILD PLANS)"
            );

            Console.WriteLine(
                "No filesystem repair operation was executed."
            );

            return 0;
        }

        Console.WriteLine();

        Console.WriteLine(
            "Applying recorded plans in durable batch order:"
        );

        int appliedCount =
            0;

        for (
            int index = 0;
            index < manifest.Children.Count;
            index++)
        {
            DataRelativePathRepairBatchManifestChild expectedChild =
                manifest.Children[index];

            LinuxOpenChildDirectoryReadOnlyAtResult childOpen;

            try
            {
                /*
                 * Mutation authority for the child directory is derived
                 * from the retained batch descriptor.
                 *
                 * Do not replace this with Path.Combine + pathname reopen.
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
                    $"Repair-apply-batch could not safely open recorded " +
                    $"child {expectedChild.ChildName}: {ex.Message}"
                );

                WritePartialBatchWarning(
                    appliedCount,
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
                    $"Repair-apply-batch could not safely open recorded " +
                    $"child {expectedChild.ChildName} for mutation."
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
                    appliedCount,
                    expectedChild.ChildName,
                    currentChildMayHaveProgress:
                        false
                );

                return 5;
            }

            using LinuxNoFollowPathHandle childDirectory =
                childOpen.OpenedDirectory;

            DataRelativePathRepairPlanForwardExecution execution;

            try
            {
                /*
                 * The durable batch expectation is checked again inside
                 * the existing whole-plan executor:
                 *
                 *   - before unnecessary execution-lock side effects; and
                 *   - against the authoritative manifest reread while the
                 *     per-PlanId execution lock is held.
                 *
                 * The CLI does not reconstruct a plan and does not call
                 * individual directory/file mutation executors.
                 */
                execution =
                    DataRelativePathRepairPlanForwardExecutor
                        .ExecuteExpectedManifest(
                            childDirectory,
                            args[2],
                            args[3],
                            DateTimeOffset.UtcNow,
                            expectedChild.PlanId,
                            expectedChild.ManifestSha256
                        );
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine();

                Console.Error.WriteLine(
                    $"Repair-apply-batch execution error for " +
                    $"{expectedChild.ChildName}: {ex.Message}"
                );

                WritePartialBatchWarning(
                    appliedCount,
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
                    appliedCount,
                    expectedChild.ChildName,
                    currentChildMayHaveProgress:
                        true
                );

                return 6;
            }

            appliedCount++;

            Console.WriteLine(
                $"[{index + 1}/{manifest.Children.Count}] " +
                $"{expectedChild.ChildName}: APPLIED DURABLY"
            );

            Console.WriteLine(
                $"  Plan ID:    {expectedChild.PlanId}"
            );

            Console.WriteLine(
                $"  Operations: {execution.OperationResults.Count}"
            );
        }

        Console.WriteLine();

        Console.WriteLine(
            "Execution state: BATCH APPLIED DURABLY"
        );

        Console.WriteLine(
            $"Applied plans:   {appliedCount:N0}"
        );

        Console.WriteLine();

        Console.WriteLine(
            "Repair result: BATCH APPLIED DURABLY"
        );

        Console.WriteLine(
            "Every recorded child plan reached whole-plan durable " +
            "forward success."
        );

        Console.WriteLine(
            "Each child retains its own durable journal and execution lock."
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
            "Repair batch apply was refused before child mutation."
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
                "required for batch apply."
            );

            Console.Error.WriteLine(
                "Legacy observational batches cannot be executed as a " +
                "batch."
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
            "No recorded child plan was executed by this invocation."
        );
    }

    private static void WriteExecutionFailure(
        DataRelativePathRepairBatchManifestChild expectedChild,
        DataRelativePathRepairPlanForwardExecution execution)
    {
        Console.Error.WriteLine();

        Console.Error.WriteLine(
            $"Repair-apply-batch child {expectedChild.ChildName} did not " +
            "reach whole-plan durable success."
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
                DataRelativePathRepairPlanForwardOperationExecution result
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
        int appliedCount,
        string failedChildName,
        bool currentChildMayHaveProgress)
    {
        Console.Error.WriteLine();

        Console.Error.WriteLine(
            "IMPORTANT: do not assume the batch is unchanged."
        );

        Console.Error.WriteLine(
            $"{appliedCount:N0} earlier recorded child plan(s) reached " +
            "whole-plan durable success in this invocation."
        );

        if (currentChildMayHaveProgress)
        {
            Console.Error.WriteLine(
                $"The failing child {failedChildName} may also have made " +
                "durable journal or filesystem progress before the " +
                "failure was reported."
            );
        }
        else
        {
            Console.Error.WriteLine(
                $"The failing child {failedChildName} was not executed by " +
                "this invocation."
            );
        }

        Console.Error.WriteLine(
            "Later recorded child plans were not attempted by this " +
            "invocation."
        );

        Console.Error.WriteLine(
            "Inspect the batch with repair-status-batch and inspect the " +
            "failing child with repair-status before retrying."
        );

        Console.Error.WriteLine(
            "No automatic rollback was attempted."
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
