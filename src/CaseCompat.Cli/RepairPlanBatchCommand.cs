using CaseCompat.Core.Repair;
using CaseCompat.Core.Resolution;
using CaseCompat.Filesystem.Linux;

public static class RepairPlanBatchCommand
{
    public static int Run(string[] args)
    {
        if (args.Length != 5)
        {
            Console.Error.WriteLine(
                "Error: repair-plan-batch requires a Skyrim Data directory, " +
                "path-list file, batch directory, and manifest file name."
            );
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "Usage: casecompat repair-plan-batch " +
                "<Skyrim Data directory> <path-list file> " +
                "<batch directory> <manifest file name>"
            );

            return 2;
        }

        string dataRoot =
            args[1];

        string pathListFile =
            args[2];

        string batchDirectoryPath =
            args[3];

        string manifestName =
            args[4];

        /*
         * Validate the child manifest name before any batch child
         * directory can be published.
         *
         * This mirrors the manifest writer's direct-child constraint;
         * the authoritative writer still validates it again.
         */
        if (!IsValidManifestChildName(
                manifestName))
        {
            Console.Error.WriteLine(
                "Repair-plan-batch manifest file name must identify " +
                "exactly one direct child and cannot be '.', '..', " +
                "or contain path separators or NUL."
            );

            return 3;
        }

        string fullDataRoot;
        string fullBatchDirectoryPath;

        try
        {
            fullDataRoot =
                Path.GetFullPath(
                    dataRoot
                );

            fullBatchDirectoryPath =
                Path.GetFullPath(
                    batchDirectoryPath
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Repair-plan-batch path error: {ex.Message}"
            );

            return 3;
        }

        /*
         * The batch command itself creates child plan directories.
         * Never permit those metadata writes inside Skyrim Data.
         */
        if (IsPathAtOrBelow(
                fullDataRoot,
                fullBatchDirectoryPath))
        {
            Console.Error.WriteLine(
                "Repair-plan-batch batch directory must be outside " +
                "the Skyrim Data directory."
            );

            return 3;
        }

        string[] requestedPaths;

        try
        {
            requestedPaths =
                File.ReadAllLines(
                    pathListFile
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Repair-plan-batch input error: {ex.Message}"
            );

            return 3;
        }

        if (requestedPaths.Length == 0)
        {
            Console.Error.WriteLine(
                "Repair-plan-batch input contains no paths."
            );

            return 3;
        }

        var seen =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        for (
            int index = 0;
            index < requestedPaths.Length;
            index++)
        {
            string requestedPath =
                requestedPaths[index];

            if (string.IsNullOrWhiteSpace(
                    requestedPath))
            {
                Console.Error.WriteLine(
                    $"Repair-plan-batch input line {index + 1} is blank."
                );

                return 3;
            }

            if (!seen.Add(
                    requestedPath))
            {
                Console.Error.WriteLine(
                    $"Repair-plan-batch input contains a duplicate path " +
                    $"at line {index + 1}: {requestedPath}"
                );

                return 3;
            }
        }

        /*
         * Preflight the complete input before publishing any batch
         * metadata.
         *
         * This uses the existing resolver and repair projector only.
         * No repair operation is executed here.
         */
        var preflight =
            new List<
                (
                    string RequestedPath,
                    DataRelativePathRepairPlanProjection Projection
                )
            >(
                requestedPaths.Length
            );

        for (
            int index = 0;
            index < requestedPaths.Length;
            index++)
        {
            string requestedPath =
                requestedPaths[index];

            DataRelativePathResolution resolution;

            try
            {
                resolution =
                    DataRelativePathResolver.ResolveFile(
                        fullDataRoot,
                        requestedPath
                    );
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"Repair-plan-batch resolver error at line " +
                    $"{index + 1}: {ex.Message}"
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
                    $"Repair-plan-batch projection error at line " +
                    $"{index + 1}: {ex.Message}"
                );

                return 3;
            }

