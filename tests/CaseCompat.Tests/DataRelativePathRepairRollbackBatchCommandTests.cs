using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairRollbackBatchCommandTests
{
    private const string BatchManifestName =
        "batch-manifest.json";

    [Fact]
    public void Run_InvalidArguments_ReturnsUsageError()
    {
        Assert.Equal(
            2,
            global::RepairRollbackBatchCommand.Run(
                [
                    "repair-rollback-batch"
                ]
            )
        );
    }

    [Fact]
    public void
        Run_LegacyBatchWithoutCompletionManifest_RefusesBeforeRollback()
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

        fixture.ApplyPlan(
            plan
        );

        Assert.True(
            File.Exists(
                plan.DestinationPath
            )
        );

        Assert.Equal(
            4,
            fixture.RunBatchRollback()
        );

        Assert.True(
            File.Exists(
                plan.DestinationPath
            )
        );

        Assert.Equal(
            plan.Payload,
            File.ReadAllText(
                plan.DestinationPath
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
            fixture.RunBatchRollback()
        );

        Assert.Single(
            Directory.EnumerateFileSystemEntries(
                fixture.BatchRoot
            )
        );
    }

    [Fact]
    public void
        Run_CompletedNeverAppliedBatch_SucceedsWithoutFilesystemMutation()
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

        Assert.Equal(
            0,
            fixture.RunBatchRollback()
        );

        Assert.True(
            File.Exists(
                first.SourcePath
            )
        );

        Assert.True(
            File.Exists(
                second.SourcePath
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
    }

    [Fact]
    public void
        Run_AppliedCompletedBatch_RollsBackAllRecordedChildren()
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

        fixture.ApplyBatch();

        Assert.True(
            File.Exists(
                first.DestinationPath
            )
        );

        Assert.True(
            File.Exists(
                second.DestinationPath
            )
        );

        Assert.Equal(
            0,
            fixture.RunBatchRollbackShort()
        );

        Assert.True(
            File.Exists(
                first.SourcePath
            )
        );

        Assert.True(
            File.Exists(
                second.SourcePath
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
    }

    [Fact]
    public void
        Run_DefaultManifestMismatch_RefusesBeforeAnyChildRollback()
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

        fixture.ApplyPlan(
            plan
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

        Assert.True(
            File.Exists(
                plan.DestinationPath
            )
        );

        Assert.Equal(
            plan.Payload,
            File.ReadAllText(
                plan.DestinationPath
            )
        );

        Assert.Equal(
            4,
            fixture.RunBatchRollbackShort()
        );

        Assert.True(
            File.Exists(
                plan.DestinationPath
            )
        );

        Assert.Equal(
            plan.Payload,
            File.ReadAllText(
                plan.DestinationPath
            )
        );
    }

    [Fact]
    public void
        Run_MembershipMismatch_RefusesBeforeAnyChildRollback()
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

        fixture.ApplyPlan(
            first
        );

        fixture.ApplyPlan(
            second
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
            fixture.RunBatchRollback()
        );

        Assert.True(
            File.Exists(
                first.DestinationPath
            )
        );

        Assert.True(
            File.Exists(
                second.DestinationPath
            )
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
        Run_MiddleRollbackFailure_ProvesReverseOrderAndStopsBeforeEarlierChild()
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

        fixture.ApplyBatch();

        Assert.True(
            File.Exists(
                first.DestinationPath
            )
        );

        Assert.True(
            File.Exists(
                second.DestinationPath
            )
        );

        Assert.True(
            File.Exists(
                third.DestinationPath
            )
        );

        /*
         * Batch membership remains intact because the manifests are
         * untouched. Make child 2's current filesystem state conflict
         * with its durable rollback journal.
         *
         * Reverse batch order requires:
         *
         *   child 3 -> rolls back
         *   child 2 -> fails closed
         *   child 1 -> is never attempted
         */
        File.WriteAllText(
            second.DestinationPath,
            "externally-modified-destination"
        );

        Assert.Equal(
            6,
            fixture.RunBatchRollback()
        );

        Assert.False(
            File.Exists(
                third.DestinationPath
            )
        );

        Assert.True(
            File.Exists(
                second.DestinationPath
            )
        );

        Assert.Equal(
            "externally-modified-destination",
            File.ReadAllText(
                second.DestinationPath
            )
        );

        Assert.True(
            File.Exists(
                first.DestinationPath
            )
        );

        Assert.Equal(
            first.Payload,
            File.ReadAllText(
                first.DestinationPath
            )
        );
    }

    [Fact]
    public void
        Run_SharedRepairDirectory_RollsBackBorrowerBeforeOwner_AndRerunSucceeds()
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

        fixture.WriteBatchManifest(
            fixture.BuildBatchManifest(
                [
                    first,
                    second
                ]
            )
        );

        fixture.ApplyBatch();

        string sharedDirectory =
            Path.GetDirectoryName(
                first.DestinationPath
            )!;

        Assert.Equal(
            sharedDirectory,
            Path.GetDirectoryName(
                second.DestinationPath
            )
        );

        Assert.True(
            Directory.Exists(
                sharedDirectory
            )
        );

        Assert.True(
            File.Exists(
                first.DestinationPath
            )
        );

        Assert.True(
            File.Exists(
                second.DestinationPath
            )
        );

        /*
         * Reverse batch order must retire child 2's BatchReused
         * directory journal without removing the shared directory.
         *
         * Child 1 is the genuine owner. Only after child 2 has
         * completed rollback may child 1 remove its own file and
         * then the now-empty shared directory.
         */
        Assert.Equal(
            0,
            fixture.RunBatchRollback()
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

        Assert.False(
            Directory.Exists(
                sharedDirectory
            )
        );

        Assert.True(
            File.Exists(
                first.SourcePath
            )
        );

        Assert.True(
            File.Exists(
                second.SourcePath
            )
        );

        /*
         * The borrower is already durably RolledBack while the
         * genuine owner has subsequently removed the shared final
         * directory. A completed batch rollback must therefore
         * remain idempotent when the borrower classifies as
         * ReusedRolledBackFinalMissing.
         */
        Assert.Equal(
            0,
            fixture.RunBatchRollback()
        );

        Assert.False(
            Directory.Exists(
                sharedDirectory
            )
        );

        Assert.True(
            File.Exists(
                first.SourcePath
            )
        );

        Assert.True(
            File.Exists(
                second.SourcePath
            )
        );
    }

    [Fact]
    public void
        Run_AlreadyRolledBackCompletedBatch_RerunSucceeds()
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

        fixture.ApplyBatch();

        Assert.Equal(
            0,
            fixture.RunBatchRollback()
        );

        Assert.False(
            File.Exists(
                plan.DestinationPath
            )
        );

        Assert.True(
            File.Exists(
                plan.SourcePath
            )
        );

        Assert.Equal(
            0,
            fixture.RunBatchRollback()
        );

        Assert.False(
            File.Exists(
                plan.DestinationPath
            )
        );

        Assert.True(
            File.Exists(
                plan.SourcePath
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
                    "casecompat-repair-rollback-batch-command-tests",
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
                $"batch-rollback-payload-{index}";

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

        public void ApplyPlan(
            PlanSpec plan)
        {
            int result =
                global::RepairApplyCommand.Run(
                    [
                        "repair-apply",
                        plan.ChildDirectoryPath,
                        ManifestName,
                        DataRoot
                    ]
                );

            Assert.Equal(
                0,
                result
            );

            Assert.Equal(
                plan.Payload,
                File.ReadAllText(
                    plan.DestinationPath
                )
            );
        }

        public void ApplyBatch()
        {
            int result =
                global::RepairApplyBatchCommand.Run(
                    [
                        "repair-apply-batch",
                        BatchRoot,
                        ManifestName,
                        DataRoot
                    ]
                );

            Assert.Equal(
                0,
                result
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

        public int RunBatchRollback()
        {
            return
                global::RepairRollbackBatchCommand.Run(
                    [
                        "repair-rollback-batch",
                        BatchRoot,
                        ManifestName,
                        DataRoot
                    ]
                );
        }

        public int RunBatchRollbackShort()
        {
            return
                global::RepairRollbackBatchCommand.Run(
                    [
                        "repair-rollback-batch",
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
