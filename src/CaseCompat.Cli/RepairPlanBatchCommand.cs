using CaseCompat.Core.Repair;
using CaseCompat.Core.Resolution;
using CaseCompat.Filesystem.Linux;

public static class RepairPlanBatchCommand
{
    private const string BatchManifestName =
        "batch-manifest.json";

    public static int Run(string[] args)
    {
        if (args.Length < 4 ||
            args.Length > 5)
        {
            Console.Error.WriteLine(
                "Error: repair-plan-batch requires a Skyrim Data directory, " +
                "path-list file, batch directory, and optional manifest file name."
            );
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "Usage: casecompat repair-plan-batch " +
                "<Skyrim Data directory> <path-list file> " +
                "<batch directory> [manifest file name]"
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
            args.Length == 5
                ? args[4]
                : RepairCliDefaults.PlanManifestChildName;

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
            new List<BatchPreflightEntry>(
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
                    DataRelativePathRepairPlanProjector
                        .ProjectBatchCandidate(
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
                new BatchPreflightEntry(
                    RequestedPath:
                        requestedPath,
                    Resolution:
                        resolution,
                    Projection:
                        projection,
                    CoverageDecision:
                        null
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

        /*
         * Batch candidates are only technical projections at this point.
         *
         * Promote them to batch-authorized plans only after the complete
         * candidate set proves aggregate namespace coverage.
         */
        int[] candidateInputIndexes =
            preflight
                .Select(
                    (
                        entry,
                        index
                    ) =>
                        (
                            Entry:
                                entry,
                            Index:
                                index
                        )
                )
                .Where(item =>
                    item.Entry.Projection.HasPlan
                )
                .Select(item =>
                    item.Index
                )
                .ToArray();

        DataRelativePathRepairPlanProjection[]
            candidateProjections =
                candidateInputIndexes
                    .Select(index =>
                        preflight[index]
                            .Projection
                    )
                    .ToArray();

        DataRelativePathRepairBatchCoverageAuthorization
            coverageAuthorization =
                DataRelativePathRepairBatchCoverageAuthorizer
                    .Authorize(
                        candidateProjections
                    );

        for (
            int candidateIndex = 0;
            candidateIndex <
                candidateInputIndexes.Length;
            candidateIndex++)
        {
            int inputIndex =
                candidateInputIndexes[
                    candidateIndex
                ];

            preflight[inputIndex] =
                preflight[inputIndex] with
                {
                    CoverageDecision =
                        coverageAuthorization
                            .Decisions[
                                candidateIndex
                            ]
                };
        }

        int projectedCount =
            preflight.Count(entry =>
                entry.IsAuthorized
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

            if (entry.IsAuthorized)
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

            if (
                entry.Projection.HasPlan &&
                entry.CoverageDecision is not null &&
                !entry.CoverageDecision.Authorized)
            {
                Console.WriteLine(
                    $"      Coverage:   " +
                    $"{entry.CoverageDecision.State}"
                );

                if (!string.IsNullOrWhiteSpace(
                        entry.CoverageDecision.Error))
                {
                    Console.WriteLine(
                        $"      Coverage error: " +
                        $"{entry.CoverageDecision.Error}"
                    );
                }
            }

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
                "No child plan directories will be created."
            );
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine(
                "Persisting safe child plans:"
            );
        }

        /*
         * Re-enumerate aggregate physical namespace coverage immediately
         * before publishing the first child directory.
         *
         * This catches namespace changes after the reporting preflight
         * without falling back to standalone per-child authorization.
         */
        DataRelativePathRepairBatchCoverageAuthorization
            publicationCoverage =
                DataRelativePathRepairBatchCoverageAuthorizer
                    .Authorize(
                        candidateProjections
                    );

        for (
            int candidateIndex = 0;
            candidateIndex <
                candidateInputIndexes.Length;
            candidateIndex++)
        {
            int inputIndex =
                candidateInputIndexes[
                    candidateIndex
                ];

            if (
                preflight[inputIndex].IsAuthorized &&
                !publicationCoverage
                    .Decisions[
                        candidateIndex
                    ]
                    .Authorized)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(
                    "Repair-plan-batch aggregate namespace coverage " +
                    "changed after preflight."
                );
                Console.Error.WriteLine(
                    $"Input: {preflight[inputIndex].RequestedPath}"
                );
                Console.Error.WriteLine(
                    $"Coverage state: " +
                    $"{publicationCoverage.Decisions[candidateIndex].State}"
                );

                if (!string.IsNullOrWhiteSpace(
                        publicationCoverage
                            .Decisions[
                                candidateIndex
                            ]
                            .Error))
                {
                    Console.Error.WriteLine(
                        publicationCoverage
                            .Decisions[
                                candidateIndex
                            ]
                            .Error
                    );
                }

                Console.Error.WriteLine(
                    "No child plan directories were published."
                );

                return 6;
            }
        }

        int persistedCount =
            0;

        var batchChildren =
            new List<
                DataRelativePathRepairBatchManifestChild
            >(
                projectedCount
            );

        foreach (
            var entry
            in preflight)
        {
            if (!entry.IsAuthorized)
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

            Console.WriteLine();
            Console.WriteLine(
                $"--- {childName}: {entry.RequestedPath} ---"
            );

            /*
             * The complete batch has already granted aggregate namespace
             * authority to this exact technical projection.
             *
             * Do not call standalone repair-plan here: doing so would
             * intentionally reapply standalone sparse-branch policy and
             * revoke valid collective authority.
             *
             * Instead, create the schema-v2 manifest directly from the
             * resolver evidence and snapshots retained by this authorized
             * batch preflight, then publish it through the ordinary durable
             * manifest writer.
             */
            LinuxOpenChildDirectoryReadOnlyAtResult
                planDirectoryOpen;

            try
            {
                planDirectoryOpen =
                    LinuxOpenChildDirectoryReadOnlyAt.Open(
                        batchDirectory,
                        childName
                    );
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(
                    $"Repair-plan-batch could not safely open {childName} " +
                    $"for manifest publication: {ex.Message}"
                );

                WritePartialMetadataWarning(
                    persistedCount
                );

                return 7;
            }

            if (
                !planDirectoryOpen.Success ||
                planDirectoryOpen.OpenedDirectory is null)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(
                    $"Repair-plan-batch could not safely open {childName} " +
                    "for manifest publication."
                );
                Console.Error.WriteLine(
                    planDirectoryOpen.Error ??
                    planDirectoryOpen.State.ToString()
                );

                WritePartialMetadataWarning(
                    persistedCount
                );

                return 7;
            }

            DataRelativePathRepairPlanManifestRecord
                expectedManifest;

            using (
                LinuxNoFollowPathHandle planDirectory =
                    planDirectoryOpen.OpenedDirectory)
            {
                DataRelativePathRepairPlanManifestCreation
                    creation;

                try
                {
                    creation =
                        DataRelativePathRepairPlanManifest
                            .CreateFromResolution(
                                Guid.NewGuid(),
                                DateTimeOffset.UtcNow,
                                entry.Resolution,
                                entry.Projection
                                    .SourceSnapshot!,
                                entry.Projection
                                    .DestinationParentSnapshot!,
                                entry.Projection
                                    .Operations
                            );
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine();
                    Console.Error.WriteLine(
                        $"Repair-plan-batch manifest creation failed for " +
                        $"{childName}: {ex.Message}"
                    );

                    WritePartialMetadataWarning(
                        persistedCount
                    );

                    return 7;
                }

                if (
                    !creation.Success ||
                    creation.Manifest is null)
                {
                    Console.Error.WriteLine();
                    Console.Error.WriteLine(
                        $"Repair-plan-batch manifest creation failed for " +
                        $"{childName}."
                    );
                    Console.Error.WriteLine(
                        creation.Error ??
                        creation.State.ToString()
                    );

                    WritePartialMetadataWarning(
                        persistedCount
                    );

                    return 7;
                }

                expectedManifest =
                    creation.Manifest;

                DataRelativePathRepairPlanManifestWriterResult
                    write;

                try
                {
                    write =
                        DataRelativePathRepairPlanManifestWriter
                            .CreateInitial(
                                planDirectory,
                                manifestName,
                                expectedManifest
                            );
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine();
                    Console.Error.WriteLine(
                        $"Repair-plan-batch manifest write failed for " +
                        $"{childName}: {ex.Message}"
                    );

                    WritePartialMetadataWarning(
                        persistedCount
                    );

                    return 7;
                }

                if (!write.Success)
                {
                    Console.Error.WriteLine();
                    Console.Error.WriteLine(
                        $"Repair-plan-batch manifest was not durably " +
                        $"created for {childName}."
                    );
                    Console.Error.WriteLine(
                        $"Write state: {write.State}"
                    );

                    if (!string.IsNullOrWhiteSpace(
                            write.Error))
                    {
                        Console.Error.WriteLine(
                            $"Error: {write.Error}"
                        );
                    }

                    WritePartialMetadataWarning(
                        persistedCount
                    );

                    return 7;
                }
            }

            /*
             * The child manifest is now durably published. Count it before
             * independent readback so a subsequent verification failure
             * reports that durable metadata may already exist.
             */
            persistedCount++;

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
                Console.Error.WriteLine();
                Console.Error.WriteLine(
                    $"Repair-plan-batch could not reopen {childName} " +
                    $"descriptor-relatively after persistence: " +
                    $"{ex.Message}"
                );

                WritePartialMetadataWarning(
                    persistedCount
                );

                return 8;
            }

            if (!childOpen.Success)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(
                    $"Repair-plan-batch could not safely reopen " +
                    $"{childName} after persistence."
                );

                Console.Error.WriteLine(
                    childOpen.Error ??
                    childOpen.State.ToString()
                );

                WritePartialMetadataWarning(
                    persistedCount
                );

                return 8;
            }

            using LinuxOpenedChildHandle childDirectory =
                childOpen.OpenedChild!;

            DataRelativePathRepairPlanStatusInspection
                childInspection;

            try
            {
                childInspection =
                    DataRelativePathRepairPlanStatusInspector
                        .Inspect(
                            childDirectory,
                            manifestName,
                            fullDataRoot
                        );
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(
                    $"Repair-plan-batch could not inspect persisted " +
                    $"{childName}: {ex.Message}"
                );

                WritePartialMetadataWarning(
                    persistedCount
                );

                return 8;
            }

            if (
                !childInspection.Success ||
                childInspection.Manifest is null ||
                childInspection.ManifestRead is null ||
                !childInspection.ManifestRead.Success ||
                childInspection.ManifestRead.ManifestSha256 is null)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(
                    $"Repair-plan-batch could not bind persisted child " +
                    $"{childName} to a validated exact-byte manifest."
                );

                Console.Error.WriteLine(
                    childInspection.Error ??
                    childInspection.State.ToString()
                );

                WritePartialMetadataWarning(
                    persistedCount
                );

                return 8;
            }