            preflight.Add(
                (
                    RequestedPath:
                        requestedPath,
                    Projection:
                        projection
                )
            );
        }

        LinuxNoFollowPathOpenResult batchOpen;

        try
        {
            batchOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    fullBatchDirectoryPath
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Repair-plan-batch directory open error: {ex.Message}"
            );

            return 5;
        }

        if (!batchOpen.Success)
        {
            Console.Error.WriteLine(
                "Repair-plan-batch directory could not be opened safely."
            );
            Console.Error.WriteLine(
                batchOpen.Error ??
                batchOpen.State.ToString()
            );

            return 5;
        }

        using LinuxNoFollowPathHandle batchDirectory =
            batchOpen.OpenedPath!;

        /*
         * This first batch-planning version requires a dedicated empty
         * output directory. The actual child creation remains
         * descriptor-relative and fail-closed if concurrent state
         * changes after this prepublication check.
         */
        try
        {
            if (
                Directory
                    .EnumerateFileSystemEntries(
                        batchDirectory.FullPath
                    )
                    .Any())
            {
                Console.Error.WriteLine(
                    "Repair-plan-batch batch directory must be empty."
                );

                return 5;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Repair-plan-batch directory inspection error: " +
                $"{ex.Message}"
            );

            return 5;
        }

        int projectedCount =
            preflight.Count(entry =>
                entry.Projection.HasPlan
            );

        int rejectedCount =
            preflight.Count -
            projectedCount;

        Console.WriteLine(
            "CaseCompat Repair Plan Batch"
        );
        Console.WriteLine(
            "============================"
        );
        Console.WriteLine();
        Console.WriteLine(
            $"Skyrim Data:       {fullDataRoot}"
        );
        Console.WriteLine(
            $"Path list:         {Path.GetFullPath(pathListFile)}"
        );
        Console.WriteLine(
            $"Batch directory:   {batchDirectory.FullPath}"
        );
        Console.WriteLine(
            $"Input paths:       {preflight.Count:N0}"
        );
        Console.WriteLine(
            $"Safe projections:  {projectedCount:N0}"
        );
        Console.WriteLine(
            $"Safe rejections:   {rejectedCount:N0}"
        );

        Console.WriteLine();
        Console.WriteLine(
            "Preflight:"
        );

        for (
            int index = 0;
            index < preflight.Count;
            index++)
        {
            var entry =
                preflight[index];

            if (entry.Projection.HasPlan)
            {
                Console.WriteLine(
                    $"[{index + 1}] PLAN    {entry.RequestedPath}"
                );

                continue;
            }

            Console.WriteLine(
                $"[{index + 1}] REJECT  {entry.RequestedPath}"
            );
            Console.WriteLine(
                $"      Projection: {entry.Projection.State}"
            );
            Console.WriteLine(
                $"      Topology:   {entry.Projection.TopologyState}"
            );

            if (!string.IsNullOrWhiteSpace(
                    entry.Projection.Error))
            {
                Console.WriteLine(
                    $"      Error:      {entry.Projection.Error}"
                );
            }
        }

        if (projectedCount == 0)
        {
            Console.WriteLine();
            Console.WriteLine(
                "No safe repair plans were projected."
            );
            Console.WriteLine(
                "Repair operations executed: NO"
            );
            Console.WriteLine(
                "Batch plan metadata created: 0"
            );

            return 0;
        }

        Console.WriteLine();
        Console.WriteLine(
            "Persisting safe child plans:"
        );

        int persistedCount =
            0;

        foreach (
            var entry
            in preflight)
        {
            if (!entry.Projection.HasPlan)
            {
                continue;
            }

            string childName =
                $"plan-{persistedCount + 1:D6}";

            LinuxCreateDirectoryAtResult create =
                LinuxCreateDirectoryAt.Create(
                    batchDirectory,
                    childName
                );

            if (!create.Success)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(
                    $"Repair-plan-batch could not create child " +
                    $"directory {childName}."
                );
                Console.Error.WriteLine(
                    create.Error ??
                    create.State.ToString()
                );

                WritePartialMetadataWarning(
                    persistedCount
                );

                return 6;
            }

            /*
             * Make the child-directory namespace entry durable before
             * publishing its plan manifest.
             */
            LinuxFsyncResult batchSync =
                LinuxFsync.Sync(
                    batchDirectory
                );

            if (!batchSync.Success)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(
                    $"Repair-plan-batch could not durably publish " +
                    $"child directory {childName}."
                );
                Console.Error.WriteLine(
                    batchSync.Error ??
                    batchSync.State.ToString()
                );

                WritePartialMetadataWarning(
                    persistedCount
                );

                return 6;
            }

            string childPath =
                Path.Combine(
                    batchDirectory.FullPath,
                    childName
                );

            Console.WriteLine();
            Console.WriteLine(
                $"--- {childName}: {entry.RequestedPath} ---"
            );

            /*
             * Deliberately delegate the authoritative persistence path
             * back to the existing single-plan command.
             *
             * It re-resolves and re-projects the path, creates the
             * schema-v2 manifest from resolver evidence, safely opens
             * this journal directory, publishes the manifest durably,
             * and reads it back for verification.
             *
             * A state change between preflight and this second pass
             * therefore fails closed rather than allowing the batch
             * preflight to authorize stale work.
             */
            int planResult =
                RepairPlanCommand.Run(
                    [
                        "repair-plan",
                        fullDataRoot,
                        entry.RequestedPath,
                        childPath,
                        manifestName
                    ]
                );

            if (planResult != 0)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(
                    $"Repair-plan-batch stopped because {childName} " +
                    $"did not persist successfully."
                );
                Console.Error.WriteLine(
                    $"Nested repair-plan exit: {planResult}"
                );

                WritePartialMetadataWarning(
                    persistedCount
                );

                return 7;
            }

            persistedCount++;
        }

        Console.WriteLine();
        Console.WriteLine(
            "CaseCompat Repair Plan Batch Summary"
        );
        Console.WriteLine(
            "===================================="
        );
        Console.WriteLine();
        Console.WriteLine(
            $"Input paths:       {preflight.Count:N0}"
        );
        Console.WriteLine(
            $"Safe rejections:   {rejectedCount:N0}"
        );
        Console.WriteLine(
            $"Plans persisted:   {persistedCount:N0}"
        );
        Console.WriteLine(
            "Repair operations executed: NO"
        );
        Console.WriteLine(
            "Each persisted child remains an independent " +
            "single-path repair plan."
        );
        Console.WriteLine(
            "No batch apply authority was created."
        );

        return 0;
    }

    private static bool IsPathAtOrBelow(
        string rootPath,
        string candidatePath)
    {
        string relative =
            Path.GetRelativePath(
                rootPath,
                candidatePath
            );

        if (
            string.Equals(
                relative,
                ".",
                StringComparison.Ordinal))
        {
            return true;
        }

        if (
            string.Equals(
                relative,
                "..",
                StringComparison.Ordinal))
        {
            return false;
        }

        if (
            relative.StartsWith(
                "../",
                StringComparison.Ordinal) ||
            relative.StartsWith(
                "..\\",
                StringComparison.Ordinal))
        {
            return false;
        }

        return
            !Path.IsPathRooted(
                relative
            );
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

    private static void WritePartialMetadataWarning(
        int persistedCount)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine(
            "IMPORTANT: batch planning stopped after metadata " +
            "publication had begun."
        );
        Console.Error.WriteLine(
            $"Previously persisted child plans: {persistedCount:N0}"
        );
        Console.Error.WriteLine(
            "Do not assume the batch directory is empty."
        );
        Console.Error.WriteLine(
            "No repair operations were requested by " +
            "repair-plan-batch."
        );
    }
}
