using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

public static class RepairApplyBatchCommand
{
    private const string BatchManifestName =
        "batch-manifest.json";

    private const string ApplyAuthorizationName =
        "batch-apply-authorization.json";

    public static int Run(string[] args)
    {
        return Run(
            args,
            beforeAuthorizationPublish:
                null
        );
    }

    internal static int Run(
        string[] args,
        Action<
            LinuxNoFollowPathHandle,
            DataRelativePathRepairBatchApplyAuthorizationRecord>?
                beforeAuthorizationPublish)
    {
        if (args.Length < 3 ||
            args.Length > 4)
        {
            Console.Error.WriteLine(
                "Error: repair-apply-batch requires a batch directory, " +
                "Skyrim Data directory, and optional manifest file name."
            );

            Console.Error.WriteLine();

            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine(
                "  casecompat repair-apply-batch <batch directory> " +
                "<Skyrim Data directory>"
            );
            Console.Error.WriteLine(
                "  casecompat repair-apply-batch <batch directory> " +
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
                    manifestChildName,
                    trustedDataRoot
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

        /*
         * A schema-v2 / coverage-policy-v1 batch is not allowed to enter
         * child mutation merely because its immutable planner provenance
         * says aggregate coverage was established at planning time.
         *
         * Before the first mutation boundary, use only child manifests that
         * the completion inspector has just authenticated by exact PlanId and
         * exact manifest-byte SHA-256, then freshly re-prove the aggregate
         * physical namespace against the current filesystem.
         *
         * Only after that proof succeeds may the batch publish its immutable
         * batch-wide apply authorization.
         *
         * If a valid authorization was already present, completion inspection
         * has already rebound it to these exact completed batch-manifest bytes.
         * Do not replace it and do not reinterpret the original pre-start
         * namespace. The durable authorization is the crash/restart boundary.
         */
        bool requiresAggregateApplyAuthority =
            manifest.SchemaVersion ==
                DataRelativePathRepairBatchManifestRecord
                    .SchemaVersion2 &&
            manifest.CoveragePolicyVersion ==
                DataRelativePathRepairBatchManifestRecord
                    .CoveragePolicyVersion1;

        if (
            requiresAggregateApplyAuthority &&
            completionInspection.ApplyAuthorizationRead is null)
        {
            var authenticatedChildManifests =
                new DataRelativePathRepairPlanManifestRecord[
                    completionInspection.Children.Count
                ];

            for (
                int index = 0;
                index < completionInspection.Children.Count;
                index++)
            {
                DataRelativePathRepairPlanManifestRecord?
                    authenticatedManifest =
                        completionInspection
                            .Children[index]
                            .Inspection
                            .Manifest;

                if (authenticatedManifest is null)
                {
                    Console.Error.WriteLine();
                    Console.Error.WriteLine(
                        "Repair-apply-batch aggregate coverage could not " +
                        "retain an authenticated child manifest."
                    );
                    Console.Error.WriteLine(
                        $"Child: " +
                        $"{completionInspection.Children[index].ChildName}"
                    );
                    Console.Error.WriteLine(
                        "No recorded child plan was executed by this invocation."
                    );

                    return 4;
                }

                authenticatedChildManifests[index] =
                    authenticatedManifest;
            }

            DataRelativePathRepairBatchCoverageAuthorization coverage;

            try
            {
                coverage =
                    DataRelativePathRepairBatchCoverageAuthorizer
                        .AuthorizePersistedManifests(
                            authenticatedChildManifests
                        );
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(
                    "Repair-apply-batch aggregate namespace coverage " +
                    $"inspection failed: {ex.Message}"
                );
                Console.Error.WriteLine(
                    "No recorded child plan was executed by this invocation."
                );

                return 4;
            }

            if (!coverage.AllAuthorized)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(
                    "Repair-apply-batch refused aggregate namespace " +
                    "authorization before child mutation."
                );

                foreach (
                    DataRelativePathRepairBatchCoverageDecision decision
                    in coverage.Decisions
                        .Where(decision =>
                            decision.State !=
                            DataRelativePathRepairBatchCoverageDecisionState
                                .Authorized
                        ))
                {
                    string childName =
                        decision.CandidateIndex >= 0 &&
                        decision.CandidateIndex <
                            completionInspection.Children.Count
                            ? completionInspection
                                .Children[decision.CandidateIndex]
                                .ChildName
                            : $"candidate-{decision.CandidateIndex}";

                    Console.Error.WriteLine(
                        $"[{decision.CandidateIndex}] {childName}: " +
                        $"{decision.State}"
                    );

                    if (
                        !string.IsNullOrWhiteSpace(
                            decision.Error))
                    {
                        Console.Error.WriteLine(
                            $"  Error: {decision.Error}"
                        );
                    }
                }

                Console.Error.WriteLine(
                    "No recorded child plan was executed by this invocation."
                );

                return 4;
            }

            string? exactBatchManifestSha256 =
                completionInspection
                    .BatchManifestRead?
                    .ManifestSha256;

            if (
                string.IsNullOrWhiteSpace(
                    exactBatchManifestSha256))
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(
                    "Repair-apply-batch could not retain the exact " +
                    "batch-manifest SHA-256 required to bind durable " +
                    "aggregate apply authorization."
                );
                Console.Error.WriteLine(
                    "No recorded child plan was executed by this invocation."
                );

                return 4;
            }

            DataRelativePathRepairBatchApplyAuthorizationCreation
                authorizationCreation =
                    DataRelativePathRepairBatchApplyAuthorization
                        .CreateForCompletedBatch(
                            manifest,
                            exactBatchManifestSha256,
                            DateTimeOffset.UtcNow
                        );

            if (
                !authorizationCreation.Success ||
                authorizationCreation.Authorization is null)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(
                    "Repair-apply-batch could not create immutable " +
                    "batch-wide apply authorization."
                );
                Console.Error.WriteLine(
                    authorizationCreation.Error ??
                    authorizationCreation.State.ToString()
                );
                Console.Error.WriteLine(
                    "No recorded child plan was executed by this invocation."
                );

                return 4;
            }

            /*
             * Internal deterministic test seam for the publication race.
             *
             * Normal CLI execution always supplies null. Tests may publish
             * the exact authorization here to model another invocation
             * winning the one-shot namespace race after our initial
             * completion inspection and fresh aggregate coverage.
             */
            beforeAuthorizationPublish?.Invoke(
                batchDirectory,
                authorizationCreation.Authorization
            );

            DataRelativePathRepairBatchApplyAuthorizationWriterResult
                authorizationWrite;

            try
            {
                authorizationWrite =
                    DataRelativePathRepairBatchApplyAuthorizationWriter
                        .CreateInitial(
                            batchDirectory,
                            ApplyAuthorizationName,
                            authorizationCreation.Authorization
                        );
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(
                    "Repair-apply-batch apply-authorization publication " +
                    $"error: {ex.Message}"
                );
                Console.Error.WriteLine(
                    "No recorded child plan was executed by this invocation."
                );

                return 4;
            }

            bool authorizationPublishedByThisInvocation =
                authorizationWrite.Success;

            bool authorizationPublicationRaceLost =
                authorizationWrite.State ==
                DataRelativePathRepairBatchApplyAuthorizationWriteState
                    .AuthorizationAlreadyExists;

            if (
                !authorizationPublishedByThisInvocation &&
                !authorizationPublicationRaceLost)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(
                    "Repair-apply-batch could not establish durable " +
                    "batch-wide apply authorization."
                );
                Console.Error.WriteLine(
                    $"Authorization state (internal): " +
                    $"{authorizationWrite.State}"
                );

                if (
                    !string.IsNullOrWhiteSpace(
                        authorizationWrite.Error))
                {
                    Console.Error.WriteLine(
                        $"Error: {authorizationWrite.Error}"
                    );
                }

                if (authorizationWrite.AuthorizationEntryChanged)
                {
                    Console.Error.WriteLine(
                        "The reserved authorization entry may now exist, " +
                        "but durable publication was not proven. Retry only " +
                        "through repair-apply-batch so completion inspection " +
                        "can reauthenticate the exact observed entry."
                    );
                }

                Console.Error.WriteLine(
                    "No recorded child plan was executed by this invocation."
                );

                return 4;
            }

