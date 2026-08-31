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
        Execute_JournalGap_IsRejectedBeforeFilesystemMutation()
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

        DataRelativePathRepairPlanManifestOperation later =
            manifest.Operations[1];

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
            laterIntentTransition =
                DataRelativePathRepairDirectoryJournal.CreateIntent(
                    Guid.NewGuid(),
                    T0.AddSeconds(1),
                    fixture.DataRoot,
                    wrongOperation,
                    manifest.InitialDestinationParentSnapshot,
                    parentIncarnation.Identity!
                );

        Assert.True(
            laterIntentTransition.Success,
            laterIntentTransition.Error
        );

        DataRelativePathRepairDirectoryJournalWriterResult laterWrite =
            DataRelativePathRepairDirectoryJournalWriter.CreateInitial(
                fixture.JournalDirectory,
                later.JournalChildName,
                laterIntentTransition.Record!
            );

        Assert.True(
            laterWrite.Success,
            laterWrite.Error
        );

        DataRelativePathRepairDirectoryJournalReaderResult laterBefore =
            DataRelativePathRepairDirectoryJournalReader.Read(
                fixture.JournalDirectory,
                later.JournalChildName
            );

        Assert.True(
            laterBefore.Success,
            laterBefore.Error
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
                .PreflightFailed,
            execution.State
        );

        DataRelativePathRepairPlanForwardOperationExecution failed =
            Assert.Single(
                execution.OperationResults
            );

        Assert.Equal(
            DataRelativePathRepairPlanForwardOperationExecutionState
                .JournalGap,
            failed.State
        );

        Assert.Equal(
            1,
            failed.Index
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

        DataRelativePathRepairDirectoryJournalReaderResult firstAfter =
            DataRelativePathRepairDirectoryJournalReader.Read(
                fixture.JournalDirectory,
                first.JournalChildName
            );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalReadState
                .JournalUnavailable,
            firstAfter.State
        );

        DataRelativePathRepairDirectoryJournalReaderResult laterAfter =
            DataRelativePathRepairDirectoryJournalReader.Read(
                fixture.JournalDirectory,
                later.JournalChildName
            );

        Assert.True(
            laterAfter.Success,
            laterAfter.Error
        );

        Assert.Equal(
            laterBefore.Record,
            laterAfter.Record
        );
    }

    [Fact]
    public void
        Execute_LaterJournalMismatch_IsRejectedBeforeEarlierRecovery()
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

        DataRelativePathRepairPlanManifestOperation later =
            manifest.Operations[1];

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

        DataRelativePathRepairDirectoryJournalTransitionResult
            firstIntentTransition =
                DataRelativePathRepairDirectoryJournal.CreateIntent(
                    Guid.NewGuid(),
                    T0.AddSeconds(1),
                    fixture.DataRoot,
                    first.Operation,
                    manifest.InitialDestinationParentSnapshot,
                    parentIncarnation.Identity!
                );

        Assert.True(
            firstIntentTransition.Success,
            firstIntentTransition.Error
        );

        DataRelativePathRepairDirectoryJournalWriterResult firstWrite =
            DataRelativePathRepairDirectoryJournalWriter.CreateInitial(
                fixture.JournalDirectory,
                first.JournalChildName,
                firstIntentTransition.Record!
            );

        Assert.True(
            firstWrite.Success,
            firstWrite.Error
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
            laterIntentTransition =
                DataRelativePathRepairDirectoryJournal.CreateIntent(
                    Guid.NewGuid(),
                    T0.AddSeconds(2),
                    fixture.DataRoot,
                    wrongOperation,
                    manifest.InitialDestinationParentSnapshot,
                    parentIncarnation.Identity!
                );

        Assert.True(
            laterIntentTransition.Success,
            laterIntentTransition.Error
        );

        DataRelativePathRepairDirectoryJournalWriterResult laterWrite =
            DataRelativePathRepairDirectoryJournalWriter.CreateInitial(
                fixture.JournalDirectory,
                later.JournalChildName,
                laterIntentTransition.Record!
            );

        Assert.True(
            laterWrite.Success,
            laterWrite.Error
        );

        DataRelativePathRepairDirectoryJournalReaderResult firstBefore =
            DataRelativePathRepairDirectoryJournalReader.Read(
                fixture.JournalDirectory,
                first.JournalChildName
            );

        DataRelativePathRepairDirectoryJournalReaderResult laterBefore =
            DataRelativePathRepairDirectoryJournalReader.Read(
                fixture.JournalDirectory,
                later.JournalChildName
            );

        Assert.True(
            firstBefore.Success,
            firstBefore.Error
        );

        Assert.True(
            laterBefore.Success,
            laterBefore.Error
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
                .PreflightFailed,
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

        Assert.Equal(
            1,
            failed.Index
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

        DataRelativePathRepairDirectoryJournalReaderResult firstAfter =
            DataRelativePathRepairDirectoryJournalReader.Read(
                fixture.JournalDirectory,
                first.JournalChildName
            );

        DataRelativePathRepairDirectoryJournalReaderResult laterAfter =
            DataRelativePathRepairDirectoryJournalReader.Read(
                fixture.JournalDirectory,
                later.JournalChildName
            );

        Assert.True(
            firstAfter.Success,
            firstAfter.Error
        );

        Assert.True(
            laterAfter.Success,
            laterAfter.Error
        );

        Assert.Equal(
            firstBefore.Record,
            firstAfter.Record
        );

        Assert.Equal(
            laterBefore.Record,
            laterAfter.Record
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
                .PreflightFailed,
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

    [Fact]
    public void
        GuardedRecoveryActions_DifferentJournalIncarnation_IsRejected()
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

        DataRelativePathRepairPlanManifestOperation outerEntry =
            manifest.Operations[0];

        DataRelativePathRepairPlanManifestOperation innerEntry =
            manifest.Operations[1];

        DataRelativePathRepairPlanManifestOperation fileEntry =
            manifest.Operations[2];

        DataRelativePathRepairDirectoryJournalReaderResult outerRead =
            DataRelativePathRepairDirectoryJournalReader.Read(
                fixture.JournalDirectory,
                outerEntry.JournalChildName
            );

        DataRelativePathRepairDirectoryJournalReaderResult innerRead =
            DataRelativePathRepairDirectoryJournalReader.Read(
                fixture.JournalDirectory,
                innerEntry.JournalChildName
            );

        DataRelativePathRepairFileJournalReaderResult fileRead =
            DataRelativePathRepairFileJournalReader.Read(
                fixture.JournalDirectory,
                fileEntry.JournalChildName
            );

        Assert.True(
            outerRead.Success,
            outerRead.Error
        );

        Assert.True(
            innerRead.Success,
            innerRead.Error
        );

        Assert.True(
            fileRead.Success,
            fileRead.Error
        );

        LinuxFileIncarnationIdentity outerJournalIdentity =
            outerRead.JournalIncarnationIdentity!;

        LinuxFileIncarnationIdentity innerJournalIdentity =
            innerRead.JournalIncarnationIdentity!;

        LinuxFileIncarnationIdentity fileJournalIdentity =
            fileRead.JournalIncarnationIdentity!;

        Assert.False(
            outerJournalIdentity.SameIncarnationAs(
                innerJournalIdentity
            )
        );

        Assert.False(
            outerJournalIdentity.SameIncarnationAs(
                fileJournalIdentity
            )
        );

        JournalCheckpoint[] before =
            fixture.CaptureJournalCheckpoints(
                manifest
            );

        DataRelativePathRepairDirectoryIntentRecovery intent =
            DataRelativePathRepairDirectoryIntentRecoveryAction.Recover(
                fixture.JournalDirectory,
                outerEntry.JournalChildName,
                fixture.DataRoot,
                T0.AddSeconds(20),
                innerJournalIdentity
            );

        Assert.False(
            intent.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryIntentRecoveryState
                .JournalIncarnationChanged,
            intent.State
        );

        Assert.Null(
            intent.Classification
        );

        DataRelativePathRepairDirectoryReprepareRecovery reprepare =
            DataRelativePathRepairDirectoryReprepareRecoveryAction.Recover(
                fixture.JournalDirectory,
                outerEntry.JournalChildName,
                fixture.DataRoot,
                T0.AddSeconds(21),
                innerJournalIdentity
            );

        Assert.False(
            reprepare.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryReprepareRecoveryState
                .JournalIncarnationChanged,
            reprepare.State
        );

        Assert.Null(
            reprepare.Classification
        );

        DataRelativePathRepairDirectoryForwardRecovery directoryForward =
            DataRelativePathRepairDirectoryForwardRecoveryAction.Recover(
                fixture.JournalDirectory,
                outerEntry.JournalChildName,
                fixture.DataRoot,
                T0.AddSeconds(22),
                innerJournalIdentity
            );

        Assert.False(
            directoryForward.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryForwardRecoveryState
                .JournalIncarnationChanged,
            directoryForward.State
        );

        Assert.Null(
            directoryForward.Classification
        );

        DataRelativePathRepairFileForwardRecovery fileForward =
            DataRelativePathRepairFileForwardRecoveryAction.Recover(
                fixture.JournalDirectory,
                fileEntry.JournalChildName,
                fixture.DataRoot,
                T0.AddSeconds(23),
                outerJournalIdentity
            );

        Assert.False(
            fileForward.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileForwardRecoveryState
                .JournalIncarnationChanged,
            fileForward.State
        );

        Assert.Null(
            fileForward.Classification
        );

        JournalCheckpoint[] after =
            fixture.CaptureJournalCheckpoints(
                manifest
            );

        Assert.Equal(
            before,
            after
        );

        fixture.AssertAllOperationJournalsApplied(
            manifest
        );

        Assert.True(
            File.Exists(
                fixture.DestinationPath
            )
        );
    }

    [Fact]
    public void
        GuardedRecoveryActions_InvalidExpectedJournalIncarnation_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        var invalidExpected =
            new LinuxFileIncarnationIdentity(
                PhysicalIdentity:
                    new LinuxOpenedFileIdentityResult(
                        State:
                            LinuxOpenedFileIdentityState
                                .MetadataUnavailable,
                        DeviceMajor:
                            null,
                        DeviceMinor:
                            null,
                        Inode:
                            null,
                        LinkCount:
                            null,
                        MountId:
                            null,
                        Errno:
                            null,
                        Error:
                            "fixture"
                    ),
                InodeGeneration:
                    0U
            );

        Assert.False(
            invalidExpected.Success
        );

        DataRelativePathRepairDirectoryIntentRecovery intent =
            DataRelativePathRepairDirectoryIntentRecoveryAction.Recover(
                fixture.JournalDirectory,
                "missing-intent.json",
                fixture.DataRoot,
                T0.AddSeconds(30),
                invalidExpected
            );

        Assert.False(
            intent.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryIntentRecoveryState
                .InvalidExpectedJournalIdentity,
            intent.State
        );

        Assert.Null(
            intent.LockState
        );

        Assert.Null(
            intent.JournalRead
        );

        Assert.Null(
            intent.Classification
        );

        DataRelativePathRepairDirectoryReprepareRecovery reprepare =
            DataRelativePathRepairDirectoryReprepareRecoveryAction.Recover(
                fixture.JournalDirectory,
                "missing-reprepare.json",
                fixture.DataRoot,
                T0.AddSeconds(31),
                invalidExpected
            );

        Assert.False(
            reprepare.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryReprepareRecoveryState
                .InvalidExpectedJournalIdentity,
            reprepare.State
        );

        Assert.Null(
            reprepare.LockState
        );

        Assert.Null(
            reprepare.JournalRead
        );

        Assert.Null(
            reprepare.Classification
        );

        DataRelativePathRepairDirectoryForwardRecovery directoryForward =
            DataRelativePathRepairDirectoryForwardRecoveryAction.Recover(
                fixture.JournalDirectory,
                "missing-directory-forward.json",
                fixture.DataRoot,
                T0.AddSeconds(32),
                invalidExpected
            );

        Assert.False(
            directoryForward.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryForwardRecoveryState
                .InvalidExpectedJournalIdentity,
            directoryForward.State
        );

        Assert.Null(
            directoryForward.LockState
        );

        Assert.Null(
            directoryForward.JournalRead
        );

        Assert.Null(
            directoryForward.Classification
        );

        DataRelativePathRepairFileForwardRecovery fileForward =
            DataRelativePathRepairFileForwardRecoveryAction.Recover(
                fixture.JournalDirectory,
                "missing-file-forward.json",
                fixture.DataRoot,
                T0.AddSeconds(33),
                invalidExpected
            );

        Assert.False(
            fileForward.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileForwardRecoveryState
                .InvalidExpectedJournalIdentity,
            fileForward.State
        );

        Assert.Null(
            fileForward.LockState
        );

        Assert.Null(
            fileForward.JournalRead
        );

        Assert.Null(
            fileForward.Classification
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

    [Fact]
    public void
        Rollback_PreparedPublishedDirectory_ReconcilesThenRollsBack()
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

        /*
         * Execute exactly the first directory operation.
         *
         * The transaction reaches Applied, but its IntentRecovery
         * result retains the exact durable Prepared record that existed
         * immediately before publication.
         */
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

        DataRelativePathRepairDirectoryJournalRecord prepared =
            Assert.IsType<
                DataRelativePathRepairDirectoryJournalRecord
            >(
                firstExecution.IntentRecovery?
                    .PreparedTransition?
                    .Record
            );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.Prepared,
            prepared.State
        );

        Assert.True(
            Directory.Exists(
                fixture.RequestedTopDirectoryPath
            )
        );

        Assert.False(
            Directory.Exists(
                fixture.RequestedParentPath
            )
        );

        DataRelativePathRepairDirectoryJournalReaderResult appliedRead =
            DataRelativePathRepairDirectoryJournalReader.Read(
                fixture.JournalDirectory,
                first.JournalChildName
            );

        Assert.True(
            appliedRead.Success,
            appliedRead.Error
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.Applied,
            appliedRead.Record!.State
        );

        /*
         * Recreate the exact crash boundary:
         *
         *     final name already published
         *     staging name gone
         *     durable journal still Prepared
         *
         * ReplaceExisting is bound to the incarnation of the Applied
         * journal we just read, so the test does not bypass journal
         * replacement authority.
         */
        DataRelativePathRepairDirectoryJournalWriterResult rewind =
            DataRelativePathRepairDirectoryJournalWriter.ReplaceExisting(
                fixture.JournalDirectory,
                first.JournalChildName,
                appliedRead.JournalIncarnationIdentity!,
                prepared
            );

        Assert.True(
            rewind.Success,
            rewind.Error
        );

        DataRelativePathRepairDirectoryJournalReaderResult crashRead =
            DataRelativePathRepairDirectoryJournalReader.Read(
                fixture.JournalDirectory,
                first.JournalChildName
            );

        Assert.True(
            crashRead.Success,
            crashRead.Error
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.Prepared,
            crashRead.Record!.State
        );

        DataRelativePathRepairDirectoryRecoveryClassification
            crashClassification =
                DataRelativePathRepairDirectoryRecoveryClassifier
                    .Classify(
                        crashRead.Record,
                        fixture.DataRoot
                    );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .PreparedFinalMatchesStagingMissing,
            crashClassification.State
        );

        DataRelativePathRepairPlanRollbackExecution rollback =
            DataRelativePathRepairPlanRollbackExecutor.Execute(
                fixture.JournalDirectory,
                Fixture.ManifestName,
                fixture.DataRoot,
                T0.AddSeconds(20)
            );

        Assert.True(
            rollback.Success,
            rollback.Error
        );

        Assert.Equal(
            DataRelativePathRepairPlanRollbackExecutionState
                .RolledBackDurably,
            rollback.State
        );

        Assert.Equal(
            new[]
            {
                2,
                1,
                0
            },
            rollback.OperationResults
                .Select(
                    result =>
                        result.Index
                )
                .ToArray()
        );

        Assert.Equal(
            DataRelativePathRepairPlanRollbackOperationExecutionState
                .NotStartedSkipped,
            rollback.OperationResults[0].State
        );

        Assert.Equal(
            DataRelativePathRepairPlanRollbackOperationExecutionState
                .NotStartedSkipped,
            rollback.OperationResults[1].State
        );

        Assert.Equal(
            DataRelativePathRepairPlanRollbackOperationExecutionState
                .RolledBackDurably,
            rollback.OperationResults[2].State
        );

        Assert.NotNull(
            rollback.OperationResults[2]
                .DirectoryReconciliation
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryReconciliationState
                .AppliedDurably,
            rollback.OperationResults[2]
                .DirectoryReconciliation!
                .State
        );

        Assert.False(
            Directory.Exists(
                fixture.RequestedTopDirectoryPath
            )
        );

        Assert.True(
            Directory.Exists(
                fixture.SourceTopDirectoryPath
            )
        );

        Assert.True(
            File.Exists(
                fixture.SourcePath
            )
        );

        DataRelativePathRepairDirectoryJournalReaderResult finalRead =
            DataRelativePathRepairDirectoryJournalReader.Read(
                fixture.JournalDirectory,
                first.JournalChildName
            );

        Assert.True(
            finalRead.Success,
            finalRead.Error
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.RolledBack,
            finalRead.Record!.State
        );

        DataRelativePathRepairDirectoryJournalReaderResult secondRead =
            DataRelativePathRepairDirectoryJournalReader.Read(
                fixture.JournalDirectory,
                manifest.Operations[1].JournalChildName
            );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalReadState
                .JournalUnavailable,
            secondRead.State
        );

        DataRelativePathRepairFileJournalReaderResult thirdRead =
            DataRelativePathRepairFileJournalReader.Read(
                fixture.JournalDirectory,
                manifest.Operations[2].JournalChildName
            );

        Assert.Equal(
            DataRelativePathRepairFileJournalReadState
                .JournalUnavailable,
            thirdRead.State
        );
    }

    [Fact]
    public void
        Rollback_PreparedPublishedFile_ReconcilesThenRollsBack()
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

        DataRelativePathRepairPlanManifestRecord manifest =
            fixture.PersistManifest(
                initialResolution,
                projection
            );

        /*
         * Run the genuine plan so both parent directory operations are
         * durably Applied before the file transaction starts.
         */
        DataRelativePathRepairPlanForwardExecution forward =
            DataRelativePathRepairPlanForwardExecutor.Execute(
                fixture.JournalDirectory,
                Fixture.ManifestName,
                fixture.DataRoot,
                T0.AddSeconds(10)
            );

        if (NoReplaceUnsupported(forward))
        {
            return;
        }

        Assert.True(
            forward.Success,
            forward.Error
        );

        DataRelativePathRepairPlanForwardOperationExecution
            fileOperationResult =
                Assert.Single(
                    forward.OperationResults,
                    result =>
                        result.Index == 2
                );

        Assert.NotNull(
            fileOperationResult.FileExecution
        );

        DataRelativePathRepairFileJournalRecord prepared =
            Assert.IsType<
                DataRelativePathRepairFileJournalRecord
            >(
                fileOperationResult.FileExecution?
                    .PreparedTransition?
                    .Record
            );

        Assert.Equal(
            DataRelativePathRepairFileJournalState.Prepared,
            prepared.State
        );

        Assert.True(
            File.Exists(
                fixture.DestinationPath
            )
        );

        Assert.True(
            Directory.Exists(
                fixture.RequestedParentPath
            )
        );

        DataRelativePathRepairPlanManifestOperation fileEntry =
            manifest.Operations[2];

        DataRelativePathRepairFileJournalReaderResult appliedRead =
            DataRelativePathRepairFileJournalReader.Read(
                fixture.JournalDirectory,
                fileEntry.JournalChildName
            );

        Assert.True(
            appliedRead.Success,
            appliedRead.Error
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalState.Applied,
            appliedRead.Record!.State
        );

        /*
         * Restore the exact Prepared checkpoint from this same file
         * transaction while leaving the already-published destination
         * intact.
         *
         * This models a crash after publication/parent durability but
         * before the final Applied journal replacement became durable.
         */
        DataRelativePathRepairFileJournalWriterResult rewind =
            DataRelativePathRepairFileJournalWriter.ReplaceExisting(
                fixture.JournalDirectory,
                fileEntry.JournalChildName,
                appliedRead.JournalIncarnationIdentity!,
                prepared
            );

        Assert.True(
            rewind.Success,
            rewind.Error
        );

        DataRelativePathRepairFileJournalReaderResult crashRead =
            DataRelativePathRepairFileJournalReader.Read(
                fixture.JournalDirectory,
                fileEntry.JournalChildName
            );

        Assert.True(
            crashRead.Success,
            crashRead.Error
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalState.Prepared,
            crashRead.Record!.State
        );

        DataRelativePathRepairFileRecoveryClassification
            crashClassification =
                DataRelativePathRepairFileRecoveryClassifier.Classify(
                    crashRead.Record,
                    fixture.DataRoot
                );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .PreparedDestinationMatches,
            crashClassification.State
        );

        DataRelativePathRepairPlanRollbackExecution rollback =
            DataRelativePathRepairPlanRollbackExecutor.Execute(
                fixture.JournalDirectory,
                Fixture.ManifestName,
                fixture.DataRoot,
                T0.AddSeconds(20)
            );

        Assert.True(
            rollback.Success,
            rollback.Error
        );

        Assert.Equal(
            DataRelativePathRepairPlanRollbackExecutionState
                .RolledBackDurably,
            rollback.State
        );

        Assert.Equal(
            new[]
            {
                2,
                1,
                0
            },
            rollback.OperationResults
                .Select(
                    result =>
                        result.Index
                )
                .ToArray()
        );

        Assert.All(
            rollback.OperationResults,
            result =>
            {
                Assert.Equal(
                    DataRelativePathRepairPlanRollbackOperationExecutionState
                        .RolledBackDurably,
                    result.State
                );
            }
        );

        DataRelativePathRepairPlanRollbackOperationExecution
            fileRollback =
                rollback.OperationResults[0];

        Assert.Equal(
            2,
            fileRollback.Index
        );

        Assert.NotNull(
            fileRollback.FileReconciliation
        );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryReconciliationState
                .AppliedDurably,
            fileRollback.FileReconciliation!.State
        );

        Assert.False(
            File.Exists(
                fixture.DestinationPath
            )
        );

        Assert.False(
            Directory.Exists(
                fixture.RequestedParentPath
            )
        );

        Assert.False(
            Directory.Exists(
                fixture.RequestedTopDirectoryPath
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

        DataRelativePathRepairFileJournalReaderResult finalFileRead =
            DataRelativePathRepairFileJournalReader.Read(
                fixture.JournalDirectory,
                fileEntry.JournalChildName
            );

        Assert.True(
            finalFileRead.Success,
            finalFileRead.Error
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalState.RolledBack,
            finalFileRead.Record!.State
        );

        for (
            int index = 0;
            index < 2;
            index++)
        {
            DataRelativePathRepairDirectoryJournalReaderResult
                directoryRead =
                    DataRelativePathRepairDirectoryJournalReader.Read(
                        fixture.JournalDirectory,
                        manifest.Operations[index].JournalChildName
                    );

            Assert.True(
                directoryRead.Success,
                directoryRead.Error
            );

            Assert.Equal(
                DataRelativePathRepairDirectoryJournalState.RolledBack,
                directoryRead.Record!.State
            );
        }
    }

    [Fact]
    public void
        Execute_RollbackRequestedJournal_IsRejectedBeforeFilesystemMutation()
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

        DataRelativePathRepairPlanForwardExecution initialForward =
            DataRelativePathRepairPlanForwardExecutor.Execute(
                fixture.JournalDirectory,
                Fixture.ManifestName,
                fixture.DataRoot,
                T0.AddSeconds(10)
            );

        if (NoReplaceUnsupported(initialForward))
        {
            return;
        }

        Assert.True(
            initialForward.Success,
            initialForward.Error
        );

        fixture.AssertAllOperationJournalsApplied(
            manifest
        );

        DataRelativePathRepairPlanManifestOperation fileEntry =
            manifest.Operations[2];

        DataRelativePathRepairFileJournalReaderResult appliedRead =
            DataRelativePathRepairFileJournalReader.Read(
                fixture.JournalDirectory,
                fileEntry.JournalChildName
            );

        Assert.True(
            appliedRead.Success,
            appliedRead.Error
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalState.Applied,
            appliedRead.Record!.State
        );

        /*
         * Establish a genuine durable rollback-owned state using the
         * same guarded action the reverse orchestrator uses.
         */
        DataRelativePathRepairFileRollbackRequest request =
            DataRelativePathRepairFileRollbackRequestAction.Request(
                fixture.JournalDirectory,
                fileEntry.JournalChildName,
                fixture.DataRoot,
                T0.AddSeconds(20),
                appliedRead.JournalIncarnationIdentity!
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

        DataRelativePathRepairFileJournalReaderResult requestedRead =
            DataRelativePathRepairFileJournalReader.Read(
                fixture.JournalDirectory,
                fileEntry.JournalChildName
            );

        Assert.True(
            requestedRead.Success,
            requestedRead.Error
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalState.RollbackRequested,
            requestedRead.Record!.State
        );

        DataRelativePathRepairFileRecoveryClassification
            requestedClassification =
                DataRelativePathRepairFileRecoveryClassifier.Classify(
                    requestedRead.Record,
                    fixture.DataRoot
                );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .RollbackRequestedDestinationMatches,
            requestedClassification.State
        );

        JournalCheckpoint[] before =
            fixture.CaptureJournalCheckpoints(
                manifest
            );

        byte[] destinationBefore =
            File.ReadAllBytes(
                fixture.DestinationPath
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

        /*
         * Forward execution must not reinterpret or undo a durable
         * rollback-owned state.
         *
         * The whole-plan preflight should stop at operation 2 before
         * any per-operation forward mutation begins.
         */
        DataRelativePathRepairPlanForwardExecution forward =
            DataRelativePathRepairPlanForwardExecutor.Execute(
                fixture.JournalDirectory,
                Fixture.ManifestName,
                fixture.DataRoot,
                T0.AddSeconds(30)
            );

        Assert.False(
            forward.Success
        );

        Assert.Equal(
            DataRelativePathRepairPlanForwardExecutionState
                .PreflightFailed,
            forward.State
        );

        DataRelativePathRepairPlanForwardOperationExecution failed =
            Assert.Single(
                forward.OperationResults
            );

        Assert.Equal(
            2,
            failed.Index
        );

        Assert.Equal(
            DataRelativePathRepairPlanForwardOperationExecutionState
                .FileRecoveryStateNotForwardSafe,
            failed.State
        );

        Assert.NotNull(
            failed.FileClassification
        );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .RollbackRequestedDestinationMatches,
            failed.FileClassification!.State
        );

        JournalCheckpoint[] after =
            fixture.CaptureJournalCheckpoints(
                manifest
            );

        Assert.Equal(
            before,
            after
        );

        Assert.True(
            File.Exists(
                fixture.DestinationPath
            )
        );

        Assert.Equal(
            destinationBefore,
            File.ReadAllBytes(
                fixture.DestinationPath
            )
        );

        Assert.True(
            Directory.Exists(
                fixture.RequestedParentPath
            )
        );

        Assert.True(
            Directory.Exists(
                fixture.RequestedTopDirectoryPath
            )
        );

        DataRelativePathRepairFileJournalReaderResult finalRead =
            DataRelativePathRepairFileJournalReader.Read(
                fixture.JournalDirectory,
                fileEntry.JournalChildName
            );

        Assert.True(
            finalRead.Success,
            finalRead.Error
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalState.RollbackRequested,
            finalRead.Record!.State
        );
    }

    [Fact]
    public void
        Rollback_FullyAppliedPlan_RollsBackInReverseAndIsIdempotent()
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

        DataRelativePathRepairPlanManifestRecord manifest =
            fixture.PersistManifest(
                initialResolution,
                projection
            );

        DataRelativePathRepairPlanForwardExecution forward =
            DataRelativePathRepairPlanForwardExecutor.Execute(
                fixture.JournalDirectory,
                Fixture.ManifestName,
                fixture.DataRoot,
                T0.AddSeconds(10)
            );

        if (NoReplaceUnsupported(forward))
        {
            return;
        }

        Assert.True(
            forward.Success,
            forward.Error
        );

        fixture.AssertAllOperationJournalsApplied(
            manifest
        );

        DataRelativePathRepairPlanRollbackExecution rollback =
            DataRelativePathRepairPlanRollbackExecutor.Execute(
                fixture.JournalDirectory,
                Fixture.ManifestName,
                fixture.DataRoot,
                T0.AddSeconds(20)
            );

        Assert.True(
            rollback.Success,
            rollback.Error
        );

        Assert.Equal(
            DataRelativePathRepairPlanRollbackExecutionState
                .RolledBackDurably,
            rollback.State
        );

        Assert.Equal(
            new[]
            {
                2,
                1,
                0
            },
            rollback.OperationResults
                .Select(
                    result =>
                        result.Index
                )
                .ToArray()
        );

        Assert.All(
            rollback.OperationResults,
            result =>
            {
                Assert.Equal(
                    DataRelativePathRepairPlanRollbackOperationExecutionState
                        .RolledBackDurably,
                    result.State
                );
            }
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

        Assert.False(
            File.Exists(
                fixture.DestinationPath
            )
        );

        Assert.False(
            Directory.Exists(
                fixture.RequestedParentPath
            )
        );

        Assert.False(
            Directory.Exists(
                fixture.RequestedTopDirectoryPath
            )
        );

        Assert.True(
            Directory.Exists(
                fixture.SourceParentPath
            )
        );

        Assert.True(
            Directory.Exists(
                fixture.SourceTopDirectoryPath
            )
        );

        DataRelativePathResolution restored =
            fixture.ResolveRequestedPath();

        Assert.False(
            restored.LinuxResolves
        );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .DirectStrictCaseMismatch,
            DataRelativePathCaseMismatchTopologyClassifier.Classify(
                restored
            )
        );

        for (
            int index = 0;
            index < 2;
            index++)
        {
            DataRelativePathRepairDirectoryJournalReaderResult read =
                DataRelativePathRepairDirectoryJournalReader.Read(
                    fixture.JournalDirectory,
                    manifest.Operations[index].JournalChildName
                );

            Assert.True(
                read.Success,
                read.Error
            );

            Assert.Equal(
                DataRelativePathRepairDirectoryJournalState
                    .RolledBack,
                read.Record!.State
            );

            Assert.Equal(
                4,
                read.Record.Revision
            );
        }

        DataRelativePathRepairFileJournalReaderResult fileRead =
            DataRelativePathRepairFileJournalReader.Read(
                fixture.JournalDirectory,
                manifest.Operations[2].JournalChildName
            );

        Assert.True(
            fileRead.Success,
            fileRead.Error
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalState
                .RolledBack,
            fileRead.Record!.State
        );

        Assert.Equal(
            4,
            fileRead.Record.Revision
        );

        JournalCheckpoint[] beforeSecondRun =
            fixture.CaptureJournalCheckpoints(
                manifest
            );

        DataRelativePathRepairPlanRollbackExecution second =
            DataRelativePathRepairPlanRollbackExecutor.Execute(
                fixture.JournalDirectory,
                Fixture.ManifestName,
                fixture.DataRoot,
                T0.AddSeconds(30)
            );

        Assert.True(
            second.Success,
            second.Error
        );

        Assert.Equal(
            new[]
            {
                2,
                1,
                0
            },
            second.OperationResults
                .Select(
                    result =>
                        result.Index
                )
                .ToArray()
        );

        Assert.All(
            second.OperationResults,
            result =>
            {
                Assert.Equal(
                    DataRelativePathRepairPlanRollbackOperationExecutionState
                        .RolledBackDurably,
                    result.State
                );
            }
        );

        JournalCheckpoint[] afterSecondRun =
            fixture.CaptureJournalCheckpoints(
                manifest
            );

        Assert.Equal(
            beforeSecondRun,
            afterSecondRun
        );
    }

    [Fact]
    public void
        Rollback_OnlyFirstOperationApplied_SkipsUntouchedSuffix()
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

        Assert.True(
            Directory.Exists(
                fixture.RequestedTopDirectoryPath
            )
        );

        Assert.False(
            Directory.Exists(
                fixture.RequestedParentPath
            )
        );

        DataRelativePathRepairPlanRollbackExecution rollback =
            DataRelativePathRepairPlanRollbackExecutor.Execute(
                fixture.JournalDirectory,
                Fixture.ManifestName,
                fixture.DataRoot,
                T0.AddSeconds(20)
            );

        Assert.True(
            rollback.Success,
            rollback.Error
        );

        Assert.Equal(
            new[]
            {
                2,
                1,
                0
            },
            rollback.OperationResults
                .Select(
                    result =>
                        result.Index
                )
                .ToArray()
        );

        Assert.Equal(
            DataRelativePathRepairPlanRollbackOperationExecutionState
                .NotStartedSkipped,
            rollback.OperationResults[0].State
        );

        Assert.Equal(
            DataRelativePathRepairPlanRollbackOperationExecutionState
                .NotStartedSkipped,
            rollback.OperationResults[1].State
        );

        Assert.Equal(
            DataRelativePathRepairPlanRollbackOperationExecutionState
                .RolledBackDurably,
            rollback.OperationResults[2].State
        );

        Assert.False(
            Directory.Exists(
                fixture.RequestedTopDirectoryPath
            )
        );

        DataRelativePathRepairDirectoryJournalReaderResult secondRead =
            DataRelativePathRepairDirectoryJournalReader.Read(
                fixture.JournalDirectory,
                manifest.Operations[1].JournalChildName
            );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalReadState
                .JournalUnavailable,
            secondRead.State
        );

        DataRelativePathRepairFileJournalReaderResult thirdRead =
            DataRelativePathRepairFileJournalReader.Read(
                fixture.JournalDirectory,
                manifest.Operations[2].JournalChildName
            );

        Assert.Equal(
            DataRelativePathRepairFileJournalReadState
                .JournalUnavailable,
            thirdRead.State
        );

        DataRelativePathRepairDirectoryJournalReaderResult firstRead =
            DataRelativePathRepairDirectoryJournalReader.Read(
                fixture.JournalDirectory,
                first.JournalChildName
            );

        Assert.True(
            firstRead.Success,
            firstRead.Error
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState
                .RolledBack,
            firstRead.Record!.State
        );
    }

    [Fact]
    public void
        Rollback_JournalGap_IsRejectedBeforeFilesystemMutation()
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

        DataRelativePathRepairPlanForwardExecution forward =
            DataRelativePathRepairPlanForwardExecutor.Execute(
                fixture.JournalDirectory,
                Fixture.ManifestName,
                fixture.DataRoot,
                T0.AddSeconds(10)
            );

        if (NoReplaceUnsupported(forward))
        {
            return;
        }

        Assert.True(
            forward.Success,
            forward.Error
        );

        DataRelativePathRepairDirectoryJournalReaderResult innerBefore =
            DataRelativePathRepairDirectoryJournalReader.Read(
                fixture.JournalDirectory,
                manifest.Operations[1].JournalChildName
            );

        DataRelativePathRepairFileJournalReaderResult fileBefore =
            DataRelativePathRepairFileJournalReader.Read(
                fixture.JournalDirectory,
                manifest.Operations[2].JournalChildName
            );

        Assert.True(
            innerBefore.Success,
            innerBefore.Error
        );

        Assert.True(
            fileBefore.Success,
            fileBefore.Error
        );

        File.Delete(
            Path.Combine(
                fixture.JournalDirectoryPath,
                manifest.Operations[0].JournalChildName
            )
        );

        DataRelativePathRepairPlanRollbackExecution rollback =
            DataRelativePathRepairPlanRollbackExecutor.Execute(
                fixture.JournalDirectory,
                Fixture.ManifestName,
                fixture.DataRoot,
                T0.AddSeconds(20)
            );

        Assert.False(
            rollback.Success
        );

        Assert.Equal(
            DataRelativePathRepairPlanRollbackExecutionState
                .PreflightFailed,
            rollback.State
        );

        DataRelativePathRepairPlanRollbackOperationExecution failed =
            Assert.Single(
                rollback.OperationResults
            );

        Assert.Equal(
            DataRelativePathRepairPlanRollbackOperationExecutionState
                .JournalGap,
            failed.State
        );

        Assert.Equal(
            1,
            failed.Index
        );

        Assert.True(
            File.Exists(
                fixture.DestinationPath
            )
        );

        Assert.True(
            Directory.Exists(
                fixture.RequestedParentPath
            )
        );

        Assert.True(
            Directory.Exists(
                fixture.RequestedTopDirectoryPath
            )
        );

        DataRelativePathRepairDirectoryJournalReaderResult innerAfter =
            DataRelativePathRepairDirectoryJournalReader.Read(
                fixture.JournalDirectory,
                manifest.Operations[1].JournalChildName
            );

        DataRelativePathRepairFileJournalReaderResult fileAfter =
            DataRelativePathRepairFileJournalReader.Read(
                fixture.JournalDirectory,
                manifest.Operations[2].JournalChildName
            );

        Assert.Equal(
            innerBefore.Record,
            innerAfter.Record
        );

        Assert.Equal(
            fileBefore.Record,
            fileAfter.Record
        );
    }

    [Fact]
    public void
        Rollback_UnsafeLowerOperation_IsRejectedBeforeHigherRemoval()
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

        DataRelativePathRepairPlanForwardExecution forward =
            DataRelativePathRepairPlanForwardExecutor.Execute(
                fixture.JournalDirectory,
                Fixture.ManifestName,
                fixture.DataRoot,
                T0.AddSeconds(10)
            );

        if (NoReplaceUnsupported(forward))
        {
            return;
        }

        Assert.True(
            forward.Success,
            forward.Error
        );

        DataRelativePathRepairDirectoryJournalReaderResult innerBefore =
            DataRelativePathRepairDirectoryJournalReader.Read(
                fixture.JournalDirectory,
                manifest.Operations[1].JournalChildName
            );

        DataRelativePathRepairFileJournalReaderResult fileBefore =
            DataRelativePathRepairFileJournalReader.Read(
                fixture.JournalDirectory,
                manifest.Operations[2].JournalChildName
            );

        Assert.True(
            innerBefore.Success,
            innerBefore.Error
        );

        Assert.True(
            fileBefore.Success,
            fileBefore.Error
        );

        DataRelativePathRepairPlanManifestOperation outerEntry =
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

        DataRelativePathRepairDirectoryJournalTransitionResult
            replacementIntent =
                DataRelativePathRepairDirectoryJournal.CreateIntent(
                    Guid.NewGuid(),
                    T0.AddSeconds(15),
                    fixture.DataRoot,
                    outerEntry.Operation,
                    manifest.InitialDestinationParentSnapshot,
                    parentIncarnation.Identity!
                );

        Assert.True(
            replacementIntent.Success,
            replacementIntent.Error
        );

        File.Delete(
            Path.Combine(
                fixture.JournalDirectoryPath,
                outerEntry.JournalChildName
            )
        );

        DataRelativePathRepairDirectoryJournalWriterResult replacementWrite =
            DataRelativePathRepairDirectoryJournalWriter.CreateInitial(
                fixture.JournalDirectory,
                outerEntry.JournalChildName,
                replacementIntent.Record!
            );

        Assert.True(
            replacementWrite.Success,
            replacementWrite.Error
        );

        DataRelativePathRepairDirectoryJournalReaderResult outerRead =
            DataRelativePathRepairDirectoryJournalReader.Read(
                fixture.JournalDirectory,
                outerEntry.JournalChildName
            );

        Assert.True(
            outerRead.Success,
            outerRead.Error
        );

        DataRelativePathRepairDirectoryRecoveryClassification
            outerClassification =
                DataRelativePathRepairDirectoryRecoveryClassifier.Classify(
                    outerRead.Record!,
                    fixture.DataRoot
                );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .IntentFinalConflict,
            outerClassification.State
        );

        DataRelativePathRepairPlanRollbackExecution rollback =
            DataRelativePathRepairPlanRollbackExecutor.Execute(
                fixture.JournalDirectory,
                Fixture.ManifestName,
                fixture.DataRoot,
                T0.AddSeconds(20)
            );

        Assert.False(
            rollback.Success
        );

        Assert.Equal(
            DataRelativePathRepairPlanRollbackExecutionState
                .PreflightFailed,
            rollback.State
        );

        DataRelativePathRepairPlanRollbackOperationExecution failed =
            Assert.Single(
                rollback.OperationResults
            );

        Assert.Equal(
            0,
            failed.Index
        );

        Assert.Equal(
            DataRelativePathRepairPlanRollbackOperationExecutionState
                .DirectoryRecoveryStateNotRollbackSafe,
            failed.State
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .IntentFinalConflict,
            failed.DirectoryClassification!.State
        );

        /*
         * Preflight discovered the lower unsafe operation before the
         * reverse execution phase reached the higher file or directory.
         */
        Assert.True(
            File.Exists(
                fixture.DestinationPath
            )
        );

        Assert.True(
            Directory.Exists(
                fixture.RequestedParentPath
            )
        );

        Assert.True(
            Directory.Exists(
                fixture.RequestedTopDirectoryPath
            )
        );

        DataRelativePathRepairDirectoryJournalReaderResult innerAfter =
            DataRelativePathRepairDirectoryJournalReader.Read(
                fixture.JournalDirectory,
                manifest.Operations[1].JournalChildName
            );

        DataRelativePathRepairFileJournalReaderResult fileAfter =
            DataRelativePathRepairFileJournalReader.Read(
                fixture.JournalDirectory,
                manifest.Operations[2].JournalChildName
            );

        Assert.Equal(
            innerBefore.Record,
            innerAfter.Record
        );

        Assert.Equal(
            fileBefore.Record,
            fileAfter.Record
        );
    }

    [Fact]
    public void
        Rollback_RolledBackAncestorReappears_IsRejected()
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

        DataRelativePathRepairPlanForwardExecution forward =
            DataRelativePathRepairPlanForwardExecutor.Execute(
                fixture.JournalDirectory,
                Fixture.ManifestName,
                fixture.DataRoot,
                T0.AddSeconds(10)
            );

        if (NoReplaceUnsupported(forward))
        {
            return;
        }

        Assert.True(
            forward.Success,
            forward.Error
        );

        DataRelativePathRepairPlanRollbackExecution firstRollback =
            DataRelativePathRepairPlanRollbackExecutor.Execute(
                fixture.JournalDirectory,
                Fixture.ManifestName,
                fixture.DataRoot,
                T0.AddSeconds(20)
            );

        Assert.True(
            firstRollback.Success,
            firstRollback.Error
        );

        JournalCheckpoint[] beforeSecondRun =
            fixture.CaptureJournalCheckpoints(
                manifest
            );

        /*
         * Recreate the outer requested namespace entry with a new
         * incarnation after durable rollback. A second plan rollback
         * must not treat the old RolledBack journals alone as proof
         * that the subtree is still absent.
         */
        Directory.CreateDirectory(
            fixture.RequestedTopDirectoryPath
        );

        Assert.True(
            Directory.Exists(
                fixture.RequestedTopDirectoryPath
            )
        );

        DataRelativePathRepairPlanRollbackExecution second =
            DataRelativePathRepairPlanRollbackExecutor.Execute(
                fixture.JournalDirectory,
                Fixture.ManifestName,
                fixture.DataRoot,
                T0.AddSeconds(30)
            );

        Assert.False(
            second.Success
        );

        Assert.Equal(
            DataRelativePathRepairPlanRollbackExecutionState
                .PreflightFailed,
            second.State
        );

        DataRelativePathRepairPlanRollbackOperationExecution failed =
            Assert.Single(
                second.OperationResults
            );

        Assert.Equal(
            0,
            failed.Index
        );

        Assert.Equal(
            DataRelativePathRepairPlanRollbackOperationExecutionState
                .DirectoryRecoveryStateNotRollbackSafe,
            failed.State
        );

        Assert.NotNull(
            failed.DirectoryClassification
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .RolledBackConflict,
            failed.DirectoryClassification!.State
        );

        Assert.True(
            Directory.Exists(
                fixture.RequestedTopDirectoryPath
            )
        );

        JournalCheckpoint[] afterSecondRun =
            fixture.CaptureJournalCheckpoints(
                manifest
            );

        Assert.Equal(
            beforeSecondRun,
            afterSecondRun
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
