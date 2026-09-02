using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed partial class
    DataRelativePathRepairBatchDirectoryReuseAuthorizerTests
{
    [Fact]
    public void
        PublishAuthorized_CurrentDestinationStillMatches_PublishesDurably()
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

        Guid batchId =
            Guid.NewGuid();

        DataRelativePathRepairBatchExecutionContext context =
            fixture.BuildContext(
                [
                    owner,
                    borrower
                ],
                currentChildIndex:
                    1,
                batchId:
                    batchId
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

        Assert.Equal(
            DataRelativePathRepairBatchDirectoryReusePublicationState
                .PublishedDurably,
            publication.State
        );

        Assert.NotNull(
            publication.DestinationIncarnation?.Identity
        );

        Assert.True(
            authorization.Provenance!
                .ReusedDirectoryIncarnationIdentity
                .SameIncarnationAs(
                    publication.DestinationIncarnation!.Identity!
                )
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalWriteState
                .CreatedDurably,
            publication.JournalWrite!.State
        );

        DataRelativePathRepairDirectoryJournalReaderResult read =
            DataRelativePathRepairDirectoryJournalReader.Read(
                journalDirectory,
                entry.JournalChildName
            );

        Assert.True(
            read.Success,
            read.Error
        );

        DataRelativePathRepairDirectoryJournalRecord record =
            read.Record!;

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalRecord.SchemaVersion3,
            record.SchemaVersion
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.Applied,
            record.State
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryOwnershipDisposition.BatchReused,
            record.OwnershipDisposition
        );

        Assert.NotNull(
            record.BatchReuseProvenance
        );

        Assert.Equal(
            batchId,
            record.BatchReuseProvenance!.BatchId
        );

        Assert.Equal(
            owner.ChildName,
            record.BatchReuseProvenance.OwnerChildName
        );

        DataRelativePathRepairDirectoryRecoveryClassification
            classification =
                DataRelativePathRepairDirectoryRecoveryClassifier.Classify(
                    record,
                    fixture.DataRoot
                );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .ReusedAppliedFinalMatches,
            classification.State
        );
    }

    [Fact]
    public void
        PublishAuthorized_DestinationReplacedAfterAuthorization_Rejects()
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

        string moved =
            owner.DestinationDirectoryPath +
            "-authorized-original";

        Directory.Move(
            owner.DestinationDirectoryPath,
            moved
        );

        Directory.CreateDirectory(
            owner.DestinationDirectoryPath
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

        Assert.False(
            publication.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchDirectoryReusePublicationState
                .DestinationIncarnationMismatch,
            publication.State
        );

        Assert.NotNull(
            publication.DestinationIncarnation?.Identity
        );

        Assert.False(
            authorization.Provenance!
                .ReusedDirectoryIncarnationIdentity
                .SameIncarnationAs(
                    publication.DestinationIncarnation!.Identity!
                )
        );

        Assert.Null(
            publication.RecordCreation
        );

        Assert.Null(
            publication.JournalWrite
        );

        Assert.False(
            File.Exists(
                Path.Combine(
                    borrower.ChildDirectoryPath,
                    entry.JournalChildName
                )
            )
        );
    }
}
