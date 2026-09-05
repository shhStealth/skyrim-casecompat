using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairPlanAggregateBatchCommandTests
{
    private const string BatchManifestName =
        "batch-manifest.json";

    [Fact]
    public void
        Run_CompleteRecursiveAlternateBranch_PersistsSchemaV4ChildrenAndSchemaV3Batch()
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

        string requestedParent =
            Directory.CreateDirectory(
                Path.Combine(
                    meshes,
                    "Actors",
                    "Character",
                    "Character Assets"
                )
            ).FullName;

        string alternateFaceParts =
            Directory.CreateDirectory(
                Path.Combine(
                    meshes,
                    "actors",
                    "character",
                    "character assets",
                    "faceparts"
                )
            ).FullName;

        string alternateTre =
            Directory.CreateDirectory(
                Path.Combine(
                    alternateFaceParts,
                    "TRE"
                )
            ).FullName;

        string sourceRoot =
            Path.Combine(
                alternateFaceParts,
                "MaleHeadbrows.tri"
            );

        string sourceNested =
            Path.Combine(
                alternateTre,
                "NestedBrow.tri"
            );

        File.WriteAllText(
            sourceRoot,
            "aggregate-faceparts-root"
        );

        File.WriteAllText(
            sourceNested,
            "aggregate-faceparts-nested"
        );

        string destinationFaceParts =
            Path.Combine(
                requestedParent,
                "FaceParts"
            );

        string destinationRoot =
            Path.Combine(
                destinationFaceParts,
                "MaleHeadbrows.tri"
            );

        string destinationNested =
            Path.Combine(
                destinationFaceParts,
                "TRE",
                "NestedBrow.tri"
            );

        string pathList =
            Path.Combine(
                temp.RootPath,
                "paths.txt"
            );

        File.WriteAllLines(
            pathList,
            [
                "meshes/Actors/Character/Character Assets/" +
                "FaceParts/MaleHeadbrows.tri",
                "meshes/Actors/Character/Character Assets/" +
                "FaceParts/TRE/NestedBrow.tri"
            ]
        );

        string batchDirectoryPath =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "AggregateBatch"
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

        Assert.False(
            Directory.Exists(
                destinationFaceParts
            )
        );

        int result =
            global::RepairPlanAggregateBatchCommand.Run(
                [
                    "repair-plan-aggregate-batch",
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
         * Planning metadata is external to Skyrim Data.
         */
        Assert.False(
            Directory.Exists(
                destinationFaceParts
            )
        );

        Assert.False(
            File.Exists(
                destinationRoot
            )
        );

        Assert.False(
            File.Exists(
                destinationNested
            )
        );

        Assert.True(
            File.Exists(
                sourceRoot
            )
        );

        Assert.True(
            File.Exists(
                sourceNested
            )
        );

        string[] childDirectories =
            Directory
                .GetDirectories(
                    batchDirectoryPath
                )
                .OrderBy(
                    path =>
                        Path.GetFileName(
                            path
                        ),
                    StringComparer.Ordinal
                )
                .ToArray();

        Assert.Equal(
            2,
            childDirectories.Length
        );

        Assert.Equal(
            "plan-000001",
            Path.GetFileName(
                childDirectories[0]
            )
        );

        Assert.Equal(
            "plan-000002",
            Path.GetFileName(
                childDirectories[1]
            )
        );

        foreach (
            string childDirectoryPath
            in childDirectories)
        {
            LinuxNoFollowPathOpenResult childOpened =
                LinuxNoFollowPath.OpenRootReadOnly(
                    childDirectoryPath
                );

            Assert.True(
                childOpened.Success,
                childOpened.Error
            );

            using LinuxNoFollowPathHandle childDirectory =
                Assert.IsType<
                    LinuxNoFollowPathHandle
                >(
                    childOpened.OpenedPath
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
                    .SchemaVersion4,
                manifest.SchemaVersion
            );

            Assert.NotNull(
                manifest.ResolvedPrefixSteps
            );

            Assert.Empty(
                Directory.EnumerateFiles(
                    childDirectoryPath,
                    ".casecompat-plan-*-op-*.json"
                )
            );
        }

        LinuxNoFollowPathOpenResult batchOpened =
            LinuxNoFollowPath.OpenRootReadOnly(
                batchDirectoryPath
            );

        Assert.True(
            batchOpened.Success,
            batchOpened.Error
        );

        using LinuxNoFollowPathHandle batchDirectory =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                batchOpened.OpenedPath
            );

        DataRelativePathRepairBatchManifestReaderResult batchRead =
            DataRelativePathRepairBatchManifestReader.Read(
                batchDirectory,
                BatchManifestName
            );

        Assert.True(
            batchRead.Success,
            batchRead.Error
        );

        DataRelativePathRepairBatchManifestRecord batchManifest =
            Assert.IsType<
                DataRelativePathRepairBatchManifestRecord
            >(
                batchRead.Manifest
            );

        Assert.Equal(
            DataRelativePathRepairBatchManifestRecord
                .SchemaVersion3,
            batchManifest.SchemaVersion
        );

        Assert.Equal(
            DataRelativePathRepairBatchManifestRecord
                .CoveragePolicyVersion2,
            batchManifest.CoveragePolicyVersion
        );

        Assert.Equal(
            2,
            batchManifest.InputPathCount
        );

        Assert.Equal(
            0,
            batchManifest.SafeRejectionCount
        );

        Assert.Equal(
            2,
            batchManifest.Children.Count
        );

        int statusResult =
            global::RepairStatusBatchCommand.Run(
                [
                    "repair-status-batch",
                    batchDirectoryPath,
                    manifestName,
                    dataRoot
                ]
            );

        Assert.Equal(
            0,
            statusResult
        );
    }

    [Fact]
    public void
        Run_IncompleteRecursivePhysicalCoverage_PublishesZeroMetadata()
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

        string requestedParent =
            Directory.CreateDirectory(
                Path.Combine(
                    meshes,
                    "Actors",
                    "Character",
                    "Character Assets"
                )
            ).FullName;

        string alternateFaceParts =
            Directory.CreateDirectory(
                Path.Combine(
                    meshes,
                    "actors",
                    "character",
                    "character assets",
                    "faceparts"
                )
            ).FullName;

        string alternateTre =
            Directory.CreateDirectory(
                Path.Combine(
                    alternateFaceParts,
                    "TRE"
                )
            ).FullName;

        string representedSource =
            Path.Combine(
                alternateFaceParts,
                "MaleHeadbrows.tri"
            );

        string omittedSource =
            Path.Combine(
                alternateTre,
                "NestedBrow.tri"
            );

        File.WriteAllText(
            representedSource,
            "aggregate-represented"
        );

        File.WriteAllText(
            omittedSource,
            "aggregate-omitted"
        );

        string destinationFaceParts =
            Path.Combine(
                requestedParent,
                "FaceParts"
            );

        string pathList =
            Path.Combine(
                temp.RootPath,
                "paths.txt"
            );

        File.WriteAllLines(
            pathList,
            [
                "meshes/Actors/Character/Character Assets/" +
                "FaceParts/MaleHeadbrows.tri"
            ]
        );

        string batchDirectoryPath =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "AggregateBatch"
                )
            ).FullName;

        if (
            !SupportsManifestPublication(
                batchDirectoryPath))
        {
            return;
        }

        int result =
            global::RepairPlanAggregateBatchCommand.Run(
                [
                    "repair-plan-aggregate-batch",
                    dataRoot,
                    pathList,
                    batchDirectoryPath,
                    "repair-plan.json"
                ]
            );

        Assert.Equal(
            6,
            result
        );

        /*
         * Policy-v2 failed before first child publication.
         */
        Assert.Empty(
            Directory.EnumerateFileSystemEntries(
                batchDirectoryPath
            )
        );

        Assert.False(
            Directory.Exists(
                destinationFaceParts
            )
        );

        Assert.True(
            File.Exists(
                representedSource
            )
        );

        Assert.True(
            File.Exists(
                omittedSource
            )
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
                    "casecompat-aggregate-batch-cli-tests-" +
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
