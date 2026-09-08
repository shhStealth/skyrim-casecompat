using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

public static class TargetedConsumerRollbackCommand
{
    public static int Run(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine(
                "Error: targeted-consumer-rollback requires a journal " +
                "directory and a plan ID."
            );
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "Usage: casecompat targeted-consumer-rollback " +
                "<journal directory> <plan ID>"
            );

            return 2;
        }

        if (!Guid.TryParse(
                args[2],
                out Guid planId))
        {
            Console.Error.WriteLine(
                $"Error: '{args[2]}' is not a valid plan ID (GUID)."
            );

            return 2;
        }

        LinuxNoFollowPathOpenResult journalDirectoryOpen;

        try
        {
            journalDirectoryOpen =
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

        if (!journalDirectoryOpen.Success)
        {
            Console.Error.WriteLine(
                "Journal directory could not be opened safely."
            );
            Console.Error.WriteLine(
                journalDirectoryOpen.Error ??
                journalDirectoryOpen.State.ToString()
            );

            return 3;
        }

        using LinuxNoFollowPathHandle journalDirectory =
            journalDirectoryOpen.OpenedPath!;

        DataRelativePathTargetedConsumerCaseRepairApplyRollbackResult
            rollback;

        try
        {
            rollback =
                DataRelativePathTargetedConsumerCaseRepairApplyRollback
                    .Rollback(
                        journalDirectory,
                        planId,
                        DateTimeOffset.UtcNow
                    );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Targeted rollback error: {ex.Message}"
            );

            return 4;
        }

        Console.WriteLine(
            "CaseCompat Targeted Consumer-Case Repair Rollback"
        );

        Console.WriteLine(
            "=================================================="
        );

        Console.WriteLine();

        Console.WriteLine(
            $"Plan ID:          {rollback.PlanId}"
        );

        Console.WriteLine(
            $"Destination:      " +
            $"{rollback.DestinationPath ?? "(unknown)"}"
        );

        Console.WriteLine(
            $"Rollback state:   {rollback.State}"
        );

        Console.WriteLine(
            $"Rollback journal: " +
            $"{rollback.RolledBackJournalChildName ?? "(not written)"}"
        );

        if (!string.IsNullOrWhiteSpace(
                rollback.Error))
        {
            Console.WriteLine(
                $"Error:            {rollback.Error}"
            );
        }

        Console.WriteLine();

        if (rollback.Success)
        {
            Console.WriteLine(
                "Destination removed: YES"
            );

            return 0;
        }

        Console.WriteLine(
            "Destination removed: NO"
        );

        if (
            rollback.State ==
            DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                .DestinationIdentityMismatch)
        {
            Console.WriteLine(
                "The current destination does not have the exact file " +
                "incarnation this apply published. Rollback refuses to " +
                "remove a file it cannot prove it created."
            );
        }

        return 5;
    }
}
