using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairBatchCompletionInspectorTests
{
    private const string BatchManifestName =
        "batch-manifest.json";

    [Fact]
    public void Inspect_CompletedBatch_ReturnsVerified()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        fixture.CreatePlan(
            "plan-000001"
        );

        fixture.CreateBatchManifest(
            inputPathCount:
                1,
            safeRejectionCount:
                0,
            childNames:
                [
                    "plan-000001"
                ]
        );

        DataRelativePathRepairBatchCompletionInspection inspection =
            fixture.Inspect();

        Assert.True(
            inspection.Success,
            inspection.Error
        );

        Assert.Equal(
            DataRelativePathRepairBatchCompletionInspectionState
                .Verified,
            inspection.State
        );

        Assert.NotNull(
            inspection.Manifest
        );

        Assert.Single(
            inspection.Children
        );

        Assert.Equal(
            "plan-000001",
            inspection.Children[0].ChildName
        );
    }

    [Fact]
    public void Inspect_CompletedZeroChildBatch_ReturnsVerified()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        fixture.CreateBatchManifest(
            inputPathCount:
                0,
            safeRejectionCount:
                0,
            childNames:
                []
        );

        DataRelativePathRepairBatchCompletionInspection inspection =
            fixture.Inspect();

        Assert.True(
            inspection.Success,
            inspection.Error
        );

        Assert.Equal(
            DataRelativePathRepairBatchCompletionInspectionState
                .Verified,
            inspection.State
        );

        Assert.Empty(
            inspection.Children
        );
    }

    [Fact]
    public void Inspect_MissingBatchManifest_ReturnsManifestUnavailable()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        fixture.CreatePlan(
            "plan-000001"
        );

        DataRelativePathRepairBatchCompletionInspection inspection =
            fixture.Inspect();

        Assert.False(
            inspection.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchCompletionInspectionState
                .ManifestUnavailable,
            inspection.State
        );

        Assert.NotNull(
            inspection.Enumeration
        );

        Assert.Null(
            inspection.Manifest
        );
    }

    [Fact]
    public void Inspect_ExtraRootChild_ReturnsTopologyInvalid()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        fixture.CreatePlan(
            "plan-000001"
        );

        fixture.CreateBatchManifest(
            inputPathCount:
                1,
            safeRejectionCount:
                0,
            childNames:
                [
                    "plan-000001"
                ]
        );

        File.WriteAllText(
            Path.Combine(
                fixture.BatchRoot,
                "unexpected"
            ),
            "unexpected"
        );

        DataRelativePathRepairBatchCompletionInspection inspection =
            fixture.Inspect();

        Assert.False(
            inspection.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchCompletionInspectionState
                .TopologyInvalid,
            inspection.State
        );

        Assert.Empty(
            inspection.Children
        );
    }

    [Fact]
    public void Inspect_PlanIdMismatch_ReturnsChildPlanIdMismatch()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        fixture.CreatePlan(
            "plan-000001"
        );

        DataRelativePathRepairBatchManifestRecord manifest =
            fixture.BuildBatchManifest(
                inputPathCount:
                    1,
                safeRejectionCount:
                    0,
                childNames:
                    [
                        "plan-000001"
                    ]
            );

        DataRelativePathRepairBatchManifestChild child =
            Assert.Single(
                manifest.Children
            );

        Guid wrongPlanId;

        do
        {
            wrongPlanId =
                Guid.NewGuid();
        }
        while (wrongPlanId == child.PlanId);

        manifest =
            manifest with
            {
                Children =
                    [
                        child with
                        {
                            PlanId =
                                wrongPlanId
                        }
                    ]
            };

        fixture.WriteBatchManifest(
            manifest
        );

        DataRelativePathRepairBatchCompletionInspection inspection =
            fixture.Inspect();

        Assert.False(
            inspection.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchCompletionInspectionState
                .ChildPlanIdMismatch,
            inspection.State
        );

        Assert.Equal(
            "plan-000001",
            inspection.FailedChildName
        );

        Assert.Empty(
            inspection.Children
        );
    }

    [Fact]
    public void
        Inspect_ManifestSha256Mismatch_ReturnsChildManifestSha256Mismatch()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        fixture.CreatePlan(
            "plan-000001"
        );

        DataRelativePathRepairBatchManifestRecord manifest =
            fixture.BuildBatchManifest(
                inputPathCount:
                    1,
                safeRejectionCount:
                    0,
                childNames:
                    [
                        "plan-000001"
                    ]
            );

        DataRelativePathRepairBatchManifestChild child =
            Assert.Single(
                manifest.Children
            );

        char first =
            child.ManifestSha256[0] == '0'
                ? '1'
                : '0';

        string wrongSha256 =
            $"{first}{child.ManifestSha256[1..]}";

        Assert.Equal(
            64,
            wrongSha256.Length
        );

        manifest =
            manifest with
            {
                Children =
                    [
                        child with
                        {
                            ManifestSha256 =
                                wrongSha256
                        }
                    ]
            };

        fixture.WriteBatchManifest(
            manifest
        );

        DataRelativePathRepairBatchCompletionInspection inspection =
            fixture.Inspect();

        Assert.False(
            inspection.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchCompletionInspectionState
                .ChildManifestSha256Mismatch,
            inspection.State
        );

        Assert.Equal(
            "plan-000001",
            inspection.FailedChildName
        );

        Assert.Empty(
            inspection.Children
        );
    }

    private sealed class Fixture
        : IDisposable
    {
        private Fixture(
            string rootPath,
            string dataRoot,
            string batchRoot,
            string manifestName,
            string requestedPath)
        {
            RootPath =
                rootPath;

            DataRoot =
                dataRoot;

            BatchRoot =
                batchRoot;

            ManifestName =
                manifestName;

            RequestedPath =
                requestedPath;
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

        public string RequestedPath
        {
            get;
        }

        public static Fixture Create()
        {
            string rootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-batch-completion-inspector-tests",
                    Guid.NewGuid().ToString("N")
                );

            string dataRoot =
                Directory.CreateDirectory(
                    Path.Combine(
                        rootPath,
                        "Data"
                    )
                ).FullName;

            string physicalSourceDirectory =
                Directory.CreateDirectory(
                    Path.Combine(
                        dataRoot,
                        "meshes",
                        "example"
                    )
                ).FullName;

            File.WriteAllText(
                Path.Combine(
                    physicalSourceDirectory,
                    "Thing.nif"
                ),
                "batch-completion-inspector-test-payload"
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
                "repair-plan.json",
                "meshes/Example/Thing.nif"
            );
        }

        public void CreatePlan(
            string childName)
        {
            string childPath =
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
                        RequestedPath,
                        childPath,
                        ManifestName
                    ]
                );

            Assert.Equal(
                0,
                result
            );
        }

        public DataRelativePathRepairBatchManifestRecord
            BuildBatchManifest(
                int inputPathCount,
                int safeRejectionCount,
                IReadOnlyList<string> childNames)
        {
            using LinuxNoFollowPathHandle batchDirectory =
                OpenDirectory(
                    BatchRoot
                );

            var children =
                new List<
                    DataRelativePathRepairBatchManifestChild>(
                        childNames.Count
                    );

            foreach (
                string childName
                in childNames)
            {
                LinuxOpenChildReadOnlyAtResult childOpen =
                    LinuxOpenChildReadOnlyAt.Open(
                        batchDirectory,
                        childName
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

                children.Add(
                    new(
                        ChildName:
                            childName,
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
                        inputPathCount,
                    safeRejectionCount:
                        safeRejectionCount,
                    children:
                        children
                );

            Assert.True(
                creation.Success,
                creation.Error
            );

            return creation.Manifest!;
        }

        public void CreateBatchManifest(
            int inputPathCount,
            int safeRejectionCount,
            IReadOnlyList<string> childNames)
        {
            WriteBatchManifest(
                BuildBatchManifest(
                    inputPathCount,
                    safeRejectionCount,
                    childNames
                )
            );
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

        public DataRelativePathRepairBatchCompletionInspection
            Inspect()
        {
            using LinuxNoFollowPathHandle batchDirectory =
                OpenDirectory(
                    BatchRoot
                );

            return
                DataRelativePathRepairBatchCompletionInspector.Inspect(
                    batchDirectory,
                    BatchManifestName,
                    ManifestName,
                    DataRoot
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
