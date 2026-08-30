using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using System.Text.Json;
using Xunit;

namespace CaseCompat.Tests;

public sealed class DataRelativePathRepairFileJournalTests
{
    private static readonly DateTimeOffset T0 =
        new(
            2026,
            8,
            30,
            20,
            0,
            0,
            TimeSpan.Zero
        );

    [Fact]
    public void CreateIntent_ValidCreateFile_CreatesRevisionZero()
    {
        DataRelativePathRepairFileJournalTransitionResult result =
            CreateIntent();

        Assert.True(
            result.Success
        );

        DataRelativePathRepairFileJournalRecord record =
            Assert.IsType<
                DataRelativePathRepairFileJournalRecord
            >(
                result.Record
            );

        Assert.Equal(
            DataRelativePathRepairFileJournalState
                .IntentRecorded,
            record.State
        );

        Assert.Equal(
            0,
            record.Revision
        );

        Assert.Null(
            record.PreparedFileIdentity
        );

        Assert.False(
            record.IsTerminal
        );

        Assert.Null(
            DataRelativePathRepairFileJournal.Validate(
                record
            )
        );
    }

    [Fact]
    public void CreateIntent_CreateDirectoryOperation_IsRejected()
    {
        DataRelativePathRepairFileJournalTransitionResult result =
            DataRelativePathRepairFileJournal.CreateIntent(
                Guid.NewGuid(),
                T0,
                "/game/Data",
                new DataRelativePathRepairPlanOperation(
                    Kind:
                        DataRelativePathRepairPlanOperationKind
                            .CreateDirectory,
                    DestinationPath:
                        "/game/Data/Meshes",
                    SourcePath:
                        null
                ),
                SourceSnapshot(),
                ParentSnapshot()
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalTransitionState
                .InvalidRecord,
            result.State
        );
    }

    [Fact]
    public void CreateIntent_CasefoldParent_IsRejected()
    {
        DataRelativePathRepairDestinationParentSnapshot parent =
            ParentSnapshot() with
            {
                CasefoldEnabled =
                    true
            };

        DataRelativePathRepairFileJournalTransitionResult result =
            DataRelativePathRepairFileJournal.CreateIntent(
                Guid.NewGuid(),
                T0,
                "/game/Data",
                Operation(),
                SourceSnapshot(),
                parent
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalTransitionState
                .InvalidRecord,
            result.State
        );
    }

    [Fact]
    public void MarkPrepared_ZeroLinkOpenedIdentity_IsAccepted()
    {
        DataRelativePathRepairFileJournalRecord intent =
            RequireRecord(
                CreateIntent()
            );

        LinuxOpenedFileIdentityResult prepared =
            PreparedIdentity();

        DataRelativePathRepairFileJournalTransitionResult result =
            DataRelativePathRepairFileJournal.MarkPrepared(
                intent,
                prepared,
                T0.AddSeconds(1)
            );

        Assert.True(
            result.Success
        );

        DataRelativePathRepairFileJournalRecord record =
            RequireRecord(
                result
            );

        Assert.Equal(
            DataRelativePathRepairFileJournalState.Prepared,
            record.State
        );

        Assert.Equal(
            1,
            record.Revision
        );

        Assert.Same(
            prepared,
            record.PreparedFileIdentity
        );
    }

    [Fact]
    public void MarkPrepared_LinkedIdentity_IsRejected()
    {
        DataRelativePathRepairFileJournalRecord intent =
            RequireRecord(
                CreateIntent()
            );

        LinuxOpenedFileIdentityResult linked =
            PreparedIdentity() with
            {
                LinkCount =
                    1U
            };

        DataRelativePathRepairFileJournalTransitionResult result =
            DataRelativePathRepairFileJournal.MarkPrepared(
                intent,
                linked,
                T0.AddSeconds(1)
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalTransitionState
                .InvalidPreparedIdentity,
            result.State
        );
    }

    [Fact]
    public void Lifecycle_ForwardAndRollbackTransitionsIncrementRevision()
    {
        DataRelativePathRepairFileJournalRecord intent =
            RequireRecord(
                CreateIntent()
            );

        DataRelativePathRepairFileJournalRecord prepared =
            RequireRecord(
                DataRelativePathRepairFileJournal.MarkPrepared(
                    intent,
                    PreparedIdentity(),
                    T0.AddSeconds(1)
                )
            );

        DataRelativePathRepairFileJournalRecord applied =
            RequireRecord(
                DataRelativePathRepairFileJournal.MarkApplied(
                    prepared,
                    T0.AddSeconds(2)
                )
            );

        DataRelativePathRepairFileJournalRecord
            rollbackRequested =
                RequireRecord(
                    DataRelativePathRepairFileJournal
                        .RequestRollback(
                            applied,
                            T0.AddSeconds(3)
                        )
                );

        DataRelativePathRepairFileJournalRecord rolledBack =
            RequireRecord(
                DataRelativePathRepairFileJournal.MarkRolledBack(
                    rollbackRequested,
                    T0.AddSeconds(4)
                )
            );

        Assert.Equal(
            1,
            prepared.Revision
        );

        Assert.Equal(
            2,
            applied.Revision
        );

        Assert.Equal(
            3,
            rollbackRequested.Revision
        );

        Assert.Equal(
            4,
            rolledBack.Revision
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalState.RolledBack,
            rolledBack.State
        );

        Assert.True(
            rolledBack.IsTerminal
        );

        Assert.True(
            prepared.PreparedFileIdentity!
                .SameObjectAs(
                    rolledBack.PreparedFileIdentity!
                )
        );
    }

