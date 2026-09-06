using CaseCompat.Core.Repair;
using CaseCompat.Core.Resolution;
using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;

public static class RepairPlanAggregateNamespaceBatchCommand
{
    private const string BatchManifestName =
        "batch-manifest.json";

    internal sealed record TestHooks(
        Action? AfterGate1BeforeGate2 = null,
        Action<int>? AfterChildPublishedBeforeReadback = null,
        Action? AfterAllChildReadbacksBeforeGate3 = null
    );

    public static int Run(string[] args)
    {
        return Run(
            args,
            hooks:
                null
        );
    }

    internal static int Run(
        string[] args,
        TestHooks? hooks)
    {
        ArgumentNullException.ThrowIfNull(
            args
        );

        if (
            args.Length < 5 ||
            args.Length > 6)
        {
            WriteUsageError();
            return 2;
        }

        string manifestName =
            args.Length == 6
                ? args[5]
                : RepairCliDefaults.PlanManifestChildName;

        if (!IsValidChildName(
                manifestName))
        {
            Console.Error.WriteLine(
                "Repair-plan-aggregate-namespace-batch plan manifest file " +
                "name must identify exactly one direct child and cannot " +
                "be '.', '..', contain path separators, or contain NUL."
            );
            return 2;
        }

        string fullDataRoot;
        string fullPathList;
        string fullSidecarPath;
        string fullBatchDirectory;

        try
        {
            fullDataRoot =
                Path.GetFullPath(
                    args[1]
                );

            fullPathList =
                Path.GetFullPath(
                    args[2]
                );

            fullSidecarPath =
                Path.GetFullPath(
                    args[3]
                );

            fullBatchDirectory =
                Path.GetFullPath(
                    args[4]
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                "Repair-plan-aggregate-namespace-batch path error: " +
                ex.Message
            );
            return 3;
        }

        string? sidecarParent =
            Path.GetDirectoryName(
                fullSidecarPath
            );

        string sidecarName =
            Path.GetFileName(
                fullSidecarPath
            );

        if (
            string.IsNullOrWhiteSpace(
                sidecarParent) ||
            !IsValidChildName(
                sidecarName))
        {
            Console.Error.WriteLine(
                "Repair-plan-aggregate-namespace-batch aggregate namespace " +
                "manifest path must identify one existing direct-child file."
            );
            return 3;
        }

        if (IsPathAtOrBelow(
                fullDataRoot,
                fullBatchDirectory))
        {
            Console.Error.WriteLine(
                "Repair-plan-aggregate-namespace-batch batch directory must " +
                "be outside the Skyrim Data directory."
            );
            return 3;
        }

        string[] requestedPaths;

        try
        {
            requestedPaths =
                File.ReadAllLines(
                    fullPathList
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                "Repair-plan-aggregate-namespace-batch input error: " +
                ex.Message
            );
            return 3;
        }

        if (requestedPaths.Length == 0)
        {
            Console.Error.WriteLine(
                "Repair-plan-aggregate-namespace-batch input contains no paths."
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
                    $"Repair-plan-aggregate-namespace-batch input line " +
                    $"{index + 1} is blank."
                );
                return 3;
            }

            if (!seen.Add(
                    requestedPath))
            {
                Console.Error.WriteLine(
                    "Repair-plan-aggregate-namespace-batch input contains " +
                    $"a duplicate path at line {index + 1}: {requestedPath}"
                );
                return 3;
            }
        }

        Guid batchId =
            Guid.NewGuid();

        DateTimeOffset createdUtc =
            DateTimeOffset.UtcNow;

        DataRelativePathRepairBatchAggregateNamespacePlanningCandidate[]
            candidates =
                new DataRelativePathRepairBatchAggregateNamespacePlanningCandidate[
                    requestedPaths.Length
                ];

        /*
         * Build the complete immutable candidate set before opening the
         * output batch directory or reading aggregate namespace authority.
         * C2 is all-input: one failed projection blocks the whole invocation.
         */
        for (
            int index = 0;
            index < requestedPaths.Length;
            index++)
        {
            string requestedPath =
                requestedPaths[index];

            DataRelativePathResolution resolution;
            DataRelativePathRepairPlanProjection projection;

            try
            {
                resolution =
                    DataRelativePathResolver.ResolveFile(
                        fullDataRoot,
                        requestedPath
                    );

                projection =
                    DataRelativePathRepairPlanProjector
                        .ProjectAggregateAlternateBranchBatchCandidate(
                            resolution
                        );
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"Repair-plan-aggregate-namespace-batch candidate " +
                    $"projection failed at line {index + 1}: {ex.Message}"
                );
                Console.Error.WriteLine(
                    "No child plan directories were published."
                );
                return 4;
            }

