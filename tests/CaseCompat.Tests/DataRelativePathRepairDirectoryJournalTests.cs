using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class DataRelativePathRepairDirectoryJournalTests
{
    private static readonly DateTimeOffset T0 =
        new(
            2026,
            8,
            31,
            0,
            0,
            0,
            TimeSpan.Zero
        );

    [Fact]
    public void CreateIntent_ValidCreateDirectory_CreatesRevisionZero()
    {
        DataRelativePathRepairDirectoryJournalTransitionResult result =
            CreateIntent();

        Assert.True(
            result.Success,
            result.Error
        );

        DataRelativePathRepairDirectoryJournalRecord record =
            RequireRecord(
                result
            );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState
                .IntentRecorded,
            record.State
        );

        Assert.Equal(
            0,
            record.Revision
        );

        Assert.Null(
            record.PreparedStagingChildName
        );

        Assert.Null(
            record.PreparedDirectoryIdentity
        );

        Assert.Null(
            DataRelativePathRepairDirectoryJournal.Validate(
                record
            )
        );

        Assert.False(
            record.IsTerminal
        );
    }

    [Fact]
    public void CreateIntent_CreateFileOperation_IsRejected()
    {
        DataRelativePathRepairPlanOperation operation =
            new(
                Kind:
                    DataRelativePathRepairPlanOperationKind
                        .CreateFile,
                DestinationPath:
                    "/game/Data/Meshes/Final",
                SourcePath:
                    "/game/Data/source.nif"
            );

        DataRelativePathRepairDirectoryJournalTransitionResult result =
            DataRelativePathRepairDirectoryJournal.CreateIntent(
                Guid.NewGuid(),
                T0,
                "/game/Data",
                operation,
                ParentSnapshot()
            ,
                SyntheticDirectoryJournalIncarnation.FromPhysical(
                    (ParentSnapshot()).Identity
                ));

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalTransitionState
                .InvalidRecord,
            result.State
        );
    }

    [Fact]
    public void CreateIntent_CreateDirectoryWithSourcePath_IsRejected()
    {
        DataRelativePathRepairPlanOperation operation =
            Operation() with
            {
                SourcePath =
                    "/game/Data/source.nif"
            };

        DataRelativePathRepairDirectoryJournalTransitionResult result =
            DataRelativePathRepairDirectoryJournal.CreateIntent(
                Guid.NewGuid(),
                T0,
                "/game/Data",
                operation,
                ParentSnapshot()
            ,
                SyntheticDirectoryJournalIncarnation.FromPhysical(
                    (ParentSnapshot()).Identity
                ));

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalTransitionState
                .InvalidRecord,
            result.State
        );
    }

    [Fact]
    public void CreateIntent_AncestorParentSnapshot_IsRejected()
    {
        DataRelativePathRepairDestinationParentSnapshot parent =
            ParentSnapshot() with
            {
                PhysicalPath =
                    "/game/Data"
            };

        DataRelativePathRepairDirectoryJournalTransitionResult result =
            DataRelativePathRepairDirectoryJournal.CreateIntent(
                Guid.NewGuid(),
                T0,
                "/game/Data",
                Operation(),
                parent
            ,
                SyntheticDirectoryJournalIncarnation.FromPhysical(
                    (parent).Identity
                ));

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalTransitionState
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

        DataRelativePathRepairDirectoryJournalTransitionResult result =
            DataRelativePathRepairDirectoryJournal.CreateIntent(
                Guid.NewGuid(),
                T0,
                "/game/Data",
                Operation(),
                parent
            ,
                SyntheticDirectoryJournalIncarnation.FromPhysical(
                    (parent).Identity
                ));

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalTransitionState
                .InvalidRecord,
            result.State
        );
    }

    [Fact]
    public void MarkPrepared_CompleteDirectoryIdentity_IsAccepted()
    {
        DataRelativePathRepairDirectoryJournalRecord intent =
            RequireRecord(
                CreateIntent()
            );

        LinuxFileIdentityResult identity =
            DirectoryIdentity();

        DataRelativePathRepairDirectoryJournalTransitionResult result =
            DataRelativePathRepairDirectoryJournal.MarkPrepared(
                intent,
                ".casecompat-stage-1",
                SyntheticDirectoryJournalIncarnation.FromPhysical(

                    identity

                ),
                T0.AddSeconds(1)
            );

        Assert.True(
            result.Success,
            result.Error
        );

        DataRelativePathRepairDirectoryJournalRecord prepared =
            RequireRecord(
                result
            );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.Prepared,
            prepared.State
        );

        Assert.Equal(
            1,
            prepared.Revision
        );

        Assert.Equal(
            ".casecompat-stage-1",
            prepared.PreparedStagingChildName
        );

        Assert.Same(
            identity,
            prepared.PreparedDirectoryIdentity
        );
    }

    [Fact]
    public void MarkPrepared_MissingMountId_IsRejected()
    {
        DataRelativePathRepairDirectoryJournalRecord intent =
            RequireRecord(
                CreateIntent()
            );

        LinuxFileIdentityResult incomplete =
            DirectoryIdentity() with
            {
                MountId =
                    null
            };

        DataRelativePathRepairDirectoryJournalTransitionResult result =
            DataRelativePathRepairDirectoryJournal.MarkPrepared(
                intent,
                ".casecompat-stage-1",
                SyntheticDirectoryJournalIncarnation.FromPhysical(

                    incomplete

                ),
                T0.AddSeconds(1)
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalTransitionState
                .InvalidPreparedIdentity,
            result.State
        );
    }

    [Fact]
    public void MarkPrepared_FinalDestinationNameAsStaging_IsRejected()
    {
        DataRelativePathRepairDirectoryJournalRecord intent =
            RequireRecord(
                CreateIntent()
            );

        DataRelativePathRepairDirectoryJournalTransitionResult result =
            DataRelativePathRepairDirectoryJournal.MarkPrepared(
                intent,
                "Final",
                SyntheticDirectoryJournalIncarnation.FromPhysical(

                    DirectoryIdentity()

                ),
                T0.AddSeconds(1)
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalTransitionState
                .InvalidStagingName,
            result.State
        );
    }

    [Fact]
    public void Reprepare_PreparedRecord_ReplacesStagingEvidence()
    {
        DataRelativePathRepairDirectoryJournalRecord prepared =
            RequireRecord(
                DataRelativePathRepairDirectoryJournal.MarkPrepared(
                    RequireRecord(
                        CreateIntent()
                    ),
                    ".casecompat-stage-1",
                    SyntheticDirectoryJournalIncarnation.FromPhysical(

                        DirectoryIdentity()

                    ),
                    T0.AddSeconds(1)
                )
            );

        LinuxFileIdentityResult replacement =
            DirectoryIdentity() with
            {
                Inode =
                    987654UL
            };

        DataRelativePathRepairDirectoryJournalTransitionResult result =
            DataRelativePathRepairDirectoryJournal.Reprepare(
                prepared,
                ".casecompat-stage-2",
                SyntheticDirectoryJournalIncarnation.FromPhysical(

                    replacement

                ),
                T0.AddSeconds(2)
            );

        Assert.True(
            result.Success,
            result.Error
        );

        DataRelativePathRepairDirectoryJournalRecord reprepared =
            RequireRecord(
                result
            );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.Prepared,
            reprepared.State
        );

        Assert.Equal(
            prepared.Revision + 1,
            reprepared.Revision
        );

        Assert.Equal(
            ".casecompat-stage-2",
            reprepared.PreparedStagingChildName
        );

        Assert.Same(
            replacement,
            reprepared.PreparedDirectoryIdentity
        );

        Assert.Equal(
            prepared.Operation,
            reprepared.Operation
        );

        Assert.Equal(
            prepared.DestinationParentSnapshot,
            reprepared.DestinationParentSnapshot
        );

        Assert.Equal(
            prepared.CreatedUtc,
            reprepared.CreatedUtc
        );
    }

    [Fact]
    public void Reprepare_FromIntent_IsRejected()
    {
        DataRelativePathRepairDirectoryJournalRecord intent =
            RequireRecord(
                CreateIntent()
            );

        DataRelativePathRepairDirectoryJournalTransitionResult result =
            DataRelativePathRepairDirectoryJournal.Reprepare(
                intent,
                ".casecompat-stage-2",
                SyntheticDirectoryJournalIncarnation.FromPhysical(

                    DirectoryIdentity()

                ),
                T0.AddSeconds(1)
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalTransitionState
                .InvalidTransition,
            result.State
        );
    }

    [Fact]
    public void Lifecycle_ForwardAndRollbackTransitionsIncrementRevision()
    {
        DataRelativePathRepairDirectoryJournalRecord intent =
            RequireRecord(
                CreateIntent()
            );

        DataRelativePathRepairDirectoryJournalRecord prepared =
            RequireRecord(
                DataRelativePathRepairDirectoryJournal.MarkPrepared(
                    intent,
                    ".casecompat-stage-1",
                    SyntheticDirectoryJournalIncarnation.FromPhysical(

                        DirectoryIdentity()

                    ),
                    T0.AddSeconds(1)
                )
            );

        DataRelativePathRepairDirectoryJournalRecord applied =
            RequireRecord(
                DataRelativePathRepairDirectoryJournal.MarkApplied(
                    prepared,
                    T0.AddSeconds(2)
                )
            );

        DataRelativePathRepairDirectoryJournalRecord
            rollbackRequested =
                RequireRecord(
                    DataRelativePathRepairDirectoryJournal
                        .RequestRollback(
                            applied,
                            T0.AddSeconds(3)
                        )
                );

        DataRelativePathRepairDirectoryJournalRecord rolledBack =
            RequireRecord(
                DataRelativePathRepairDirectoryJournal
                    .MarkRolledBack(
                        rollbackRequested,
                        T0.AddSeconds(4)
                    )
            );

        Assert.Equal(
            0,
            intent.Revision
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
            DataRelativePathRepairDirectoryJournalState.RolledBack,
            rolledBack.State
        );

        Assert.True(
            rolledBack.IsTerminal
        );

        Assert.Equal(
            prepared.PreparedStagingChildName,
            rolledBack.PreparedStagingChildName
        );

        Assert.Equal(
            prepared.PreparedDirectoryIdentity,
            rolledBack.PreparedDirectoryIdentity
        );
    }

    private static
        DataRelativePathRepairDirectoryJournalTransitionResult
        CreateIntent()
    {
        return
            DataRelativePathRepairDirectoryJournal.CreateIntent(
                Guid.NewGuid(),
                T0,
                "/game/Data",
                Operation(),
                ParentSnapshot()
            ,
                SyntheticDirectoryJournalIncarnation.FromPhysical(
                    (ParentSnapshot()).Identity
                ));
    }

    private static DataRelativePathRepairPlanOperation Operation()
    {
        return new(
            Kind:
                DataRelativePathRepairPlanOperationKind
                    .CreateDirectory,
            DestinationPath:
                "/game/Data/Meshes/Final",
            SourcePath:
                null
        );
    }

    private static
        DataRelativePathRepairDestinationParentSnapshot
        ParentSnapshot()
    {
        return new(
            PhysicalPath:
                "/game/Data/Meshes",
            Identity:
                new LinuxFileIdentityResult(
                    FullPath:
                        "/game/Data/Meshes",
                    DeviceMajor:
                        8U,
                    DeviceMinor:
                        1U,
                    Inode:
                        100UL,
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

    private static LinuxFileIdentityResult DirectoryIdentity()
    {
        return new(
            FullPath:
                "/game/Data/Meshes/.casecompat-stage-1",
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
        );
    }

    private static
        DataRelativePathRepairDirectoryJournalRecord RequireRecord(
            DataRelativePathRepairDirectoryJournalTransitionResult
                result)
    {
        Assert.True(
            result.Success,
            result.Error
        );

        return Assert.IsType<
            DataRelativePathRepairDirectoryJournalRecord
        >(
            result.Record
        );
    }
}
