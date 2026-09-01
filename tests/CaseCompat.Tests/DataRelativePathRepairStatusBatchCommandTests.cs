using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class DataRelativePathRepairStatusBatchCommandTests
{
    private const string BatchManifestName =
        "batch-manifest.json";

    [Fact]
    public void MissingArguments_ReturnsUsageError()
    {
        int result =
            RepairStatusBatchCommand.Run(
                [
                    "repair-status-batch"
                ]
            );

        Assert.Equal(
            2,
            result
        );
    }

    [Fact]
    public void InvalidManifestName_ReturnsUsageError()
    {
        int result =
            RepairStatusBatchCommand.Run(
                [
                    "repair-status-batch",
                    "unused-batch",
                    "../repair-plan.json",
                    "unused-data"
                ]
            );

        Assert.Equal(
            2,
            result
        );
    }

    [Fact]
    public void EmptyBatch_ReturnsSuccessWithoutDataMutation()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        int result =
            fixture.RunBatchStatus();

        Assert.Equal(
            0,
            result
        );

        Assert.False(
            Directory.Exists(
                fixture.RepairDestinationDirectory
            )
        );
    }

    [Fact]
    public void ContiguousPlans_ReturnSuccessWithoutDataMutation()
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

        fixture.CreatePlan(
            "plan-000002"
        );

        int result =
            fixture.RunBatchStatus();

        Assert.Equal(
            0,
            result
        );

        Assert.False(
            Directory.Exists(
                fixture.RepairDestinationDirectory
            )
        );
    }

    [Fact]
    public void NumberingGap_IsRejectedBeforePlanInspection()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        Directory.CreateDirectory(
            Path.Combine(
                fixture.BatchRoot,
                "plan-000002"
            )
        );

        int result =
            fixture.RunBatchStatus();

        Assert.Equal(
            4,
            result
        );
    }

    [Fact]
    public void UnexpectedDirectChild_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        File.WriteAllText(
            Path.Combine(
                fixture.BatchRoot,
                "notes.txt"
            ),
            "unexpected"
        );

        int result =
            fixture.RunBatchStatus();

        Assert.Equal(
            4,
            result
        );
    }

    [Fact]
    public void SymlinkPlanChild_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        string target =
            Path.Combine(
                fixture.RootPath,
                "RealPlan"
            );

        Directory.CreateDirectory(
            target
        );

        Directory.CreateSymbolicLink(
            Path.Combine(
                fixture.BatchRoot,
                "plan-000001"
            ),
            target
        );

        int result =
            fixture.RunBatchStatus();

        Assert.Equal(
            4,
            result
        );
    }

    [Fact]
    public void RegularFilePlanChild_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        File.WriteAllText(
            Path.Combine(
                fixture.BatchRoot,
                "plan-000001"
            ),
            "not a directory"
        );

        int result =
            fixture.RunBatchStatus();

        Assert.Equal(
            4,
            result
        );
    }

    [Fact]
    public void MissingChildManifest_FailsWholeBatchInspection()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        Directory.CreateDirectory(
            Path.Combine(
                fixture.BatchRoot,
                "plan-000001"
            )
        );

        int result =
            fixture.RunBatchStatus();

        Assert.Equal(
            4,
            result
        );
    }

    [Fact]
    public void TrustedDataMismatch_FailsWholeBatchInspection()
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

        string otherData =
            Path.Combine(
                fixture.RootPath,
                "OtherData"
            );

        Directory.CreateDirectory(
            otherData
        );

        int result =
            RepairStatusBatchCommand.Run(
                [
                    "repair-status-batch",
                    fixture.BatchRoot,
                    fixture.ManifestName,
                    otherData
                ]
            );

        Assert.Equal(
            4,
            result
        );

        Assert.False(
            Directory.Exists(
                fixture.RepairDestinationDirectory
            )
        );
    }

    [Fact]
    public void ManifestBackedBatch_ReturnsSuccessWithoutDataMutation()
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

        int result =
            fixture.RunBatchStatus();

        Assert.Equal(
            0,
            result
        );

        Assert.False(
            Directory.Exists(
                fixture.RepairDestinationDirectory
            )
        );
    }

    [Fact]
    public void ManifestBackedZeroChildBatch_ReturnsSuccess()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        fixture.CreateBatchManifest(
            inputPathCount:
                3,
            safeRejectionCount:
                3,
            childNames:
                []
        );

        int result =
            fixture.RunBatchStatus();

        Assert.Equal(
            0,
            result
        );

        Assert.False(
            Directory.Exists(
                fixture.RepairDestinationDirectory
            )
        );
    }

    [Fact]
    public void ManifestBackedChildManifestNameMismatch_IsRejected()
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

        fixture.WriteBatchManifest(
            manifest with
            {
                ChildManifestName =
                    "other-plan.json"
            }
        );

        Assert.Equal(
            4,
            fixture.RunBatchStatus()
        );
    }

    [Fact]
    public void ManifestBackedDataRootMismatch_IsRejected()
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

        string otherData =
            Path.Combine(
                fixture.RootPath,
                "OtherData"
            );

        Directory.CreateDirectory(
            otherData
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

        fixture.WriteBatchManifest(
            manifest with
            {
                DataRoot =
                    otherData
            }
        );

        Assert.Equal(
            4,
            fixture.RunBatchStatus()
        );
    }

    [Fact]
    public void ManifestBackedExtraRootChild_IsRejected()
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
                "notes.txt"
            ),
            "unexpected"
        );

        Assert.Equal(
            4,
            fixture.RunBatchStatus()
        );
    }

    [Fact]
    public void ManifestBackedMissingRecordedChild_IsRejected()
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

        Directory.Delete(
            Path.Combine(
                fixture.BatchRoot,
                "plan-000001"
            ),
            recursive:
                true
        );

        Assert.Equal(
            4,
            fixture.RunBatchStatus()
        );
    }

    [Fact]
    public void ManifestBackedPlanIdMismatch_IsRejected()
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

        fixture.WriteBatchManifest(
            manifest with
            {
                Children =
                    [
                        manifest.Children[0] with
                        {
                            PlanId =
                                Guid.NewGuid()
                        }
                    ]
            }
        );

        Assert.Equal(
            4,
            fixture.RunBatchStatus()
        );
    }

    [Fact]
    public void ManifestBackedManifestSha256Mismatch_IsRejected()
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

        fixture.WriteBatchManifest(
            manifest with
            {
                Children =
                    [
                        manifest.Children[0] with
                        {
                            ManifestSha256 =
                                new string(
                                    '0',
                                    64
                                )
                        }
                    ]
            }
        );

        Assert.Equal(
            4,
            fixture.RunBatchStatus()
        );
    }

    [Fact]
    public void InvalidBatchManifest_FailsInsteadOfLegacyFallback()
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

        File.WriteAllText(
            Path.Combine(
                fixture.BatchRoot,
                BatchManifestName
            ),
            "not valid JSON"
        );

        Assert.Equal(
            4,
            fixture.RunBatchStatus()
        );
    }

    [Fact]
    public void SymlinkBatchManifest_FailsInsteadOfLegacyFallback()
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

        string target =
            Path.Combine(
                fixture.RootPath,
                "outside-batch-manifest.json"
            );

        File.WriteAllText(
            target,
            "{}"
        );

        File.CreateSymbolicLink(
            Path.Combine(
                fixture.BatchRoot,
                BatchManifestName
            ),
            target
        );

        Assert.Equal(
            4,
            fixture.RunBatchStatus()
        );
    }

    private sealed class Fixture : IDisposable
    {
        private Fixture(
            string rootPath,
            string dataRoot,
            string batchRoot,
            string manifestName,
            string requestedPath,
            string repairDestinationDirectory)
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

            RepairDestinationDirectory =
                repairDestinationDirectory;
        }

        public string RootPath { get; }

        public string DataRoot { get; }

        public string BatchRoot { get; }

        public string ManifestName { get; }

        public string RequestedPath { get; }

        public string RepairDestinationDirectory { get; }

        public static Fixture Create()
        {
            string rootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-repair-status-batch-tests",
                    Guid.NewGuid()
                        .ToString("N")
                );

            string dataRoot =
                Path.Combine(
                    rootPath,
                    "Data"
                );

            string batchRoot =
                Path.Combine(
                    rootPath,
                    "Batch"
                );

            string sourceDirectory =
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "fafny stash",
                    "Bishop Armor"
                );

            Directory.CreateDirectory(
                sourceDirectory
            );

            File.WriteAllText(
                Path.Combine(
                    sourceDirectory,
                    "armor.nif"
                ),
                "batch-status-test-payload"
            );

            Directory.CreateDirectory(
                batchRoot
            );

            return new Fixture(
                rootPath,
                dataRoot,
                batchRoot,
                "repair-plan.json",
                "meshes/Fafny stash/Bishop Armor/armor.nif",
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "Fafny stash"
                )
            );
        }

        public void CreatePlan(
            string childName)
        {
            string childPath =
                Path.Combine(
                    BatchRoot,
                    childName
                );

            Directory.CreateDirectory(
                childPath
            );

            int result =
                RepairPlanCommand.Run(
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

            Assert.False(
                Directory.Exists(
                    RepairDestinationDirectory
                )
            );
        }

        public DataRelativePathRepairBatchManifestRecord
            BuildBatchManifest(
                int inputPathCount,
                int safeRejectionCount,
                IReadOnlyList<string> childNames)
        {
            var children =
                new List<
                    DataRelativePathRepairBatchManifestChild
                >();

            foreach (string childName in childNames)
            {
                string childPath =
                    Path.Combine(
                        BatchRoot,
                        childName
                    );

                LinuxNoFollowPathOpenResult childOpen =
                    LinuxNoFollowPath.OpenRootReadOnly(
                        childPath
                    );

                Assert.True(
                    childOpen.Success,
                    childOpen.Error
                );

                using LinuxNoFollowPathHandle childDirectory =
                    childOpen.OpenedPath!;

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
            LinuxNoFollowPathOpenResult batchOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    BatchRoot
                );

            Assert.True(
                batchOpen.Success,
                batchOpen.Error
            );

            using LinuxNoFollowPathHandle batchDirectory =
                batchOpen.OpenedPath!;

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

        public int RunBatchStatus()
        {
            return
                RepairStatusBatchCommand.Run(
                    [
                        "repair-status-batch",
                        BatchRoot,
                        ManifestName,
                        DataRoot
                    ]
                );
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