            if (
                !projection.HasPlan ||
                projection.SourceSnapshot is null ||
                projection.DestinationParentSnapshot is null)
            {
                Console.Error.WriteLine(
                    "Repair-plan-aggregate-namespace-batch requires every " +
                    "input to project as an aggregate alternate-branch " +
                    "candidate."
                );
                Console.Error.WriteLine(
                    $"Input:      {requestedPath}"
                );
                Console.Error.WriteLine(
                    $"Projection: {projection.State}"
                );
                Console.Error.WriteLine(
                    $"Topology:   {projection.TopologyState}"
                );

                if (!string.IsNullOrWhiteSpace(
                        projection.Error))
                {
                    Console.Error.WriteLine(
                        $"Error:      {projection.Error}"
                    );
                }

                Console.Error.WriteLine(
                    "No child plan directories were published."
                );
                return 4;
            }

            DataRelativePathRepairPlanManifestCreation creation;

            try
            {
                creation =
                    DataRelativePathRepairPlanManifest
                        .CreateAggregateAlternateBranchFromResolution(
                            Guid.NewGuid(),
                            createdUtc,
                            resolution,
                            projection.SourceSnapshot,
                            projection.DestinationParentSnapshot,
                            projection.Operations
                        );
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    "Repair-plan-aggregate-namespace-batch schema-v4 " +
                    $"candidate creation failed for {requestedPath}: " +
                    ex.Message
                );
                Console.Error.WriteLine(
                    "No child plan directories were published."
                );
                return 4;
            }

            if (
                !creation.Success ||
                creation.Manifest is null)
            {
                Console.Error.WriteLine(
                    "Repair-plan-aggregate-namespace-batch schema-v4 " +
                    $"candidate creation failed for {requestedPath}."
                );
                Console.Error.WriteLine(
                    creation.Error ??
                    creation.State.ToString()
                );
                Console.Error.WriteLine(
                    "No child plan directories were published."
                );
                return 4;
            }

            candidates[index] =
                new(
                    ChildName:
                        $"plan-{index + 1:D6}",
                    Manifest:
                        creation.Manifest
                );
        }

        /*
         * Open and retain the external batch directory. The directory must
         * already exist, be outside Data, and be empty before Gate 1.
         */
        LinuxNoFollowPathOpenResult batchOpen;

        try
        {
            batchOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    fullBatchDirectory
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                "Repair-plan-aggregate-namespace-batch batch directory " +
                $"open error: {ex.Message}"
            );
            return 5;
        }

        if (
            !batchOpen.Success ||
            batchOpen.OpenedPath is null)
        {
            Console.Error.WriteLine(
                "Repair-plan-aggregate-namespace-batch batch directory " +
                "could not be opened safely."
            );
            Console.Error.WriteLine(
                batchOpen.Error ??
                batchOpen.State.ToString()
            );
            return 5;
        }

        using LinuxNoFollowPathHandle batchDirectory =
            batchOpen.OpenedPath!;

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
                    "Repair-plan-aggregate-namespace-batch batch directory " +
                    "must be empty."
                );
                return 5;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                "Repair-plan-aggregate-namespace-batch batch directory " +
                $"inspection error: {ex.Message}"
            );
            return 5;
        }

        /*
         * GATE 1 — preflight.
         *
         * Fresh Data and sidecar capabilities are acquired inside PlanGate.
         * No child or batch metadata has been published.
         */
        GateResult gate1 =
            PlanGate(
                fullDataRoot,
                sidecarParent,
                sidecarName,
                batchId,
                createdUtc,
                manifestName,
                candidates
            );

        if (!TryCreateAuthority(
                gate1,
                requestedPaths,
                out GateAuthority? authority,
                out string? authorityError))
        {
            WriteGateFailure(
                "Gate 1",
                gate1,
                authorityError
            );
            Console.Error.WriteLine(
                "No child plan directories were published."
            );
            return 6;
        }

        GateAuthority gate1Authority =
            authority!;

        /*
         * Internal deterministic test seam. Production Run() supplies null.
         */
        hooks?.AfterGate1BeforeGate2?.Invoke();

        /*
         * GATE 2 — immediate pre-publication reauthentication.
         */
        GateResult gate2 =
            PlanGate(
                fullDataRoot,
                sidecarParent,
                sidecarName,
                batchId,
                createdUtc,
                manifestName,
                candidates
            );

        if (!GateMatchesAuthority(
                gate2,
                gate1Authority,
                out string? gate2Error))
        {
            WriteGateFailure(
                "Gate 2",
                gate2,
                gate2Error
            );
            Console.Error.WriteLine(
                "No child plan directories were published."
            );
            return 6;
        }

        int persistedCount =
            0;

        /*
         * Publish only the exact Gate-1 children. Never reconstruct a child
         * after authorization.
         */
        foreach (
            AuthorizedChild child
            in gate1Authority.Children)
        {
            LinuxCreateDirectoryAtResult create =
                LinuxCreateDirectoryAt.Create(
                    batchDirectory,
                    child.ChildName
                );

            if (!create.Success)
            {
                Console.Error.WriteLine(
                    "Repair-plan-aggregate-namespace-batch could not create " +
                    $"child directory {child.ChildName}."
                );
                Console.Error.WriteLine(
                    create.Error ??
                    create.State.ToString()
                );
                WritePartialMetadataWarning(
                    persistedCount
                );
                return 7;
            }

            LinuxFsyncResult batchSync =
                LinuxFsync.Sync(
                    batchDirectory
                );

            if (!batchSync.Success)
            {
                Console.Error.WriteLine(
                    "Repair-plan-aggregate-namespace-batch could not " +
                    $"durably publish child directory {child.ChildName}."
                );
                Console.Error.WriteLine(
                    batchSync.Error ??
                    batchSync.State.ToString()
                );
                WritePartialMetadataWarning(
                    persistedCount
                );
                return 7;
            }

            LinuxOpenChildDirectoryReadOnlyAtResult
                childDirectoryOpen;

            try
            {
                childDirectoryOpen =
                    LinuxOpenChildDirectoryReadOnlyAt.Open(
                        batchDirectory,
                        child.ChildName
                    );
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    "Repair-plan-aggregate-namespace-batch could not " +
                    $"open {child.ChildName} for manifest publication: " +
                    ex.Message
                );
                WritePartialMetadataWarning(
                    persistedCount
                );
                return 7;
            }

            if (
                !childDirectoryOpen.Success ||
                childDirectoryOpen.OpenedDirectory is null)
            {
                Console.Error.WriteLine(
                    "Repair-plan-aggregate-namespace-batch could not " +
                    $"safely open {child.ChildName}."
                );
                Console.Error.WriteLine(
                    childDirectoryOpen.Error ??
                    childDirectoryOpen.State.ToString()
                );
                WritePartialMetadataWarning(
                    persistedCount
                );
                return 7;
            }

            using LinuxNoFollowPathHandle childDirectory =
                childDirectoryOpen.OpenedDirectory!;

            DataRelativePathRepairPlanManifestWriterResult write;

            try
            {
                write =
                    DataRelativePathRepairPlanManifestWriter.CreateInitial(
                        childDirectory,
                        manifestName,
                        child.Manifest
                    );
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    "Repair-plan-aggregate-namespace-batch child manifest " +
                    $"write failed for {child.ChildName}: {ex.Message}"
                );
                WritePartialMetadataWarning(
                    persistedCount
                );
                return 7;
            }

            if (!write.Success)
            {
                Console.Error.WriteLine(
                    "Repair-plan-aggregate-namespace-batch child manifest " +
                    $"was not durably created for {child.ChildName}."
                );
                Console.Error.WriteLine(
                    write.Error ??
                    write.State.ToString()
                );
                WritePartialMetadataWarning(
                    persistedCount
                );
                return 7;
            }

            /*
             * Writer success is the durable child publication boundary.
             */
            persistedCount++;

            hooks?
                .AfterChildPublishedBeforeReadback?
                .Invoke(
                    child.CandidateIndex
                );

            DataRelativePathRepairPlanManifestReaderResult childRead;

            try
            {
                childRead =
                    DataRelativePathRepairPlanManifestReader.Read(
                        childDirectory,
                        manifestName
                    );
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    "Repair-plan-aggregate-namespace-batch child readback " +
                    $"failed for {child.ChildName}: {ex.Message}"
                );
                WritePartialMetadataWarning(
                    persistedCount
                );
                return 8;
            }

            if (
                !childRead.Success ||
                childRead.Manifest is null ||
                childRead.ManifestSha256 is null)
            {
                Console.Error.WriteLine(
                    "Repair-plan-aggregate-namespace-batch child readback " +
                    $"was not valid for {child.ChildName}."
                );
                Console.Error.WriteLine(
                    childRead.Error ??
                    childRead.State.ToString()
                );
                WritePartialMetadataWarning(
                    persistedCount
                );
                return 8;
            }

            DataRelativePathRepairPlanManifestRecord
                observedManifest =
                    childRead.Manifest!;

            byte[] observedCanonical;

            try
            {
                observedCanonical =
                    DataRelativePathRepairPlanManifestJson.Serialize(
                        observedManifest
                    );
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    "Repair-plan-aggregate-namespace-batch could not " +
                    $"canonicalize readback child {child.ChildName}: " +
                    ex.Message
                );
                WritePartialMetadataWarning(
                    persistedCount
                );
                return 8;
            }

            if (
                !string.Equals(
                    childRead.ManifestSha256,
                    child.ManifestSha256,
                    StringComparison.OrdinalIgnoreCase) ||
                !observedCanonical.AsSpan().SequenceEqual(
                    child.CanonicalBytes
                ) ||
                observedManifest.PlanId !=
                    child.Manifest.PlanId ||
                observedManifest.SchemaVersion !=
                    DataRelativePathRepairPlanManifestRecord.SchemaVersion4 ||
                !string.Equals(
                    observedManifest.DataRoot,
                    child.Manifest.DataRoot,
                    StringComparison.Ordinal))
            {
                Console.Error.WriteLine(
                    "Repair-plan-aggregate-namespace-batch child readback " +
                    $"did not match the exact Gate-1 authority for " +
                    $"{child.ChildName}."
                );
                WritePartialMetadataWarning(
                    persistedCount
                );
                return 8;
            }
        }

        hooks?
            .AfterAllChildReadbacksBeforeGate3?
            .Invoke();

        /*
         * GATE 3 — final authorization before the completion record.
         */
        GateResult gate3 =
            PlanGate(
                fullDataRoot,
                sidecarParent,
                sidecarName,
                batchId,
                createdUtc,
                manifestName,
                candidates
            );

        if (!GateMatchesAuthority(
                gate3,
                gate1Authority,
                out string? gate3Error))
        {
            WriteGateFailure(
                "Gate 3",
                gate3,
                gate3Error
            );
            WritePartialMetadataWarning(
                persistedCount
            );
            return 9;
        }

        DataRelativePathRepairBatchManifestWriterResult
            batchWrite;

        try
        {
            batchWrite =
                DataRelativePathRepairBatchManifestWriter.CreateInitial(
                    batchDirectory,
                    BatchManifestName,
                    gate1Authority.BatchManifest
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                "Repair-plan-aggregate-namespace-batch batch-completion " +
                $"write error: {ex.Message}"
            );
            WritePartialMetadataWarning(
                persistedCount
            );
            return 10;
        }

        if (!batchWrite.Success)
        {
            Console.Error.WriteLine(
                "Repair-plan-aggregate-namespace-batch did not establish " +
                "durable batch completion."
            );
            Console.Error.WriteLine(
                batchWrite.Error ??
                batchWrite.State.ToString()
            );

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

            return 10;
        }

        DataRelativePathRepairBatchManifestReaderResult
            batchRead;

        try
        {
            batchRead =
                DataRelativePathRepairBatchManifestReader.Read(
                    batchDirectory,
                    BatchManifestName
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                "Repair-plan-aggregate-namespace-batch batch-completion " +
                $"readback error: {ex.Message}"
            );
            WriteBatchVerificationFailureWarning(
                persistedCount
            );
            return 11;
        }

        if (
            !batchRead.Success ||
            batchRead.Manifest is null ||
            batchRead.ManifestSha256 is null)
        {
            Console.Error.WriteLine(
                "Repair-plan-aggregate-namespace-batch batch-completion " +
                "readback failed."
            );
            Console.Error.WriteLine(
                batchRead.Error ??
                batchRead.State.ToString()
            );
            WriteBatchVerificationFailureWarning(
                persistedCount
            );
            return 11;
        }

        DataRelativePathRepairBatchManifestRecord
            observedBatchManifest =
                batchRead.Manifest!;

        byte[] observedBatchCanonical;

        try
        {
            observedBatchCanonical =
                DataRelativePathRepairBatchManifestJson.Serialize(
                    observedBatchManifest
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                "Repair-plan-aggregate-namespace-batch could not " +
                "canonicalize the completed batch readback: " +
                ex.Message
            );
            WriteBatchVerificationFailureWarning(
                persistedCount
            );
            return 11;
        }

        if (
            !string.Equals(
                batchRead.ManifestSha256,
                gate1Authority.BatchManifestSha256,
                StringComparison.OrdinalIgnoreCase) ||
            !observedBatchCanonical.AsSpan().SequenceEqual(
                gate1Authority.BatchCanonicalBytes
            ) ||
            observedBatchManifest.SchemaVersion !=
                DataRelativePathRepairBatchManifestRecord.SchemaVersion4 ||
            observedBatchManifest.CoveragePolicyVersion !=
                DataRelativePathRepairBatchManifestRecord
                    .CoveragePolicyVersion3)
        {
            Console.Error.WriteLine(
                "Repair-plan-aggregate-namespace-batch completed batch " +
                "does not match the exact Gate-1 authority."
            );
            WriteBatchVerificationFailureWarning(
                persistedCount
            );
            return 11;
        }

        Console.WriteLine(
            "CaseCompat Aggregate Namespace Repair Plan Batch"
        );
        Console.WriteLine(
            "=============================================="
        );
        Console.WriteLine();
        Console.WriteLine(
            $"Skyrim Data:           {fullDataRoot}"
        );
        Console.WriteLine(
            $"Path list:             {fullPathList}"
        );
        Console.WriteLine(
            $"Namespace root:        " +
            $"{gate1Authority.NamespaceEvidenceReference.RootWindowsLogicalPath}"
        );
        Console.WriteLine(
            $"Namespace sidecar SHA: {gate1Authority.SidecarSha256}"
        );
        Console.WriteLine(
            $"Input paths:           {requestedPaths.Length:N0}"
        );
        Console.WriteLine(
            $"Plans persisted:       {persistedCount:N0}"
        );
        Console.WriteLine(
            "Child plan schema:     4"
        );
        Console.WriteLine(
            "Batch schema:          4"
        );
        Console.WriteLine(
            "Coverage policy:       3"
        );
        Console.WriteLine(
            "Batch completion:      DURABLE AND READ BACK"
        );
        Console.WriteLine(
            "Repair operations executed: NO"
        );
        Console.WriteLine(
            "Policy-v3 execution support: DISABLED"
        );
        Console.WriteLine();
        Console.WriteLine(
            "This command produced planning metadata only. " +
            "Schema-v4 / coverage-policy-v3 execution authority is not " +
            "enabled."
        );

        return 0;
    }

    private static GateResult PlanGate(
        string fullDataRoot,
        string sidecarParent,
        string sidecarName,
        Guid batchId,
        DateTimeOffset createdUtc,
        string manifestName,
        IReadOnlyList<
            DataRelativePathRepairBatchAggregateNamespacePlanningCandidate
        > candidates)
    {
        LinuxNoFollowPathOpenResult dataOpen;

        try
        {
            dataOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    fullDataRoot
                );
        }
        catch (Exception ex)
        {
            return GateResult.Failed(
                "Skyrim Data root open failed: " +
                ex.Message
            );
        }

        if (
            !dataOpen.Success ||
            dataOpen.OpenedPath is null)
        {
            return GateResult.Failed(
                dataOpen.Error ??
                dataOpen.State.ToString()
            );
        }

        using LinuxNoFollowPathHandle dataRoot =
            dataOpen.OpenedPath!;

        LinuxNoFollowPathOpenResult sidecarDirectoryOpen;

        try
        {
            sidecarDirectoryOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    sidecarParent
                );
        }
        catch (Exception ex)
        {
            return GateResult.Failed(
                "Aggregate namespace manifest directory open failed: " +
                ex.Message
            );
        }

        if (
            !sidecarDirectoryOpen.Success ||
            sidecarDirectoryOpen.OpenedPath is null)
        {
            return GateResult.Failed(
                sidecarDirectoryOpen.Error ??
                sidecarDirectoryOpen.State.ToString()
            );
        }

        using LinuxNoFollowPathHandle sidecarDirectory =
            sidecarDirectoryOpen.OpenedPath!;

        DataRelativePathAggregateNamespaceManifestReaderResult
            namespaceEvidence;

        try
        {
            namespaceEvidence =
                DataRelativePathAggregateNamespaceManifestReader.Read(
                    sidecarDirectory,
                    sidecarName
                );
        }
        catch (Exception ex)
        {
            return GateResult.Failed(
                "Aggregate namespace manifest read failed: " +
                ex.Message
            );
        }

        if (!namespaceEvidence.Success)
        {
            return new(
                NamespaceEvidence:
                    namespaceEvidence,
                Plan:
                    null,
                Error:
                    namespaceEvidence.Error ??
                    namespaceEvidence.State.ToString()
            );
        }

        try
        {
            DataRelativePathRepairBatchAggregateNamespacePlanResult plan =
                DataRelativePathRepairBatchAggregateNamespacePlanner.Plan(
                    dataRoot,
                    batchId,
                    createdUtc,
                    manifestName,
                    candidates,
                    namespaceEvidence
                );

            return new(
                NamespaceEvidence:
                    namespaceEvidence,
                Plan:
                    plan,
                Error:
                    plan.Success
                        ? null
                        : plan.Error
            );
        }
        catch (Exception ex)
        {
            return new(
                NamespaceEvidence:
                    namespaceEvidence,
                Plan:
                    null,
                Error:
                    "Aggregate namespace planner threw: " +
                    ex.Message
            );
        }
    }

    private static bool TryCreateAuthority(
        GateResult gate,
        IReadOnlyList<string> requestedPaths,
        out GateAuthority? authority,
        out string? error)
    {
        authority =
            null;

        error =
            null;

        DataRelativePathAggregateNamespaceManifestReaderResult?
            namespaceEvidence =
                gate.NamespaceEvidence;

        if (
            namespaceEvidence is null ||
            !namespaceEvidence.Success ||
            namespaceEvidence.Manifest is null ||
            namespaceEvidence.ManifestSha256 is null)
        {
            error =
                gate.Error ??
                "Gate 1 did not produce valid SHA-bound namespace evidence.";
            return false;
        }

        DataRelativePathAggregateNamespaceManifestRecord
            namespaceManifest =
                namespaceEvidence.Manifest!;

        string sidecarSha256 =
            namespaceEvidence.ManifestSha256!;

        DataRelativePathRepairBatchAggregateNamespacePlanResult?
            plan =
                gate.Plan;

        if (
            plan is null ||
            !plan.Success ||
            plan.BatchManifest is null ||
            plan.NamespaceEvidenceReference is null)
        {
            error =
                plan?.Error ??
                gate.Error ??
                "Gate 1 aggregate namespace planning did not succeed.";
            return false;
        }

        DataRelativePathRepairBatchManifestRecord
            batchManifest =
                plan.BatchManifest!;

        DataRelativePathRepairBatchAggregateNamespaceEvidenceReference
            namespaceReference =
                plan.NamespaceEvidenceReference!;

        if (
            plan.Decisions.Count !=
                requestedPaths.Count ||
            plan.PlannedChildren.Count !=
                requestedPaths.Count ||
            plan.Decisions.Any(
                decision =>
                    !decision.Authorized) ||
            batchManifest.SchemaVersion !=
                DataRelativePathRepairBatchManifestRecord.SchemaVersion4 ||
            batchManifest.CoveragePolicyVersion !=
                DataRelativePathRepairBatchManifestRecord
                    .CoveragePolicyVersion3 ||
            batchManifest.SafeRejectionCount !=
                0 ||
            batchManifest.InputPathCount !=
                requestedPaths.Count ||
            batchManifest.Children.Count !=
                requestedPaths.Count)
        {
            error =
                "Gate 1 did not return the exact all-input schema-v4 / " +
                "coverage-policy-v3 planning shape.";
            return false;
        }

        var children =
            new List<AuthorizedChild>(
                plan.PlannedChildren.Count
            );

        for (
            int index = 0;
            index < plan.PlannedChildren.Count;
            index++)
        {
            DataRelativePathRepairBatchAggregateNamespacePlannedChild
                planned =
                    plan.PlannedChildren[index];

            DataRelativePathRepairBatchAggregateNamespacePlanningDecision
                decision =
                    plan.Decisions[index];

            if (
                planned.CandidateIndex != index ||
                decision.CandidateIndex != index ||
                !decision.Authorized ||
                !string.Equals(
                    planned.Manifest.RequestedPath,
                    requestedPaths[index],
                    StringComparison.Ordinal) ||
                planned.Manifest.SchemaVersion !=
                    DataRelativePathRepairPlanManifestRecord.SchemaVersion4)
            {
                error =
                    $"Gate 1 candidate ordering/shape mismatch at index {index}.";
                return false;
            }

            byte[] bytes;

            try
            {
                bytes =
                    DataRelativePathRepairPlanManifestJson.Serialize(
                        planned.Manifest
                    );
            }
            catch (Exception ex)
            {
                error =
                    $"Gate 1 child serialization failed at index {index}: " +
                    ex.Message;
                return false;
            }

            string sha256 =
                Sha256(
                    bytes
                );

            if (!string.Equals(
                    sha256,
                    planned.ManifestSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                error =
                    $"Gate 1 child SHA mismatch at index {index}.";
                return false;
            }

            DataRelativePathRepairBatchManifestChild
                batchChild =
                    batchManifest.Children[index];

            if (
                !string.Equals(
                    batchChild.ChildName,
                    planned.ChildName,
                    StringComparison.Ordinal) ||
                batchChild.PlanId !=
                    planned.Manifest.PlanId ||
                !string.Equals(
                    batchChild.ManifestSha256,
                    planned.ManifestSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                error =
                    $"Gate 1 batch-child binding mismatch at index {index}.";
                return false;
            }

            children.Add(
                new(
                    CandidateIndex:
                        planned.CandidateIndex,
                    ChildName:
                        planned.ChildName,
                    Manifest:
                        planned.Manifest,
                    CanonicalBytes:
                        bytes,
                    ManifestSha256:
                        planned.ManifestSha256,
                    SourceInodeGeneration:
                        planned.SourceInodeGeneration
                )
            );
        }

        byte[] batchBytes;

        try
        {
            batchBytes =
                DataRelativePathRepairBatchManifestJson.Serialize(
                    batchManifest
                );
        }
        catch (Exception ex)
        {
            error =
                "Gate 1 batch serialization failed: " +
                ex.Message;
            return false;
        }

        string batchSha256 =
            Sha256(
                batchBytes
            );

        if (
            batchManifest.AggregateNamespaceEvidence is null ||
            batchManifest.AggregateNamespaceEvidence.Count != 1 ||
            batchManifest.AggregateNamespaceEvidence[0] !=
                namespaceReference ||
            !string.Equals(
                namespaceReference.ManifestSha256,
                sidecarSha256,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                namespaceReference.RootWindowsLogicalPath,
                namespaceManifest.RootWindowsLogicalPath,
                StringComparison.Ordinal))
        {
            error =
                "Gate 1 namespace-evidence reference does not match the " +
                "descriptor-read sidecar.";
            return false;
        }

        authority =
            new(
                BatchManifest:
                    batchManifest,
                BatchCanonicalBytes:
                    batchBytes,
                BatchManifestSha256:
                    batchSha256,
                Children:
                    children,
                NamespaceEvidenceReference:
                    namespaceReference,
                Decisions:
                    plan.Decisions.ToArray(),
                SidecarSha256:
                    sidecarSha256
            );

        return true;
    }

    private static bool GateMatchesAuthority(
        GateResult gate,
        GateAuthority authority,
        out string? error)
    {
        error =
            null;

        DataRelativePathAggregateNamespaceManifestReaderResult?
            namespaceEvidence =
                gate.NamespaceEvidence;

        if (
            namespaceEvidence is null ||
            !namespaceEvidence.Success ||
            namespaceEvidence.ManifestSha256 is null)
        {
            error =
                gate.Error ??
                "Reauthentication did not produce valid namespace evidence.";
            return false;
        }

        if (!string.Equals(
                namespaceEvidence.ManifestSha256,
                authority.SidecarSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            error =
                "The supplied aggregate namespace manifest SHA changed " +
                "after Gate 1.";
            return false;
        }

        DataRelativePathRepairBatchAggregateNamespacePlanResult?
            plan =
                gate.Plan;

        if (
            plan is null ||
            !plan.Success ||
            plan.BatchManifest is null ||
            plan.NamespaceEvidenceReference is null)
        {
            error =
                plan?.Error ??
                gate.Error ??
                "Aggregate namespace replanning did not succeed.";
            return false;
        }

        DataRelativePathRepairBatchManifestRecord
            batchManifest =
                plan.BatchManifest!;

        DataRelativePathRepairBatchAggregateNamespaceEvidenceReference
            namespaceReference =
                plan.NamespaceEvidenceReference!;

        if (
            plan.PlannedChildren.Count !=
                authority.Children.Count ||
            plan.Decisions.Count !=
                authority.Decisions.Count)
        {
            error =
                "Reauthentication changed the planned child or decision count.";
            return false;
        }

        if (
            namespaceReference !=
                authority.NamespaceEvidenceReference)
        {
            error =
                "Reauthentication changed the namespace evidence reference.";
            return false;
        }

        for (
            int index = 0;
            index < authority.Children.Count;
            index++)
        {
            AuthorizedChild expected =
                authority.Children[index];

            DataRelativePathRepairBatchAggregateNamespacePlannedChild
                observed =
                    plan.PlannedChildren[index];

            if (
                observed.CandidateIndex !=
                    expected.CandidateIndex ||
                !string.Equals(
                    observed.ChildName,
                    expected.ChildName,
                    StringComparison.Ordinal) ||
                observed.Manifest.PlanId !=
                    expected.Manifest.PlanId ||
                !string.Equals(
                    observed.ManifestSha256,
                    expected.ManifestSha256,
                    StringComparison.OrdinalIgnoreCase) ||
                observed.SourceInodeGeneration !=
                    expected.SourceInodeGeneration)
            {
                error =
                    $"Reauthentication changed child authority at index {index}.";
                return false;
            }

            byte[] observedBytes;

            try
            {
                observedBytes =
                    DataRelativePathRepairPlanManifestJson.Serialize(
                        observed.Manifest
                    );
            }
            catch (Exception ex)
            {
                error =
                    $"Reauthentication child serialization failed at " +
                    $"index {index}: {ex.Message}";
                return false;
            }

            if (!observedBytes.AsSpan().SequenceEqual(
                    expected.CanonicalBytes))
            {
                error =
                    $"Reauthentication changed canonical child bytes at " +
                    $"index {index}.";
                return false;
            }

            DataRelativePathRepairBatchAggregateNamespacePlanningDecision
                expectedDecision =
                    authority.Decisions[index];

            DataRelativePathRepairBatchAggregateNamespacePlanningDecision
                observedDecision =
                    plan.Decisions[index];

            if (
                observedDecision.CandidateIndex !=
                    expectedDecision.CandidateIndex ||
                observedDecision.State !=
                    expectedDecision.State ||
                observedDecision.Authorized !=
                    expectedDecision.Authorized ||
                observedDecision.SourceGenerationBindingState !=
                    expectedDecision.SourceGenerationBindingState ||
                observedDecision.CoverageDecisionState !=
                    expectedDecision.CoverageDecisionState)
            {
                error =
                    $"Reauthentication changed decision authority at " +
                    $"index {index}.";
                return false;
            }
        }

        byte[] batchBytes;

        try
        {
            batchBytes =
                DataRelativePathRepairBatchManifestJson.Serialize(
                    batchManifest
                );
        }
        catch (Exception ex)
        {
            error =
                "Reauthentication batch serialization failed: " +
                ex.Message;
            return false;
        }

        if (!batchBytes.AsSpan().SequenceEqual(
                authority.BatchCanonicalBytes))
        {
            error =
                "Reauthentication changed canonical batch-manifest bytes.";
            return false;
        }

        return true;
    }

    private static string Sha256(
        byte[] bytes)
    {
        return Convert
            .ToHexString(
                SHA256.HashData(
                    bytes
                )
            )
            .ToLowerInvariant();
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

    private static bool IsValidChildName(
        string? childName)
    {
        return
            !string.IsNullOrWhiteSpace(
                childName) &&
            childName is not "." and not ".." &&
            !childName.Contains('/') &&
            !childName.Contains('\\') &&
            !childName.Contains('\0');
    }

    private static void WriteUsageError()
    {
        Console.Error.WriteLine(
            "Error: repair-plan-aggregate-namespace-batch requires a Skyrim " +
            "Data directory, path-list file, aggregate namespace manifest " +
            "file, batch directory, and optional plan manifest file name."
        );
        Console.Error.WriteLine();
        Console.Error.WriteLine(
            "Usage: casecompat repair-plan-aggregate-namespace-batch " +
            "<Skyrim Data directory> <path-list file> " +
            "<aggregate namespace manifest file> <batch directory> " +
            "[plan manifest file name]"
        );
    }

    private static void WriteGateFailure(
        string gateName,
        GateResult gate,
        string? detail)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine(
            $"Repair-plan-aggregate-namespace-batch {gateName} failed."
        );

        if (
            gate.Plan is not null &&
            gate.Plan.Decisions.Count > 0)
        {
            foreach (
                DataRelativePathRepairBatchAggregateNamespacePlanningDecision
                    decision
                in gate.Plan.Decisions)
            {
                Console.Error.WriteLine(
                    $"[{decision.CandidateIndex + 1}] {decision.State}"
                );

                if (!string.IsNullOrWhiteSpace(
                        decision.Error))
                {
                    Console.Error.WriteLine(
                        $"      {decision.Error}"
                    );
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(
                detail ??
                gate.Error))
        {
            Console.Error.WriteLine(
                detail ??
                gate.Error
            );
        }
    }

    private static void WritePartialMetadataWarning(
        int persistedCount)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine(
            "IMPORTANT: aggregate namespace batch planning stopped before " +
            "durable batch completion."
        );
        Console.Error.WriteLine(
            $"Previously persisted child plans: {persistedCount:N0}"
        );
        Console.Error.WriteLine(
            $"The completion entry {BatchManifestName} was not published by " +
            "this successful path."
        );
        Console.Error.WriteLine(
            "Do not assume the batch directory is empty."
        );
        Console.Error.WriteLine(
            "No repair operations were requested."
        );
    }

    private static void WriteUncertainBatchCompletionWarning(
        int persistedCount)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine(
            "IMPORTANT: batch-completion publication changed the batch " +
            "directory before the writer reported failure."
        );
        Console.Error.WriteLine(
            $"Previously persisted child plans: {persistedCount:N0}"
        );
        Console.Error.WriteLine(
            $"The named completion entry {BatchManifestName} may exist, but " +
            "this invocation did not establish successful exact readback."
        );
        Console.Error.WriteLine(
            "No repair operations were requested."
        );
    }

    private static void WriteBatchVerificationFailureWarning(
        int persistedCount)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine(
            "IMPORTANT: the batch-completion writer reported durable " +
            "success before exact independent readback failed."
        );
        Console.Error.WriteLine(
            $"Previously persisted child plans: {persistedCount:N0}"
        );
        Console.Error.WriteLine(
            $"The durable completion entry {BatchManifestName} may be present."
        );
        Console.Error.WriteLine(
            "No repair operations were requested."
        );
    }

    private sealed record GateResult(
        DataRelativePathAggregateNamespaceManifestReaderResult?
            NamespaceEvidence,
        DataRelativePathRepairBatchAggregateNamespacePlanResult?
            Plan,
        string? Error
    )
    {
        public static GateResult Failed(
            string error)
        {
            return new(
                NamespaceEvidence:
                    null,
                Plan:
                    null,
                Error:
                    error
            );
        }
    }

    private sealed record AuthorizedChild(
        int CandidateIndex,
        string ChildName,
        DataRelativePathRepairPlanManifestRecord Manifest,
        byte[] CanonicalBytes,
        string ManifestSha256,
        uint SourceInodeGeneration
    );

    private sealed record GateAuthority(
        DataRelativePathRepairBatchManifestRecord BatchManifest,
        byte[] BatchCanonicalBytes,
        string BatchManifestSha256,
        IReadOnlyList<AuthorizedChild> Children,
        DataRelativePathRepairBatchAggregateNamespaceEvidenceReference
            NamespaceEvidenceReference,
        IReadOnlyList<
            DataRelativePathRepairBatchAggregateNamespacePlanningDecision
        > Decisions,
        string SidecarSha256
    );
}