    [Fact]
    public void MarkApplied_DirectlyFromIntent_IsRejected()
    {
        DataRelativePathRepairFileJournalRecord intent =
            RequireRecord(
                CreateIntent()
            );

        DataRelativePathRepairFileJournalTransitionResult result =
            DataRelativePathRepairFileJournal.MarkApplied(
                intent,
                T0.AddSeconds(1)
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalTransitionState
                .InvalidTransition,
            result.State
        );
    }

    [Fact]
    public void RecoveryConflict_FromPrepared_RequiresReasonAndIsTerminal()
    {
        DataRelativePathRepairFileJournalRecord prepared =
            RequireRecord(
                DataRelativePathRepairFileJournal.MarkPrepared(
                    RequireRecord(
                        CreateIntent()
                    ),
                    PreparedIdentity(),
                    T0.AddSeconds(1)
                )
            );

        DataRelativePathRepairFileJournalTransitionResult emptyReason =
            DataRelativePathRepairFileJournal.MarkRecoveryConflict(
                prepared,
                "",
                T0.AddSeconds(2)
            );

        Assert.False(
            emptyReason.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalTransitionState
                .InvalidConflictReason,
            emptyReason.State
        );

        DataRelativePathRepairFileJournalRecord conflict =
            RequireRecord(
                DataRelativePathRepairFileJournal
                    .MarkRecoveryConflict(
                        prepared,
                        "Final path exists with a different inode.",
                        T0.AddSeconds(2)
                    )
            );

        Assert.Equal(
            DataRelativePathRepairFileJournalState
                .RecoveryConflict,
            conflict.State
        );

        Assert.True(
            conflict.IsTerminal
        );

        Assert.Equal(
            "Final path exists with a different inode.",
            conflict.RecoveryConflictReason
        );
    }

    [Fact]
    public void JournalRecord_SystemTextJsonRoundTrip_PreservesEvidence()
    {
        DataRelativePathRepairFileJournalRecord prepared =
            RequireRecord(
                DataRelativePathRepairFileJournal.MarkPrepared(
                    RequireRecord(
                        CreateIntent()
                    ),
                    PreparedIdentity(),
                    T0.AddSeconds(1)
                )
            );

        string json =
            JsonSerializer.Serialize(
                prepared
            );

        DataRelativePathRepairFileJournalRecord? roundTrip =
            JsonSerializer.Deserialize<
                DataRelativePathRepairFileJournalRecord
            >(
                json
            );

        DataRelativePathRepairFileJournalRecord restored =
            Assert.IsType<
                DataRelativePathRepairFileJournalRecord
            >(
                roundTrip
            );

        Assert.Equal(
            prepared,
            restored
        );

        Assert.Null(
            DataRelativePathRepairFileJournal.Validate(
                restored
            )
        );

        Assert.True(
            prepared.PreparedFileIdentity!
                .SameObjectAs(
                    restored.PreparedFileIdentity!
                )
        );
    }

    [Fact]
    public void CreateIntent_DestinationParentIsAncestor_IsRejected()
    {
        DataRelativePathRepairDestinationParentSnapshot parent =
            ParentSnapshot() with
            {
                PhysicalPath =
                    "/game/Data/Meshes"
            };

        DataRelativePathRepairFileJournalTransitionResult result =
            DataRelativePathRepairFileJournal.CreateIntent(
                Guid.NewGuid(),
                T0,
                "/game/Data",
                Operation(),
                SourceSnapshot(),
                parent
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalTransitionState
                .InvalidRecord,
            result.State
        );

        Assert.Contains(
            "direct physical parent",
            result.Error
        );
    }

    [Fact]
    public void CreateIntent_SourceOperationAndSnapshotDiffer_IsRejected()
    {
        DataRelativePathRepairPlanOperation operation =
            Operation() with
            {
                SourcePath =
                    "/game/Data/meshes/other/Armor.nif"
            };

        DataRelativePathRepairFileJournalTransitionResult result =
            DataRelativePathRepairFileJournal.CreateIntent(
                Guid.NewGuid(),
                T0,
                "/game/Data",
                operation,
                SourceSnapshot(),
                ParentSnapshot()
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalTransitionState
                .InvalidRecord,
            result.State
        );

        Assert.Contains(
            "source path must match",
            result.Error
        );
    }

