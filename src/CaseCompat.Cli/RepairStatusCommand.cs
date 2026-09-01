using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

public static class RepairStatusCommand
{
    public static int Run(string[] args)
    {
        if (args.Length != 4)
        {
            Console.Error.WriteLine(
                "Error: repair-status requires a journal directory, " +
                "manifest child name, and trusted Data root."
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

        if (!journalOpen.Success)
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
            journalOpen.OpenedPath!;

        DataRelativePathRepairPlanStatusInspection inspection;

        try
        {
            inspection =
                DataRelativePathRepairPlanStatusInspector.Inspect(
                    journalDirectory,
                    args[2],
                    args[3]
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Repair-status inspection error: {ex.Message}"
            );

            return 4;
        }

        if (!inspection.Success)
        {
            Console.Error.WriteLine(
                "Repair-status inspection failed."
            );
            Console.Error.WriteLine(
                $"Inspection state: {inspection.State}"
            );

            if (!string.IsNullOrWhiteSpace(
                    inspection.Error))
            {
                Console.Error.WriteLine(
                    $"Error: {inspection.Error}"
                );
            }

            return 4;
        }

        DataRelativePathRepairPlanManifestRecord manifest =
            inspection.Manifest!;

        Console.WriteLine(
            "CaseCompat Repair Status"
        );
        Console.WriteLine(
            "========================"
        );
        Console.WriteLine();

        Console.WriteLine(
            $"Plan ID:           {manifest.PlanId}"
        );
        Console.WriteLine(
            $"Created UTC:       {manifest.CreatedUtc:O}"
        );
        Console.WriteLine(
            $"Manifest Data:     {manifest.DataRoot}"
        );
        Console.WriteLine(
            $"Trusted Data:      {Path.GetFullPath(args[3])}"
        );
        Console.WriteLine(
            $"Requested path:    {manifest.RequestedPath}"
        );
        Console.WriteLine(
            $"Physical source:   {manifest.SourceSnapshot.PhysicalPath}"
        );
        Console.WriteLine(
            $"Operation count:   {manifest.Operations.Count}"
        );
        Console.WriteLine(
            $"Overall status:    " +
            FormatOverall(
                inspection.OverallStatus!.Value
            )
        );

        Console.WriteLine();
        Console.WriteLine(
            "Operations:"
        );

        foreach (
            DataRelativePathRepairPlanOperationStatus status
            in inspection.OperationStatuses)
        {
            DataRelativePathRepairPlanManifestOperation entry =
                status.Entry;

            Console.WriteLine();
            Console.WriteLine(
                $"[{entry.Index}] {entry.Operation.Kind}"
            );
            Console.WriteLine(
                $"  Destination:   {entry.Operation.DestinationPath}"
            );
            Console.WriteLine(
                $"  Journal:       {entry.JournalChildName}"
            );
            Console.WriteLine(
                $"  Durable state: {FormatOperation(status.State)}"
            );
        }

        Console.WriteLine();
        Console.WriteLine(
            "Read-only status inspection: no files were modified."
        );

        return 0;
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

    private static string FormatOperation(
        DataRelativePathRepairPlanObservedOperationState state)
    {
        return state switch
        {
            DataRelativePathRepairPlanObservedOperationState
                .NotStarted =>
                    "NOT STARTED (journal absent)",

            _ =>
                state.ToString()
        };
    }
}
