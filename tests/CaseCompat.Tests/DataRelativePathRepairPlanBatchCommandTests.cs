using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairPlanBatchCommandTests
{
    private const string BatchManifestName =
        "batch-manifest.json";

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

        Assert.NotNull(
            read.ManifestSha256
        );

        Assert.True(
            File.Exists(
                Path.Combine(
                    batchDirectoryPath,
                    BatchManifestName
                )
            )
        );

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

        DataRelativePathRepairBatchManifestReaderResult
            batchRead =
                DataRelativePathRepairBatchManifestReader.Read(
                    batchDirectory,
                    BatchManifestName
                );

        Assert.True(
            batchRead.Success,
            batchRead.Error
        );

        DataRelativePathRepairBatchManifestRecord
            batchManifest =
                Assert.IsType<
                    DataRelativePathRepairBatchManifestRecord
                >(
                    batchRead.Manifest
                );

        Assert.Equal(
            dataRoot,
            batchManifest.DataRoot
        );

        Assert.Equal(
            manifestName,
            batchManifest.ChildManifestName
        );

        Assert.Equal(
            2,
            batchManifest.InputPathCount
        );

        Assert.Equal(
            1,
            batchManifest.SafeRejectionCount
        );

        DataRelativePathRepairBatchManifestChild
            batchChild =
                Assert.Single(
                    batchManifest.Children
                );

        Assert.Equal(
            "plan-000001",
            batchChild.ChildName
        );

        Assert.Equal(
            manifest.PlanId,
            batchChild.PlanId
        );

        Assert.Equal(
            read.ManifestSha256,
            batchChild.ManifestSha256,
            StringComparer.OrdinalIgnoreCase
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
        Run_PartialBatchCoverageWithConsistentRequestedDirectorySpelling_IsRejected()
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

        string physicalDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    meshes,
                    "alpha"
                )
            ).FullName;

        string source1 =
            Path.Combine(
                physicalDirectory,
                "Thing1.nif"
            );

        string source2 =
            Path.Combine(
                physicalDirectory,
                "Thing2.nif"
            );

        string untargeted =
            Path.Combine(
                physicalDirectory,
                "Untargeted.nif"
            );

        File.WriteAllText(
            source1,
            "batch-partial-coverage-1"
        );

        File.WriteAllText(
            source2,
            "batch-partial-coverage-2"
        );

        File.WriteAllText(
            untargeted,
            "batch-partial-coverage-untargeted"
        );

        string pathList =
            Path.Combine(
                temp.RootPath,
                "paths.txt"
            );

        /*
         * Both requested repairs agree on the replacement spelling, but
         * the physical source directory contains a third existing file that
         * is absent from the immutable input batch.
         *
         * This must remain rejected. Otherwise Alpha/ would become a sparse
         * parallel hierarchy and Untargeted.nif would remain stranded under
         * physical alpha/.
         */
        File.WriteAllLines(
            pathList,
            [
                "meshes/Alpha/Thing1.nif",
                "meshes/Alpha/Thing2.nif"
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

        Assert.True(
            File.Exists(
                source1
            )
        );

        Assert.True(
            File.Exists(
                source2
            )
        );

        Assert.True(
            File.Exists(
                untargeted
            )
        );

        Assert.False(
            Directory.Exists(
                Path.Combine(
                    meshes,
                    "Alpha"
                )
            )
        );

        Assert.Empty(
            Directory.GetDirectories(
                batchDirectoryPath
            )
        );

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

        DataRelativePathRepairBatchManifestReaderResult read =
            DataRelativePathRepairBatchManifestReader.Read(
                batchDirectory,
                BatchManifestName
            );

        Assert.True(
            read.Success,
            read.Error
        );

        DataRelativePathRepairBatchManifestRecord manifest =
            Assert.IsType<
                DataRelativePathRepairBatchManifestRecord
            >(
                read.Manifest
            );

        Assert.Equal(
            2,
            manifest.InputPathCount
        );

        Assert.Equal(
            2,
            manifest.SafeRejectionCount
        );

        Assert.Empty(
            manifest.Children
        );
    }

    [Fact]
    public void
        Run_CompleteBatchCoverageWithConflictingRequestedDirectorySpellings_IsRejected()
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

        string physicalDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    meshes,
                    "alpha"
                )
            ).FullName;

        string source1 =
            Path.Combine(
                physicalDirectory,
                "Thing1.nif"
            );

        string source2 =
            Path.Combine(
                physicalDirectory,
                "Thing2.nif"
            );

        File.WriteAllText(
            source1,
            "batch-conflicting-spelling-1"
        );

        File.WriteAllText(
            source2,
            "batch-conflicting-spelling-2"
        );

        string pathList =
            Path.Combine(
                temp.RootPath,
                "paths.txt"
            );

        /*
         * The batch covers every file in physical "alpha", but it proposes
         * two different replacement spellings for that same physical
         * directory.
         *
         * Aggregate coverage must never authorize this:
         *
         *     alpha/
         *       Thing1.nif  -> Alpha/Thing1.nif
         *       Thing2.nif  -> ALPHA/Thing2.nif
         *
         * Doing so would create two competing sparse parallel roots.
         */
        File.WriteAllLines(
            pathList,
            [
                "meshes/Alpha/Thing1.nif",
                "meshes/ALPHA/Thing2.nif"
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

        Assert.True(
            File.Exists(
                source1
            )
        );

        Assert.True(
            File.Exists(
                source2
            )
        );

        Assert.False(
            Directory.Exists(
                Path.Combine(
                    meshes,
                    "Alpha"
                )
            )
        );

        Assert.False(
            Directory.Exists(
                Path.Combine(
                    meshes,
                    "ALPHA"
                )
            )
        );

        Assert.Empty(
            Directory.GetDirectories(
                batchDirectoryPath
            )
        );

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

        DataRelativePathRepairBatchManifestReaderResult read =
            DataRelativePathRepairBatchManifestReader.Read(
                batchDirectory,
                BatchManifestName
            );

        Assert.True(
            read.Success,
            read.Error
        );

        DataRelativePathRepairBatchManifestRecord manifest =
            Assert.IsType<
                DataRelativePathRepairBatchManifestRecord
            >(
                read.Manifest
            );

        Assert.Equal(
            2,
            manifest.InputPathCount
        );

        Assert.Equal(
            2,
            manifest.SafeRejectionCount
        );

        Assert.Empty(
            manifest.Children
        );
    }

    [Fact]
    public void
        Run_TwoFilesInSameCaseVariantDirectory_BatchCoverageProjectsBoth()
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

        string physicalDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    meshes,
                    "alpha"
                )
            ).FullName;

        string source1 =
            Path.Combine(
                physicalDirectory,
                "Thing1.nif"
            );

        string source2 =
            Path.Combine(
                physicalDirectory,
                "Thing2.nif"
            );

        File.WriteAllText(
            source1,
            "batch-aggregate-coverage-1"
        );

        File.WriteAllText(
            source2,
            "batch-aggregate-coverage-2"
        );

        string pathList =
            Path.Combine(
                temp.RootPath,
                "paths.txt"
            );

        File.WriteAllLines(
            pathList,
            [
                "meshes/Alpha/Thing1.nif",
                "meshes/Alpha/Thing2.nif"
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

        string requestedDirectory =
            Path.Combine(
                meshes,
                "Alpha"
            );

        string destination1 =
            Path.Combine(
                requestedDirectory,
                "Thing1.nif"
            );

        string destination2 =
            Path.Combine(
                requestedDirectory,
                "Thing2.nif"
            );

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
         * Planning must remain read-only with respect to Skyrim Data.
         */
        Assert.True(
            File.Exists(
                source1
            )
        );

        Assert.True(
            File.Exists(
                source2
            )
        );

        Assert.False(
            Directory.Exists(
                requestedDirectory
            )
        );

        Assert.False(
            File.Exists(
                destination1
            )
        );

        Assert.False(
            File.Exists(
                destination2
            )
        );

        /*
         * Individually, each source sees the other file as content in the
         * old case-variant directory. Collectively, however, this exact
         * two-child batch covers both existing files.
         *
         * The durable completed batch must therefore record both repairs
         * rather than treating each sibling as untargeted standalone
         * content.
         */
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
            string childDirectory
            in childDirectories)
        {
            Assert.True(
                File.Exists(
                    Path.Combine(
                        childDirectory,
                        manifestName
                    )
                )
            );

            Assert.Empty(
                Directory.EnumerateFiles(
                    childDirectory,
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
            dataRoot,
            batchManifest.DataRoot
        );

        Assert.Equal(
            manifestName,
            batchManifest.ChildManifestName
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

        Assert.Equal(
            "plan-000001",
            batchManifest.Children[0].ChildName
        );

        Assert.Equal(
            "plan-000002",
            batchManifest.Children[1].ChildName
        );
    }

    [Fact]
    public void
        Run_AllSafeRejected_PublishesZeroChildCompletionManifest()
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

        string example =
            Directory.CreateDirectory(
                Path.Combine(
                    meshes,
                    "example"
                )
            ).FullName;

        string sourcePath =
            Path.Combine(
                example,
                "file.nif"
            );

        File.WriteAllText(
            sourcePath,
            "already-resolvable"
        );

        const string requestedPath =
            "meshes/example/file.nif";

        string pathList =
            Path.Combine(
                temp.RootPath,
                "paths.txt"
            );

        File.WriteAllLines(
            pathList,
            [
                requestedPath
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

        Assert.Empty(
            Directory.GetDirectories(
                batchDirectoryPath
            )
        );

        string[] rootFiles =
            Directory
                .GetFiles(
                    batchDirectoryPath
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
                .ToArray();

        Assert.Equal(
            [
                BatchManifestName
            ],
            rootFiles
        );

        Assert.Equal(
            "already-resolvable",
            File.ReadAllText(
                sourcePath
            )
        );

        LinuxNoFollowPathOpenResult opened =
            LinuxNoFollowPath.OpenRootReadOnly(
                batchDirectoryPath
            );

        Assert.True(
            opened.Success,
            opened.Error
        );

        using LinuxNoFollowPathHandle batchDirectory =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                opened.OpenedPath
            );

        DataRelativePathRepairBatchManifestReaderResult read =
            DataRelativePathRepairBatchManifestReader.Read(
                batchDirectory,
                BatchManifestName
            );

        Assert.True(
            read.Success,
            read.Error
        );

        DataRelativePathRepairBatchManifestRecord manifest =
            Assert.IsType<
                DataRelativePathRepairBatchManifestRecord
            >(
                read.Manifest
            );

        Assert.Equal(
            dataRoot,
            manifest.DataRoot
        );

        Assert.Equal(
            manifestName,
            manifest.ChildManifestName
        );

        Assert.Equal(
            1,
            manifest.InputPathCount
        );

        Assert.Equal(
            1,
            manifest.SafeRejectionCount
        );

        Assert.Empty(
            manifest.Children
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
