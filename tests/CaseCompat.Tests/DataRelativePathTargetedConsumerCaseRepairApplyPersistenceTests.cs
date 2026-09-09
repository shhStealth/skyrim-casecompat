using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathTargetedConsumerCaseRepairApplyPersistenceTests
{
    private static readonly DateTimeOffset T0 =
        new(
            2026,
            9,
            8,
            12,
            0,
            0,
            TimeSpan.Zero
        );

    // ---- Journal transitions (pure, no IO) ----

    [Fact]
    public void CreateIntent_ValidInputs_ProducesIntentRecorded()
    {
        DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
            result =
                CreateIntent();

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairApplyJournalState
                .IntentRecorded,
            result.Record!.State
        );

        Assert.Null(
            result.Record.PreparedFileIncarnationIdentity
        );

        Assert.Null(
            result.Record.AppliedFileIncarnationIdentity
        );
    }

    [Fact]
    public void MarkPrepared_FromIntentRecorded_ProducesPrepared()
    {
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord intent =
            RequireRecord(
                CreateIntent()
            );

        DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
            prepared =
                DataRelativePathTargetedConsumerCaseRepairApplyJournal
                    .MarkPrepared(
                        intent,
                        FakeIdentity(
                            inode:
                                100UL
                        ),
                        T0.AddSeconds(
                            1
                        )
                    );

        Assert.True(
            prepared.Success,
            prepared.Error
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairApplyJournalState
                .Prepared,
            prepared.Record!.State
        );

        Assert.NotNull(
            prepared.Record.PreparedFileIncarnationIdentity
        );
    }

    [Fact]
    public void MarkPrepared_FromNonIntentRecorded_IsRejected()
    {
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord
            prepared =
                RequirePrepared();

        DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
            secondPrepare =
                DataRelativePathTargetedConsumerCaseRepairApplyJournal
                    .MarkPrepared(
                        prepared,
                        FakeIdentity(
                            inode:
                                200UL
                        ),
                        T0.AddSeconds(
                            2
                        )
                    );

        Assert.False(
            secondPrepare.Success
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionState
                .InvalidTransition,
            secondPrepare.State
        );
    }

    [Fact]
    public void MarkApplied_FromPrepared_ProducesApplied()
    {
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord
            prepared =
                RequirePrepared();

        DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
            applied =
                DataRelativePathTargetedConsumerCaseRepairApplyJournal
                    .MarkApplied(
                        prepared,
                        FakeIdentity(
                            inode:
                                101UL
                        ),
                        T0.AddSeconds(
                            2
                        )
                    );

        Assert.True(
            applied.Success,
            applied.Error
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairApplyJournalState
                .Applied,
            applied.Record!.State
        );
    }

    [Fact]
    public void MarkRolledBack_FromApplied_ProducesRolledBack()
    {
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord applied =
            RequireApplied();

        DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
            rolledBack =
                DataRelativePathTargetedConsumerCaseRepairApplyJournal
                    .MarkRolledBack(
                        applied,
                        T0.AddSeconds(
                            3
                        )
                    );

        Assert.True(
            rolledBack.Success,
            rolledBack.Error
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairApplyJournalState
                .RolledBack,
            rolledBack.Record!.State
        );
    }

    [Fact]
    public void MarkRolledBack_FromNonApplied_IsRejected()
    {
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord intent =
            RequireRecord(
                CreateIntent()
            );

        DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
            rolledBack =
                DataRelativePathTargetedConsumerCaseRepairApplyJournal
                    .MarkRolledBack(
                        intent,
                        T0.AddSeconds(
                            1
                        )
                    );

        Assert.False(
            rolledBack.Success
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionState
                .InvalidTransition,
            rolledBack.State
        );
    }

    // ---- JSON round trip ----

    [Fact]
    public void Json_RoundTrip_PreservesRecord()
    {
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord applied =
            RequireApplied();

        DataRelativePathTargetedConsumerCaseRepairApplyJournalJsonSerializationResult
            encoded =
                DataRelativePathTargetedConsumerCaseRepairApplyJournalJson
                    .SerializeValidated(
                        applied
                    );

        Assert.True(
            encoded.Success,
            encoded.Error
        );

        DataRelativePathTargetedConsumerCaseRepairApplyJournalJsonDeserializationResult
            decoded =
                DataRelativePathTargetedConsumerCaseRepairApplyJournalJson
                    .DeserializeValidated(
                        encoded.Bytes!
                    );

        Assert.True(
            decoded.Success,
            decoded.Error
        );

        Assert.Equal(
            applied.PlanId,
            decoded.Record!.PlanId
        );

        Assert.Equal(
            applied.State,
            decoded.Record.State
        );

        Assert.Equal(
            applied.Operation,
            decoded.Record.Operation
        );
    }

    // ---- Writer / Reader ----

    [Fact]
    public void Writer_ThenReader_RoundTripsDurably()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        if (!fixture.SupportsUnnamedFiles())
        {
            return;
        }

        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord intent =
            RequireRecord(
                CreateIntent()
            );

        DataRelativePathTargetedConsumerCaseRepairApplyJournalWriterResult
            write =
                DataRelativePathTargetedConsumerCaseRepairApplyJournalWriter
                    .CreateInitial(
                        fixture.JournalDirectory,
                        "phase.json",
                        intent
                    );

        Assert.True(
            write.Success,
            write.Error
        );

        DataRelativePathTargetedConsumerCaseRepairApplyJournalReaderResult
            read =
                DataRelativePathTargetedConsumerCaseRepairApplyJournalReader
                    .Read(
                        fixture.JournalDirectory,
                        "phase.json"
                    );

        Assert.True(
            read.Success,
            read.Error
        );

        Assert.Equal(
            intent.PlanId,
            read.Phase!.PlanId
        );
    }

    [Fact]
    public void Writer_ExistingPhase_IsNotOverwritten()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        if (!fixture.SupportsUnnamedFiles())
        {
            return;
        }

        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord first =
            RequireRecord(
                CreateIntent()
            );

        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord second =
            RequireRecord(
                CreateIntent(
                    planId:
                        Guid.NewGuid()
                )
            );

        DataRelativePathTargetedConsumerCaseRepairApplyJournalWriterResult
            firstWrite =
                DataRelativePathTargetedConsumerCaseRepairApplyJournalWriter
                    .CreateInitial(
                        fixture.JournalDirectory,
                        "phase.json",
                        first
                    );

        Assert.True(
            firstWrite.Success,
            firstWrite.Error
        );

        DataRelativePathTargetedConsumerCaseRepairApplyJournalWriterResult
            duplicate =
                DataRelativePathTargetedConsumerCaseRepairApplyJournalWriter
                    .CreateInitial(
                        fixture.JournalDirectory,
                        "phase.json",
                        second
                    );

        Assert.False(
            duplicate.Success
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairApplyJournalWriteState
                .JournalPhaseAlreadyExists,
            duplicate.State
        );
    }

    // ---- Executor ----

    [Fact]
    public void Execute_ValidPlan_CreatesDestinationAndAllThreeJournalPhases()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        if (!fixture.SupportsUnnamedFiles())
        {
            return;
        }

        byte[] sourceBytesBeforeApply =
            File.ReadAllBytes(
                fixture.SourcePath
            );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            fixture.CreatePlan();

        DataRelativePathTargetedConsumerCaseRepairApplyExecution execution =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                fixture.DataRoot,
                fixture.JournalDirectory,
                plan,
                T0
            );

        Assert.True(
            execution.Success,
            execution.Error
        );

        Assert.True(
            File.Exists(
                fixture.DestinationPath
            )
        );

        // Apply publishes by renaming the source's own inode, not by
        // copying its bytes - the original mismatched-case name must no
        // longer exist afterward. Two files resolving the same asset by
        // different case is exactly the directory shape this design
        // exists to eliminate.
        Assert.False(
            File.Exists(
                fixture.SourcePath
            )
        );

        Assert.Equal(
            sourceBytesBeforeApply,
            File.ReadAllBytes(
                fixture.DestinationPath
            )
        );

        Assert.NotNull(
            execution.IntentJournalChildName
        );

        Assert.NotNull(
            execution.PreparedJournalChildName
        );

        Assert.NotNull(
            execution.AppliedJournalChildName
        );

        Assert.True(
            File.Exists(
                Path.Combine(
                    fixture.JournalDirectoryPath,
                    execution.AppliedJournalChildName!
                )
            )
        );
    }

    [Fact]
    public void Execute_MultiOperationPlan_CreatesDirectoryThenFile()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        if (!fixture.SupportsUnnamedFiles())
        {
            return;
        }

        byte[] nestedSourceBytesBeforeApply =
            File.ReadAllBytes(
                fixture.NestedSourcePath
            );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            fixture.CreateMultiOperationPlan();

        DataRelativePathTargetedConsumerCaseRepairApplyExecution execution =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                fixture.DataRoot,
                fixture.JournalDirectory,
                plan,
                T0
            );

        Assert.True(
            execution.Success,
            execution.Error
        );

        Assert.Single(
            execution.CreatedDirectoryPaths
        );

        Assert.True(
            Directory.Exists(
                fixture.NestedDestinationParentPath
            )
        );

        Assert.True(
            File.Exists(
                fixture.NestedDestinationPath
            )
        );

        // The source lived under a differently-cased parent directory
        // ("inner" vs "Inner"), so this exercises the cross-directory
        // rename path specifically - both the old file name and its old
        // parent's entry for it must be gone.
        Assert.False(
            File.Exists(
                fixture.NestedSourcePath
            )
        );

        Assert.Equal(
            nestedSourceBytesBeforeApply,
            File.ReadAllBytes(
                fixture.NestedDestinationPath
            )
        );

        Assert.NotNull(
            execution.AppliedJournalChildName
        );
    }

    [Fact]
    public void
        Execute_RealPipeline_ExistingDirectoryWithSiblingsMovesTogether()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        // A real end-to-end run through Candidate -> Project ->
        // DurablePlan.Create -> Execute, exercising directory-rename
        // discovery against a physically-existing, differently-cased
        // directory that also contains a sibling file this candidate
        // never touches. This is the actual regression this design
        // exists to prevent: an untouched sibling being left behind in
        // an abandoned old-cased directory while only the fixed file
        // moves to a new, otherwise-empty correctly-cased one.
        string dataRoot =
            RealPipelineDataRoot();

        string oldCasedDirectory =
            Path.Combine(
                dataRoot,
                "meshes",
                "018auri"
            );

        Directory.CreateDirectory(
            oldCasedDirectory
        );

        File.WriteAllText(
            Path.Combine(
                oldCasedDirectory,
                "femalehead.tri"
            ),
            "the fixed file"
        );

        File.WriteAllText(
            Path.Combine(
                oldCasedDirectory,
                "sibling.tri"
            ),
            "an untouched sibling"
        );

        string requestedPath =
            "Meshes/018Auri/FemaleHead.tri";

        DataRelativePathTargetedConsumerCaseRepairCandidate candidate =
            RealPipelineCandidate(
                dataRoot,
                requestedPath
            );

        using LinuxNoFollowPathHandle root =
            RealPipelineOpenRoot(
                dataRoot
            );

        DataRelativePathTargetedConsumerCaseRepairPlanProjection
            projection =
                DataRelativePathTargetedConsumerCaseRepairPlanProjector
                    .Project(
                        root,
                        candidate
                    );

        Assert.True(
            projection.HasPlan,
            projection.Error
        );

        // Both "meshes" (top level) and "meshes/018auri" physically
        // exist under the wrong case - both must be discovered as
        // rename sources, not just the innermost one.
        Assert.Equal(
            2,
            projection.DirectoryRenameSources.Count
        );

        Assert.Contains(
            projection.DirectoryRenameSources,
            source =>
                source.PhysicalPath ==
                Path.Combine(
                    dataRoot,
                    "meshes"
                )
        );

        Assert.Contains(
            projection.DirectoryRenameSources,
            source =>
                source.PhysicalPath ==
                oldCasedDirectory
        );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanCreation
            creation =
                DataRelativePathTargetedConsumerCaseRepairDurablePlan.Create(
                    root,
                    Guid.NewGuid(),
                    T0,
                    projection
                );

        Assert.True(
            creation.Success,
            creation.Error
        );

        string journalDirectoryPath =
            Path.Combine(
                Path.GetDirectoryName(
                    dataRoot
                )!,
                "Journal"
            );

        Directory.CreateDirectory(
            journalDirectoryPath
        );

        using LinuxNoFollowPathHandle journalDirectory =
            RealPipelineOpenRoot(
                journalDirectoryPath
            );

        if (!SupportsUnnamedFilesAt(
                journalDirectory))
        {
            return;
        }

        DataRelativePathTargetedConsumerCaseRepairApplyExecution execution =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                root,
                journalDirectory,
                creation.Record!,
                T0
            );

        Assert.True(
            execution.Success,
            $"State={execution.State} Error={execution.Error}"
        );

        string newCasedDirectory =
            Path.Combine(
                dataRoot,
                "Meshes",
                "018Auri"
            );

        // The whole directory moved: the fixed file under its new name,
        // and the untouched sibling right along with it, both content
        // preserved exactly.
        Assert.Equal(
            "the fixed file",
            File.ReadAllText(
                Path.Combine(
                    newCasedDirectory,
                    "FemaleHead.tri"
                )
            )
        );

        Assert.Equal(
            "an untouched sibling",
            File.ReadAllText(
                Path.Combine(
                    newCasedDirectory,
                    "sibling.tri"
                )
            )
        );

        // Nothing left behind under the old name at any level.
        Assert.False(
            Directory.Exists(
                oldCasedDirectory
            )
        );

        Assert.False(
            Directory.Exists(
                Path.Combine(
                    dataRoot,
                    "meshes"
                )
            )
        );
    }

    [Fact]
    public void Execute_RequiredDirectoryAlreadyExists_RefusesWithoutCreatingFile()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        if (!fixture.SupportsUnnamedFiles())
        {
            return;
        }

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            fixture.CreateMultiOperationPlan();

        Directory.CreateDirectory(
            fixture.NestedDestinationParentPath
        );

        DataRelativePathTargetedConsumerCaseRepairApplyExecution execution =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                fixture.DataRoot,
                fixture.JournalDirectory,
                plan,
                T0
            );

        Assert.False(
            execution.Success
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                .DirectoryAlreadyExists,
            execution.State
        );

        Assert.False(
            File.Exists(
                fixture.NestedDestinationPath
            )
        );

        // Refusing before the rename must leave the source exactly
        // where it was.
        Assert.True(
            File.Exists(
                fixture.NestedSourcePath
            )
        );

        Assert.Empty(
            Directory.GetFiles(
                fixture.JournalDirectoryPath
            )
        );
    }

    [Fact]
    public void Execute_DestinationAlreadyExists_RefusesWithoutWritingIntentJournal()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        if (!fixture.SupportsUnnamedFiles())
        {
            return;
        }

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            fixture.CreatePlan();

        File.WriteAllText(
            fixture.DestinationPath,
            "already here"
        );

        DataRelativePathTargetedConsumerCaseRepairApplyExecution execution =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                fixture.DataRoot,
                fixture.JournalDirectory,
                plan,
                T0
            );

        Assert.False(
            execution.Success
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                .DestinationExists,
            execution.State
        );

        Assert.Null(
            execution.IntentJournalChildName
        );

        // Refusing before the rename must leave the source exactly
        // where it was.
        Assert.True(
            File.Exists(
                fixture.SourcePath
            )
        );

        Assert.Empty(
            Directory.GetFiles(
                fixture.JournalDirectoryPath
            )
        );
    }

    [Fact]
    public void Execute_SourceIdentityChangedSinceThePlanWasProjected_Refuses()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        if (!fixture.SupportsUnnamedFiles())
        {
            return;
        }

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            fixture.CreatePlan();

        byte[] sourceBytes =
            File.ReadAllBytes(
                fixture.SourcePath
            );

        File.Delete(
            fixture.SourcePath
        );

        File.WriteAllBytes(
            fixture.SourcePath,
            sourceBytes
        );

        DataRelativePathTargetedConsumerCaseRepairApplyExecution execution =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                fixture.DataRoot,
                fixture.JournalDirectory,
                plan,
                T0
            );

        Assert.False(
            execution.Success
        );

        /*
         * Deleting and recreating the source file always changes its
         * inode generation. Whether the device/inode number itself also
         * changes depends on the underlying filesystem's reuse timing,
         * so either freshness check firing is correct evidence that the
         * executor caught the tampered source.
         */
        Assert.True(
            execution.State is
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .SourceIdentityMismatch or
                DataRelativePathTargetedConsumerCaseRepairApplyExecutionState
                    .SourceGenerationMismatch,
            $"Unexpected execution state: {execution.State}"
        );

        Assert.False(
            File.Exists(
                fixture.DestinationPath
            )
        );
    }

    // ---- Rollback ----

    [Fact]
    public void Rollback_AfterSuccessfulApply_RemovesDestinationAndWritesRolledBackJournal()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        if (!fixture.SupportsUnnamedFiles())
        {
            return;
        }

        byte[] sourceBytesBeforeApply =
            File.ReadAllBytes(
                fixture.SourcePath
            );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            fixture.CreatePlan();

        DataRelativePathTargetedConsumerCaseRepairApplyExecution execution =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                fixture.DataRoot,
                fixture.JournalDirectory,
                plan,
                T0
            );

        Assert.True(
            execution.Success,
            execution.Error
        );

        DataRelativePathTargetedConsumerCaseRepairApplyRollbackResult
            rollback =
                DataRelativePathTargetedConsumerCaseRepairApplyRollback
                    .Rollback(
                        fixture.JournalDirectory,
                        plan.PlanId,
                        T0.AddMinutes(
                            1
                        )
                    );

        Assert.True(
            rollback.Success,
            rollback.Error
        );

        Assert.False(
            File.Exists(
                fixture.DestinationPath
            )
        );

        // Rollback is the same rename in reverse, not a delete: the
        // original mismatched-case name must be restored with its
        // exact original content.
        Assert.True(
            File.Exists(
                fixture.SourcePath
            )
        );

        Assert.Equal(
            sourceBytesBeforeApply,
            File.ReadAllBytes(
                fixture.SourcePath
            )
        );

        Assert.True(
            File.Exists(
                Path.Combine(
                    fixture.JournalDirectoryPath,
                    rollback.RolledBackJournalChildName!
                )
            )
        );
    }

    [Fact]
    public void Rollback_OriginalLocationOccupied_RefusesWithoutRenamingEitherFile()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        if (!fixture.SupportsUnnamedFiles())
        {
            return;
        }

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            fixture.CreatePlan();

        DataRelativePathTargetedConsumerCaseRepairApplyExecution execution =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                fixture.DataRoot,
                fixture.JournalDirectory,
                plan,
                T0
            );

        Assert.True(
            execution.Success,
            execution.Error
        );

        // Simulate something re-populating the original mismatched-case
        // name after apply - e.g. a mod update re-deploying its loose
        // file.
        File.WriteAllText(
            fixture.SourcePath,
            "reappeared after apply"
        );

        DataRelativePathTargetedConsumerCaseRepairApplyRollbackResult
            rollback =
                DataRelativePathTargetedConsumerCaseRepairApplyRollback
                    .Rollback(
                        fixture.JournalDirectory,
                        plan.PlanId,
                        T0.AddMinutes(
                            1
                        )
                    );

        Assert.False(
            rollback.Success
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                .OriginalLocationOccupied,
            rollback.State
        );

        // Neither file should have moved: the reappeared original stays
        // put, and the correctly-cased destination stays put too.
        Assert.Equal(
            "reappeared after apply",
            File.ReadAllText(
                fixture.SourcePath
            )
        );

        Assert.True(
            File.Exists(
                fixture.DestinationPath
            )
        );
    }

    [Fact]
    public void Rollback_DestinationChangedSinceApply_RefusesToRemove()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        if (!fixture.SupportsUnnamedFiles())
        {
            return;
        }

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            fixture.CreatePlan();

        DataRelativePathTargetedConsumerCaseRepairApplyExecution execution =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                fixture.DataRoot,
                fixture.JournalDirectory,
                plan,
                T0
            );

        Assert.True(
            execution.Success,
            execution.Error
        );

        byte[] destinationBytes =
            File.ReadAllBytes(
                fixture.DestinationPath
            );

        File.Delete(
            fixture.DestinationPath
        );

        File.WriteAllBytes(
            fixture.DestinationPath,
            destinationBytes
        );

        DataRelativePathTargetedConsumerCaseRepairApplyRollbackResult
            rollback =
                DataRelativePathTargetedConsumerCaseRepairApplyRollback
                    .Rollback(
                        fixture.JournalDirectory,
                        plan.PlanId,
                        T0.AddMinutes(
                            1
                        )
                    );

        Assert.False(
            rollback.Success
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                .DestinationIdentityMismatch,
            rollback.State
        );

        Assert.True(
            File.Exists(
                fixture.DestinationPath
            )
        );

        // Refusing before the rename-back must leave the original name
        // vacant, exactly as the successful apply left it.
        Assert.False(
            File.Exists(
                fixture.SourcePath
            )
        );
    }

    [Fact]
    public void Rollback_AlreadyRolledBack_IsRefusedIdempotently()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        if (!fixture.SupportsUnnamedFiles())
        {
            return;
        }

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan =
            fixture.CreatePlan();

        DataRelativePathTargetedConsumerCaseRepairApplyExecution execution =
            DataRelativePathTargetedConsumerCaseRepairApplyExecutor.Execute(
                fixture.DataRoot,
                fixture.JournalDirectory,
                plan,
                T0
            );

        Assert.True(
            execution.Success,
            execution.Error
        );

        DataRelativePathTargetedConsumerCaseRepairApplyRollbackResult
            firstRollback =
                DataRelativePathTargetedConsumerCaseRepairApplyRollback
                    .Rollback(
                        fixture.JournalDirectory,
                        plan.PlanId,
                        T0.AddMinutes(
                            1
                        )
                    );

        Assert.True(
            firstRollback.Success,
            firstRollback.Error
        );

        DataRelativePathTargetedConsumerCaseRepairApplyRollbackResult
            secondRollback =
                DataRelativePathTargetedConsumerCaseRepairApplyRollback
                    .Rollback(
                        fixture.JournalDirectory,
                        plan.PlanId,
                        T0.AddMinutes(
                            2
                        )
                    );

        Assert.False(
            secondRollback.Success
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairApplyRollbackState
                .AlreadyRolledBack,
            secondRollback.State
        );
    }

    private static
        DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
        CreateIntent(
            Guid? planId = null)
    {
        const string dataRoot =
            "/game/Data";

        const string source =
            "/game/Data/sub/target.dat";

        var sourceSnapshot =
            new DataRelativePathRepairSourceSnapshot(
                PhysicalPath:
                    source,
                Size:
                    6,
                Sha256:
                    new string(
                        'A',
                        64
                    ),
                Identity:
                    new LinuxFileIdentityResult(
                        FullPath:
                            source,
                        DeviceMajor:
                            8U,
                        DeviceMinor:
                            1U,
                        Inode:
                            100UL,
                        LinkCount:
                            1U,
                        MountId:
                            44UL,
                        Error:
                            null
                    )
            );

        var operation =
            new DataRelativePathRepairPlanOperation(
                Kind:
                    DataRelativePathRepairPlanOperationKind.CreateFile,
                DestinationPath:
                    "/game/Data/sub/Target.DAT",
                SourcePath:
                    source
            );

        return
            DataRelativePathTargetedConsumerCaseRepairApplyJournal
                .CreateIntent(
                    planId ??
                    Guid.Parse(
                        "e2f6c1f2-2222-4444-8888-abcdefabcdef"
                    ),
                    T0,
                    dataRoot,
                    operation,
                    sourceSnapshot
                );
    }

    private static LinuxFileIncarnationIdentity FakeIdentity(
        ulong inode)
    {
        return new(
            PhysicalIdentity:
                new LinuxOpenedFileIdentityResult(
                    State:
                        LinuxOpenedFileIdentityState.Captured,
                    DeviceMajor:
                        8U,
                    DeviceMinor:
                        1U,
                    Inode:
                        inode,
                    LinkCount:
                        1U,
                    MountId:
                        44UL,
                    Errno:
                        null,
                    Error:
                        null
                ),
            InodeGeneration:
                123U
        );
    }

    private static
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord
        RequirePrepared()
    {
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord intent =
            RequireRecord(
                CreateIntent()
            );

        return RequireRecord(
            DataRelativePathTargetedConsumerCaseRepairApplyJournal
                .MarkPrepared(
                    intent,
                    FakeIdentity(
                        inode:
                            100UL
                    ),
                    T0.AddSeconds(
                        1
                    )
                )
        );
    }

    private static
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord
        RequireApplied()
    {
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord
            prepared =
                RequirePrepared();

        return RequireRecord(
            DataRelativePathTargetedConsumerCaseRepairApplyJournal
                .MarkApplied(
                    prepared,
                    FakeIdentity(
                        inode:
                            101UL
                    ),
                    T0.AddSeconds(
                        2
                    )
                )
        );
    }

    private static
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord
        RequireRecord(
            DataRelativePathTargetedConsumerCaseRepairApplyJournalTransitionResult
                result)
    {
        Assert.True(
            result.Success,
            result.Error
        );

        return result.Record!;
    }

    // ---- Real-pipeline helpers (Candidate -> Project), independent of
    // the hand-built Fixture plans above ----

    private static string RealPipelineDataRoot()
    {
        string path =
            Path.Combine(
                Path.GetTempPath(),
                "casecompat-targeted-apply-real-pipeline-tests",
                Guid.NewGuid().ToString("N"),
                "Data"
            );

        Directory.CreateDirectory(
            path
        );

        return Path.GetFullPath(
            path
        );
    }

    private static
        DataRelativePathTargetedConsumerCaseRepairCandidate
        RealPipelineCandidate(
            string dataRoot,
            string requestedPath)
    {
        using LinuxNoFollowPathHandle root =
            RealPipelineOpenRoot(
                dataRoot
            );

        string rootLogical =
            requestedPath
                .Split('/')[0]
                .ToUpperInvariant();

        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis
            current =
                DataRelativePathRepairAggregateNamespaceCurrentLeafAnalyzer
                    .Analyze(
                        root,
                        rootLogical,
                        requestedPath
                    );

        DataRelativePathAggregateConsumerSpellingEvidence consumer =
            DataRelativePathAggregateConsumerSpellingClassifier.Classify(
                requestedPath.ToUpperInvariant(),
                new[]
                {
                    requestedPath
                }
            );

        DataRelativePathTargetedConsumerCaseRepairCandidateProjection
            projection =
                DataRelativePathTargetedConsumerCaseRepairCandidateProjector
                    .Project(
                        dataRoot,
                        consumer,
                        current
                    );

        Assert.True(
            projection.HasCandidate,
            projection.Error
        );

        return Assert.IsType<
            DataRelativePathTargetedConsumerCaseRepairCandidate
        >(
            projection.Candidate
        );
    }

    private static LinuxNoFollowPathHandle RealPipelineOpenRoot(
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

    private static bool SupportsUnnamedFilesAt(
        LinuxNoFollowPathHandle directory)
    {
        LinuxCreateUnnamedFileAtResult probe =
            LinuxCreateUnnamedFileAt.Create(
                directory
            );

        if (
            probe.State ==
            LinuxCreateUnnamedFileAtState.TmpfileUnsupported)
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

    private sealed class Fixture
        : IDisposable
    {
        public Fixture()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-targeted-apply-tests",
                    Guid.NewGuid().ToString("N")
                );

            DataRootPath =
                Path.Combine(
                    RootPath,
                    "Data"
                );

            SubDirectoryPath =
                Path.Combine(
                    DataRootPath,
                    "sub"
                );

            JournalDirectoryPath =
                Path.Combine(
                    RootPath,
                    "Journal"
                );

            Directory.CreateDirectory(
                SubDirectoryPath
            );

            Directory.CreateDirectory(
                JournalDirectoryPath
            );

            SourcePath =
                Path.Combine(
                    SubDirectoryPath,
                    "target.dat"
                );

            DestinationPath =
                Path.Combine(
                    SubDirectoryPath,
                    "Target.DAT"
                );

            File.WriteAllBytes(
                SourcePath,
                Encoding.ASCII.GetBytes(
                    "hello!"
                )
            );

            NestedSourceParentPath =
                Path.Combine(
                    SubDirectoryPath,
                    "inner"
                );

            Directory.CreateDirectory(
                NestedSourceParentPath
            );

            NestedSourcePath =
                Path.Combine(
                    NestedSourceParentPath,
                    "target2.dat"
                );

            NestedDestinationParentPath =
                Path.Combine(
                    SubDirectoryPath,
                    "Inner"
                );

            NestedDestinationPath =
                Path.Combine(
                    NestedDestinationParentPath,
                    "Target2.DAT"
                );

            File.WriteAllBytes(
                NestedSourcePath,
                Encoding.ASCII.GetBytes(
                    "nested!"
                )
            );

            DataRoot =
                OpenRoot(
                    DataRootPath
                );

            JournalDirectory =
                OpenRoot(
                    JournalDirectoryPath
                );
        }

        public string RootPath { get; }

        public string DataRootPath { get; }

        public string SubDirectoryPath { get; }

        public string JournalDirectoryPath { get; }

        public string SourcePath { get; }

        public string DestinationPath { get; }

        public string NestedSourceParentPath { get; }

        public string NestedSourcePath { get; }

        public string NestedDestinationParentPath { get; }

        public string NestedDestinationPath { get; }

        public LinuxNoFollowPathHandle DataRoot { get; }

        public LinuxNoFollowPathHandle JournalDirectory { get; }

        public bool SupportsUnnamedFiles()
        {
            LinuxCreateUnnamedFileAtResult probe =
                LinuxCreateUnnamedFileAt.Create(
                    JournalDirectory
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

        public DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
            CreatePlan()
        {
            byte[] sourceBytes =
                File.ReadAllBytes(
                    SourcePath
                );

            string sourceSha256 =
                Convert.ToHexString(
                    SHA256.HashData(
                        sourceBytes
                    )
                );

            LinuxFileIdentityResult sourceIdentity =
                LinuxFileIdentity.Inspect(
                    SourcePath
                );

            Assert.True(
                sourceIdentity.Success,
                sourceIdentity.Error
            );

            LinuxFileIdentityResult subDirectoryIdentity =
                LinuxFileIdentity.Inspect(
                    SubDirectoryPath
                );

            Assert.True(
                subDirectoryIdentity.Success,
                subDirectoryIdentity.Error
            );

            uint sourceInodeGeneration =
                CaptureInodeGeneration(
                    SubDirectoryPath,
                    "target.dat"
                );

            var sourceSnapshot =
                new DataRelativePathRepairSourceSnapshot(
                    PhysicalPath:
                        SourcePath,
                    Size:
                        sourceBytes.Length,
                    Sha256:
                        sourceSha256,
                    Identity:
                        sourceIdentity
                );

            var parentSnapshot =
                new DataRelativePathRepairDestinationParentSnapshot(
                    PhysicalPath:
                        SubDirectoryPath,
                    Identity:
                        subDirectoryIdentity,
                    CasefoldEnabled:
                        false,
                    RawFlags:
                        0
                );

            DataRelativePathRepairPlanOperation[] operations =
            [
                new(
                    Kind:
                        DataRelativePathRepairPlanOperationKind.CreateFile,
                    DestinationPath:
                        DestinationPath,
                    SourcePath:
                        SourcePath
                )
            ];

            var record =
                new DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord(
                    SchemaVersion:
                        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
                            .SchemaVersion1,
                    PlanId:
                        Guid.NewGuid(),
                    CreatedUtc:
                        T0,
                    DataRoot:
                        DataRootPath,
                    RequestedPath:
                        "sub/Target.DAT",
                    SourceSnapshot:
                        sourceSnapshot,
                    SourceInodeGeneration:
                        sourceInodeGeneration,
                    InitialDestinationParentSnapshot:
                        parentSnapshot,
                    Operations:
                        operations,
                    DirectoryRenameSources:
                        Array.Empty<
                            DataRelativePathRepairDirectoryRenameSource
                        >()
                );

            string? validationError =
                DataRelativePathTargetedConsumerCaseRepairDurablePlan
                    .Validate(
                        record
                    );

            Assert.Null(
                validationError
            );

            return record;
        }

        public DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
            CreateMultiOperationPlan()
        {
            byte[] sourceBytes =
                File.ReadAllBytes(
                    NestedSourcePath
                );

            string sourceSha256 =
                Convert.ToHexString(
                    SHA256.HashData(
                        sourceBytes
                    )
                );

            LinuxFileIdentityResult sourceIdentity =
                LinuxFileIdentity.Inspect(
                    NestedSourcePath
                );

            Assert.True(
                sourceIdentity.Success,
                sourceIdentity.Error
            );

            LinuxFileIdentityResult subDirectoryIdentity =
                LinuxFileIdentity.Inspect(
                    SubDirectoryPath
                );

            Assert.True(
                subDirectoryIdentity.Success,
                subDirectoryIdentity.Error
            );

            uint sourceInodeGeneration =
                CaptureInodeGeneration(
                    NestedSourceParentPath,
                    "target2.dat"
                );

            var sourceSnapshot =
                new DataRelativePathRepairSourceSnapshot(
                    PhysicalPath:
                        NestedSourcePath,
                    Size:
                        sourceBytes.Length,
                    Sha256:
                        sourceSha256,
                    Identity:
                        sourceIdentity
                );

            var parentSnapshot =
                new DataRelativePathRepairDestinationParentSnapshot(
                    PhysicalPath:
                        SubDirectoryPath,
                    Identity:
                        subDirectoryIdentity,
                    CasefoldEnabled:
                        false,
                    RawFlags:
                        0
                );

            DataRelativePathRepairPlanOperation[] operations =
            [
                new(
                    Kind:
                        DataRelativePathRepairPlanOperationKind
                            .CreateDirectory,
                    DestinationPath:
                        NestedDestinationParentPath,
                    SourcePath:
                        null
                ),
                new(
                    Kind:
                        DataRelativePathRepairPlanOperationKind.CreateFile,
                    DestinationPath:
                        NestedDestinationPath,
                    SourcePath:
                        NestedSourcePath
                )
            ];

            var record =
                new DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord(
                    SchemaVersion:
                        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
                            .SchemaVersion1,
                    PlanId:
                        Guid.NewGuid(),
                    CreatedUtc:
                        T0,
                    DataRoot:
                        DataRootPath,
                    RequestedPath:
                        "sub/Inner/Target2.DAT",
                    SourceSnapshot:
                        sourceSnapshot,
                    SourceInodeGeneration:
                        sourceInodeGeneration,
                    InitialDestinationParentSnapshot:
                        parentSnapshot,
                    Operations:
                        operations,
                    DirectoryRenameSources:
                        Array.Empty<
                            DataRelativePathRepairDirectoryRenameSource
                        >()
                );

            string? validationError =
                DataRelativePathTargetedConsumerCaseRepairDurablePlan
                    .Validate(
                        record
                    );

            Assert.Null(
                validationError
            );

            return record;
        }

        private static uint CaptureInodeGeneration(
            string parentPath,
            string childName)
        {
            LinuxNoFollowPathHandle parent =
                OpenRoot(
                    parentPath
                );

            try
            {
                LinuxOpenChildRegularFileReadOnlyAtResult opened =
                    LinuxOpenChildRegularFileReadOnlyAt.Open(
                        parent,
                        childName
                    );

                Assert.True(
                    opened.Success,
                    opened.Error
                );

                using LinuxOpenedChildHandle child =
                    opened.OpenedFile!;

                LinuxOpenedFileIncarnationResult incarnation =
                    LinuxOpenedFileIncarnation.Capture(
                        child
                    );

                Assert.True(
                    incarnation.Success,
                    incarnation.Error
                );

                return incarnation.Identity!.InodeGeneration;
            }
            finally
            {
                parent.Dispose();
            }
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
                LinuxNoFollowPathHandle
            >(
                opened.OpenedPath
            );
        }

        public void Dispose()
        {
            DataRoot.Dispose();

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