    [Fact]
    public void CreateIntent_DestinationOutsideDataRoot_IsRejected()
    {
        DataRelativePathRepairPlanOperation operation =
            Operation() with
            {
                DestinationPath =
                    "/game/Outside/Armor.nif"
            };

        DataRelativePathRepairDestinationParentSnapshot parent =
            ParentSnapshot() with
            {
                PhysicalPath =
                    "/game/Outside"
            };

        DataRelativePathRepairFileJournalTransitionResult result =
            DataRelativePathRepairFileJournal.CreateIntent(
                Guid.NewGuid(),
                T0,
                "/game/Data",
                operation,
                SourceSnapshot(),
                parent
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalTransitionState
                .InvalidRecord,
            result.State
        );

        Assert.Contains(
            "destination file must be inside",
            result.Error
        );
    }

    [Fact]
    public void CreateIntent_SourceOutsideDataRoot_IsRejected()
    {
        DataRelativePathRepairSourceSnapshot source =
            SourceSnapshot() with
            {
                PhysicalPath =
                    "/game/Outside/Armor.nif"
            };

        DataRelativePathRepairPlanOperation operation =
            Operation() with
            {
                SourcePath =
                    "/game/Outside/Armor.nif"
            };

        DataRelativePathRepairFileJournalTransitionResult result =
            DataRelativePathRepairFileJournal.CreateIntent(
                Guid.NewGuid(),
                T0,
                "/game/Data",
                operation,
                source,
                ParentSnapshot()
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalTransitionState
                .InvalidRecord,
            result.State
        );

        Assert.Contains(
            "source snapshot must be inside",
            result.Error
        );
    }

    private static
        DataRelativePathRepairFileJournalTransitionResult
        CreateIntent()
    {
        return DataRelativePathRepairFileJournal.CreateIntent(
            Guid.Parse(
                "11111111-2222-3333-4444-555555555555"
            ),
            T0,
            "/game/Data",
            Operation(),
            SourceSnapshot(),
            ParentSnapshot()
        );
    }

    private static DataRelativePathRepairPlanOperation Operation()
    {
        return new DataRelativePathRepairPlanOperation(
            Kind:
                DataRelativePathRepairPlanOperationKind
                    .CreateFile,
            DestinationPath:
                "/game/Data/Meshes/Foo/Armor.nif",
            SourcePath:
                "/game/Data/meshes/foo/Armor.nif"
        );
    }

    private static DataRelativePathRepairSourceSnapshot
        SourceSnapshot()
    {
        return new DataRelativePathRepairSourceSnapshot(
            PhysicalPath:
                "/game/Data/meshes/foo/Armor.nif",
            Size:
                1234,
            Sha256:
                new string(
                    'A',
                    64
                ),
            Identity:
                new LinuxFileIdentityResult(
                    FullPath:
                        "/game/Data/meshes/foo/Armor.nif",
                    DeviceMajor:
                        8U,
                    DeviceMinor:
                        1U,
                    Inode:
                        100UL,
                    LinkCount:
                        1U,
                    MountId:
                        55UL,
                    Error:
                        null
                )
        );
    }

    private static
        DataRelativePathRepairDestinationParentSnapshot
        ParentSnapshot()
    {
        return new
            DataRelativePathRepairDestinationParentSnapshot(
                PhysicalPath:
                    "/game/Data/Meshes/Foo",
                Identity:
                    new LinuxFileIdentityResult(
                        FullPath:
                            "/game/Data/Meshes/Foo",
                        DeviceMajor:
                            8U,
                        DeviceMinor:
                            1U,
                        Inode:
                            200UL,
                        LinkCount:
                            2U,
                        MountId:
                            55UL,
                        Error:
                            null
                    ),
                CasefoldEnabled:
                    false,
                RawFlags:
                    0
            );
    }

    private static LinuxOpenedFileIdentityResult
        PreparedIdentity()
    {
        return new LinuxOpenedFileIdentityResult(
            State:
                LinuxOpenedFileIdentityState.Captured,
            DeviceMajor:
                8U,
            DeviceMinor:
                1U,
            Inode:
                300UL,
            LinkCount:
                0U,
            MountId:
                55UL,
            Errno:
                null,
            Error:
                null
        );
    }

    private static DataRelativePathRepairFileJournalRecord
        RequireRecord(
            DataRelativePathRepairFileJournalTransitionResult result)
    {
        Assert.True(
            result.Success,
            result.Error
        );

        return Assert.IsType<
            DataRelativePathRepairFileJournalRecord
        >(
            result.Record
        );
    }
}
