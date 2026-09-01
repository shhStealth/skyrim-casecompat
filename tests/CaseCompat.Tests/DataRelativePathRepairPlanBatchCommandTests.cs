using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairPlanBatchCommandTests
{
    [Fact]
    public void Run_MissingArguments_ReturnsUsageError()
    {
        int result =
            global::RepairPlanBatchCommand.Run(
                ["repair-plan-batch"]
            );

        Assert.Equal(
            2,
            result
        );
    }

    [Fact]
    public void
        Run_BatchDirectoryInsideData_FailsBeforeMetadataPublication()
    {
        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Data"
                )
            ).FullName;

        string batchDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "Batch"
                )
            ).FullName;

        string pathList =
            Path.Combine(
                temp.RootPath,
                "paths.txt"
            );

        File.WriteAllLines(
            pathList,
            [
                "meshes/example/file.nif"
            ]
        );

        int result =
            global::RepairPlanBatchCommand.Run(
                [
                    "repair-plan-batch",
                    dataRoot,
                    pathList,
                    batchDirectory,
                    "repair-plan.json"
                ]
            );

        Assert.Equal(
            3,
            result
        );

        Assert.Empty(
            Directory.EnumerateFileSystemEntries(
                batchDirectory
            )
        );
    }

    [Fact]
    public void
        Run_NonEmptyBatchDirectory_FailsBeforeChildPlanPublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Data"
                )
            ).FullName;

        string pathList =
            Path.Combine(
                temp.RootPath,
                "paths.txt"
            );

        File.WriteAllLines(
            pathList,
            [
                "meshes/example/file.nif"
            ]
        );

        string batchDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Batch"
                )
            ).FullName;

        string preexisting =
            Path.Combine(
                batchDirectory,
                "do-not-overwrite.txt"
            );

        File.WriteAllText(
            preexisting,
            "existing batch content"
        );

        int result =
            global::RepairPlanBatchCommand.Run(
                [
                    "repair-plan-batch",
                    dataRoot,
                    pathList,
                    batchDirectory,
                    "repair-plan.json"
                ]
            );

        Assert.Equal(
            5,
            result
        );

        Assert.Equal(
            ["do-not-overwrite.txt"],
            Directory
                .EnumerateFileSystemEntries(
                    batchDirectory
                )
                .Select(
                    entry =>
                        Path.GetFileName(
                            entry
                        )!
                )
                .ToArray()
        );
    }

    [Fact]
    public void
        Run_InvalidManifestName_FailsBeforeMetadataPublication()
    {
        using var temp =
            new TemporaryDirectory();

        string pathList =
            Path.Combine(
                temp.RootPath,
                "paths.txt"
            );

        File.WriteAllLines(
            pathList,
            [
                "meshes/example/file.nif"
            ]
        );

        string batchDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Batch"
                )
            ).FullName;

        int result =
            global::RepairPlanBatchCommand.Run(
                [
                    "repair-plan-batch",
                    Path.Combine(
                        temp.RootPath,
                        "Data"
                    ),
                    pathList,
                    batchDirectory,
                    "../repair-plan.json"
                ]
            );

        Assert.Equal(
            3,
            result
        );

        Assert.Empty(
            Directory.EnumerateFileSystemEntries(
                batchDirectory
            )
        );
    }

    [Fact]
    public void Run_DuplicateInput_FailsBeforeMetadataPublication()
    {
        using var temp =
            new TemporaryDirectory();

        string pathList =
            Path.Combine(
                temp.RootPath,
                "paths.txt"
            );

        const string requested =
            "meshes/example/file.nif";

        File.WriteAllLines(
            pathList,
            [
                requested,
                requested
            ]
        );

        string batchDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Batch"
                )
            ).FullName;

        int result =
            global::RepairPlanBatchCommand.Run(
                [
                    "repair-plan-batch",
                    Path.Combine(
                        temp.RootPath,
                        "Data"
                    ),
                    pathList,
                    batchDirectory,
                    "repair-plan.json"
                ]
            );

        Assert.Equal(
            3,
            result
        );

        Assert.Empty(
            Directory.EnumerateFileSystemEntries(
                batchDirectory
            )
        );
    }

    [Fact]
    public void
        Run_MixedInput_PersistsOnlySafeIndependentPlanWithoutRepairMutation()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Data"
                )
            ).FullName;

        string meshes =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes"
                )
            ).FullName;

        DirectoryCasefoldResult meshesFlags =
            LinuxDirectoryFlags.Inspect(
                meshes
            );

        if (
            !meshesFlags.Exists ||
            meshesFlags.Error is not null ||
            meshesFlags.CasefoldEnabled != false)
        {
            return;
        }

        string physicalTop =
            Directory.CreateDirectory(
                Path.Combine(
                    meshes,
                    "fafny stash"
                )
            ).FullName;

        string physicalParent =
            Directory.CreateDirectory(
                Path.Combine(
                    physicalTop,
                    "Bishop Armor"
                )
            ).FullName;

        string sourcePath =
            Path.Combine(
                physicalParent,
                "armor.nif"
            );

        File.WriteAllText(
            sourcePath,
            "repair-plan-batch-cli-fixture"
        );

        const string alreadyResolvable =
            "meshes/fafny stash/Bishop Armor/armor.nif";

        const string repairable =
            "meshes/Fafny stash/Bishop Armor/armor.nif";

        string requestedTop =
            Path.Combine(
                meshes,
                "Fafny stash"
            );

        string requestedParent =
            Path.Combine(
                requestedTop,
                "Bishop Armor"
            );

        string destinationPath =
            Path.Combine(
                requestedParent,
                "armor.nif"
            );

        string pathList =
            Path.Combine(
                temp.RootPath,
                "paths.txt"
            );

        File.WriteAllLines(
            pathList,
            [
                alreadyResolvable,
                repairable
            ]
        );

        string batchDirectoryPath =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Batch"
                )
            ).FullName;

        if (
            !SupportsManifestPublication(
                batchDirectoryPath))
        {
            return;
        }

        const string manifestName =
            "repair-plan.json";

        int result =
            global::RepairPlanBatchCommand.Run(
                [
                    "repair-plan-batch",
                    dataRoot,
                    pathList,
                    batchDirectoryPath,
                    manifestName
                ]
            );

        Assert.Equal(
            0,
            result
        );

        /*
         * Batch planning may create plan metadata outside Skyrim Data,
         * but it must not execute the projected repair.
         */
        Assert.False(
            Directory.Exists(
                requestedTop
            )
        );

        Assert.False(
            Directory.Exists(
                requestedParent
            )
        );

        Assert.False(
            File.Exists(
                destinationPath
            )
        );

        Assert.True(
            File.Exists(
                sourcePath
            )
        );

        string[] childDirectories =
            Directory.GetDirectories(
                batchDirectoryPath
            );

        Assert.Single(
            childDirectories
        );

        string childDirectoryPath =
            childDirectories[0];

        Assert.Equal(
            "plan-000001",
            Path.GetFileName(
                childDirectoryPath
            )
        );

        Assert.True(
            File.Exists(
                Path.Combine(
                    childDirectoryPath,
                    manifestName
                )
            )
        );

        Assert.Empty(
            Directory.EnumerateFiles(
                childDirectoryPath,
                ".casecompat-plan-*-op-*.json"
            )
        );

        LinuxNoFollowPathOpenResult opened =
            LinuxNoFollowPath.OpenRootReadOnly(
                childDirectoryPath
            );

        Assert.True(
            opened.Success,
            opened.Error
        );

        using LinuxNoFollowPathHandle childDirectory =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                opened.OpenedPath
            );

        DataRelativePathRepairPlanManifestReaderResult read =
            DataRelativePathRepairPlanManifestReader.Read(
                childDirectory,
                manifestName
            );

        Assert.True(
            read.Success,
            read.Error
        );

        DataRelativePathRepairPlanManifestRecord manifest =
            Assert.IsType<
                DataRelativePathRepairPlanManifestRecord
            >(
                read.Manifest
            );

        Assert.Equal(
            DataRelativePathRepairPlanManifestRecord
                .SchemaVersion2,
            manifest.SchemaVersion
        );

        Assert.Equal(
            repairable,
            manifest.RequestedPath
        );

        Assert.NotNull(
            manifest.ResolvedPrefixSteps
        );

        Assert.NotEmpty(
            manifest.Operations
        );
    }

    private static bool SupportsManifestPublication(
        string journalDirectoryPath)
    {
        LinuxNoFollowPathOpenResult opened =
            LinuxNoFollowPath.OpenRootReadOnly(
                journalDirectoryPath
            );

        Assert.True(
            opened.Success,
            opened.Error
        );

        using LinuxNoFollowPathHandle journalDirectory =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                opened.OpenedPath
            );

        LinuxCreateUnnamedFileAtResult probe =
            LinuxCreateUnnamedFileAt.Create(
                journalDirectory
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

    private sealed class TemporaryDirectory :
        IDisposable
    {
        public TemporaryDirectory()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-batch-cli-tests-" +
                    Guid.NewGuid().ToString("N")
                );

            Directory.CreateDirectory(
                RootPath
            );
        }

        public string RootPath
        {
            get;
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
