using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class DataRelativePathRepairPlanCommandTests
{
    [Fact]
    public void Run_MissingArguments_ReturnsUsageError()
    {
        int result =
            global::RepairPlanCommand.Run(
                ["repair-plan"]
            );

        Assert.Equal(
            2,
            result
        );
    }

    [Fact]
    public void
        Run_DirectStrictCaseMismatch_PersistsManifestWithoutRepairMutation()
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

        /*
         * This CLI test intentionally exercises the real filesystem
         * inspector rather than an injected fixture inspector.
         *
         * A strict destination parent is required for a genuine
         * direct strict-case mismatch. If the test filesystem cannot
         * establish that prerequisite, this environment cannot exercise
         * this integration path.
         */
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
            "repair-plan-cli-fixture"
        );

        const string requestedPath =
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

        string journalDirectoryPath =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Journal"
                )
            ).FullName;

        if (
            !SupportsManifestPublication(
                journalDirectoryPath))
        {
            return;
        }

        const string manifestName =
            "repair-plan.json";

        int exitCode =
            global::RepairPlanCommand.Run(
                [
                    "repair-plan",
                    dataRoot,
                    requestedPath,
                    journalDirectoryPath,
                    manifestName
                ]
            );

        Assert.Equal(
            0,
            exitCode
        );

        /*
         * Planning may persist metadata in the separate journal
         * directory, but it must not cross into repair execution.
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

        DataRelativePathRepairPlanManifestReaderResult read =
            DataRelativePathRepairPlanManifestReader.Read(
                journalDirectory,
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
            Path.GetFullPath(
                dataRoot
            ),
            manifest.DataRoot
        );

        Assert.Equal(
            requestedPath,
            manifest.RequestedPath
        );

        Assert.Equal(
            Path.GetFullPath(
                sourcePath
            ),
            manifest.SourceSnapshot.PhysicalPath
        );

        Assert.NotEmpty(
            manifest.Operations
        );

        Assert.Equal(
            DataRelativePathRepairPlanOperationKind.CreateFile,
            manifest.Operations[^1].Operation.Kind
        );

        Assert.Equal(
            Path.GetFullPath(
                destinationPath
            ),
            manifest.Operations[^1].Operation.DestinationPath
        );

        Assert.Equal(
            Path.GetFullPath(
                sourcePath
            ),
            manifest.Operations[^1].Operation.SourcePath
        );

        foreach (
            DataRelativePathRepairPlanManifestOperation entry
            in manifest.Operations)
        {
            Assert.False(
                File.Exists(
                    Path.Combine(
                        journalDirectoryPath,
                        entry.JournalChildName
                    )
                )
            );
        }

        string[] journalEntries =
            Directory.GetFileSystemEntries(
                journalDirectoryPath
            );

        Assert.Single(
            journalEntries
        );

        Assert.Equal(
            manifestName,
            Path.GetFileName(
                journalEntries[0]
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

    private sealed class TemporaryDirectory
        : IDisposable
    {
        public TemporaryDirectory()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-repair-plan-command-tests",
                    Guid.NewGuid().ToString("N")
                );

            Directory.CreateDirectory(
                RootPath
            );
        }

        public string RootPath { get; }

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
