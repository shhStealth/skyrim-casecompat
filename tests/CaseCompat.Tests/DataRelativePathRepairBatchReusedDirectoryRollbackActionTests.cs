using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed partial class
    DataRelativePathRepairBatchDirectoryReuseAuthorizerTests
{
    [Fact]
    public void
        BatchReusedRollback_AppliedFinalMissing_RefusesWithoutJournalChange()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        PlanSpec owner =
            fixture.CreatePlan(
                1
            );

        PlanSpec borrower =
            fixture.CreatePlan(
                2
            );

        fixture.ApplyPlan(
            owner
        );

        DataRelativePathRepairBatchExecutionContext context =
            fixture.BuildContext(
                [
                    owner,
                    borrower
                ],
                currentChildIndex:
                    1
            );

        DataRelativePathRepairPlanManifestRecord borrowerManifest =
            fixture.ReadManifest(
                borrower
            );

        DataRelativePathRepairPlanManifestOperation entry =
            borrowerManifest.Operations[0];

        using DataRelativePathRepairValidatedDestinationParentLease parent =
            fixture.AcquireParentLease(
                borrowerManifest
            );

        using LinuxNoFollowPathHandle batch =
            Fixture.OpenRoot(
                fixture.BatchRoot
            );

        DataRelativePathRepairBatchDirectoryReuseAuthorization
            authorization =
                DataRelativePathRepairBatchDirectoryReuseAuthorizer
                    .Authorize(
                        batch,
                        context,
                        parent,
                        entry
                    );

        Assert.True(
            authorization.Success,
            authorization.Error
        );

        using LinuxNoFollowPathHandle journalDirectory =
            Fixture.OpenRoot(
                borrower.ChildDirectoryPath
            );

        DataRelativePathRepairBatchDirectoryReusePublication publication =
            DataRelativePathRepairBatchDirectoryReusePublisher
                .PublishAuthorized(
                    journalDirectory,
                    entry.JournalChildName,
                    parent,
                    entry,
                    fixture.DataRoot,
                    T0.AddMinutes(
                        1
                    ),
                    authorization.Provenance!
                );

        Assert.True(
            publication.Success,
            publication.Error
        );

        DataRelativePathRepairDirectoryJournalReaderResult before =
            DataRelativePathRepairDirectoryJournalReader.Read(
                journalDirectory,
                entry.JournalChildName
            );

        Assert.True(
            before.Success,
            before.Error
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.Applied,
            before.Record!.State
        );

        string moved =
            owner.DestinationDirectoryPath +
            "-missing-original";

        Directory.Move(
            owner.DestinationDirectoryPath,
            moved
        );

        Assert.False(
            Directory.Exists(
                owner.DestinationDirectoryPath
            )
        );

        DataRelativePathRepairBatchReusedDirectoryRollback result =
            DataRelativePathRepairBatchReusedDirectoryRollbackAction
                .Advance(
                    journalDirectory,
                    entry.JournalChildName,
                    fixture.DataRoot,
                    T0.AddMinutes(
                        2
                    ),
                    before.JournalIncarnationIdentity!
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchReusedDirectoryRollbackState
                .RecoveryStateNotEligible,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .ReusedAppliedFinalMissing,
            result.Classification!.State
        );

        Assert.Null(
            result.JournalTransition
        );

        Assert.Null(
            result.JournalWrite
        );

        DataRelativePathRepairDirectoryJournalReaderResult after =
            DataRelativePathRepairDirectoryJournalReader.Read(
                journalDirectory,
                entry.JournalChildName
            );

        Assert.True(
            after.Success,
            after.Error
        );

        Assert.Equal(
            before.Record,
            after.Record
        );

        Assert.True(
            Directory.Exists(
                moved
            )
        );
    }

    [Fact]
    public void
        BatchReusedRollback_AppliedFinalReplaced_RefusesWithoutJournalChange()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        PlanSpec owner =
            fixture.CreatePlan(
                1
            );

        PlanSpec borrower =
            fixture.CreatePlan(
                2
            );

        fixture.ApplyPlan(
            owner
        );

        DataRelativePathRepairBatchExecutionContext context =
            fixture.BuildContext(
                [
                    owner,
                    borrower
                ],
                currentChildIndex:
                    1
            );

        DataRelativePathRepairPlanManifestRecord borrowerManifest =
            fixture.ReadManifest(
                borrower
            );

        DataRelativePathRepairPlanManifestOperation entry =
            borrowerManifest.Operations[0];

        using DataRelativePathRepairValidatedDestinationParentLease parent =
            fixture.AcquireParentLease(
                borrowerManifest
            );

        using LinuxNoFollowPathHandle batch =
            Fixture.OpenRoot(
                fixture.BatchRoot
            );

        DataRelativePathRepairBatchDirectoryReuseAuthorization
            authorization =
                DataRelativePathRepairBatchDirectoryReuseAuthorizer
                    .Authorize(
                        batch,
                        context,
                        parent,
                        entry
                    );

        Assert.True(
            authorization.Success,
            authorization.Error
        );

        using LinuxNoFollowPathHandle journalDirectory =
            Fixture.OpenRoot(
                borrower.ChildDirectoryPath
            );

        DataRelativePathRepairBatchDirectoryReusePublication publication =
            DataRelativePathRepairBatchDirectoryReusePublisher
                .PublishAuthorized(
                    journalDirectory,
                    entry.JournalChildName,
                    parent,
                    entry,
                    fixture.DataRoot,
                    T0.AddMinutes(
                        1
                    ),
                    authorization.Provenance!
                );

        Assert.True(
            publication.Success,
            publication.Error
        );

        DataRelativePathRepairDirectoryJournalReaderResult before =
            DataRelativePathRepairDirectoryJournalReader.Read(
                journalDirectory,
                entry.JournalChildName
            );

        Assert.True(
            before.Success,
            before.Error
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.Applied,
            before.Record!.State
        );

        string moved =
            owner.DestinationDirectoryPath +
            "-replaced-original";

        Directory.Move(
            owner.DestinationDirectoryPath,
            moved
        );

        Directory.CreateDirectory(
            owner.DestinationDirectoryPath
        );

        DataRelativePathRepairDirectoryRecoveryClassification
            replacedClassification =
                DataRelativePathRepairDirectoryRecoveryClassifier
                    .Classify(
                        before.Record,
                        fixture.DataRoot
                    );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .ReusedAppliedConflict,
            replacedClassification.State
        );

        DataRelativePathRepairBatchReusedDirectoryRollback result =
            DataRelativePathRepairBatchReusedDirectoryRollbackAction
                .Advance(
                    journalDirectory,
                    entry.JournalChildName,
                    fixture.DataRoot,
                    T0.AddMinutes(
                        2
                    ),
                    before.JournalIncarnationIdentity!
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchReusedDirectoryRollbackState
                .RecoveryStateNotEligible,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .ReusedAppliedConflict,
            result.Classification!.State
        );

        Assert.Null(
            result.JournalTransition
        );

        Assert.Null(
            result.JournalWrite
        );

        DataRelativePathRepairDirectoryJournalReaderResult after =
            DataRelativePathRepairDirectoryJournalReader.Read(
                journalDirectory,
                entry.JournalChildName
            );

        Assert.True(
            after.Success,
            after.Error
        );

        Assert.Equal(
            before.Record,
            after.Record
        );

        Assert.True(
            Directory.Exists(
                moved
            )
        );

        Assert.True(
            Directory.Exists(
                owner.DestinationDirectoryPath
            )
        );
    }

    [Fact]
    public void
        BatchReusedRollback_RollbackRequestedFinalMissing_RefusesWithoutJournalChange()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        PlanSpec owner =
            fixture.CreatePlan(
                1
            );

        PlanSpec borrower =
            fixture.CreatePlan(
                2
            );

        fixture.ApplyPlan(
            owner
        );

        DataRelativePathRepairBatchExecutionContext context =
            fixture.BuildContext(
                [
                    owner,
                    borrower
                ],
                currentChildIndex:
                    1
            );

        DataRelativePathRepairPlanManifestRecord borrowerManifest =
            fixture.ReadManifest(
                borrower
            );

        DataRelativePathRepairPlanManifestOperation entry =
            borrowerManifest.Operations[0];

        using DataRelativePathRepairValidatedDestinationParentLease parent =
            fixture.AcquireParentLease(
                borrowerManifest
            );

        using LinuxNoFollowPathHandle batch =
            Fixture.OpenRoot(
                fixture.BatchRoot
            );

        DataRelativePathRepairBatchDirectoryReuseAuthorization
            authorization =
                DataRelativePathRepairBatchDirectoryReuseAuthorizer
                    .Authorize(
                        batch,
                        context,
                        parent,
                        entry
                    );

        Assert.True(
            authorization.Success,
            authorization.Error
        );

        using LinuxNoFollowPathHandle journalDirectory =
            Fixture.OpenRoot(
                borrower.ChildDirectoryPath
            );

        DataRelativePathRepairBatchDirectoryReusePublication publication =
            DataRelativePathRepairBatchDirectoryReusePublisher
                .PublishAuthorized(
                    journalDirectory,
                    entry.JournalChildName,
                    parent,
                    entry,
                    fixture.DataRoot,
                    T0.AddMinutes(
                        1
                    ),
                    authorization.Provenance!
                );

        Assert.True(
            publication.Success,
            publication.Error
        );

        DataRelativePathRepairDirectoryJournalReaderResult applied =
            DataRelativePathRepairDirectoryJournalReader.Read(
                journalDirectory,
                entry.JournalChildName
            );

        Assert.True(
            applied.Success,
            applied.Error
        );

        DataRelativePathRepairBatchReusedDirectoryRollback request =
            DataRelativePathRepairBatchReusedDirectoryRollbackAction
                .Advance(
                    journalDirectory,
                    entry.JournalChildName,
                    fixture.DataRoot,
                    T0.AddMinutes(
                        2
                    ),
                    applied.JournalIncarnationIdentity!
                );

        Assert.True(
            request.Success,
            request.Error
        );

        Assert.Equal(
            DataRelativePathRepairBatchReusedDirectoryRollbackState
                .RequestedDurably,
            request.State
        );

        DataRelativePathRepairDirectoryJournalReaderResult requested =
            DataRelativePathRepairDirectoryJournalReader.Read(
                journalDirectory,
                entry.JournalChildName
            );

        Assert.True(
            requested.Success,
            requested.Error
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.RollbackRequested,
            requested.Record!.State
        );

        string moved =
            owner.DestinationDirectoryPath +
            "-rollback-requested-missing-original";

        Directory.Move(
            owner.DestinationDirectoryPath,
            moved
        );

        DataRelativePathRepairBatchReusedDirectoryRollback completion =
            DataRelativePathRepairBatchReusedDirectoryRollbackAction
                .Advance(
                    journalDirectory,
                    entry.JournalChildName,
                    fixture.DataRoot,
                    T0.AddMinutes(
                        3
                    ),
                    requested.JournalIncarnationIdentity!
                );

        Assert.False(
            completion.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchReusedDirectoryRollbackState
                .RecoveryStateNotEligible,
            completion.State
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .ReusedRollbackRequestedFinalMissing,
            completion.Classification!.State
        );

        Assert.Null(
            completion.JournalTransition
        );

        Assert.Null(
            completion.JournalWrite
        );

        DataRelativePathRepairDirectoryJournalReaderResult after =
            DataRelativePathRepairDirectoryJournalReader.Read(
                journalDirectory,
                entry.JournalChildName
            );

        Assert.True(
            after.Success,
            after.Error
        );

        Assert.Equal(
            requested.Record,
            after.Record
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.RollbackRequested,
            after.Record!.State
        );

        Assert.True(
            Directory.Exists(
                moved
            )
        );
    }

    [Fact]
    public void
        BatchReusedRollback_RollbackRequestedFinalReplaced_RefusesWithoutJournalChange()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        PlanSpec owner =
            fixture.CreatePlan(
                1
            );

        PlanSpec borrower =
            fixture.CreatePlan(
                2
            );

        fixture.ApplyPlan(
            owner
        );

        DataRelativePathRepairBatchExecutionContext context =
            fixture.BuildContext(
                [
                    owner,
                    borrower
                ],
                currentChildIndex:
                    1
            );

        DataRelativePathRepairPlanManifestRecord borrowerManifest =
            fixture.ReadManifest(
                borrower
            );

        DataRelativePathRepairPlanManifestOperation entry =
            borrowerManifest.Operations[0];

        using DataRelativePathRepairValidatedDestinationParentLease parent =
            fixture.AcquireParentLease(
                borrowerManifest
            );

        using LinuxNoFollowPathHandle batch =
            Fixture.OpenRoot(
                fixture.BatchRoot
            );

        DataRelativePathRepairBatchDirectoryReuseAuthorization
            authorization =
                DataRelativePathRepairBatchDirectoryReuseAuthorizer
                    .Authorize(
                        batch,
                        context,
                        parent,
                        entry
                    );

        Assert.True(
            authorization.Success,
            authorization.Error
        );

        using LinuxNoFollowPathHandle journalDirectory =
            Fixture.OpenRoot(
                borrower.ChildDirectoryPath
            );

        DataRelativePathRepairBatchDirectoryReusePublication publication =
            DataRelativePathRepairBatchDirectoryReusePublisher
                .PublishAuthorized(
                    journalDirectory,
                    entry.JournalChildName,
                    parent,
                    entry,
                    fixture.DataRoot,
                    T0.AddMinutes(
                        1
                    ),
                    authorization.Provenance!
                );

        Assert.True(
            publication.Success,
            publication.Error
        );

        DataRelativePathRepairDirectoryJournalReaderResult applied =
            DataRelativePathRepairDirectoryJournalReader.Read(
                journalDirectory,
                entry.JournalChildName
            );

        Assert.True(
            applied.Success,
            applied.Error
        );

        DataRelativePathRepairBatchReusedDirectoryRollback request =
            DataRelativePathRepairBatchReusedDirectoryRollbackAction
                .Advance(
                    journalDirectory,
                    entry.JournalChildName,
                    fixture.DataRoot,
                    T0.AddMinutes(
                        2
                    ),
                    applied.JournalIncarnationIdentity!
                );

        Assert.True(
            request.Success,
            request.Error
        );

        Assert.Equal(
            DataRelativePathRepairBatchReusedDirectoryRollbackState
                .RequestedDurably,
            request.State
        );

        DataRelativePathRepairDirectoryJournalReaderResult requested =
            DataRelativePathRepairDirectoryJournalReader.Read(
                journalDirectory,
                entry.JournalChildName
            );

        Assert.True(
            requested.Success,
            requested.Error
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.RollbackRequested,
            requested.Record!.State
        );

        string moved =
            owner.DestinationDirectoryPath +
            "-rollback-requested-replaced-original";

        Directory.Move(
            owner.DestinationDirectoryPath,
            moved
        );

        Directory.CreateDirectory(
            owner.DestinationDirectoryPath
        );

        DataRelativePathRepairBatchReusedDirectoryRollback completion =
            DataRelativePathRepairBatchReusedDirectoryRollbackAction
                .Advance(
                    journalDirectory,
                    entry.JournalChildName,
                    fixture.DataRoot,
                    T0.AddMinutes(
                        3
                    ),
                    requested.JournalIncarnationIdentity!
                );

        Assert.False(
            completion.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchReusedDirectoryRollbackState
                .RecoveryStateNotEligible,
            completion.State
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .ReusedRollbackRequestedConflict,
            completion.Classification!.State
        );

        Assert.Null(
            completion.JournalTransition
        );

        Assert.Null(
            completion.JournalWrite
        );

        DataRelativePathRepairDirectoryJournalReaderResult after =
            DataRelativePathRepairDirectoryJournalReader.Read(
                journalDirectory,
                entry.JournalChildName
            );

        Assert.True(
            after.Success,
            after.Error
        );

        Assert.Equal(
            requested.Record,
            after.Record
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.RollbackRequested,
            after.Record!.State
        );

        Assert.True(
            Directory.Exists(
                moved
            )
        );

        Assert.True(
            Directory.Exists(
                owner.DestinationDirectoryPath
            )
        );
    }

}
