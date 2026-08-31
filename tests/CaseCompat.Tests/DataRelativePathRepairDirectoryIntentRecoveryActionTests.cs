using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairDirectoryIntentRecoveryActionTests
{
    private static readonly DateTimeOffset T0 =
        new(
            2026,
            8,
            31,
            7,
            0,
            0,
            TimeSpan.Zero
        );

    [Fact]
    public void TrustedDataRootMismatch_DoesNotPrepareDirectory()
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

        fixture.PersistIntent();

        DataRelativePathRepairDirectoryJournalReaderResult before =
            fixture.ReadJournal();

        string trustedDataRoot =
            Path.Combine(
                fixture.RootPath,
                "OtherData"
            );

        DataRelativePathRepairDirectoryIntentRecovery result =
            DataRelativePathRepairDirectoryIntentRecoveryAction
                .Recover(
                    fixture.JournalDirectory,
                    "journal.json",
                    trustedDataRoot,
                    T0.AddSeconds(1)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryIntentRecoveryState
                .RecoveryStateNotEligible,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .DataRootMismatch,
            result.Classification!.State
        );

        Assert.Null(
            result.FreshStagingChildName
        );

        Assert.Empty(
            Directory.EnumerateFileSystemEntries(
                fixture.ParentPath
            )
        );

        DataRelativePathRepairDirectoryJournalReaderResult after =
            fixture.ReadJournal();

        Assert.Equal(
            before.Record,
            after.Record
        );
    }

    [Fact]
    public void IntentFinalMissing_Recover_CreatesDurablePreparedState()
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

        fixture.PersistIntent();

        DataRelativePathRepairDirectoryJournalReaderResult before =
            fixture.ReadJournal();

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.IntentRecorded,
            before.Record!.State
        );

        Assert.Equal(
            0,
            before.Record.Revision
        );

        Guid journalId =
            before.Record.JournalId;

        DateTimeOffset createdUtc =
            before.Record.CreatedUtc;

        DataRelativePathRepairDirectoryIntentRecovery result =
            DataRelativePathRepairDirectoryIntentRecoveryAction
                .Recover(
                    fixture.JournalDirectory,
                    "journal.json",
                    fixture.DataRoot,
                    T0.AddSeconds(1)
                );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryIntentRecoveryState
                .PreparedDurably,
            result.State
        );

        Assert.NotNull(
            result.FreshStagingChildName
        );

        Assert.NotNull(
            result.Preparation
        );

        Assert.True(
            result.Preparation!.Success
        );

        Assert.NotNull(
            result.PreparedTransition
        );

        Assert.True(
            result.PreparedTransition!.Success
        );

        Assert.NotNull(
            result.PreparedJournalWrite
        );

        Assert.True(
            result.PreparedJournalWrite!.Success
        );

        Assert.False(
            result.UnjournaledStagingEntryMayRemain
        );

        string stagingName =
            result.FreshStagingChildName!;

        Assert.True(
            Directory.Exists(
                fixture.PathFor(
                    stagingName
                )
            )
        );

        Assert.False(
            Directory.Exists(
                fixture.PathFor(
                    "Final"
                )
            )
        );

        DataRelativePathRepairDirectoryJournalReaderResult after =
            fixture.ReadJournal();

        Assert.Equal(
            journalId,
            after.Record!.JournalId
        );

        Assert.Equal(
            createdUtc,
            after.Record.CreatedUtc
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.Prepared,
            after.Record.State
        );

        Assert.Equal(
            1,
            after.Record.Revision
        );

        Assert.Equal(
            stagingName,
            after.Record.PreparedStagingChildName
        );

        Assert.NotNull(
            after.Record.PreparedDirectoryIncarnationIdentity
        );

        LinuxDirectoryIncarnationIdentity actual =
            fixture.CaptureDirectoryIdentity(
                stagingName
            );

        Assert.True(
            after.Record.PreparedDirectoryIncarnationIdentity!
                .SameIncarnationAs(
                    actual
                )
        );

        DataRelativePathRepairDirectoryRecoveryClassification
            classification =
                DataRelativePathRepairDirectoryRecoveryClassifier
                    .Classify(
                        after.Record,
                        fixture.DataRoot
                    );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .PreparedStagingMatchesFinalMissing,
            classification.State
        );

        Assert.True(
            classification.StagingMatchesPreparedIdentity
        );
    }

    [Fact]
    public void IntentFinalPresent_IsNotRecoveredOrOverwritten()
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

        fixture.PersistIntent();

        DataRelativePathRepairDirectoryJournalReaderResult before =
            fixture.ReadJournal();

        fixture.CreateDirectory(
            "Final"
        );

        LinuxDirectoryIncarnationIdentity finalBefore =
            fixture.CaptureDirectoryIdentity(
                "Final"
            );

        DataRelativePathRepairDirectoryIntentRecovery result =
            DataRelativePathRepairDirectoryIntentRecoveryAction
                .Recover(
                    fixture.JournalDirectory,
                    "journal.json",
                    fixture.DataRoot,
                    T0.AddSeconds(1)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryIntentRecoveryState
                .RecoveryStateNotEligible,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .IntentFinalConflict,
            result.Classification!.State
        );

        Assert.Null(
            result.FreshStagingChildName
        );

        Assert.True(
            Directory.Exists(
                fixture.PathFor(
                    "Final"
                )
            )
        );

        LinuxDirectoryIncarnationIdentity finalAfter =
            fixture.CaptureDirectoryIdentity(
                "Final"
            );

        Assert.True(
            finalBefore.SameIncarnationAs(
                finalAfter
            )
        );

        Assert.Single(
            Directory.EnumerateFileSystemEntries(
                fixture.ParentPath
            )
        );

        DataRelativePathRepairDirectoryJournalReaderResult after =
            fixture.ReadJournal();

        Assert.Equal(
            before.Record,
            after.Record
        );
    }

    [Fact]
    public void HeldJournalLock_PreventsIntentRecovery()
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

        fixture.PersistIntent();

        LinuxExclusiveDirectoryLockResult held =
            LinuxExclusiveDirectoryLock.Acquire(
                fixture.JournalDirectory
            );

        Assert.True(
            held.Success,
            held.Error
        );

        using LinuxExclusiveDirectoryLockLease lease =
            held.Lease!;

        DataRelativePathRepairDirectoryIntentRecovery result =
            DataRelativePathRepairDirectoryIntentRecoveryAction
                .Recover(
                    fixture.JournalDirectory,
                    "journal.json",
                    fixture.DataRoot,
                    T0.AddSeconds(1)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryIntentRecoveryState
                .LockUnavailable,
            result.State
        );

        Assert.Empty(
            Directory.EnumerateFileSystemEntries(
                fixture.ParentPath
            )
        );

        DataRelativePathRepairDirectoryJournalReaderResult after =
            fixture.ReadJournal();

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.IntentRecorded,
            after.Record!.State
        );

        Assert.Equal(
            0,
            after.Record.Revision
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
                    "casecompat-directory-intent-recovery-tests",
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
                    "Parent"
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

            Parent =
                OpenRoot(
                    ParentPath
                );

            JournalDirectory =
                OpenRoot(
                    JournalDirectoryPath
                );
        }

        public string RootPath { get; }

        public string DataRoot { get; }

        public string ParentPath { get; }

        public string JournalDirectoryPath { get; }

        public LinuxNoFollowPathHandle Parent { get; }

        public LinuxNoFollowPathHandle JournalDirectory { get; }

        public string PathFor(
            string childName)
        {
            return Path.Combine(
                ParentPath,
                childName
            );
        }

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

        public void CreateDirectory(
            string childName)
        {
            Directory.CreateDirectory(
                PathFor(
                    childName
                )
            );
        }

        public void PersistIntent()
        {
            DataRelativePathRepairDirectoryJournalRecord intent =
                CreateIntent();

            DataRelativePathRepairDirectoryJournalWriterResult write =
                DataRelativePathRepairDirectoryJournalWriter
                    .CreateInitial(
                        JournalDirectory,
                        "journal.json",
                        intent
                    );

            Assert.True(
                write.Success,
                write.Error
            );
        }

        public DataRelativePathRepairDirectoryJournalRecord
            CreateIntent()
        {
            DataRelativePathRepairDirectoryJournalTransitionResult
                result =
                    DataRelativePathRepairDirectoryJournal.CreateIntent(
                        Guid.NewGuid(),
                        T0,
                        DataRoot,
                        new DataRelativePathRepairPlanOperation(
                            Kind:
                                DataRelativePathRepairPlanOperationKind
                                    .CreateDirectory,
                            DestinationPath:
                                PathFor(
                                    "Final"
                                ),
                            SourcePath:
                                null
                        ),
                        CaptureParentSnapshot(),
                        LiveDirectoryJournalIncarnation.Capture(
                            Parent
                        )
                    );

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

        public DataRelativePathRepairDirectoryJournalReaderResult
            ReadJournal()
        {
            DataRelativePathRepairDirectoryJournalReaderResult result =
                DataRelativePathRepairDirectoryJournalReader.Read(
                    JournalDirectory,
                    "journal.json"
                );

            Assert.True(
                result.Success,
                result.Error
            );

            return result;
        }

        public LinuxDirectoryIncarnationIdentity CaptureDirectoryIdentity(
            string childName)
        {
            LinuxOpenChildReadOnlyAtResult opened =
                LinuxOpenChildReadOnlyAt.Open(
                    Parent,
                    childName
                );

            Assert.True(
                opened.Success,
                opened.Error
            );

            using LinuxOpenedChildHandle child =
                Assert.IsType<
                    LinuxOpenedChildHandle
                >(
                    opened.OpenedChild
                );

            return LiveDirectoryJournalIncarnation.Capture(
                child,
                PathFor(
                    childName
                )
            );
        }

        private
            DataRelativePathRepairDestinationParentSnapshot
            CaptureParentSnapshot()
        {
            LinuxOpenedDirectorySnapshotResult snapshot =
                LinuxOpenedDirectorySnapshot.Capture(
                    Parent
                );

            Assert.True(
                snapshot.Success,
                snapshot.Error
            );

            Assert.NotNull(
                snapshot.Identity
            );

            Assert.NotNull(
                snapshot.Identity!.MountId
            );

            Assert.NotNull(
                snapshot.CasefoldEnabled
            );

            Assert.NotNull(
                snapshot.RawFlags
            );

            Assert.False(
                snapshot.CasefoldEnabled!.Value
            );

            return new(
                PhysicalPath:
                    ParentPath,
                Identity:
                    snapshot.Identity,
                CasefoldEnabled:
                    snapshot.CasefoldEnabled.Value,
                RawFlags:
                    snapshot.RawFlags!.Value
            );
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
            Parent.Dispose();

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
