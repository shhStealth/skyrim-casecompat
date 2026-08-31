using CaseCompat.Core.Repair;
using CaseCompat.Core.Resolution;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairProjectedPlanLifecycleIntegrationTests
{
    private static readonly DateTimeOffset T0 =
        new(
            2026,
            8,
            31,
            19,
            0,
            0,
            TimeSpan.Zero
        );

    [Fact]
    public void
        ProjectPersistExecute_MultiDirectoryCaseMismatch_ReachesApplied()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        if (!fixture.SupportsExecutionPrerequisites())
        {
            return;
        }

        DataRelativePathResolution initialResolution =
            fixture.ResolveRequestedPath();

        Assert.False(
            initialResolution.LinuxResolves
        );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .DirectStrictCaseMismatch,
            DataRelativePathCaseMismatchTopologyClassifier.Classify(
                initialResolution
            )
        );

        byte[] sourceBefore =
            File.ReadAllBytes(
                fixture.SourcePath
            );

        DataRelativePathRepairPlanProjection projection =
            DataRelativePathRepairPlanProjector.Project(
                initialResolution
            );

        Assert.True(
            projection.HasPlan,
            projection.Error
        );

        Assert.Equal(
            3,
            projection.Operations.Count
        );

        Assert.Equal(
            DataRelativePathRepairPlanOperationKind.CreateDirectory,
            projection.Operations[0].Kind
        );

        Assert.Equal(
            DataRelativePathRepairPlanOperationKind.CreateDirectory,
            projection.Operations[1].Kind
        );

        Assert.Equal(
            DataRelativePathRepairPlanOperationKind.CreateFile,
            projection.Operations[2].Kind
        );

        Assert.False(
            Directory.Exists(
                fixture.RequestedTopDirectoryPath
            )
        );

        DataRelativePathRepairPlanManifestRecord manifest =
            fixture.PersistManifest(
                initialResolution,
                projection
            );

        Assert.Equal(
            3,
            manifest.Operations.Count
        );

        DataRelativePathRepairPlanForwardExecution execution =
            DataRelativePathRepairPlanForwardExecutor.Execute(
                fixture.JournalDirectory,
                Fixture.ManifestName,
                fixture.DataRoot,
                T0.AddSeconds(10)
            );

        if (NoReplaceUnsupported(execution))
        {
            return;
        }

        Assert.True(
            execution.Success,
            execution.Error
        );

        Assert.Equal(
            DataRelativePathRepairPlanForwardExecutionState
                .AppliedDurably,
            execution.State
        );

        Assert.Equal(
            3,
            execution.OperationResults.Count
        );

        Assert.All(
            execution.OperationResults,
            result =>
            {
                Assert.True(
                    result.Success,
                    result.Error
                );
            }
        );

        Assert.True(
            File.Exists(
                fixture.SourcePath
            )
        );

        Assert.True(
            Directory.Exists(
                fixture.RequestedTopDirectoryPath
            )
        );

        Assert.True(
            Directory.Exists(
                fixture.RequestedParentPath
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

        fixture.AssertAllOperationJournalsApplied(
            manifest
        );

        JournalCheckpoint[] beforeSecondRun =
            fixture.CaptureJournalCheckpoints(
                manifest
            );

        /*
         * Re-running the same durable plan is read/classify only for
         * already Applied operations. It must neither recreate nor
         * rewrite their journals.
         */
        DataRelativePathRepairPlanForwardExecution second =
            DataRelativePathRepairPlanForwardExecutor.Execute(
                fixture.JournalDirectory,
                Fixture.ManifestName,
                fixture.DataRoot,
                T0.AddSeconds(20)
            );

        Assert.True(
            second.Success,
            second.Error
        );

        Assert.Equal(
            3,
            second.OperationResults.Count
        );

        JournalCheckpoint[] afterSecondRun =
            fixture.CaptureJournalCheckpoints(
                manifest
            );

        Assert.Equal(
            beforeSecondRun,
            afterSecondRun
        );

        Assert.Equal(
            sourceBefore,
            File.ReadAllBytes(
                fixture.DestinationPath
            )
        );
    }

    [Fact]
    public void
        Execute_FirstDirectoryAlreadyApplied_ResumesRemainingOperations()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        if (!fixture.SupportsExecutionPrerequisites())
        {
            return;
        }

        DataRelativePathResolution resolution =
            fixture.ResolveRequestedPath();

        DataRelativePathRepairPlanProjection projection =
            DataRelativePathRepairPlanProjector.Project(
                resolution
            );

        Assert.True(
            projection.HasPlan,
            projection.Error
        );

        DataRelativePathRepairPlanManifestRecord manifest =
            fixture.PersistManifest(
                resolution,
                projection
            );

        DataRelativePathRepairPlanManifestOperation first =
            manifest.Operations[0];

        DataRelativePathRepairDirectoryExecution firstExecution =
            DataRelativePathRepairDirectoryExecutor.Execute(
                fixture.JournalDirectory,
                first.JournalChildName,
                first.Operation,
                manifest.InitialDestinationParentSnapshot,
                fixture.DataRoot,
                T0.AddSeconds(5)
            );

        if (
            firstExecution.ForwardRecovery?.Publication?.State ==
            LinuxPublishOwnedDirectoryAtState.NoReplaceUnsupported)
        {
            return;
        }

        Assert.True(
            firstExecution.Success,
            firstExecution.Error
        );

        DataRelativePathRepairDirectoryJournalReaderResult before =
            DataRelativePathRepairDirectoryJournalReader.Read(
                fixture.JournalDirectory,
                first.JournalChildName
            );

        Assert.True(
            before.Success,
            before.Error
        );

        Guid firstJournalId =
            before.Record!.JournalId;

        int firstRevision =
            before.Record.Revision;

        DataRelativePathRepairPlanForwardExecution execution =
            DataRelativePathRepairPlanForwardExecutor.Execute(
                fixture.JournalDirectory,
                Fixture.ManifestName,
                fixture.DataRoot,
                T0.AddSeconds(10)
            );

        if (NoReplaceUnsupported(execution))
        {
            return;
        }

        Assert.True(
            execution.Success,
            execution.Error
        );

        Assert.Equal(
            3,
            execution.OperationResults.Count
        );

        DataRelativePathRepairDirectoryJournalReaderResult after =
            DataRelativePathRepairDirectoryJournalReader.Read(
                fixture.JournalDirectory,
                first.JournalChildName
            );

        Assert.True(
            after.Success,
            after.Error
        );

        Assert.Equal(
            firstJournalId,
            after.Record!.JournalId
        );

        Assert.Equal(
            firstRevision,
            after.Record.Revision
        );

        Assert.True(
            File.Exists(
                fixture.DestinationPath
            )
        );

        fixture.AssertAllOperationJournalsApplied(
            manifest
        );
    }

    [Fact]
    public void
        Execute_ExistingJournalDoesNotMatchManifest_IsRejectedBeforeRecovery()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        if (!fixture.SupportsExecutionPrerequisites())
        {
            return;
        }

        DataRelativePathResolution resolution =
            fixture.ResolveRequestedPath();

        DataRelativePathRepairPlanProjection projection =
            DataRelativePathRepairPlanProjector.Project(
                resolution
            );

        Assert.True(
            projection.HasPlan,
            projection.Error
        );

        DataRelativePathRepairPlanManifestRecord manifest =
            fixture.PersistManifest(
                resolution,
                projection
            );

        DataRelativePathRepairPlanManifestOperation first =
            manifest.Operations[0];

        using LinuxNoFollowPathHandle parent =
            Fixture.OpenRoot(
                fixture.MeshesPath
            );

        LinuxOpenedDirectoryIncarnationResult parentIncarnation =
            LinuxOpenedDirectoryIncarnation.Capture(
                parent,
                fixture.MeshesPath
            );

        Assert.True(
            parentIncarnation.Success,
            parentIncarnation.Error
        );

        var wrongOperation =
            new DataRelativePathRepairPlanOperation(
                Kind:
                    DataRelativePathRepairPlanOperationKind
                        .CreateDirectory,
                DestinationPath:
                    Path.Combine(
                        fixture.MeshesPath,
                        "Unrelated"
                    ),
                SourcePath:
                    null
            );

        DataRelativePathRepairDirectoryJournalTransitionResult
            wrongIntentTransition =
                DataRelativePathRepairDirectoryJournal.CreateIntent(
                    Guid.NewGuid(),
                    T0.AddSeconds(1),
                    fixture.DataRoot,
                    wrongOperation,
                    manifest.InitialDestinationParentSnapshot,
                    parentIncarnation.Identity!
                );

        Assert.True(
            wrongIntentTransition.Success,
            wrongIntentTransition.Error
        );

        DataRelativePathRepairDirectoryJournalWriterResult wrongWrite =
            DataRelativePathRepairDirectoryJournalWriter.CreateInitial(
                fixture.JournalDirectory,
                first.JournalChildName,
                wrongIntentTransition.Record!
            );

        Assert.True(
            wrongWrite.Success,
            wrongWrite.Error
        );

        DataRelativePathRepairPlanForwardExecution execution =
            DataRelativePathRepairPlanForwardExecutor.Execute(
                fixture.JournalDirectory,
                Fixture.ManifestName,
                fixture.DataRoot,
                T0.AddSeconds(10)
            );

        Assert.False(
            execution.Success
        );

        Assert.Equal(
            DataRelativePathRepairPlanForwardExecutionState
                .OperationFailed,
            execution.State
        );

        DataRelativePathRepairPlanForwardOperationExecution failed =
            Assert.Single(
                execution.OperationResults
            );

        Assert.Equal(
            DataRelativePathRepairPlanForwardOperationExecutionState
                .JournalMismatch,
            failed.State
        );

        Assert.Null(
            failed.DirectoryClassification
        );

        Assert.False(
            Directory.Exists(
                fixture.RequestedTopDirectoryPath
            )
        );

        Assert.False(
            Directory.Exists(
                Path.Combine(
                    fixture.MeshesPath,
                    "Unrelated"
                )
            )
        );
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

    private sealed record JournalCheckpoint(
        Guid JournalId,
        int Revision
    );

    private sealed class Fixture
        : IDisposable
    {
        public const string ManifestName =
            "plan.json";

        public Fixture()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-projected-plan-lifecycle-tests",
                    Guid.NewGuid().ToString("N")
                );

            DataRoot =
                Path.Combine(
                    RootPath,
                    "Data"
                );

            MeshesPath =
                Path.Combine(
                    DataRoot,
                    "meshes"
                );

            SourceTopDirectoryPath =
                Path.Combine(
                    MeshesPath,
                    "fafny stash"
                );

            SourceParentPath =
                Path.Combine(
                    SourceTopDirectoryPath,
                    "Bishop Armor"
                );

            SourcePath =
                Path.Combine(
                    SourceParentPath,
                    "armor.nif"
                );

            RequestedTopDirectoryPath =
                Path.Combine(
                    MeshesPath,
                    "Fafny stash"
                );

            RequestedParentPath =
                Path.Combine(
                    RequestedTopDirectoryPath,
                    "Bishop Armor"
                );

            DestinationPath =
                Path.Combine(
                    RequestedParentPath,
                    "armor.nif"
                );

            JournalDirectoryPath =
                Path.Combine(
                    RootPath,
                    "Journal"
                );

            Directory.CreateDirectory(
                SourceParentPath
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
                    0x54,
                    0x2D,
                    0x50,
                    0x4C,
                    0x41,
                    0x4E
                ]
            );

            JournalDirectory =
                OpenRoot(
                    JournalDirectoryPath
                );
        }

        public string RootPath { get; }

        public string DataRoot { get; }

        public string MeshesPath { get; }

        public string SourceTopDirectoryPath { get; }

        public string SourceParentPath { get; }

        public string SourcePath { get; }

        public string RequestedTopDirectoryPath { get; }

        public string RequestedParentPath { get; }

        public string DestinationPath { get; }

        public string JournalDirectoryPath { get; }

        public LinuxNoFollowPathHandle JournalDirectory { get; }

        public DataRelativePathResolution ResolveRequestedPath()
        {
            return DataRelativePathResolver.ResolveFile(
                DataRoot,
                "meshes/Fafny stash/Bishop Armor/armor.nif",
                InspectFixtureCasefold
            );
        }

        public DataRelativePathRepairPlanManifestRecord
            PersistManifest(
                DataRelativePathResolution resolution,
                DataRelativePathRepairPlanProjection projection)
        {
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

            DataRelativePathRepairPlanManifestCreation creation =
                DataRelativePathRepairPlanManifest.Create(
                    Guid.NewGuid(),
                    T0,
                    DataRoot,
                    resolution.RequestedPath,
                    sourceSnapshot,
                    parentSnapshot,
                    projection.Operations
                );

            Assert.True(
                creation.Success,
                creation.Error
            );

            DataRelativePathRepairPlanManifestRecord manifest =
                creation.Manifest!;

            DataRelativePathRepairPlanManifestWriterResult write =
                DataRelativePathRepairPlanManifestWriter.CreateInitial(
                    JournalDirectory,
                    ManifestName,
                    manifest
                );

            Assert.True(
                write.Success,
                write.Error
            );

            DataRelativePathRepairPlanManifestReaderResult read =
                DataRelativePathRepairPlanManifestReader.Read(
                    JournalDirectory,
                    ManifestName
                );

            Assert.True(
                read.Success,
                read.Error
            );

            Assert.Equal(
                manifest.PlanId,
                read.Manifest!.PlanId
            );

            return read.Manifest;
        }

        public void AssertAllOperationJournalsApplied(
            DataRelativePathRepairPlanManifestRecord manifest)
        {
            Assert.Equal(
                3,
                manifest.Operations.Count
            );

            for (
                int index = 0;
                index < 2;
                index++)
            {
                DataRelativePathRepairPlanManifestOperation entry =
                    manifest.Operations[index];

                DataRelativePathRepairDirectoryJournalReaderResult read =
                    DataRelativePathRepairDirectoryJournalReader.Read(
                        JournalDirectory,
                        entry.JournalChildName
                    );

                Assert.True(
                    read.Success,
                    read.Error
                );

                Assert.Equal(
                    DataRelativePathRepairDirectoryJournalState.Applied,
                    read.Record!.State
                );

                DataRelativePathRepairDirectoryRecoveryClassification
                    classification =
                        DataRelativePathRepairDirectoryRecoveryClassifier
                            .Classify(
                                read.Record,
                                DataRoot
                            );

                Assert.Equal(
                    DataRelativePathRepairDirectoryRecoveryState
                        .AppliedFinalMatches,
                    classification.State
                );
            }

            DataRelativePathRepairPlanManifestOperation fileEntry =
                manifest.Operations[2];

            DataRelativePathRepairFileJournalReaderResult fileRead =
                DataRelativePathRepairFileJournalReader.Read(
                    JournalDirectory,
                    fileEntry.JournalChildName
                );

            Assert.True(
                fileRead.Success,
                fileRead.Error
            );

            Assert.Equal(
                DataRelativePathRepairFileJournalState.Applied,
                fileRead.Record!.State
            );

            DataRelativePathRepairFileRecoveryClassification
                fileClassification =
                    DataRelativePathRepairFileRecoveryClassifier
                        .Classify(
                            fileRead.Record,
                            DataRoot
                        );

            Assert.Equal(
                DataRelativePathRepairFileRecoveryState
                    .AppliedDestinationMatches,
                fileClassification.State
            );
        }

        public JournalCheckpoint[] CaptureJournalCheckpoints(
            DataRelativePathRepairPlanManifestRecord manifest)
        {
            var checkpoints =
                new JournalCheckpoint[
                    manifest.Operations.Count
                ];

            for (
                int index = 0;
                index < manifest.Operations.Count;
                index++)
            {
                DataRelativePathRepairPlanManifestOperation entry =
                    manifest.Operations[index];

                if (
                    entry.Operation.Kind ==
                    DataRelativePathRepairPlanOperationKind
                        .CreateDirectory)
                {
                    DataRelativePathRepairDirectoryJournalReaderResult read =
                        DataRelativePathRepairDirectoryJournalReader.Read(
                            JournalDirectory,
                            entry.JournalChildName
                        );

                    Assert.True(
                        read.Success,
                        read.Error
                    );

                    checkpoints[index] =
                        new(
                            read.Record!.JournalId,
                            read.Record.Revision
                        );
                }
                else
                {
                    DataRelativePathRepairFileJournalReaderResult read =
                        DataRelativePathRepairFileJournalReader.Read(
                            JournalDirectory,
                            entry.JournalChildName
                        );

                    Assert.True(
                        read.Success,
                        read.Error
                    );

                    checkpoints[index] =
                        new(
                            read.Record!.JournalId,
                            read.Record.Revision
                        );
                }
            }

            return checkpoints;
        }

        public bool SupportsExecutionPrerequisites()
        {
            DirectoryCasefoldResult meshesFlags =
                LinuxDirectoryFlags.Inspect(
                    MeshesPath
                );

            if (
                !meshesFlags.Exists ||
                meshesFlags.Error is not null ||
                meshesFlags.CasefoldEnabled != false)
            {
                return false;
            }

            using LinuxNoFollowPathHandle meshes =
                OpenRoot(
                    MeshesPath
                );

            LinuxOpenedDirectoryIncarnationResult directoryIncarnation =
                LinuxOpenedDirectoryIncarnation.Capture(
                    meshes,
                    MeshesPath
                );

            if (!directoryIncarnation.Success)
            {
                return false;
            }

            if (
                !SupportsStrongUnnamedFile(
                    JournalDirectory
                ))
            {
                return false;
            }

            if (
                !SupportsStrongUnnamedFile(
                    meshes
                ))
            {
                return false;
            }

            return true;
        }

        private static bool SupportsStrongUnnamedFile(
            LinuxNoFollowPathHandle parent)
        {
            LinuxCreateUnnamedFileAtResult create =
                LinuxCreateUnnamedFileAt.Create(
                    parent
                );

            if (!create.Success)
            {
                return false;
            }

            using LinuxUnnamedFileHandle unnamed =
                create.OpenedFile!;

            LinuxOpenedFileIncarnationResult incarnation =
                LinuxOpenedFileIncarnation.Capture(
                    unnamed
                );

            return incarnation.Success;
        }

        /*
         * Model the confirmed Skyrim layout:
         *
         * Data may be casefold-enabled, while meshes itself and its
         * descendants are strict.
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

            return new(
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

        public static LinuxNoFollowPathHandle OpenRoot(
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
                LinuxNoFollowPathHandle
            >(
                opened.OpenedPath
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