            /*
             * Publication itself is not enough.
             *
             * If our one-shot writer lost the no-overwrite namespace race,
             * AuthorizationAlreadyExists grants no authority by itself.
             * The exact observed entry must pass the same canonical
             * completion inspection and completed-batch binding as an entry
             * published by this invocation.
             *
             * Re-run canonical completion
             * inspection from the same retained batch descriptor and require
             * the newly published authorization to be read, validated, and
             * rebound to the exact current completed batch before child 1.
             *
             * This also catches any child-manifest replacement that raced the
             * fresh aggregate coverage/publication interval.
             */
            DataRelativePathRepairBatchCompletionInspection
                postAuthorizationInspection;

            try
            {
                postAuthorizationInspection =
                    DataRelativePathRepairBatchCompletionInspector.Inspect(
                        batchDirectory,
                        BatchManifestName,
                        manifestChildName,
                        trustedDataRoot
                    );
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(
                    "Repair-apply-batch post-authorization completion " +
                    $"inspection error: {ex.Message}"
                );
                Console.Error.WriteLine(
                    authorizationPublishedByThisInvocation
                        ? "Durable batch apply authorization was published, " +
                          "but no recorded child plan was executed by this invocation."
                        : "A competing batch apply authorization publisher won " +
                          "the namespace race, but the observed authorization " +
                          "could not be reauthenticated. No recorded child plan " +
                          "was executed by this invocation."
                );

                return 4;
            }

