using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class DataRelativePathRepairRollbackCommandTests
{
    [Fact]
    public void Run_MissingArguments_ReturnsUsageError()
    {
        int result =
            global::RepairRollbackCommand.Run(
                ["repair-rollback"]
            );

        Assert.Equal(
            2,
            result
        );
    }

    [Fact]
    public void
        Run_NotStartedPlan_SucceedsWithoutCreatingRollbackState()
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

        int result =
            global::RepairRollbackCommand.Run(
                [
                    "repair-rollback",
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
            Directory.Exists(
                fixture.RequestedParent
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
    }

    [Fact]
    public void
        Run_TrustedDataRootMismatch_RejectsBeforeRollbackRemoval()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        if (!ForwardPublicationSupported())
        {
            return;
        }

        using var fixture =
            new Fixture();

        if (!fixture.CanRun)
        {
            return;
        }

        fixture.PersistAndApply();

        string otherDataRoot =
            Directory.CreateDirectory(
                Path.Combine(
                    fixture.RootPath,
                    "OtherData"
                )
            ).FullName;

        int result =
            global::RepairRollbackCommand.Run(
                [
                    "repair-rollback",
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

        Assert.True(
            Directory.Exists(
                fixture.RequestedTop
            )
        );

        Assert.True(
            Directory.Exists(
                fixture.RequestedParent
            )
        );

        Assert.True(
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
            DataRelativePathRepairPlanOverallStatus.Applied,
            inspection.OverallStatus
        );

        Assert.All(
            inspection.OperationStatuses,
            status =>
                Assert.Equal(
                    DataRelativePathRepairPlanObservedOperationState
                        .Applied,
                    status.State
                )
        );
    }

    [Fact]
    public void
        Run_AppliedPlan_RollsBackDurablyAndIsIdempotent()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        if (!ForwardPublicationSupported())
        {
            return;
        }

        using var fixture =
            new Fixture();

        if (!fixture.CanRun)
        {
            return;
        }

        fixture.PersistAndApply();

        Assert.True(
            File.Exists(
                fixture.SourcePath
            )
        );

        Assert.True(
            File.Exists(
                fixture.DestinationPath
            )
        );

        int firstResult =
            global::RepairRollbackCommand.Run(
                [
                    "repair-rollback",
                    fixture.JournalDirectoryPath,
                    Fixture.ManifestName,
                    fixture.DataRoot
                ]
            );

        Assert.Equal(
            0,
            firstResult
        );

        Assert.True(
            File.Exists(
                fixture.SourcePath
            )
        );

        Assert.False(
            File.Exists(
                fixture.DestinationPath
            )
        );

        Assert.False(
            Directory.Exists(
                fixture.RequestedParent
            )
        );

        Assert.False(
            Directory.Exists(
                fixture.RequestedTop
            )
        );

        AssertRolledBackStatus(
            fixture
        );

        int secondResult =
            global::RepairRollbackCommand.Run(
                [
                    "repair-rollback",
                    fixture.JournalDirectoryPath,
                    Fixture.ManifestName,
                    fixture.DataRoot
                ]
            );

        Assert.Equal(
            0,
            secondResult
        );

        Assert.True(
            File.Exists(
                fixture.SourcePath
            )
        );

        Assert.False(
            File.Exists(
                fixture.DestinationPath
            )
        );

        Assert.False(
            Directory.Exists(
                fixture.RequestedParent
            )
        );

        Assert.False(
            Directory.Exists(
                fixture.RequestedTop
            )
        );

        AssertRolledBackStatus(
            fixture
        );
    }

    private static void AssertRolledBackStatus(
        Fixture fixture)
    {
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
            DataRelativePathRepairPlanOverallStatus.RolledBack,
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
                        .RolledBack,
                    status.State
                )
        );
    }

    private static bool ForwardPublicationSupported()
    {
        using var probe =
            new Fixture();

        if (!probe.CanRun)
        {
            return false;
        }

        Assert.Equal(
            0,
            probe.PersistPlan()
        );

        using LinuxNoFollowPathHandle journalDirectory =
            probe.OpenJournalDirectory();

        DataRelativePathRepairPlanForwardExecution execution =
            DataRelativePathRepairPlanForwardExecutor.Execute(
                journalDirectory,
                Fixture.ManifestName,
                probe.DataRoot,
                DateTimeOffset.UtcNow
            );

        if (NoReplaceUnsupported(
                execution))
        {
            return false;
        }

        Assert.True(
            execution.Success,
            execution.Error
        );

        return true;
    }

    private static bool NoReplaceUnsupported(
        DataRelativePathRepairPlanForwardExecution execution)
    {
        return execution.OperationResults.Any(
            operation =>
                operation.DirectoryExecution?
                    .ForwardRecovery?
                    .Publication?
                    .State ==
                LinuxPublishOwnedDirectoryAtState
                    .NoReplaceUnsupported ||
                operation.DirectoryForwardRecovery?
                    .Publication?
                    .State ==
                LinuxPublishOwnedDirectoryAtState
                    .NoReplaceUnsupported
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
                    "casecompat-repair-rollback-command-tests",
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

                RequestedParent =
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
                "repair-rollback-cli-fixture"
            );

            RequestedTop =
                Path.Combine(
                    meshes,
                    "Fafny stash"
                );

            RequestedParent =
                Path.Combine(
                    RequestedTop,
                    "Bishop Armor"
                );

            DestinationPath =
                Path.Combine(
                    RequestedParent,
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

        public string RequestedParent { get; }

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

        public void PersistAndApply()
        {
            Assert.Equal(
                0,
                PersistPlan()
            );

            int apply =
                global::RepairApplyCommand.Run(
                    [
                        "repair-apply",
                        JournalDirectoryPath,
                        ManifestName,
                        DataRoot
                    ]
                );

            Assert.Equal(
                0,
                apply
            );

            Assert.True(
                File.Exists(
                    DestinationPath
                )
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
