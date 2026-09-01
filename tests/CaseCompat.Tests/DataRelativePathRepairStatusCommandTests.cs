using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class DataRelativePathRepairStatusCommandTests
{
    [Fact]
    public void Run_MissingArguments_ReturnsUsageError()
    {
        int result =
            global::RepairStatusCommand.Run(
                ["repair-status"]
            );

        Assert.Equal(
            2,
            result
        );
    }

    [Fact]
    public void
        Run_NotStartedPlan_ReturnsSuccessWithoutMutation()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var fixture =
            new Fixture();

        if (!fixture.CanRun)
        {
            return;
        }

        Assert.Equal(
            0,
            fixture.PersistPlan()
        );

        int result =
            global::RepairStatusCommand.Run(
                [
                    "repair-status",
                    fixture.JournalDirectoryPath,
                    fixture.DataRoot
                ]
            );

        Assert.Equal(
            0,
            result
        );

        Assert.True(
            File.Exists(
                fixture.SourcePath
            )
        );

        Assert.False(
            Directory.Exists(
                fixture.RequestedTop
            )
        );

        Assert.False(
            File.Exists(
                fixture.DestinationPath
            )
        );

        using LinuxNoFollowPathHandle journalDirectory =
            fixture.OpenJournalDirectory();

        DataRelativePathRepairPlanStatusInspection inspection =
            DataRelativePathRepairPlanStatusInspector.Inspect(
                journalDirectory,
                Fixture.ManifestName,
                fixture.DataRoot
            );

        Assert.True(
            inspection.Success,
            inspection.Error
        );

        Assert.Equal(
            DataRelativePathRepairPlanOverallStatus.NotStarted,
            inspection.OverallStatus
        );

        Assert.Equal(
            3,
            inspection.OperationStatuses.Count
        );

        Assert.All(
            inspection.OperationStatuses,
            status =>
                Assert.Equal(
                    DataRelativePathRepairPlanObservedOperationState
                        .NotStarted,
                    status.State
                )
        );

        string[] entries =
            Directory.GetFileSystemEntries(
                fixture.JournalDirectoryPath
            );

        Assert.Single(
            entries
        );

        Assert.Equal(
            Fixture.ManifestName,
            Path.GetFileName(
                entries[0]
            )
        );
    }

    [Fact]
    public void
        Run_TrustedDataRootMismatch_ReturnsInspectionError()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var fixture =
            new Fixture();

        if (!fixture.CanRun)
        {
            return;
        }

        Assert.Equal(
            0,
            fixture.PersistPlan()
        );

        string otherDataRoot =
            Directory.CreateDirectory(
                Path.Combine(
                    fixture.RootPath,
                    "OtherData"
                )
            ).FullName;

        int result =
            global::RepairStatusCommand.Run(
                [
                    "repair-status",
                    fixture.JournalDirectoryPath,
                    Fixture.ManifestName,
                    otherDataRoot
                ]
            );

        Assert.Equal(
            4,
            result
        );

        Assert.True(
            File.Exists(
                fixture.SourcePath
            )
        );

        Assert.False(
            Directory.Exists(
                fixture.RequestedTop
            )
        );

        Assert.False(
            File.Exists(
                fixture.DestinationPath
            )
        );
    }

    private sealed class Fixture
        : IDisposable
    {
        public const string ManifestName =
            "repair-plan.json";

        public Fixture()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-repair-status-command-tests",
                    Guid.NewGuid().ToString("N")
                );

            DataRoot =
                Directory.CreateDirectory(
                    Path.Combine(
                        RootPath,
                        "Data"
                    )
                ).FullName;

            string meshes =
                Directory.CreateDirectory(
                    Path.Combine(
                        DataRoot,
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
                CanRun =
                    false;

                SourcePath =
                    string.Empty;

                RequestedTop =
                    string.Empty;

                DestinationPath =
                    string.Empty;

                JournalDirectoryPath =
                    string.Empty;

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

            SourcePath =
                Path.Combine(
                    physicalParent,
                    "armor.nif"
                );

            File.WriteAllText(
                SourcePath,
                "repair-status-cli-fixture"
            );

            RequestedTop =
                Path.Combine(
                    meshes,
                    "Fafny stash"
                );

            DestinationPath =
                Path.Combine(
                    RequestedTop,
                    "Bishop Armor",
                    "armor.nif"
                );

            JournalDirectoryPath =
                Directory.CreateDirectory(
                    Path.Combine(
                        RootPath,
                        "Journal"
                    )
                ).FullName;

            CanRun =
                SupportsManifestPublication(
                    JournalDirectoryPath
                );
        }

        public string RootPath { get; }

        public string DataRoot { get; }

        public string SourcePath { get; }

        public string RequestedTop { get; }

        public string DestinationPath { get; }

        public string JournalDirectoryPath { get; }

        public bool CanRun { get; }

        public int PersistPlan()
        {
            return global::RepairPlanCommand.Run(
                [
                    "repair-plan",
                    DataRoot,
                    "meshes/Fafny stash/Bishop Armor/armor.nif",
                    JournalDirectoryPath,
                    ManifestName
                ]
            );
        }

        public LinuxNoFollowPathHandle OpenJournalDirectory()
        {
            LinuxNoFollowPathOpenResult opened =
                LinuxNoFollowPath.OpenRootReadOnly(
                    JournalDirectoryPath
                );

            Assert.True(
                opened.Success,
                opened.Error
            );

            return Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                opened.OpenedPath
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
    }
}