            if (
                !postAuthorizationInspection.Success ||
                postAuthorizationInspection
                    .ApplyAuthorizationRead?
                    .Success != true)
            {
                WriteCompletionFailure(
                    postAuthorizationInspection
                );

                Console.Error.WriteLine(
                    authorizationPublishedByThisInvocation
                        ? "Durable batch apply authorization was published, but " +
                          "the exact authorization/completed-batch binding could " +
                          "not be reauthenticated before child mutation."
                        : "A competing publisher's existing authorization was " +
                          "observed, but its exact completed-batch binding could " +
                          "not be reauthenticated before child mutation."
                );

                return 4;
            }

            completionInspection =
                postAuthorizationInspection;

            manifest =
                postAuthorizationInspection.Manifest!;

            Console.WriteLine();
            Console.WriteLine(
                authorizationPublishedByThisInvocation
                    ? "Batch apply authorization: PUBLISHED DURABLY AND VERIFIED"
                    : "Batch apply authorization: EXISTING DURABLE AUTHORITY " +
                      "VERIFIED AFTER PUBLICATION RACE"
            );
        }
        else if (requiresAggregateApplyAuthority)
        {
            /*
             * completionInspection.Success plus a non-null observed
             * authorization means the completion inspector has already
             * validated and rebound that exact entry to the current durable
             * batch-manifest bytes.
             */
            if (
                completionInspection
                    .ApplyAuthorizationRead?
                    .Success != true)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(
                    "Repair-apply-batch observed aggregate authorization " +
                    "without a successful authenticated read."
                );
                Console.Error.WriteLine(
                    "No recorded child plan was executed by this invocation."
                );

                return 4;
            }

            Console.WriteLine();
            Console.WriteLine(
                "Batch apply authorization: EXISTING DURABLE AUTHORITY VERIFIED"
            );
        }

        /*
         * Build every immutable child context before the first recorded
         * plan is permitted to mutate anything.
         *
         * Completion inspection has already authenticated this exact
         * completed batch. Therefore a context-construction failure is an
         * internal invariant failure and aborts the batch before plan 1.
         */
        var executionContexts =
            new DataRelativePathRepairBatchExecutionContext[
                manifest.Children.Count
            ];

        for (
            int index = 0;
            index < manifest.Children.Count;
            index++)
        {
            DataRelativePathRepairBatchExecutionContextCreation
                contextCreation =
                    DataRelativePathRepairBatchExecutionContext.Create(
                        manifest,
                        index,
                        manifest.Children[index]
                    );

            if (
                !contextCreation.Success ||
                contextCreation.Context is null)
            {
                Console.Error.WriteLine();

                Console.Error.WriteLine(
                    "Repair-apply-batch could not create the exact " +
                    $"execution context for recorded child index {index}."
                );

                Console.Error.WriteLine(
                    contextCreation.Error ??
                    contextCreation.State.ToString()
                );

                Console.Error.WriteLine(
                    "No recorded child plan was executed by this invocation."
                );

                return 4;
            }

            executionContexts[index] =
                contextCreation.Context;
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

            DataRelativePathRepairBatchExecutionContext batchContext =
                executionContexts[index];

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
                        batchContext.CurrentChild.ChildName
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
                        .ExecuteExpectedBatchManifest(
                            batchDirectory,
                            batchContext,
                            childDirectory,
                            trustedDataRoot,
                            DateTimeOffset.UtcNow
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
