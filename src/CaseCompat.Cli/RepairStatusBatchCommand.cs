using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

public static class RepairStatusBatchCommand
{
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
        if (args.Length != 4)
        {
            Console.Error.WriteLine(
                "Error: repair-status-batch requires a batch directory, " +
                "manifest file name, and Skyrim Data directory."
            );

            Console.Error.WriteLine();

            Console.Error.WriteLine(
                "Usage: casecompat repair-status-batch <batch directory> " +
                "<manifest file name> <Skyrim Data directory>"
            );

            return 2;
        }

        if (!IsValidManifestChildName(args[2]))
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

        LinuxEnumerateDirectoryAtResult enumeration;

        try
        {
            enumeration =
                LinuxEnumerateDirectoryAt.Enumerate(
                    batchDirectory
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Batch directory enumeration error: {ex.Message}"
            );

            return 4;
        }

        if (!enumeration.Success)
        {
            Console.Error.WriteLine(
                "Repair-status-batch could not enumerate the retained " +
                "batch directory descriptor."
            );

            Console.Error.WriteLine(
                enumeration.Error ??
                enumeration.State.ToString()
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

        var inspectedPlans =
            new List<
                (
                    string ChildName,
                    DataRelativePathRepairPlanStatusInspection
                        Inspection
                )
            >(
                enumeration.ChildNames.Count
            );

        /*
         * Do not print a partial aggregate.
         *
         * Every child observed in the retained enumeration must
         * open and inspect successfully before any batch-level status
         * result is published.
         */
        foreach (string childName in enumeration.ChildNames)
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
                    $"Repair-status-batch could not open {childName}: " +
                    ex.Message
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
                        args[2],
                        args[3]
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
                    $"Repair-status-batch could not completely inspect " +
                    $"{childName}."
                );

                Console.Error.WriteLine(
                    $"Inspection state (internal): {inspection.State}"
                );

                if (!string.IsNullOrWhiteSpace(
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
            $"Manifest:           {args[2]}"
        );

        Console.WriteLine(
            $"Trusted Data:       {args[3]}"
        );

        Console.WriteLine(
            $"Observed plans:     {inspectedPlans.Count:N0}"
        );

        Console.WriteLine(
            "Original batch completeness: NOT RECORDED"
        );

        Console.WriteLine();

        Console.WriteLine(
            "The current batch format has no durable batch-summary " +
            "manifest."
        );

        Console.WriteLine(
            "This command reports every contiguous plan-* child observed " +
            "during descriptor-relative enumeration of the retained batch " +
            "directory, but cannot prove how many plans the original " +
            "batch-planning invocation intended to persist."
        );

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