            DataRelativePathRepairPlanManifestRecord
                persistedManifest =
                    childInspection.Manifest;

            if (
                persistedManifest.SchemaVersion !=
                    DataRelativePathRepairPlanManifestRecord
                        .SchemaVersion2 ||
                persistedManifest.PlanId !=
                    expectedManifest.PlanId ||
                !string.Equals(
                    persistedManifest.RequestedPath,
                    entry.RequestedPath,
                    StringComparison.Ordinal))
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(
                    $"Repair-plan-batch persisted child {childName} " +
                    "no longer matches the plan this batch invocation " +
                    "requested."
                );

                Console.Error.WriteLine(
                    $"Expected requested path: {entry.RequestedPath}"
                );

                Console.Error.WriteLine(
                    $"Observed requested path: " +
                    $"{persistedManifest.RequestedPath}"
                );

                Console.Error.WriteLine(
                    $"Observed schema version: " +
                    $"{persistedManifest.SchemaVersion}"
                );

                WritePartialMetadataWarning(
                    persistedCount
                );

                return 8;
            }

            batchChildren.Add(
                new(
                    ChildName:
                        childName,
                    PlanId:
                        persistedManifest.PlanId,
                    ManifestSha256:
                        childInspection
                            .ManifestRead
                            .ManifestSha256
                )
            );
        }

        /*
         * The durable root-level batch manifest is the completion
         * boundary.
         *
         * Nothing above this point represents a complete batch. Persisted
         * child manifests are immutable per-path plan records, but they do
         * not independently carry the aggregate namespace authority proved
         * for the complete candidate set.
         *
         * The intended set is not complete until the schema-v2 batch
         * manifest has durably bound exact child membership,
         * safe-rejection accounting, and coverage-policy provenance.
         *
         * This also applies to the zero-child case: an all-safe-rejected
         * invocation records its input/rejection accounting with an empty
         * child set instead of returning with no durable completion
         * evidence.
         */
        DataRelativePathRepairBatchManifestCreation
            batchCreation;

        try
        {
            batchCreation =
                DataRelativePathRepairBatchManifest
                    .CreateCoverageAuthorized(
                        batchId:
                        Guid.NewGuid(),
                    createdUtc:
                        DateTimeOffset.UtcNow,
                    dataRoot:
                        fullDataRoot,
                    childManifestName:
                        manifestName,
                    inputPathCount:
                        preflight.Count,
                    safeRejectionCount:
                        rejectedCount,
                        children:
                            batchChildren
                    );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "Repair-plan-batch could not create the batch " +
                $"completion record: {ex.Message}"
            );

            WritePartialMetadataWarning(
                persistedCount
            );

            return 9;
        }

        if (!batchCreation.Success)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "Repair-plan-batch could not create the batch " +
                "completion record."
            );

            Console.Error.WriteLine(
                batchCreation.Error ??
                batchCreation.State.ToString()
            );

            WritePartialMetadataWarning(
                persistedCount
            );

            return 9;
        }

        DataRelativePathRepairBatchManifestRecord batchManifest =
            batchCreation.Manifest!;

        DataRelativePathRepairBatchManifestWriterResult
            batchWrite;

        try
        {
            batchWrite =
                DataRelativePathRepairBatchManifestWriter
                    .CreateInitial(
                        batchDirectory,
                        BatchManifestName,
                        batchManifest
                    );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "Repair-plan-batch batch-completion write error: " +
                ex.Message
            );

            WritePartialMetadataWarning(
                persistedCount
            );

            return 9;
        }

        if (!batchWrite.Success)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "Repair-plan-batch did not establish durable batch " +
                "completion."
            );

            Console.Error.WriteLine(
                $"Batch-manifest write state: {batchWrite.State}"
            );

            if (!string.IsNullOrWhiteSpace(
                    batchWrite.Error))
            {
                Console.Error.WriteLine(
                    $"Error: {batchWrite.Error}"
                );
            }

            if (batchWrite.ManifestEntryChanged)
            {
                WriteUncertainBatchCompletionWarning(
                    persistedCount
                );
            }
            else
            {
                WritePartialMetadataWarning(
                    persistedCount
                );
            }

            return 9;
        }

        /*
         * Match the single-plan command's durability/readback pattern.
         *
         * Writer success is the durable publication boundary. The
         * independent readback makes the command fail loudly instead of
         * claiming success if the newly durable completion record cannot
         * immediately be reopened and validated.
         */
        DataRelativePathRepairBatchManifestReaderResult
            batchVerify;

        try
        {
            batchVerify =
                DataRelativePathRepairBatchManifestReader.Read(
                    batchDirectory,
                    BatchManifestName
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "Repair-plan-batch batch-completion verification read " +
                $"error: {ex.Message}"
            );

            WriteBatchVerificationFailureWarning(
                persistedCount
            );

            return 10;
        }

        if (
            !batchVerify.Success ||
            batchVerify.Manifest is null ||
            !BatchManifestMatchesExpected(
                batchManifest,
                batchVerify.Manifest))
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "Repair-plan-batch batch-completion verification failed."
            );

            Console.Error.WriteLine(
                batchVerify.Error ??
                batchVerify.State.ToString()
            );

            WriteBatchVerificationFailureWarning(
                persistedCount
            );

            return 10;
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
            $"Batch manifest:    {BatchManifestName}"
        );
        Console.WriteLine(
            $"Batch ID:          {batchManifest.BatchId}"
        );
        Console.WriteLine(
            "Batch completion:   DURABLE AND READ BACK"
        );
        Console.WriteLine(
            "Repair operations executed: NO"
        );
        Console.WriteLine(
            "Each persisted child records one immutable single-path " +
            "repair plan."
        );
        Console.WriteLine(
            "The schema-v2 batch manifest records exact child membership, " +
            "safe-rejection accounting, and aggregate namespace-coverage " +
            "provenance."
        );
        Console.WriteLine(
            "repair-apply-batch must freshly revalidate aggregate coverage " +
            "and durably establish batch-wide apply authorization before " +
            "an unstarted child may mutate."
        );
        Console.WriteLine();
        Console.WriteLine(
            "Next step: run repair-status-batch to independently verify " +
            "the completed batch topology and child membership."
        );

        return 0;
    }

    private sealed record BatchPreflightEntry(
        string RequestedPath,
        DataRelativePathResolution Resolution,
        DataRelativePathRepairPlanProjection Projection,
        DataRelativePathRepairBatchCoverageDecision?
            CoverageDecision)
    {
        public bool IsAuthorized =>
            Projection.HasPlan &&
            CoverageDecision?.Authorized == true;
    }

    private static bool BatchManifestMatchesExpected(
        DataRelativePathRepairBatchManifestRecord expected,
        DataRelativePathRepairBatchManifestRecord observed)
    {
        if (
            expected.SchemaVersion != observed.SchemaVersion ||
            expected.CoveragePolicyVersion !=
                observed.CoveragePolicyVersion ||
            expected.BatchId != observed.BatchId ||
            expected.CreatedUtc != observed.CreatedUtc ||
            !string.Equals(
                expected.DataRoot,
                observed.DataRoot,
                StringComparison.Ordinal) ||
            !string.Equals(
                expected.ChildManifestName,
                observed.ChildManifestName,
                StringComparison.Ordinal) ||
            expected.InputPathCount != observed.InputPathCount ||
            expected.SafeRejectionCount !=
                observed.SafeRejectionCount ||
            expected.Children.Count != observed.Children.Count)
        {
            return false;
        }

        for (
            int index = 0;
            index < expected.Children.Count;
            index++)
        {
            DataRelativePathRepairBatchManifestChild
                expectedChild =
                    expected.Children[index];

            DataRelativePathRepairBatchManifestChild
                observedChild =
                    observed.Children[index];

            if (
                !string.Equals(
                    expectedChild.ChildName,
                    observedChild.ChildName,
                    StringComparison.Ordinal) ||
                expectedChild.PlanId != observedChild.PlanId ||
                !string.Equals(
                    expectedChild.ManifestSha256,
                    observedChild.ManifestSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
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

    private static void
        WriteUncertainBatchCompletionWarning(
            int persistedCount)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine(
            "IMPORTANT: batch-completion publication changed the batch " +
            "directory namespace before the writer reported failure."
        );
        Console.Error.WriteLine(
            $"Previously persisted child plans: {persistedCount:N0}"
        );
        Console.Error.WriteLine(
            $"The named completion entry {BatchManifestName} may now " +
            "exist, but this invocation did not establish durable success."
        );
        Console.Error.WriteLine(
            "Do not blindly rerun repair-plan-batch against this " +
            "directory."
        );
        Console.Error.WriteLine(
            "Inspect it with repair-status-batch before deciding what " +
            "to do next."
        );
        Console.Error.WriteLine(
            "No repair operations were requested by repair-plan-batch."
        );
    }

    private static void
        WriteBatchVerificationFailureWarning(
            int persistedCount)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine(
            "IMPORTANT: the batch-completion writer reported durable " +
            "success before independent readback verification failed."
        );
        Console.Error.WriteLine(
            $"Previously persisted child plans: {persistedCount:N0}"
        );
        Console.Error.WriteLine(
            $"The durable completion entry {BatchManifestName} may be " +
            "present."
        );
        Console.Error.WriteLine(
            "Do not blindly rerun repair-plan-batch with the same batch " +
            "directory."
        );
        Console.Error.WriteLine(
            "Use repair-status-batch to independently inspect the batch."
        );
        Console.Error.WriteLine(
            "No repair operations were requested by repair-plan-batch."
        );
    }

    private static void WritePartialMetadataWarning(
        int persistedCount)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine(
            "IMPORTANT: batch planning stopped before durable " +
            "batch completion."
        );
        Console.Error.WriteLine(
            $"Previously persisted child plans: {persistedCount:N0}"
        );
        Console.Error.WriteLine(
            "Do not assume the batch directory is empty."
        );
        Console.Error.WriteLine(
            "This invocation did not establish durable batch completion."
        );
        Console.Error.WriteLine(
            "No repair operations were requested by " +
            "repair-plan-batch."
        );
    }
}
