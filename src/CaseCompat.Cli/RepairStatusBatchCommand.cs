using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

public static class RepairStatusBatchCommand
{
    private const string BatchManifestName =
        "batch-manifest.json";

    private static readonly
        DataRelativePathRepairPlanOverallStatus[]
        OverallStatusOrder =
        [
            DataRelativePathRepairPlanOverallStatus.NotStarted,
            DataRelativePathRepairPlanOverallStatus.InProgress,
            DataRelativePathRepairPlanOverallStatus.Applied,
            DataRelativePathRepairPlanOverallStatus.RollbackInProgress,
            DataRelativePathRepairPlanOverallStatus.RolledBack,
            DataRelativePathRepairPlanOverallStatus.RecoveryConflict
        ];

    public static int Run(string[] args)
    {
        if (args.Length < 3 ||
            args.Length > 4)
        {
            Console.Error.WriteLine(
                "Error: repair-status-batch requires a batch directory, " +
                "Skyrim Data directory, and optional manifest file name."
            );

            Console.Error.WriteLine();

            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine(
                "  casecompat repair-status-batch <batch directory> " +
                "<Skyrim Data directory>"
            );
            Console.Error.WriteLine(
                "  casecompat repair-status-batch <batch directory> " +
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
                "Repair-status-batch manifest file name must identify " +
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
                $"Batch directory open error: {ex.Message}"
            );

            return 3;
        }

        if (!batchOpen.Success)
        {
            Console.Error.WriteLine(
                "Batch directory could not be opened safely."
            );

            Console.Error.WriteLine(
                batchOpen.Error ??
                batchOpen.State.ToString()
            );

            return 3;
        }

        using LinuxNoFollowPathHandle batchDirectory =
            batchOpen.OpenedPath!;

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
                "Repair-status-batch completion inspection error: " +
                ex.Message
            );

            Console.Error.WriteLine(
                "No batch aggregate was published."
            );

