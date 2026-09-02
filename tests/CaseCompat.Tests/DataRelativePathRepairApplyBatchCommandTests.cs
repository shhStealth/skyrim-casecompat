using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairApplyBatchCommandTests
{
    private const string BatchManifestName =
        "batch-manifest.json";

    [Fact]
    public void Run_InvalidArguments_ReturnsUsageError()
    {
        Assert.Equal(
            2,
            global::RepairApplyBatchCommand.Run(
                [
                    "repair-apply-batch"
                ]
            )
        );
    }

    [Fact]
    public void
        Run_LegacyBatchWithoutCompletionManifest_RefusesBeforeMutation()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        PlanSpec plan =
            fixture.CreatePlan(
                index:
                    1,
                physicalComponent:
                    "alpha",
                requestedComponent:
                    "Alpha"
            );

        Assert.Equal(
            4,
            fixture.RunBatchApply()
        );

        Assert.False(
            File.Exists(
                plan.DestinationPath
            )
        );

        Assert.Empty(
            Directory.EnumerateFiles(
                plan.ChildDirectoryPath,
                ".casecompat-plan-*-op-*.json"
            )
        );
    }

    [Fact]
    public void
        Run_CompletedZeroChildBatch_SucceedsWithoutMutation()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        fixture.WriteBatchManifest(
            fixture.BuildBatchManifest(
                []
            )
        );

        Assert.Equal(
            0,
            fixture.RunBatchApply()
        );

        Assert.Single(
            Directory.EnumerateFileSystemEntries(
                fixture.BatchRoot
            )
        );

        Assert.True(
            File.Exists(
                Path.Combine(
                    fixture.BatchRoot,
                    BatchManifestName
                )
            )
        );
    }

    [Fact]
    public void
        Run_CompletedBatch_AppliesAllRecordedChildren()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        PlanSpec first =
            fixture.CreatePlan(
                index:
                    1,
                physicalComponent:
                    "alpha",
                requestedComponent:
                    "Alpha"
            );

        PlanSpec second =
            fixture.CreatePlan(
                index:
                    2,
                physicalComponent:
                    "beta",
                requestedComponent:
                    "Beta"
            );

        fixture.WriteBatchManifest(
            fixture.BuildBatchManifest(
                [
                    first,
                    second
                ]
            )
        );

        Assert.False(
            File.Exists(
                first.DestinationPath
            )
        );

        Assert.False(
            File.Exists(
                second.DestinationPath
            )
        );

        Assert.Equal(
            0,
            fixture.RunBatchApplyShort()
        );

        Assert.Equal(
            first.Payload,
            File.ReadAllText(
                first.DestinationPath
            )
        );

        Assert.Equal(
            second.Payload,
            File.ReadAllText(
                second.DestinationPath
            )
        );
    }

    [Fact]
    public void
        Run_CompletedBatch_SharedRepairDirectory_AppliesAllRecordedChildren()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        PlanSpec first =
            fixture.CreatePlan(
                index:
                    1,
                physicalComponent:
                    "alpha",
                requestedComponent:
                    "Alpha"
            );

        PlanSpec second =
            fixture.CreatePlan(
                index:
                    2,
                physicalComponent:
                    "alpha",
                requestedComponent:
                    "Alpha"
            );

        DataRelativePathRepairBatchManifestRecord batchManifest =
            fixture.BuildBatchManifest(
                [
                    first,
                    second
                ]
            );

        fixture.WriteBatchManifest(
            batchManifest
        );

        Assert.False(
            File.Exists(
                first.DestinationPath
            )
        );

        Assert.False(
            File.Exists(
                second.DestinationPath
            )
        );

        Assert.Equal(
            0,
            fixture.RunBatchApply()
        );

        Assert.Equal(
            first.Payload,
            File.ReadAllText(
                first.DestinationPath
            )
        );

        Assert.Equal(
            second.Payload,
            File.ReadAllText(
                second.DestinationPath
            )
        );

        DataRelativePathRepairPlanManifestRecord firstManifest =
            fixture.ReadPlanManifest(
                first
            );

        DataRelativePathRepairPlanManifestRecord secondManifest =
            fixture.ReadPlanManifest(
                second
            );

        DataRelativePathRepairPlanManifestOperation firstDirectoryEntry =
            firstManifest.Operations[0];

        DataRelativePathRepairPlanManifestOperation secondDirectoryEntry =
            secondManifest.Operations[0];

        Assert.Equal(
            DataRelativePathRepairPlanOperationKind.CreateDirectory,
            firstDirectoryEntry.Operation.Kind
        );

        Assert.Equal(
            DataRelativePathRepairPlanOperationKind.CreateDirectory,
            secondDirectoryEntry.Operation.Kind
        );

        DataRelativePathRepairDirectoryJournalRecord firstJournal =
            fixture.ReadDirectoryJournal(
                first,
                firstDirectoryEntry
            );

        DataRelativePathRepairDirectoryJournalRecord secondJournal =
            fixture.ReadDirectoryJournal(
                second,
                secondDirectoryEntry
            );

        /*
         * Child 1 genuinely created the directory.
         *
         * Ordinary owned-directory journals intentionally remain schema v2.
         * Their ownership is represented by the established owned lifecycle,
         * not by the schema-v3 OwnershipDisposition field.
         */
        Assert.Equal(
            DataRelativePathRepairDirectoryJournalRecord.SchemaVersion2,
            firstJournal.SchemaVersion
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.Applied,
            firstJournal.State
        );

        Assert.Null(
            firstJournal.OwnershipDisposition
        );

        Assert.Null(
            firstJournal.BatchReuseProvenance
        );

        Assert.NotNull(
            firstJournal.PreparedDirectoryIncarnationIdentity
        );

        /*
         * Child 2 did not acquire deletion ownership.
         *
         * It must persist a distinct schema-v3 BatchReused/Applied journal
         * whose provenance points back to child 1's authenticated ordinary
         * owner journal.
         */
        Assert.Equal(
            DataRelativePathRepairDirectoryJournalRecord.SchemaVersion3,
            secondJournal.SchemaVersion
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.Applied,
            secondJournal.State
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryOwnershipDisposition.BatchReused,
            secondJournal.OwnershipDisposition
        );

        Assert.Null(
            secondJournal.PreparedDirectoryIncarnationIdentity
        );

        DataRelativePathRepairDirectoryBatchReuseProvenance provenance =
            Assert.IsType<
                DataRelativePathRepairDirectoryBatchReuseProvenance
            >(
                secondJournal.BatchReuseProvenance
            );

        Assert.Equal(
            batchManifest.BatchId,
            provenance.BatchId
        );

        Assert.Equal(
            first.ChildName,
            provenance.OwnerChildName
        );

        Assert.Equal(
            firstManifest.PlanId,
            provenance.OwnerPlanId
        );

        Assert.Equal(
            batchManifest.Children[0].ManifestSha256,
            provenance.OwnerManifestSha256
        );

        Assert.Equal(
            firstDirectoryEntry.Index,
            provenance.OwnerOperationIndex
        );

        Assert.Equal(
            firstDirectoryEntry.JournalChildName,
            provenance.OwnerJournalChildName
        );

        /*
         * The borrower must reference the exact strong directory incarnation
         * that the true owner created.
         */
        Assert.True(
            firstJournal.PreparedDirectoryIncarnationIdentity!
                .SameIncarnationAs(
                    provenance.ReusedDirectoryIncarnationIdentity
                )
        );

        DataRelativePathRepairDirectoryRecoveryClassification
            borrowerClassification =
                DataRelativePathRepairDirectoryRecoveryClassifier.Classify(
                    secondJournal,
                    fixture.DataRoot
                );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .ReusedAppliedFinalMatches,
            borrowerClassification.State
        );
    }

    [Fact]
    public void
        Run_StandaloneApply_SharedRepairDirectory_RemainsDestinationExistsFailure()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        /*
         * Both plans are created from the same pre-apply filesystem state,
         * exactly like the batch regression.
         */
        PlanSpec first =
            fixture.CreatePlan(
                index:
                    1,
                physicalComponent:
                    "alpha",
                requestedComponent:
                    "Alpha"
            );

        PlanSpec second =
            fixture.CreatePlan(
                index:
                    2,
                physicalComponent:
                    "alpha",
                requestedComponent:
                    "Alpha"
            );

        Assert.Equal(
            0,
            fixture.RunStandaloneApply(
                first
            )
        );

        Assert.Equal(
            first.Payload,
            File.ReadAllText(
                first.DestinationPath
            )
        );

        /*
         * The exact same second plan is NOT entitled to reuse merely because
         * the destination now exists. Standalone repair-apply supplies no
         * authenticated batch scope.
         */
        Assert.Equal(
            4,
            fixture.RunStandaloneApply(
                second
            )
        );

        Assert.False(
            File.Exists(
                second.DestinationPath
            )
        );

        DataRelativePathRepairPlanManifestRecord secondManifest =
            fixture.ReadPlanManifest(
                second
            );

        Assert.Equal(
            DataRelativePathRepairPlanOperationKind.CreateDirectory,
            secondManifest.Operations[0].Operation.Kind
        );

        /*
         * DestinationExists occurs before the ordinary directory executor
         * creates its revision-zero journal. Standalone failure must therefore
         * leave every operation journal absent.
         */
        Assert.Empty(
            Directory.EnumerateFiles(
                second.ChildDirectoryPath,
                ".casecompat-plan-*-op-*.json"
            )
        );

        using LinuxNoFollowPathHandle secondDirectory =
            fixture.OpenPlanDirectory(
                second
            );

        DataRelativePathRepairPlanForwardExecution repeat =
            DataRelativePathRepairPlanForwardExecutor.Execute(
                secondDirectory,
                fixture.ManifestName,
                fixture.DataRoot,
                DateTimeOffset.UtcNow
            );

        Assert.False(
            repeat.Success
        );

        Assert.Equal(
            DataRelativePathRepairPlanForwardExecutionState.OperationFailed,
            repeat.State
        );

        DataRelativePathRepairPlanForwardOperationExecution failedOperation =
            Assert.Single(
                repeat.OperationResults
            );

        Assert.Equal(
            0,
            failedOperation.Index
        );

        Assert.Equal(
            DataRelativePathRepairPlanForwardOperationExecutionState
                .DirectoryExecutionFailed,
            failedOperation.State
        );

        Assert.NotNull(
            failedOperation.DirectoryExecution
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryExecutionState.DestinationExists,
            failedOperation.DirectoryExecution!.State
        );

        Assert.Empty(
            Directory.EnumerateFiles(
                second.ChildDirectoryPath,
                ".casecompat-plan-*-op-*.json"
            )
        );
    }

    [Fact]
    public void
        Run_DefaultManifestMismatch_RefusesBeforeAnyChildMutation()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create(
                "custom-plan.json"
            );

        PlanSpec plan =
            fixture.CreatePlan(
                index:
                    1,
                physicalComponent:
                    "alpha",
                requestedComponent:
                    "Alpha"
            );

        fixture.WriteBatchManifest(
            fixture.BuildBatchManifest(
                [
                    plan
                ]
            )
        );

        Assert.Equal(
            "custom-plan.json",
            fixture.ManifestName
        );

        Assert.False(
            File.Exists(
                plan.DestinationPath
            )
        );

        Assert.Equal(
            4,
            fixture.RunBatchApplyShort()
        );

        Assert.False(
            File.Exists(
                plan.DestinationPath
            )
        );

        Assert.Empty(
            Directory.EnumerateFiles(
                plan.ChildDirectoryPath,
                ".casecompat-plan-*-op-*.json"
            )
        );
    }

    [Fact]
    public void
        Run_LaterMembershipMismatch_RefusesBeforeAnyChildMutation()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        PlanSpec first =
            fixture.CreatePlan(
                index:
                    1,
                physicalComponent:
                    "alpha",
                requestedComponent:
                    "Alpha"
            );

        PlanSpec second =
            fixture.CreatePlan(
                index:
                    2,
                physicalComponent:
                    "beta",
                requestedComponent:
                    "Beta"
            );

        DataRelativePathRepairBatchManifestRecord manifest =
            fixture.BuildBatchManifest(
                [
                    first,
                    second
                ]
            );

        DataRelativePathRepairBatchManifestChild secondChild =
            manifest.Children[1];

        char replacementFirst =
            secondChild.ManifestSha256[0] == '0'
                ? '1'
                : '0';

        string wrongSha256 =
            $"{replacementFirst}" +
            $"{secondChild.ManifestSha256[1..]}";

        DataRelativePathRepairBatchManifestChild[] children =
            manifest.Children.ToArray();

        children[1] =
            secondChild with
            {
                ManifestSha256 =
                    wrongSha256
            };

        fixture.WriteBatchManifest(
            manifest with
            {
                Children =
                    children
            }
        );

        Assert.Equal(
            4,
            fixture.RunBatchApply()
        );

        Assert.False(
            File.Exists(
                first.DestinationPath
            )
        );

        Assert.False(
            File.Exists(
                second.DestinationPath
            )
        );

        Assert.Empty(
            Directory.EnumerateFiles(
                first.ChildDirectoryPath,
                ".casecompat-plan-*-op-*.json"
            )
        );

        Assert.Empty(
            Directory.EnumerateFiles(
                second.ChildDirectoryPath,
                ".casecompat-plan-*-op-*.json"
            )
        );
    }

    [Fact]
    public void
        Run_ExecutionFailure_StopsBeforeLaterRecordedChildren()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        PlanSpec first =
            fixture.CreatePlan(
                index:
                    1,
                physicalComponent:
                    "alpha",
                requestedComponent:
                    "Alpha"
            );

        PlanSpec second =
            fixture.CreatePlan(
                index:
                    2,
                physicalComponent:
                    "beta",
                requestedComponent:
                    "Beta"
            );

        PlanSpec third =
            fixture.CreatePlan(
                index:
                    3,
                physicalComponent:
                    "gamma",
                requestedComponent:
                    "Gamma"
            );

        fixture.WriteBatchManifest(
            fixture.BuildBatchManifest(
                [
                    first,
                    second,
                    third
                ]
            )
        );

        /*
         * Batch completion/membership remains intact, but the second
         * plan can no longer pass forward preflight.
         *
         * The first child should reach durable success. The second
         * should fail, and the third must not be attempted.
         */
        File.Delete(
            second.SourcePath
        );

        Assert.Equal(
            6,
            fixture.RunBatchApply()
        );

        Assert.True(
            File.Exists(
                first.DestinationPath
            )
        );

        Assert.False(
            File.Exists(
                second.DestinationPath
            )
        );

        Assert.False(
            File.Exists(
                third.DestinationPath
            )
        );

        Assert.True(
            File.Exists(
                third.SourcePath
            )
        );

        Assert.Empty(
            Directory.EnumerateFiles(
                third.ChildDirectoryPath,
                ".casecompat-plan-*-op-*.json"
            )
        );
    }

    [Fact]
    public void
        Run_AlreadyAppliedCompletedBatch_RerunSucceeds()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        PlanSpec plan =
            fixture.CreatePlan(
                index:
                    1,
                physicalComponent:
                    "alpha",
                requestedComponent:
                    "Alpha"
            );

        fixture.WriteBatchManifest(
            fixture.BuildBatchManifest(
                [
                    plan
                ]
            )
        );

        Assert.Equal(
            0,
            fixture.RunBatchApply()
        );

        Assert.Equal(
            plan.Payload,
            File.ReadAllText(
                plan.DestinationPath
            )
        );

        Assert.Equal(
            0,
            fixture.RunBatchApply()
        );

        Assert.Equal(
            plan.Payload,
            File.ReadAllText(
                plan.DestinationPath
            )
        );
    }

    private sealed record PlanSpec(
        string ChildName,
        string ChildDirectoryPath,
        string SourcePath,
        string DestinationPath,
        string Payload
    );

    private sealed class Fixture
        : IDisposable
    {
        private Fixture(
            string rootPath,
            string dataRoot,
            string batchRoot,
            string manifestName)
        {
            RootPath =
                rootPath;

            DataRoot =
                dataRoot;

            BatchRoot =
                batchRoot;

            ManifestName =
                manifestName;
        }

        public string RootPath
        {
            get;
        }

        public string DataRoot
        {
            get;
        }

        public string BatchRoot
        {
            get;
        }

        public string ManifestName
        {
            get;
        }

        public static Fixture Create(
            string manifestName = "repair-plan.json")
        {
            string rootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-repair-apply-batch-command-tests",
                    Guid.NewGuid().ToString("N")
                );

            string dataRoot =
                Directory.CreateDirectory(
                    Path.Combine(
                        rootPath,
                        "Data"
                    )
                ).FullName;

            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes"
                )
            );

            string batchRoot =
                Directory.CreateDirectory(
                    Path.Combine(
                        rootPath,
                        "batch"
                    )
                ).FullName;

            return new(
                rootPath,
                dataRoot,
                batchRoot,
                manifestName
            );
        }

        public PlanSpec CreatePlan(
            int index,
            string physicalComponent,
            string requestedComponent)
        {
            string fileName =
                $"Thing{index}.nif";

            string payload =
                $"batch-apply-payload-{index}";

            string physicalDirectory =
                Directory.CreateDirectory(
                    Path.Combine(
                        DataRoot,
                        "meshes",
                        physicalComponent
                    )
                ).FullName;

            string sourcePath =
                Path.Combine(
                    physicalDirectory,
                    fileName
                );

            File.WriteAllText(
                sourcePath,
                payload
            );

            string requestedPath =
                $"meshes/{requestedComponent}/{fileName}";

            string destinationPath =
                Path.Combine(
                    DataRoot,
                    "meshes",
                    requestedComponent,
                    fileName
                );

            string childName =
                $"plan-{index:D6}";

            string childDirectoryPath =
                Directory.CreateDirectory(
                    Path.Combine(
                        BatchRoot,
                        childName
                    )
                ).FullName;

            int result =
                global::RepairPlanCommand.Run(
                    [
                        "repair-plan",
                        DataRoot,
                        requestedPath,
                        childDirectoryPath,
                        ManifestName
                    ]
                );

            Assert.Equal(
                0,
                result
            );

            Assert.True(
                File.Exists(
                    Path.Combine(
                        childDirectoryPath,
                        ManifestName
                    )
                )
            );

            Assert.False(
                File.Exists(
                    destinationPath
                )
            );

            return new(
                ChildName:
                    childName,
                ChildDirectoryPath:
                    childDirectoryPath,
                SourcePath:
                    sourcePath,
                DestinationPath:
                    destinationPath,
                Payload:
                    payload
            );
        }

        public DataRelativePathRepairBatchManifestRecord
            BuildBatchManifest(
                IReadOnlyList<PlanSpec> plans)
        {
            using LinuxNoFollowPathHandle batchDirectory =
                OpenDirectory(
                    BatchRoot
                );

            var children =
                new List<
                    DataRelativePathRepairBatchManifestChild>(
                        plans.Count
                    );

            foreach (
                PlanSpec plan
                in plans)
            {
                LinuxOpenChildReadOnlyAtResult childOpen =
                    LinuxOpenChildReadOnlyAt.Open(
                        batchDirectory,
                        plan.ChildName
                    );

                Assert.True(
                    childOpen.Success,
                    childOpen.Error
                );

                using LinuxOpenedChildHandle childDirectory =
                    childOpen.OpenedChild!;

                DataRelativePathRepairPlanManifestReaderResult read =
                    DataRelativePathRepairPlanManifestReader.Read(
                        childDirectory,
                        ManifestName
                    );

                Assert.True(
                    read.Success,
                    read.Error
                );

                Assert.NotNull(
                    read.ManifestSha256
                );

                children.Add(
                    new(
                        ChildName:
                            plan.ChildName,
                        PlanId:
                            read.Manifest!.PlanId,
                        ManifestSha256:
                            read.ManifestSha256!
                    )
                );
            }

            DataRelativePathRepairBatchManifestCreation creation =
                DataRelativePathRepairBatchManifest.Create(
                    batchId:
                        Guid.NewGuid(),
                    createdUtc:
                        DateTimeOffset.UtcNow,
                    dataRoot:
                        DataRoot,
                    childManifestName:
                        ManifestName,
                    inputPathCount:
                        plans.Count,
                    safeRejectionCount:
                        0,
                    children:
                        children
                );

            Assert.True(
                creation.Success,
                creation.Error
            );

            return creation.Manifest!;
        }

        public void WriteBatchManifest(
            DataRelativePathRepairBatchManifestRecord manifest)
        {
            using LinuxNoFollowPathHandle batchDirectory =
                OpenDirectory(
                    BatchRoot
                );

            DataRelativePathRepairBatchManifestWriterResult write =
                DataRelativePathRepairBatchManifestWriter.CreateInitial(
                    batchDirectory,
                    BatchManifestName,
                    manifest
                );

            Assert.True(
                write.Success,
                write.Error
            );
        }

        public DataRelativePathRepairPlanManifestRecord
            ReadPlanManifest(
                PlanSpec plan)
        {
            using LinuxNoFollowPathHandle childDirectory =
                OpenDirectory(
                    plan.ChildDirectoryPath
                );

            DataRelativePathRepairPlanManifestReaderResult read =
                DataRelativePathRepairPlanManifestReader.Read(
                    childDirectory,
                    ManifestName
                );

            Assert.True(
                read.Success,
                read.Error
            );

            return Assert.IsType<
                DataRelativePathRepairPlanManifestRecord
            >(
                read.Manifest
            );
        }

        public DataRelativePathRepairDirectoryJournalRecord
            ReadDirectoryJournal(
                PlanSpec plan,
                DataRelativePathRepairPlanManifestOperation entry)
        {
            using LinuxNoFollowPathHandle childDirectory =
                OpenDirectory(
                    plan.ChildDirectoryPath
                );

            DataRelativePathRepairDirectoryJournalReaderResult read =
                DataRelativePathRepairDirectoryJournalReader.Read(
                    childDirectory,
                    entry.JournalChildName
                );

            Assert.True(
                read.Success,
                read.Error
            );

            return Assert.IsType<
                DataRelativePathRepairDirectoryJournalRecord
            >(
                read.Record
            );
        }

        public LinuxNoFollowPathHandle OpenPlanDirectory(
            PlanSpec plan)
        {
            return OpenDirectory(
                plan.ChildDirectoryPath
            );
        }

        public int RunStandaloneApply(
            PlanSpec plan)
        {
            return
                global::RepairApplyCommand.Run(
                    [
                        "repair-apply",
                        plan.ChildDirectoryPath,
                        ManifestName,
                        DataRoot
                    ]
                );
        }

        public int RunBatchApply()
        {
            return
                global::RepairApplyBatchCommand.Run(
                    [
                        "repair-apply-batch",
                        BatchRoot,
                        ManifestName,
                        DataRoot
                    ]
                );
        }

        public int RunBatchApplyShort()
        {
            return
                global::RepairApplyBatchCommand.Run(
                    [
                        "repair-apply-batch",
                        BatchRoot,
                        DataRoot
                    ]
                );
        }

        private static LinuxNoFollowPathHandle OpenDirectory(
            string path)
        {
            LinuxNoFollowPathOpenResult opened =
                LinuxNoFollowPath.OpenRootReadOnly(
                    path
                );

            Assert.True(
                opened.Success,
                opened.Error
            );

            return opened.OpenedPath!;
        }

        public void Dispose()
        {
            if (
                Directory.Exists(
                    RootPath))
            {
                Directory.Delete(
                    RootPath,
                    recursive:
                        true
                );
            }
        }
    }
}
