using CaseCompat.Core.Repair;
using CaseCompat.Core.Resolution;
using CaseCompat.Filesystem.Linux;

public static class RepairPlanCommand
{
    public static int Run(string[] args)
    {
        if (args.Length != 5)
        {
            Console.Error.WriteLine(
                "Error: repair-plan requires a Data root, " +
                "Data-relative file path, journal directory, " +
                "and manifest child name."
            );

            return 2;
        }

        DataRelativePathResolution resolution;

        try
        {
            resolution =
                DataRelativePathResolver.ResolveFile(
                    args[1],
                    args[2]
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Repair-plan resolver error: {ex.Message}"
            );

            return 3;
        }

        DataRelativePathRepairPlanProjection projection;

        try
        {
            projection =
                DataRelativePathRepairPlanProjector.Project(
                    resolution
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Repair-plan projection error: {ex.Message}"
            );

            return 3;
        }

        if (!projection.HasPlan)
        {
            Console.Error.WriteLine(
                "No safe repair plan was projected."
            );
            Console.Error.WriteLine(
                $"Projection state: {projection.State}"
            );
            Console.Error.WriteLine(
                $"Topology state:   {projection.TopologyState}"
            );

            if (!string.IsNullOrWhiteSpace(
                    projection.Error))
            {
                Console.Error.WriteLine(
                    $"Error:            {projection.Error}"
                );
            }

            return 4;
        }

        DataRelativePathRepairSourceSnapshot
            sourceSnapshot =
                projection.SourceSnapshot!;

        DataRelativePathRepairDestinationParentSnapshot
            parentSnapshot =
                projection.DestinationParentSnapshot!;

        Guid planId =
            Guid.NewGuid();

        DateTimeOffset createdUtc =
            DateTimeOffset.UtcNow;

        DataRelativePathRepairPlanManifestCreation creation;

        try
        {
            creation =
                DataRelativePathRepairPlanManifest.Create(
                    planId,
                    createdUtc,
                    resolution.DataRoot,
                    resolution.RequestedPath,
                    sourceSnapshot,
                    parentSnapshot,
                    projection.Operations
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Repair-plan manifest creation error: {ex.Message}"
            );

            return 5;
        }

        if (!creation.Success)
        {
            Console.Error.WriteLine(
                "Repair-plan manifest creation failed."
            );
            Console.Error.WriteLine(
                creation.Error ??
                creation.State.ToString()
            );

            return 5;
        }

        LinuxNoFollowPathOpenResult journalOpen;

        try
        {
            journalOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    args[3]
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Journal directory open error: {ex.Message}"
            );

            return 6;
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

            return 6;
        }

        using LinuxNoFollowPathHandle journalDirectory =
            journalOpen.OpenedPath!;

        DataRelativePathRepairPlanManifestRecord manifest =
            creation.Manifest!;

        DataRelativePathRepairPlanManifestWriterResult write;

        try
        {
            write =
                DataRelativePathRepairPlanManifestWriter.CreateInitial(
                    journalDirectory,
                    args[4],
                    manifest
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Repair-plan manifest write error: {ex.Message}"
            );

            return 7;
        }

        if (!write.Success)
        {
            Console.Error.WriteLine(
                "Repair-plan manifest was not created."
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

            return 7;
        }

        DataRelativePathRepairPlanManifestReaderResult verify;

        try
        {
            verify =
                DataRelativePathRepairPlanManifestReader.Read(
                    journalDirectory,
                    args[4]
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Repair-plan verification read error: {ex.Message}"
            );

            return 8;
        }

        if (
            !verify.Success ||
            verify.Manifest is null ||
            verify.Manifest.PlanId != manifest.PlanId)
        {
            Console.Error.WriteLine(
                "Repair-plan manifest verification failed."
            );
            Console.Error.WriteLine(
                verify.Error ??
                verify.State.ToString()
            );

            return 8;
        }

        DataRelativePathRepairPlanManifestRecord
            verifiedManifest =
                verify.Manifest;

        Console.WriteLine(
            "CaseCompat Repair Plan"
        );
        Console.WriteLine(
            "======================"
        );
        Console.WriteLine();

        Console.WriteLine(
            $"Plan ID:          {verifiedManifest.PlanId}"
        );
        Console.WriteLine(
            $"Created UTC:      {verifiedManifest.CreatedUtc:O}"
        );
        Console.WriteLine(
            $"Data root:        {verifiedManifest.DataRoot}"
        );
        Console.WriteLine(
            $"Requested path:   {verifiedManifest.RequestedPath}"
        );
        Console.WriteLine(
            $"Physical source:  " +
            $"{verifiedManifest.SourceSnapshot.PhysicalPath}"
        );
        Console.WriteLine(
            $"Source size:      " +
            $"{verifiedManifest.SourceSnapshot.Size:N0} bytes"
        );
        Console.WriteLine(
            $"Source SHA-256:   " +
            verifiedManifest.SourceSnapshot.Sha256
        );
        Console.WriteLine(
            $"Operation count:  " +
            verifiedManifest.Operations.Count
        );
        Console.WriteLine(
            $"Journal dir:      {journalDirectory.FullPath}"
        );
        Console.WriteLine(
            $"Manifest:         {args[4]}"
        );

        Console.WriteLine();
        Console.WriteLine(
            "Planned operations:"
        );

        foreach (
            DataRelativePathRepairPlanManifestOperation entry
            in verifiedManifest.Operations)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"[{entry.Index}] {entry.Operation.Kind}"
            );
            Console.WriteLine(
                $"  Destination: {entry.Operation.DestinationPath}"
            );

            if (
                !string.IsNullOrWhiteSpace(
                    entry.Operation.SourcePath))
            {
                Console.WriteLine(
                    $"  Source:      {entry.Operation.SourcePath}"
                );
            }

            Console.WriteLine(
                $"  Journal:     {entry.JournalChildName}"
            );
        }

        Console.WriteLine();
        Console.WriteLine(
            "Repair operations executed: NO"
        );
        Console.WriteLine(
            "Plan manifest created:      YES"
        );
        Console.WriteLine(
            "Only plan metadata was written to the journal directory."
        );

        return 0;
    }
}