            return 4;
        }

        DataRelativePathRepairBatchManifestRecord? batchManifest =
            null;

        var inspectedPlans =
            new List<
                (
                    string ChildName,
                    DataRelativePathRepairPlanStatusInspection
                        Inspection
                )
            >();

        if (completionInspection.Success)
        {
            batchManifest =
                completionInspection.Manifest!;

            foreach (
                DataRelativePathRepairBatchCompletionInspectedChild child
                in completionInspection.Children)
            {
                inspectedPlans.Add(
                    (
                        child.ChildName,
                        child.Inspection
                    )
                );
            }
        }
        else if (
            completionInspection.State ==
            DataRelativePathRepairBatchCompletionInspectionState
                .ManifestUnavailable)
        {
            /*
             * Legacy batches predate durable batch completion metadata.
             *
             * Legacy support remains observational only. A mutating batch
             * command must require a verified completion manifest.
             */
            LinuxEnumerateDirectoryAtResult? enumeration =
                completionInspection.Enumeration;

            if (
                enumeration is null ||
                !enumeration.Success)
            {
                Console.Error.WriteLine(
                    "Repair-status-batch could not retain the legacy " +
                    "batch directory enumeration."
                );

                Console.Error.WriteLine(
                    "No batch aggregate was published."
                );

                return 4;
            }

            string? topologyError =
                ValidateTopology(
                    enumeration.ChildNames
                );

            if (topologyError is not null)
            {
                Console.Error.WriteLine(
                    "Repair-status-batch batch topology is invalid."
                );

                Console.Error.WriteLine(
                    topologyError
                );

                return 4;
            }

            foreach (
                string childName
                in enumeration.ChildNames)
            {
                LinuxOpenChildReadOnlyAtResult childOpen;

                try
                {
                    childOpen =
                        LinuxOpenChildReadOnlyAt.Open(
                            batchDirectory,
                            childName
                        );
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(
                        $"Repair-status-batch could not open " +
                        $"{childName}: {ex.Message}"
                    );

                    return 4;
                }

                if (!childOpen.Success)
                {
                    Console.Error.WriteLine(
                        $"Repair-status-batch could not safely open " +
                        $"{childName}."
                    );

                    Console.Error.WriteLine(
                        childOpen.Error ??
                        childOpen.State.ToString()
                    );

                    return 4;
                }

                using LinuxOpenedChildHandle childDirectory =
                    childOpen.OpenedChild!;

                DataRelativePathRepairPlanStatusInspection inspection;

                try
                {
                    inspection =
                        DataRelativePathRepairPlanStatusInspector.Inspect(
                            childDirectory,
                            manifestChildName,
                            trustedDataRoot
                        );
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(
                        $"Repair-status-batch inspection error for " +
                        $"{childName}: {ex.Message}"
                    );

                    return 4;
                }

                if (!inspection.Success)
                {
                    Console.Error.WriteLine(
                        "Repair-status-batch could not completely " +
                        $"inspect {childName}."
                    );

                    Console.Error.WriteLine(
                        $"Inspection state (internal): " +
                        $"{inspection.State}"
                    );

                    if (
                        !string.IsNullOrWhiteSpace(
                            inspection.Error))
                    {
                        Console.Error.WriteLine(
                            $"Error: {inspection.Error}"
                        );
                    }

                    Console.Error.WriteLine(
                        "No batch aggregate was published."
                    );

                    return 4;
                }

                inspectedPlans.Add(
                    (
                        childName,
                        inspection
                    )
                );
            }
        }
        else
        {
            Console.Error.WriteLine(
                "Repair-status-batch could not verify durable batch " +
                "completion."
            );

            Console.Error.WriteLine(
                $"Completion state (internal): " +
                $"{completionInspection.State}"
            );

            if (
                !string.IsNullOrWhiteSpace(
                    completionInspection.FailedChildName))
            {
                Console.Error.WriteLine(
                    $"Child: {completionInspection.FailedChildName}"
                );
            }

            if (
                !string.IsNullOrWhiteSpace(
                    completionInspection.Error))
            {
                Console.Error.WriteLine(
                    $"Error: {completionInspection.Error}"
                );
            }

            Console.Error.WriteLine(
                "No batch aggregate was published."
            );

            return 4;
        }

        var counts =
            new Dictionary<
                DataRelativePathRepairPlanOverallStatus,
                int
            >();

        foreach (
            DataRelativePathRepairPlanOverallStatus status
            in OverallStatusOrder)
        {
            counts[status] =
                0;
        }

        foreach (var plan in inspectedPlans)
        {
            DataRelativePathRepairPlanOverallStatus status =
                plan.Inspection.OverallStatus!.Value;

            counts[status]++;
        }

        Console.WriteLine(
            "CaseCompat Repair Status Batch"
        );

        Console.WriteLine(
            "=============================="
        );

        Console.WriteLine();

        Console.WriteLine(
            $"Batch directory:    {batchDirectory.FullPath}"
        );

        Console.WriteLine(
            $"Manifest:           {manifestChildName}"
        );

        Console.WriteLine(
            $"Trusted Data:       {trustedDataRoot}"
        );

        Console.WriteLine(
            $"Observed plans:     {inspectedPlans.Count:N0}"
        );

        if (batchManifest is null)
        {
            Console.WriteLine(
                "Original batch completeness: NOT RECORDED"
            );

            Console.WriteLine();

            Console.WriteLine(
                "The current batch format has no durable batch-summary " +
                "manifest."
            );

            Console.WriteLine(
                "This command reports every contiguous plan-* child " +
                "observed during descriptor-relative enumeration of the " +
                "retained batch directory, but cannot prove how many " +
                "plans the original batch-planning invocation intended " +
                "to persist."
            );
        }
        else
        {
            Console.WriteLine(
                "Original batch completeness: RECORDED AND VERIFIED"
            );

            Console.WriteLine();

            Console.WriteLine(
                $"Batch manifest:      {BatchManifestName}"
            );

            Console.WriteLine(
                $"Batch ID:            {batchManifest.BatchId}"
            );

            Console.WriteLine(
                $"Input paths:         " +
                $"{batchManifest.InputPathCount:N0}"
            );

            Console.WriteLine(
                $"Safe rejections:     " +
                $"{batchManifest.SafeRejectionCount:N0}"
            );

            Console.WriteLine(
                $"Recorded plans:      " +
                $"{batchManifest.Children.Count:N0}"
            );

            Console.WriteLine();

            Console.WriteLine(
                "Every recorded child plan matched the durable batch " +
                "manifest by literal child name, PlanId, and exact " +
                "manifest-byte SHA-256."
            );
        }

        if (inspectedPlans.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine(
                "Plans:"
            );

            for (
                int index = 0;
                index < inspectedPlans.Count;
                index++)
            {
                var plan =
                    inspectedPlans[index];

                DataRelativePathRepairPlanManifestRecord manifest =
                    plan.Inspection.Manifest!;

                Console.WriteLine();

                Console.WriteLine(
                    $"[{index + 1}] {plan.ChildName}"
                );

                Console.WriteLine(
                    $"  Plan ID:        {manifest.PlanId}"
                );

                Console.WriteLine(
                    $"  Requested path: {manifest.RequestedPath}"
                );

                Console.WriteLine(
                    $"  Operations:     {manifest.Operations.Count}"
                );

                Console.WriteLine(
                    $"  Overall status: " +
                    FormatOverall(
                        plan.Inspection.OverallStatus!.Value
                    )
                );
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            "Batch status summary:"
        );

        foreach (
            DataRelativePathRepairPlanOverallStatus status
            in OverallStatusOrder)
        {
            Console.WriteLine(
                $"  {FormatOverall(status),-22}" +
                $"{counts[status]:N0}"
            );
        }

        Console.WriteLine();

        Console.WriteLine(
            "Read-only batch status inspection: no files were modified."
        );

        Console.WriteLine(
            "No batch apply or rollback authority was created."
        );

        Console.WriteLine(
            "Use repair-status on an individual child plan for " +
            "operation-level details."
        );

        return 0;
    }

    private static string? ValidateTopology(
        IReadOnlyList<string> childNames)
    {
        var names =
            new HashSet<string>(
                childNames,
                StringComparer.Ordinal
            );

        if (names.Count != childNames.Count)
        {
            return
                "The retained batch directory enumeration contained " +
                "duplicate literal child names.";
        }

        for (
            int index = 0;
            index < childNames.Count;
            index++)
        {
            string expected =
                $"plan-{index + 1:D6}";

            if (!names.Contains(expected))
            {
                return
                    $"Expected contiguous child {expected}, but it was " +
                    $"not present among the {childNames.Count:N0} direct " +
                    "batch entries. Unexpected entries, numbering gaps, " +
                    "and alternate spellings are rejected.";
            }
        }

        return null;
    }

    private static bool IsValidManifestChildName(
        string? childName)
    {
        if (
            string.IsNullOrEmpty(
                childName) ||
            childName is "." or "..")
        {
            return false;
        }

        return
            !childName.Contains('/') &&
            !childName.Contains('\\') &&
            !childName.Contains('\0');
    }

    private static string FormatOverall(
        DataRelativePathRepairPlanOverallStatus status)
    {
        return status switch
        {
            DataRelativePathRepairPlanOverallStatus
                .NotStarted =>
                    "NOT STARTED",

            DataRelativePathRepairPlanOverallStatus
                .InProgress =>
                    "IN PROGRESS",

            DataRelativePathRepairPlanOverallStatus
                .Applied =>
                    "APPLIED",

            DataRelativePathRepairPlanOverallStatus
                .RollbackInProgress =>
                    "ROLLBACK IN PROGRESS",

            DataRelativePathRepairPlanOverallStatus
                .RolledBack =>
                    "ROLLED BACK",

            DataRelativePathRepairPlanOverallStatus
                .RecoveryConflict =>
                    "RECOVERY CONFLICT",

            _ =>
                status.ToString()
        };
    }
}
