using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

public static class TargetedConsumerPlanCommand
{
    public static int Run(string[] args)
    {
        if (args.Length < 7 ||
            args.Length > 8)
        {
            Console.Error.WriteLine(
                "Error: targeted-consumer-plan requires a Data root, " +
                "Plugins.txt, loadorder.txt, Skyrim.ccc, exact requested " +
                "path, plan directory, and optional plan file name."
            );
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "Usage: casecompat targeted-consumer-plan <Data root> " +
                "<Plugins.txt> <loadorder.txt> <Skyrim.ccc> " +
                "<exact requested path> <plan directory> " +
                "[plan file name]"
            );

            return 2;
        }

        string exactRequestedPath =
            args[5];

        string planChildName =
            args.Length == 8
                ? args[7]
                : RepairCliDefaults.TargetedPlanChildName;

        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult
            discovery;

        try
        {
            discovery =
                TargetedConsumerDiscovery.Discover(
                    dataRoot:
                        args[1],
                    pluginsPath:
                        args[2],
                    loadOrderPath:
                        args[3],
                    cccPath:
                        args[4]
                );
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(
                $"Error: {ex.Message}"
            );

            return 4;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Targeted consumer-candidate discovery error: {ex.Message}"
            );

            return 3;
        }

        if (!discovery.CandidateEvidenceComplete)
        {
            Console.Error.WriteLine(
                "Targeted consumer-candidate discovery was not complete."
            );
            Console.Error.WriteLine(
                $"State: {discovery.State}"
            );

            if (!string.IsNullOrWhiteSpace(
                    discovery.Error))
            {
                Console.Error.WriteLine(
                    $"Error: {discovery.Error}"
                );
            }

            return 5;
        }

        DataRelativePathTargetedConsumerCaseRepairCandidate[] matches =
            discovery.Candidates
                .Where(
                    candidate =>
                        string.Equals(
                            candidate.AuthoritativeRequestedPath,
                            exactRequestedPath,
                            StringComparison.Ordinal
                        )
                )
                .ToArray();

        if (matches.Length != 1)
        {
            Console.Error.WriteLine(
                $"Error: {matches.Length} candidates matched the exact " +
                $"requested path '{exactRequestedPath}' " +
                "(exactly one is required)."
            );

            return 6;
        }

        DataRelativePathTargetedConsumerCaseRepairCandidate candidate =
            matches[0];

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

            return 7;
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

            return 7;
        }

        using LinuxNoFollowPathHandle dataRootHandle =
            dataRootOpen.OpenedPath!;

        DataRelativePathTargetedConsumerCaseRepairPlanProjection projection;

        try
        {
            projection =
                DataRelativePathTargetedConsumerCaseRepairPlanProjector
                    .Project(
                        dataRootHandle,
                        candidate
                    );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Targeted plan projection error: {ex.Message}"
            );

            return 8;
        }

        if (!projection.HasPlan)
        {
            Console.Error.WriteLine(
                "No targeted destination plan was projected."
            );
            Console.Error.WriteLine(
                $"Projection state: {projection.State}"
            );

            if (!string.IsNullOrWhiteSpace(
                    projection.Error))
            {
                Console.Error.WriteLine(
                    $"Error: {projection.Error}"
                );
            }

            return 8;
        }

        Guid planId =
            Guid.NewGuid();

        DateTimeOffset createdUtc =
            DateTimeOffset.UtcNow;

        DataRelativePathTargetedConsumerCaseRepairDurablePlanCreation
            creation;

        try
        {
            creation =
                DataRelativePathTargetedConsumerCaseRepairDurablePlan.Create(
                    dataRootHandle,
                    planId,
                    createdUtc,
                    projection
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Targeted durable plan creation error: {ex.Message}"
            );

            return 9;
        }

        if (!creation.Success)
        {
            Console.Error.WriteLine(
                "Targeted durable plan was not created."
            );
            Console.Error.WriteLine(
                $"Creation state:  {creation.State}"
            );

            if (creation.Admission is not null)
            {
                Console.Error.WriteLine(
                    $"Admission state: {creation.Admission.State}"
                );
            }

            if (!string.IsNullOrWhiteSpace(
                    creation.Error))
            {
                Console.Error.WriteLine(
                    $"Error:           {creation.Error}"
                );
            }

            return 9;
        }

        LinuxNoFollowPathOpenResult planDirectoryOpen;

        try
        {
            planDirectoryOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    args[6]
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Plan directory open error: {ex.Message}"
            );

            return 10;
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

            return 10;
        }

        using LinuxNoFollowPathHandle planDirectory =
            planDirectoryOpen.OpenedPath!;

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            creation.Record!;

        DataRelativePathTargetedConsumerCaseRepairDurablePlanWriterResult
            write;

        try
        {
            write =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanWriter
                    .CreateInitial(
                        planDirectory,
                        planChildName,
                        plan
                    );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Targeted durable plan write error: {ex.Message}"
            );

            return 11;
        }

        if (!write.Success)
        {
            Console.Error.WriteLine(
                "Targeted durable plan was not written."
            );
            Console.Error.WriteLine(
                $"Write state: {write.State}"
            );

            if (!string.IsNullOrWhiteSpace(
                    write.Error))
            {
                Console.Error.WriteLine(
                    $"Error:       {write.Error}"
                );
            }

            return 11;
        }

        DataRelativePathTargetedConsumerCaseRepairDurablePlanReaderResult
            verify;

        try
        {
            verify =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanReader
                    .Read(
                        planDirectory,
                        planChildName
                    );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Targeted durable plan verification read error: " +
                $"{ex.Message}"
            );
            WriteVerificationFailureWarning();

            return 12;
        }

        if (
            !verify.Success ||
            verify.Plan is null ||
            verify.Plan.PlanId != plan.PlanId)
        {
            Console.Error.WriteLine(
                "Targeted durable plan verification failed."
            );
            Console.Error.WriteLine(
                verify.Error ??
                verify.State.ToString()
            );
            WriteVerificationFailureWarning();

            return 12;
        }

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
            verifiedPlan =
                verify.Plan;

        Console.WriteLine(
            "CaseCompat Targeted Consumer-Case Repair Plan"
        );

        Console.WriteLine(
            "=============================================="
        );

        Console.WriteLine();

        Console.WriteLine(
            $"Plan ID:          {verifiedPlan.PlanId}"
        );

        Console.WriteLine(
            $"Created UTC:      {verifiedPlan.CreatedUtc:O}"
        );

        Console.WriteLine(
            $"Data root:        {verifiedPlan.DataRoot}"
        );

        Console.WriteLine(
            $"Requested path:   {verifiedPlan.RequestedPath}"
        );

        Console.WriteLine(
            $"Physical source:  " +
            $"{verifiedPlan.SourceSnapshot.PhysicalPath}"
        );

        Console.WriteLine(
            $"Source size:      {verifiedPlan.SourceSnapshot.Size:N0} bytes"
        );

        Console.WriteLine(
            $"Source SHA-256:   {verifiedPlan.SourceSnapshot.Sha256}"
        );

        Console.WriteLine(
            $"Source inode gen: {verifiedPlan.SourceInodeGeneration}"
        );

        Console.WriteLine(
            $"Operation count:  {verifiedPlan.Operations.Count}"
        );

        Console.WriteLine(
            $"Plan directory:   {planDirectory.FullPath}"
        );

        Console.WriteLine(
            $"Plan file:        {planChildName}"
        );

        Console.WriteLine(
            $"Plan SHA-256:     {verify.PlanSha256}"
        );

        Console.WriteLine();
        Console.WriteLine(
            "Planned operations:"
        );

        foreach (
            DataRelativePathRepairPlanOperation operation
            in verifiedPlan.Operations)
        {
            Console.WriteLine();

            Console.WriteLine(
                $"{operation.Kind}"
            );

            Console.WriteLine(
                $"  Destination: {operation.DestinationPath}"
            );

            if (!string.IsNullOrWhiteSpace(
                    operation.SourcePath))
            {
                Console.WriteLine(
                    $"  Source:      {operation.SourcePath}"
                );
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            "Repair operations executed: NO"
        );
        Console.WriteLine(
            "Durable plan created:       YES"
        );
        Console.WriteLine(
            "Candidate is not authorization: only plan metadata was " +
            "written to the plan directory."
        );

        return 0;
    }

    private static void WriteVerificationFailureWarning()
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine(
            "IMPORTANT: the plan write reported success before " +
            "verification failed."
        );
        Console.Error.WriteLine(
            "Do not assume no plan metadata was created, and do not " +
            "blindly rerun targeted-consumer-plan with the same plan " +
            "file name."
        );
        Console.Error.WriteLine(
            "Inspect the plan directory before retrying."
        );
    }
}
