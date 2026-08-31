using CaseCompat.Core.Repair;
using CaseCompat.Core.Resolution;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairProjectedFileLifecycleIntegrationTests
{
    private static readonly DateTimeOffset T0 =
        new(
            2026,
            8,
            31,
            18,
            0,
            0,
            TimeSpan.Zero
        );

    [Fact]
    public void ResolveProjectExecuteRollback_FinalFileCaseMismatch_RestoresOriginalState()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        if (
            !fixture.SupportsUnnamedFiles() ||
            !fixture.HasStrictDestinationParent())
        {
            return;
        }

        /*
         * The physical source exists with one spelling:
         *
         *     armor.nif
         *
         * The consumer requests:
         *
         *     Armor.nif
         *
         * Every parent component already has the requested spelling,
         * so this is intentionally a one-operation CreateFile plan.
         *
         * This lets the test join the real resolver/projector pipeline
         * to the currently implemented file executor without manually
         * inventing a repair operation or filesystem snapshot.
         */
        DataRelativePathResolution initialResolution =
            fixture.ResolveRequestedPath();

        Assert.False(
            initialResolution.LinuxResolves
        );

        Assert.Equal(
            3,
            initialResolution.FailedComponentIndex
        );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .DirectStrictCaseMismatch,
            DataRelativePathCaseMismatchTopologyClassifier
                .Classify(
                    initialResolution
                )
        );

        Assert.Single(
            initialResolution.EquivalentPhysicalCandidates
        );

        Assert.Equal(
            Path.GetFullPath(
                fixture.SourcePath
            ),
            Path.GetFullPath(
                initialResolution
                    .EquivalentPhysicalCandidates[0]
            )
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

        byte[] sourceBefore =
            File.ReadAllBytes(
                fixture.SourcePath
            );

        /*
         * Planning / dry-run boundary.
         *
         * Project() snapshots the real source and destination parent
         * but must not create the requested spelling.
         */
        DataRelativePathRepairPlanProjection projection =
            DataRelativePathRepairPlanProjector.Project(
                initialResolution
            );

        Assert.True(
            projection.HasPlan,
            projection.Error
        );

        Assert.Equal(
            DataRelativePathRepairPlanProjectionState.Projected,
            projection.State
        );

        DataRelativePathRepairPlanOperation operation =
            Assert.Single(
                projection.Operations
            );

        Assert.Equal(
            DataRelativePathRepairPlanOperationKind.CreateFile,
            operation.Kind
        );

        Assert.Equal(
            Path.GetFullPath(
                fixture.DestinationPath
            ),
            Path.GetFullPath(
                operation.DestinationPath
            )
        );

        Assert.Equal(
            Path.GetFullPath(
                fixture.SourcePath
            ),
            Path.GetFullPath(
                Assert.IsType<string>(
                    operation.SourcePath
                )
            )
        );

        DataRelativePathRepairSourceSnapshot sourceSnapshot =
            Assert.IsType<
                DataRelativePathRepairSourceSnapshot
            >(
                projection.SourceSnapshot
            );

        DataRelativePathRepairDestinationParentSnapshot
            parentSnapshot =
                Assert.IsType<
                    DataRelativePathRepairDestinationParentSnapshot
                >(
                    projection.DestinationParentSnapshot
                );

        Assert.Equal(
            Path.GetFullPath(
                fixture.SourcePath
            ),
            Path.GetFullPath(
                sourceSnapshot.PhysicalPath
            )
        );

        Assert.Equal(
            Path.GetFullPath(
                fixture.ParentPath
            ),
            Path.GetFullPath(
                parentSnapshot.PhysicalPath
            )
        );

        Assert.False(
            parentSnapshot.CasefoldEnabled
        );

        /*
         * Projection is the dry-run phase. Nothing has changed.
         */
        Assert.False(
            File.Exists(
                fixture.DestinationPath
            )
        );

        Assert.Equal(
            sourceBefore,
            File.ReadAllBytes(
                fixture.SourcePath
            )
        );

        /*
         * Bridge the real projection into the existing durable
         * transaction model.
         */
        DataRelativePathRepairFileJournalTransitionResult
            intentTransition =
                DataRelativePathRepairFileJournal.CreateIntent(
                    Guid.NewGuid(),
                    T0,
                    fixture.DataRoot,
                    operation,
                    sourceSnapshot,
                    parentSnapshot
                );

        Assert.True(
            intentTransition.Success,
            intentTransition.Error
        );

        DataRelativePathRepairFileJournalRecord intent =
            Assert.IsType<
                DataRelativePathRepairFileJournalRecord
            >(
                intentTransition.Record
            );

        Assert.Equal(
            DataRelativePathRepairFileJournalState.IntentRecorded,
            intent.State
        );

        Assert.Equal(
            0,
            intent.Revision
        );

        /*
         * Execute the exact operation and evidence produced by the
         * resolver/projector pipeline.
         */
        DataRelativePathRepairFileExecution execution =
            DataRelativePathRepairFileExecutor.Execute(
                fixture.JournalDirectory,
                "journal.json",
                intent,
                fixture.DataRoot,
                T0.AddSeconds(10)
            );

        Assert.True(
            execution.Success,
            execution.Error
        );

        Assert.Equal(
            DataRelativePathRepairFileExecutionState
                .AppliedDurably,
            execution.State
        );

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

        Assert.Equal(
            sourceBefore,
            File.ReadAllBytes(
                fixture.SourcePath
            )
        );

        Assert.Equal(
            sourceBefore,
            File.ReadAllBytes(
                fixture.DestinationPath
            )
        );

        /*
         * The originally requested spelling must now resolve exactly.
         */
        DataRelativePathResolution repairedResolution =
            fixture.ResolveRequestedPath();

        Assert.True(
            repairedResolution.LinuxResolves
        );

        Assert.Equal(
            Path.GetFullPath(
                fixture.DestinationPath
            ),
            Path.GetFullPath(
                Assert.IsType<string>(
                    repairedResolution.ResolvedPhysicalPath
                )
            )
        );

        DataRelativePathRepairFileJournalReaderResult appliedRead =
            fixture.ReadJournal();

        Assert.Equal(
            DataRelativePathRepairFileJournalState.Applied,
            appliedRead.Record!.State
        );

        Assert.Equal(
            2,
            appliedRead.Record.Revision
        );

        /*
         * Request rollback without changing the repaired asset.
         */
        DataRelativePathRepairFileRollbackRequest request =
            DataRelativePathRepairFileRollbackRequestAction.Request(
                fixture.JournalDirectory,
                "journal.json",
                fixture.DataRoot,
                T0.AddSeconds(20)
            );

        Assert.True(
            request.Success,
            request.Error
        );

        Assert.Equal(
            DataRelativePathRepairFileRollbackRequestState
                .RequestedDurably,
            request.State
        );

        Assert.True(
            File.Exists(
                fixture.DestinationPath
            )
        );

        /*
         * Recovery performs the incarnation-gated destructive step.
         */
        DataRelativePathRepairFileRollbackRecovery recovery =
            DataRelativePathRepairFileRollbackRecoveryAction.Recover(
                fixture.JournalDirectory,
                "journal.json",
                fixture.DataRoot,
                T0.AddSeconds(30)
            );

        Assert.True(
            recovery.Success,
            recovery.Error
        );

        Assert.Equal(
            DataRelativePathRepairFileRollbackRecoveryState
                .RolledBackDurably,
            recovery.State
        );

        Assert.False(
            File.Exists(
                fixture.DestinationPath
            )
        );

        Assert.True(
            File.Exists(
                fixture.SourcePath
            )
        );

        Assert.Equal(
            sourceBefore,
            File.ReadAllBytes(
                fixture.SourcePath
            )
        );

        DataRelativePathRepairFileJournalReaderResult rolledBackRead =
            fixture.ReadJournal();

        Assert.Equal(
            DataRelativePathRepairFileJournalState.RolledBack,
            rolledBackRead.Record!.State
        );

        Assert.Equal(
            4,
            rolledBackRead.Record.Revision
        );

        Assert.True(
            rolledBackRead.Record.IsTerminal
        );

        /*
         * With the CaseCompat-created spelling removed, the exact
         * same real-world mismatch is visible again.
         */
        DataRelativePathResolution finalResolution =
            fixture.ResolveRequestedPath();

        Assert.False(
            finalResolution.LinuxResolves
        );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .DirectStrictCaseMismatch,
            DataRelativePathCaseMismatchTopologyClassifier
                .Classify(
                    finalResolution
                )
        );

        Assert.Single(
            finalResolution.EquivalentPhysicalCandidates
        );

        Assert.Equal(
            Path.GetFullPath(
                fixture.SourcePath
            ),
            Path.GetFullPath(
                finalResolution
                    .EquivalentPhysicalCandidates[0]
            )
        );
    }

    private sealed class Fixture
        : IDisposable
    {
        public Fixture()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-projected-file-lifecycle-tests",
                    Guid.NewGuid().ToString("N")
                );

            DataRoot =
                Path.Combine(
                    RootPath,
                    "Data"
                );

            ParentPath =
                Path.Combine(
                    DataRoot,
                    "meshes",
                    "fafny stash",
                    "Bishop Armor"
                );

            SourcePath =
                Path.Combine(
                    ParentPath,
                    "armor.nif"
                );

            DestinationPath =
                Path.Combine(
                    ParentPath,
                    "Armor.nif"
                );

            JournalDirectoryPath =
                Path.Combine(
                    RootPath,
                    "Journal"
                );

            Directory.CreateDirectory(
                ParentPath
            );

            Directory.CreateDirectory(
                JournalDirectoryPath
            );

            File.WriteAllBytes(
                SourcePath,
                [
                    0x43,
                    0x41,
                    0x53,
                    0x45,
                    0x43,
                    0x4F,
                    0x4D,
                    0x50,
                    0x41,
                    0x54
                ]
            );

            JournalDirectory =
                OpenRoot(
                    JournalDirectoryPath
                );
        }

        public string RootPath { get; }

        public string DataRoot { get; }

        public string ParentPath { get; }

        public string SourcePath { get; }

        public string DestinationPath { get; }

        public string JournalDirectoryPath { get; }

        public LinuxNoFollowPathHandle JournalDirectory { get; }

        public DataRelativePathResolution ResolveRequestedPath()
        {
            return DataRelativePathResolver.ResolveFile(
                DataRoot,
                "meshes/fafny stash/Bishop Armor/Armor.nif",
                InspectFixtureCasefold
            );
        }

        /*
         * Model the real Skyrim layout we discovered:
         *
         * Data itself may be casefold-enabled while descendants are
         * strict unless they independently carry casefold.
         *
         * The mismatch in this first full-pipeline test occurs under
         * the strict Bishop Armor directory.
         */
        private DirectoryCasefoldResult InspectFixtureCasefold(
            string path)
        {
            string fullPath =
                Path.GetFullPath(
                    path
                );

            bool isDataRoot =
                string.Equals(
                    fullPath,
                    Path.GetFullPath(
                        DataRoot
                    ),
                    StringComparison.Ordinal
                );

            return new DirectoryCasefoldResult(
                FullPath:
                    fullPath,
                Exists:
                    Directory.Exists(
                        fullPath
                    ),
                CasefoldEnabled:
                    isDataRoot,
                RawFlags:
                    isDataRoot
                        ? LinuxDirectoryFlags.FsCasefoldFlag
                        : 0L,
                Error:
                    null
            );
        }

        public bool HasStrictDestinationParent()
        {
            DirectoryCasefoldResult result =
                LinuxDirectoryFlags.Inspect(
                    ParentPath
                );

            return
                result.Exists &&
                result.Error is null &&
                result.CasefoldEnabled == false;
        }

        public bool SupportsUnnamedFiles()
        {
            using LinuxNoFollowPathHandle parent =
                OpenRoot(
                    ParentPath
                );

            LinuxCreateUnnamedFileAtResult probe =
                LinuxCreateUnnamedFileAt.Create(
                    parent
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

        public DataRelativePathRepairFileJournalReaderResult
            ReadJournal()
        {
            DataRelativePathRepairFileJournalReaderResult result =
                DataRelativePathRepairFileJournalReader.Read(
                    JournalDirectory,
                    "journal.json"
                );

            Assert.True(
                result.Success,
                result.Error
            );

            return result;
        }

        private static LinuxNoFollowPathHandle OpenRoot(
            string path)
        {
            LinuxNoFollowPathOpenResult result =
                LinuxNoFollowPath.OpenRootReadOnly(
                    path
                );

            Assert.True(
                result.Success,
                result.Error
            );

            return Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                result.OpenedPath
            );
        }

        public void Dispose()
        {
            JournalDirectory.Dispose();

            if (
                Directory.Exists(
                    RootPath
                ))
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
