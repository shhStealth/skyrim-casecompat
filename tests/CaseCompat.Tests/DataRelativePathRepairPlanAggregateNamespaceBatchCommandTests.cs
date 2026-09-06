using CaseCompat.Core.Analysis;
using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairPlanAggregateNamespaceBatchCommandTests
{
    private const string BatchManifestName =
        "batch-manifest.json";

    private const string DefaultPlanManifestName =
        "repair-plan.json";

    [Fact]
    public void Run_MissingArguments_ReturnsUsageErrorWithoutPublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        int result =
            global::RepairPlanAggregateNamespaceBatchCommand.Run(
                [
                    "repair-plan-aggregate-namespace-batch"
                ]
            );

        Assert.Equal(
            2,
            result
        );
    }

    [Fact]
    public void Run_TooManyArguments_ReturnsUsageErrorWithoutPublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        int result =
            global::RepairPlanAggregateNamespaceBatchCommand.Run(
                [
                    "repair-plan-aggregate-namespace-batch",
                    fixture.DataRoot,
                    fixture.PathList,
                    fixture.SidecarPath,
                    fixture.BatchDirectory,
                    DefaultPlanManifestName,
                    "unexpected"
                ]
            );

        Assert.Equal(
            2,
            result
        );

        AssertBatchEmpty(
            fixture.BatchDirectory
        );
    }

    [Fact]
    public void Run_InvalidPlanManifestName_ReturnsUsageErrorWithoutPublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        int result =
            fixture.Run(
                manifestName:
                    "child/repair-plan.json"
            );

        Assert.Equal(
            2,
            result
        );

        AssertBatchEmpty(
            fixture.BatchDirectory
        );
    }

    [Fact]
    public void Run_MissingPathList_FailsBeforePublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        string[] args =
            fixture.Args();

        args[2] =
            Path.Combine(
                fixture.RootPath,
                "missing-paths.txt"
            );

        int result =
            global::RepairPlanAggregateNamespaceBatchCommand.Run(
                args
            );

        Assert.NotEqual(
            0,
            result
        );

        AssertBatchEmpty(
            fixture.BatchDirectory
        );
    }

    [Fact]
    public void Run_MissingSidecar_FailsBeforePublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        File.Delete(
            fixture.SidecarPath
        );

        int result =
            fixture.Run();

        Assert.NotEqual(
            0,
            result
        );

        AssertBatchEmpty(
            fixture.BatchDirectory
        );
    }

    [Fact]
    public void Run_BatchDirectoryInsideData_FailsBeforePublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        string insideData =
            Directory.CreateDirectory(
                Path.Combine(
                    fixture.DataRoot,
                    "CaseCompatBatch"
                )
            ).FullName;

        string[] args =
            fixture.Args();

        args[4] =
            insideData;

        int result =
            global::RepairPlanAggregateNamespaceBatchCommand.Run(
                args
            );

        Assert.NotEqual(
            0,
            result
        );

        AssertBatchEmpty(
            insideData
        );
    }

    [Fact]
    public void Run_NonEmptyBatchDirectory_FailsBeforePublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        string sentinel =
            Path.Combine(
                fixture.BatchDirectory,
                "sentinel.txt"
            );

        File.WriteAllText(
            sentinel,
            "do-not-touch"
        );

        int result =
            fixture.Run();

        Assert.NotEqual(
            0,
            result
        );

        Assert.Equal(
            new[]
            {
                "sentinel.txt"
            },
            Directory
                .EnumerateFileSystemEntries(
                    fixture.BatchDirectory
                )
                .Select(
                    path =>
                        Path.GetFileName(
                            path
                        )!
                )
                .OrderBy(
                    name =>
                        name,
                    StringComparer.Ordinal
                )
                .ToArray()
        );

        Assert.Equal(
            "do-not-touch",
            File.ReadAllText(
                sentinel
            )
        );
    }

    [Fact]
    public void Run_DuplicateInput_FailsBeforePublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        File.WriteAllLines(
            fixture.PathList,
            [
                fixture.RequestedPaths[0],
                fixture.RequestedPaths[0]
            ]
        );

        int result =
            fixture.Run();

        Assert.NotEqual(
            0,
            result
        );

        AssertBatchEmpty(
            fixture.BatchDirectory
        );
    }

    [Fact]
    public void Run_CandidateOutsideSidecarRoot_FailsBeforePublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        fixture.ConfigureTexturesCandidate();

        int result =
            fixture.Run();

        Assert.NotEqual(
            0,
            result
        );

        AssertBatchEmpty(
            fixture.BatchDirectory
        );
    }

    [Fact]
    public void Run_InvalidSidecar_FailsBeforePublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        File.WriteAllText(
            fixture.SidecarPath,
            "{}"
        );

        int result =
            fixture.Run();

        Assert.NotEqual(
            0,
            result
        );

        AssertBatchEmpty(
            fixture.BatchDirectory
        );
    }

    [Fact]
    public void Run_SidecarDataRootMismatch_FailsBeforePublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        fixture.ReplaceSidecarWithForeign();

        int result =
            fixture.Run();

        Assert.NotEqual(
            0,
            result
        );

        AssertBatchEmpty(
            fixture.BatchDirectory
        );
    }

    [Fact]
    public void Run_EquivalentMultipleRepresentations_BlocksAllPublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        fixture.AddSecondRootRepresentation(
            conflicting:
                false
        );

        fixture.RewriteMeshesSidecar();

        int result =
            fixture.Run();

        Assert.NotEqual(
            0,
            result
        );

        AssertBatchEmpty(
            fixture.BatchDirectory
        );
    }

    [Fact]
    public void Run_ConflictingMultipleRepresentations_BlocksAllPublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        fixture.AddSecondRootRepresentation(
            conflicting:
                true
        );

        fixture.RewriteMeshesSidecar();

        int result =
            fixture.Run();

        Assert.NotEqual(
            0,
            result
        );

        AssertBatchEmpty(
            fixture.BatchDirectory
        );
    }

    [Fact]
    public void Run_SourceGenerationBindingFailure_BlocksAllPublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        if (!SupportsManifestPublication(
                fixture.BatchDirectory))
        {
            return;
        }

        var hooks =
            new global::RepairPlanAggregateNamespaceBatchCommand.TestHooks(
                AfterGate1BeforeGate2:
                    fixture.ReplacePrimarySource
            );

        int result =
            fixture.Run(
                hooks:
                    hooks
            );

        Assert.NotEqual(
            0,
            result
        );

        AssertBatchEmpty(
            fixture.BatchDirectory
        );
    }

    [Fact]
    public void Run_SingleUniqueCandidate_PublishesSchemaV4PolicyV3Batch()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        if (!SupportsManifestPublication(
                fixture.BatchDirectory))
        {
            return;
        }

        int result =
            fixture.Run();

        Assert.Equal(
            0,
            result
        );

        DataRelativePathRepairBatchManifestReaderResult batch =
            ReadBatch(
                fixture.BatchDirectory
            );

        Assert.True(
            batch.Success,
            batch.Error
        );

        DataRelativePathRepairBatchManifestRecord batchManifest =
            Assert.IsType<
                DataRelativePathRepairBatchManifestRecord>(
                    batch.Manifest
                );

        Assert.Equal(
            DataRelativePathRepairBatchManifestRecord.SchemaVersion4,
            batchManifest.SchemaVersion
        );

        Assert.Equal(
            DataRelativePathRepairBatchManifestRecord.CoveragePolicyVersion3,
            batchManifest.CoveragePolicyVersion
        );

        Assert.Equal(
            1,
            batchManifest.InputPathCount
        );

        Assert.Equal(
            0,
            batchManifest.SafeRejectionCount
        );

        DataRelativePathRepairBatchManifestChild child =
            Assert.Single(
                batchManifest.Children
            );

        Assert.Equal(
            "plan-000001",
            child.ChildName
        );

        DataRelativePathRepairPlanManifestReaderResult childRead =
            ReadChild(
                fixture.BatchDirectory,
                child.ChildName,
                DefaultPlanManifestName
            );

        Assert.True(
            childRead.Success,
            childRead.Error
        );

        DataRelativePathRepairPlanManifestRecord childManifest =
            Assert.IsType<
                DataRelativePathRepairPlanManifestRecord>(
                    childRead.Manifest
                );

        Assert.Equal(
            DataRelativePathRepairPlanManifestRecord.SchemaVersion4,
            childManifest.SchemaVersion
        );

        Assert.Equal(
            child.PlanId,
            childManifest.PlanId
        );

        Assert.Equal(
            child.ManifestSha256,
            childRead.ManifestSha256,
            StringComparer.OrdinalIgnoreCase
        );

        Assert.False(
            Directory.Exists(
                fixture.DestinationFaceParts
            )
        );
    }

    [Fact]
    public void Run_MultipleUniqueCandidates_PreserveInputOrderAndExactMembership()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create(
                fileCount:
                    2
            );

        if (!SupportsManifestPublication(
                fixture.BatchDirectory))
        {
            return;
        }

        int result =
            fixture.Run();

        Assert.Equal(
            0,
            result
        );

        DataRelativePathRepairBatchManifestReaderResult batch =
            ReadBatch(
                fixture.BatchDirectory
            );

        Assert.True(
            batch.Success,
            batch.Error
        );

        DataRelativePathRepairBatchManifestRecord batchManifest =
            Assert.IsType<
                DataRelativePathRepairBatchManifestRecord>(
                    batch.Manifest
                );

        Assert.Equal(
            new[]
            {
                "plan-000001",
                "plan-000002"
            },
            batchManifest.Children
                .Select(
                    child =>
                        child.ChildName
                )
                .ToArray()
        );

        Assert.Equal(
            fixture.RequestedPaths,
            batchManifest.Children
                .Select(
                    child =>
                    {
                        DataRelativePathRepairPlanManifestReaderResult read =
                            ReadChild(
                                fixture.BatchDirectory,
                                child.ChildName,
                                DefaultPlanManifestName
                            );

                        return Assert.IsType<
                            DataRelativePathRepairPlanManifestRecord>(
                                read.Manifest
                            )
                            .RequestedPath;
                    }
                )
                .ToArray()
        );

        Assert.All(
            batchManifest.Children,
            child =>
            {
                DataRelativePathRepairPlanManifestReaderResult read =
                    ReadChild(
                        fixture.BatchDirectory,
                        child.ChildName,
                        DefaultPlanManifestName
                    );

                Assert.True(
                    read.Success,
                    read.Error
                );

                DataRelativePathRepairPlanManifestRecord manifest =
                    Assert.IsType<
                        DataRelativePathRepairPlanManifestRecord>(
                            read.Manifest
                        );

                Assert.Equal(
                    child.PlanId,
                    manifest.PlanId
                );

                Assert.Equal(
                    child.ManifestSha256,
                    read.ManifestSha256,
                    StringComparer.OrdinalIgnoreCase
                );
            }
        );
    }

    [Fact]
    public void Run_OptionalPlanManifestName_IsUsedForEveryChild()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create(
                fileCount:
                    2
            );

        if (!SupportsManifestPublication(
                fixture.BatchDirectory))
        {
            return;
        }

        const string customName =
            "aggregate-authorized-plan.json";

        int result =
            fixture.Run(
                manifestName:
                    customName
            );

        Assert.Equal(
            0,
            result
        );

        DataRelativePathRepairBatchManifestReaderResult batch =
            ReadBatch(
                fixture.BatchDirectory
            );

        Assert.True(
            batch.Success,
            batch.Error
        );

        DataRelativePathRepairBatchManifestRecord batchManifest =
            Assert.IsType<
                DataRelativePathRepairBatchManifestRecord>(
                    batch.Manifest
                );

        Assert.Equal(
            customName,
            batchManifest.ChildManifestName
        );

        foreach (
            DataRelativePathRepairBatchManifestChild child
            in batchManifest.Children)
        {
            string childPath =
                Path.Combine(
                    fixture.BatchDirectory,
                    child.ChildName
                );

            Assert.True(
                File.Exists(
                    Path.Combine(
                        childPath,
                        customName
                    )
                )
            );

            Assert.False(
                File.Exists(
                    Path.Combine(
                        childPath,
                        DefaultPlanManifestName
                    )
                )
            );
        }
    }

    [Fact]
    public void Run_Gate2_SidecarReplacement_BlocksBeforeFirstChildPublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        if (!SupportsManifestPublication(
                fixture.BatchDirectory))
        {
            return;
        }

        var hooks =
            new global::RepairPlanAggregateNamespaceBatchCommand.TestHooks(
                AfterGate1BeforeGate2:
                    fixture.ReplaceSidecarWithForeign
            );

        int result =
            fixture.Run(
                hooks:
                    hooks
            );

        Assert.NotEqual(
            0,
            result
        );

        AssertBatchEmpty(
            fixture.BatchDirectory
        );
    }

    [Fact]
    public void Run_Gate2_SourceReplacement_BlocksBeforeFirstChildPublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        if (!SupportsManifestPublication(
                fixture.BatchDirectory))
        {
            return;
        }

        var hooks =
            new global::RepairPlanAggregateNamespaceBatchCommand.TestHooks(
                AfterGate1BeforeGate2:
                    fixture.ReplacePrimarySource
            );

        int result =
            fixture.Run(
                hooks:
                    hooks
            );

        Assert.NotEqual(
            0,
            result
        );

        AssertBatchEmpty(
            fixture.BatchDirectory
        );
    }

    [Fact]
    public void Run_ChildReadbackShaMismatch_BlocksBatchCompletion()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        if (!SupportsManifestPublication(
                fixture.BatchDirectory))
        {
            return;
        }

        var hooks =
            new global::RepairPlanAggregateNamespaceBatchCommand.TestHooks(
                AfterChildPublishedBeforeReadback:
                    _ =>
                    {
                        File.AppendAllText(
                            Path.Combine(
                                fixture.BatchDirectory,
                                "plan-000001",
                                DefaultPlanManifestName
                            ),
                            Environment.NewLine
                        );
                    }
            );

        int result =
            fixture.Run(
                hooks:
                    hooks
            );

        Assert.NotEqual(
            0,
            result
        );

        Assert.True(
            Directory.Exists(
                Path.Combine(
                    fixture.BatchDirectory,
                    "plan-000001"
                )
            )
        );

        Assert.False(
            File.Exists(
                Path.Combine(
                    fixture.BatchDirectory,
                    BatchManifestName
                )
            )
        );
    }

    [Fact]
    public void Run_Gate3_SidecarReplacement_BlocksBatchCompletion()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        if (!SupportsManifestPublication(
                fixture.BatchDirectory))
        {
            return;
        }

        var hooks =
            new global::RepairPlanAggregateNamespaceBatchCommand.TestHooks(
                AfterAllChildReadbacksBeforeGate3:
                    fixture.ReplaceSidecarWithForeign
            );

        int result =
            fixture.Run(
                hooks:
                    hooks
            );

        Assert.NotEqual(
            0,
            result
        );

        Assert.False(
            File.Exists(
                Path.Combine(
                    fixture.BatchDirectory,
                    BatchManifestName
                )
            )
        );
    }

    [Fact]
    public void Run_Gate3_SourceReplacement_BlocksBatchCompletion()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        if (!SupportsManifestPublication(
                fixture.BatchDirectory))
        {
            return;
        }

        var hooks =
            new global::RepairPlanAggregateNamespaceBatchCommand.TestHooks(
                AfterAllChildReadbacksBeforeGate3:
                    fixture.ReplacePrimarySource
            );

        int result =
            fixture.Run(
                hooks:
                    hooks
            );

        Assert.NotEqual(
            0,
            result
        );

        Assert.False(
            File.Exists(
                Path.Combine(
                    fixture.BatchDirectory,
                    BatchManifestName
                )
            )
        );
    }

    [Fact]
    public void Run_Gate3Failure_LeavesChildrenButNoBatchManifest()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create(
                fileCount:
                    2
            );

        if (!SupportsManifestPublication(
                fixture.BatchDirectory))
        {
            return;
        }

        var hooks =
            new global::RepairPlanAggregateNamespaceBatchCommand.TestHooks(
                AfterAllChildReadbacksBeforeGate3:
                    fixture.ReplacePrimarySource
            );

        int result =
            fixture.Run(
                hooks:
                    hooks
            );

        Assert.NotEqual(
            0,
            result
        );

        Assert.True(
            Directory.Exists(
                Path.Combine(
                    fixture.BatchDirectory,
                    "plan-000001"
                )
            )
        );

        Assert.True(
            Directory.Exists(
                Path.Combine(
                    fixture.BatchDirectory,
                    "plan-000002"
                )
            )
        );

        Assert.False(
            File.Exists(
                Path.Combine(
                    fixture.BatchDirectory,
                    BatchManifestName
                )
            )
        );
    }

    [Fact]
    public void Run_Success_DoesNotModifyDataOrSuppliedSidecar()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        if (!SupportsManifestPublication(
                fixture.BatchDirectory))
        {
            return;
        }

        string[] beforeData =
            SnapshotDirectory(
                fixture.DataRoot
            );

        byte[] beforeSidecar =
            File.ReadAllBytes(
                fixture.SidecarPath
            );

        int result =
            fixture.Run();

        Assert.Equal(
            0,
            result
        );

        Assert.Equal(
            beforeData,
            SnapshotDirectory(
                fixture.DataRoot
            )
        );

        Assert.Equal(
            beforeSidecar,
            File.ReadAllBytes(
                fixture.SidecarPath
            )
        );

        Assert.False(
            Directory.Exists(
                fixture.DestinationFaceParts
            )
        );
    }

    [Fact]
    public void Run_Success_GrantsNoRepairExecutionAuthority()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        if (!SupportsManifestPublication(
                fixture.BatchDirectory))
        {
            return;
        }

        int result =
            fixture.Run();

        Assert.Equal(
            0,
            result
        );

        DataRelativePathRepairBatchManifestReaderResult batch =
            ReadBatch(
                fixture.BatchDirectory
            );

        Assert.True(
            batch.Success,
            batch.Error
        );

        DataRelativePathRepairBatchManifestRecord batchManifest =
            Assert.IsType<
                DataRelativePathRepairBatchManifestRecord>(
                    batch.Manifest
                );

        Assert.Equal(
            DataRelativePathRepairBatchManifestRecord.SchemaVersion4,
            batchManifest.SchemaVersion
        );

        Assert.Equal(
            DataRelativePathRepairBatchManifestRecord.CoveragePolicyVersion3,
            batchManifest.CoveragePolicyVersion
        );

        Assert.Equal(
            DataRelativePathRepairBatchManifestRecord.SchemaVersion2,
            DataRelativePathRepairBatchManifestRecord.CurrentSchemaVersion
        );

        Assert.Equal(
            new[]
            {
                BatchManifestName,
                "plan-000001"
            },
            Directory
                .EnumerateFileSystemEntries(
                    fixture.BatchDirectory
                )
                .Select(
                    path =>
                        Path.GetFileName(
                            path
                        )!
                )
                .OrderBy(
                    name =>
                        name,
                    StringComparer.Ordinal
                )
                .ToArray()
        );

        Assert.Equal(
            new[]
            {
                DefaultPlanManifestName
            },
            Directory
                .EnumerateFileSystemEntries(
                    Path.Combine(
                        fixture.BatchDirectory,
                        "plan-000001"
                    )
                )
                .Select(
                    path =>
                        Path.GetFileName(
                            path
                        )!
                )
                .ToArray()
        );

        Assert.DoesNotContain(
            Directory.EnumerateFiles(
                fixture.BatchDirectory,
                "*",
                SearchOption.AllDirectories
            ),
            path =>
                Path.GetFileName(
                    path
                )
                .Contains(
                    "authorization",
                    StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(
                    path
                )
                .Contains(
                    "journal",
                    StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Run_ProgramDispatch_RecognizesCanonicalCommand()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string cliPath =
            Path.Combine(
                AppContext.BaseDirectory,
                "CaseCompat.Cli"
            );

        Assert.True(
            File.Exists(
                cliPath
            ),
            $"Expected built CLI executable at: {cliPath}"
        );

        using var process =
            new Process
            {
                StartInfo =
                    new ProcessStartInfo
                    {
                        FileName =
                            cliPath,
                        UseShellExecute =
                            false,
                        RedirectStandardOutput =
                            true,
                        RedirectStandardError =
                            true,
                        CreateNoWindow =
                            true
                    }
            };

        process.StartInfo.ArgumentList.Add(
            "repair-plan-aggregate-namespace-batch"
        );

        Assert.True(
            process.Start(),
            "Failed to start CaseCompat.Cli."
        );

        string stdout =
            process.StandardOutput.ReadToEnd();

        string stderr =
            process.StandardError.ReadToEnd();

        process.WaitForExit();

        Assert.Equal(
            2,
            process.ExitCode
        );

        Assert.Equal(
            string.Empty,
            stdout
        );

        Assert.Contains(
            "repair-plan-aggregate-namespace-batch requires",
            stderr
        );

        Assert.DoesNotContain(
            "Unknown command",
            stderr
        );
    }

    private static DataRelativePathRepairBatchManifestReaderResult ReadBatch(
        string batchDirectory)
    {
        using LinuxNoFollowPathHandle batch =
            OpenRoot(
                batchDirectory
            );

        return DataRelativePathRepairBatchManifestReader.Read(
            batch,
            BatchManifestName
        );
    }

    private static DataRelativePathRepairPlanManifestReaderResult ReadChild(
        string batchDirectory,
        string childName,
        string manifestName)
    {
        using LinuxNoFollowPathHandle child =
            OpenRoot(
                Path.Combine(
                    batchDirectory,
                    childName
                )
            );

        return DataRelativePathRepairPlanManifestReader.Read(
            child,
            manifestName
        );
    }

    private static LinuxNoFollowPathHandle OpenRoot(
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

        return Assert.IsType<
            LinuxNoFollowPathHandle>(
                opened.OpenedPath
            );
    }

    private static bool SupportsManifestPublication(
        string directoryPath)
    {
        using LinuxNoFollowPathHandle directory =
            OpenRoot(
                directoryPath
            );

        LinuxCreateUnnamedFileAtResult probe =
            LinuxCreateUnnamedFileAt.Create(
                directory
            );

        if (
            probe.State ==
            LinuxCreateUnnamedFileAtState
                .TmpfileUnsupported)
        {
            return false;
        }

        Assert.True(
            probe.Success,
            probe.Error
        );

        probe.OpenedFile!.Dispose();
        return true;
    }

    private static void AssertBatchEmpty(
        string batchDirectory)
    {
        Assert.Empty(
            Directory.EnumerateFileSystemEntries(
                batchDirectory
            )
        );
    }

    private static string[] SnapshotDirectory(
        string root)
    {
        var entries =
            new List<string>();

        foreach (
            string directory
            in Directory.EnumerateDirectories(
                root,
                "*",
                SearchOption.AllDirectories
            ))
        {
            entries.Add(
                "D:" +
                Path.GetRelativePath(
                    root,
                    directory
                )
            );
        }

        foreach (
            string file
            in Directory.EnumerateFiles(
                root,
                "*",
                SearchOption.AllDirectories
            ))
        {
            byte[] bytes =
                File.ReadAllBytes(
                    file
                );

            entries.Add(
                "F:" +
                Path.GetRelativePath(
                    root,
                    file
                ) +
                ":" +
                Convert
                    .ToHexString(
                        SHA256.HashData(
                            bytes
                        )
                    )
                    .ToLowerInvariant()
            );
        }

        return entries
            .OrderBy(
                entry =>
                    entry,
                StringComparer.Ordinal
            )
            .ToArray();
    }

    private sealed class Fixture
        : IDisposable
    {
        private const string RequestedPrefix =
            "meshes/Actors/Character/Character Assets/" +
            "FaceParts/";

        private Fixture(
            int fileCount)
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-aggregate-namespace-cli-tests-" +
                    Guid.NewGuid().ToString("N")
                );

            Directory.CreateDirectory(
                RootPath
            );

            DataRoot =
                Directory.CreateDirectory(
                    Path.Combine(
                        RootPath,
                        "Data"
                    )
                ).FullName;

            string requestedParent =
                Directory.CreateDirectory(
                    Path.Combine(
                        DataRoot,
                        "meshes",
                        "Actors",
                        "Character",
                        "Character Assets"
                    )
                ).FullName;

            AlternateFaceParts =
                Directory.CreateDirectory(
                    Path.Combine(
                        DataRoot,
                        "meshes",
                        "actors",
                        "character",
                        "character assets",
                        "faceparts"
                    )
                ).FullName;

            DestinationFaceParts =
                Path.Combine(
                    requestedParent,
                    "FaceParts"
                );

            SourcePaths =
                new string[fileCount];

            RequestedPaths =
                new string[fileCount];

            for (
                int index = 0;
                index < fileCount;
                index++)
            {
                string fileName =
                    index == 0
                        ? "MaleHeadbrows.tri"
                        : $"Fixture{index}.tri";

                string source =
                    Path.Combine(
                        AlternateFaceParts,
                        fileName
                    );

                File.WriteAllText(
                    source,
                    $"aggregate-namespace-cli-fixture-{index}"
                );

                SourcePaths[index] =
                    source;

                RequestedPaths[index] =
                    RequestedPrefix +
                    fileName;
            }

            PathList =
                Path.Combine(
                    RootPath,
                    "paths.txt"
                );

            File.WriteAllLines(
                PathList,
                RequestedPaths
            );

            EvidenceDirectory =
                Directory.CreateDirectory(
                    Path.Combine(
                        RootPath,
                        "Evidence"
                    )
                ).FullName;

            SidecarPath =
                Path.Combine(
                    EvidenceDirectory,
                    "aggregate-namespace-manifest.json"
                );

            ForeignSidecarPath =
                Path.Combine(
                    EvidenceDirectory,
                    "foreign-aggregate-namespace-manifest.json"
                );

            BatchDirectory =
                Directory.CreateDirectory(
                    Path.Combine(
                        RootPath,
                        "Batch"
                    )
                ).FullName;

            RewriteMeshesSidecar();
            WriteForeignSidecar();
        }

        public string RootPath { get; }

        public string DataRoot { get; }

        public string AlternateFaceParts { get; }

        public string DestinationFaceParts { get; }

        public string[] SourcePaths { get; }

        public string[] RequestedPaths { get; private set; }

        public string PathList { get; }

        public string EvidenceDirectory { get; }

        public string SidecarPath { get; }

        public string ForeignSidecarPath { get; }

        public string BatchDirectory { get; }

        public static Fixture Create(
            int fileCount = 1)
        {
            return new(
                fileCount
            );
        }

        public string[] Args(
            string manifestName = DefaultPlanManifestName)
        {
            return
            [
                "repair-plan-aggregate-namespace-batch",
                DataRoot,
                PathList,
                SidecarPath,
                BatchDirectory,
                manifestName
            ];
        }

        public int Run(
            string manifestName = DefaultPlanManifestName,
            global::RepairPlanAggregateNamespaceBatchCommand.TestHooks?
                hooks = null)
        {
            string[] args =
                Args(
                    manifestName
                );

            return hooks is null
                ? global::RepairPlanAggregateNamespaceBatchCommand.Run(
                    args
                )
                : global::RepairPlanAggregateNamespaceBatchCommand.Run(
                    args,
                    hooks
                );
        }

        public void RewriteMeshesSidecar()
        {
            WriteSidecar(
                DataRoot,
                "meshes",
                SidecarPath
            );
        }

        public void ReplaceSidecarWithForeign()
        {
            File.Delete(
                SidecarPath
            );

            File.Copy(
                ForeignSidecarPath,
                SidecarPath
            );
        }

        public void ReplacePrimarySource()
        {
            string source =
                SourcePaths[0];

            File.Delete(
                source
            );

            File.WriteAllText(
                source,
                "replacement-source-" +
                Guid.NewGuid().ToString("N")
            );
        }

        public void AddSecondRootRepresentation(
            bool conflicting)
        {
            string secondRoot =
                Directory.CreateDirectory(
                    Path.Combine(
                        DataRoot,
                        "Meshes",
                        "actors",
                        "character",
                        "character assets",
                        "faceparts"
                    )
                ).FullName;

            string duplicate =
                Path.Combine(
                    secondRoot,
                    Path.GetFileName(
                        SourcePaths[0]
                    )
                );

            File.WriteAllText(
                duplicate,
                conflicting
                    ? "conflicting-content"
                    : File.ReadAllText(
                        SourcePaths[0]
                    )
            );
        }

        public void ConfigureTexturesCandidate()
        {
            string requestedParent =
                Directory.CreateDirectory(
                    Path.Combine(
                        DataRoot,
                        "textures",
                        "Actors",
                        "Character",
                        "Character Assets"
                    )
                ).FullName;

            string alternate =
                Directory.CreateDirectory(
                    Path.Combine(
                        DataRoot,
                        "textures",
                        "actors",
                        "character",
                        "character assets",
                        "faceparts"
                    )
                ).FullName;

            string fileName =
                "TextureFace.tri";

            File.WriteAllText(
                Path.Combine(
                    alternate,
                    fileName
                ),
                "texture-candidate"
            );

            string requested =
                "textures/Actors/Character/Character Assets/" +
                "FaceParts/" +
                fileName;

            RequestedPaths =
            [
                requested
            ];

            File.WriteAllLines(
                PathList,
                RequestedPaths
            );

            _ =
                requestedParent;
        }

        private void WriteForeignSidecar()
        {
            string otherData =
                Directory.CreateDirectory(
                    Path.Combine(
                        RootPath,
                        "OtherData"
                    )
                ).FullName;

            string otherMeshes =
                Directory.CreateDirectory(
                    Path.Combine(
                        otherData,
                        "meshes",
                        "foreign"
                    )
                ).FullName;

            File.WriteAllText(
                Path.Combine(
                    otherMeshes,
                    "foreign.nif"
                ),
                "foreign-sidecar"
            );

            WriteSidecar(
                otherData,
                "meshes",
                ForeignSidecarPath
            );
        }

        private static void WriteSidecar(
            string dataRoot,
            string namespaceName,
            string outputPath)
        {
            WindowsNamespaceAnalysis analysis =
                WindowsNamespaceAnalyzer.Analyze(
                    dataRoot,
                    namespaceName
                );

            Assert.True(
                analysis.Complete,
                string.Join(
                    Environment.NewLine,
                    analysis.Errors
                )
            );

            WindowsNamespaceRegularFileContentAnalysis content =
                WindowsNamespaceRegularFileContentAnalyzer.Analyze(
                    analysis
                );

            Assert.True(
                content.Complete,
                string.Join(
                    Environment.NewLine,
                    content.Errors
                )
            );

            DataRelativePathAggregateNamespaceManifestRecord manifest =
                WindowsNamespaceAggregateManifestProjector.Project(
                    analysis,
                    content,
                    DateTimeOffset.UtcNow
                );

            Assert.Null(
                DataRelativePathAggregateNamespaceManifest.Validate(
                    manifest
                )
            );

            byte[] bytes =
                DataRelativePathAggregateNamespaceManifestJson.Serialize(
                    manifest
                );

            File.WriteAllBytes(
                outputPath,
                bytes
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
