using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairDirectoryRollbackRecoveryActionTests
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
    public void RollbackRequestedMatchingEmptyDirectory_RemovesAndPersistsRolledBack()
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

        fixture.PersistRollbackRequestedWithFinal();

        DataRelativePathRepairDirectoryRollbackRecovery result =
            DataRelativePathRepairDirectoryRollbackRecoveryAction
                .Recover(
                    fixture.JournalDirectory,
                    "journal.json",
                    T0.AddSeconds(4)
                );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRollbackRecoveryState
                .RolledBackDurably,
            result.State
        );

        Assert.NotNull(
            result.RemoveResult
        );

        Assert.Equal(
            LinuxRemoveOwnedDirectoryAtState.Removed,
            result.RemoveResult!.State
        );

        Assert.NotNull(
            result.DestinationParentSync
        );

        Assert.True(
            result.DestinationParentSync!.Success
        );

        Assert.False(
            Directory.Exists(
                fixture.PathFor(
                    "Final"
                )
            )
        );

        Assert.False(
            Directory.Exists(
                fixture.PathFor(
                    ".stage"
                )
            )
        );

        DataRelativePathRepairDirectoryJournalReaderResult after =
            fixture.ReadJournal();

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.RolledBack,
            after.Record!.State
        );

        Assert.Equal(
            4,
            after.Record.Revision
        );

        DataRelativePathRepairDirectoryRecoveryClassification
            classification =
                DataRelativePathRepairDirectoryRecoveryClassifier
                    .Classify(
                        after.Record
                    );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .RolledBackBothMissing,
            classification.State
        );
    }

    [Fact]
    public void RollbackRequestedMatchingNonEmptyDirectory_IsNeverRecursivelyDeleted()
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

        fixture.PersistRollbackRequestedWithFinal();

        string payload =
            Path.Combine(
                fixture.PathFor(
                    "Final"
                ),
                "keep.txt"
            );

        File.WriteAllText(
            payload,
            "keep"
        );

        DataRelativePathRepairDirectoryRollbackRecovery result =
            DataRelativePathRepairDirectoryRollbackRecoveryAction
                .Recover(
                    fixture.JournalDirectory,
                    "journal.json",
                    T0.AddSeconds(4)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRollbackRecoveryState
                .RemoveFailed,
            result.State
        );

        Assert.NotNull(
            result.RemoveResult
        );

        Assert.Equal(
            LinuxRemoveOwnedDirectoryAtState.DirectoryNotEmpty,
            result.RemoveResult!.State
        );

        Assert.True(
            Directory.Exists(
                fixture.PathFor(
                    "Final"
                )
            )
        );

        Assert.Equal(
            "keep",
            File.ReadAllText(
                payload
            )
        );

        DataRelativePathRepairDirectoryJournalReaderResult after =
            fixture.ReadJournal();

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState
                .RollbackRequested,
            after.Record!.State
        );

        Assert.Equal(
            3,
            after.Record.Revision
        );
    }

    [Fact]
    public void RollbackRequestedMissingFinal_IsLeftForJournalReconciler()
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

        fixture.PersistRollbackRequestedMissingFinal();

        DataRelativePathRepairDirectoryRollbackRecovery result =
            DataRelativePathRepairDirectoryRollbackRecoveryAction
                .Recover(
                    fixture.JournalDirectory,
                    "journal.json",
                    T0.AddSeconds(4)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRollbackRecoveryState
                .RecoveryStateNotEligible,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .RollbackRequestedFinalMissing,
            result.Classification!.State
        );

        Assert.Null(
            result.RemoveResult
        );

        DataRelativePathRepairDirectoryJournalReaderResult after =
            fixture.ReadJournal();

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState
                .RollbackRequested,
            after.Record!.State
        );
    }

    [Fact]
    public void PreparedPublishedDirectory_IsNotEligibleForDeletion()
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

        fixture.PersistPreparedWithFinal();

        DataRelativePathRepairDirectoryRollbackRecovery result =
            DataRelativePathRepairDirectoryRollbackRecoveryAction
                .Recover(
                    fixture.JournalDirectory,
                    "journal.json",
                    T0.AddSeconds(2)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRollbackRecoveryState
                .RecoveryStateNotEligible,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .PreparedFinalMatchesStagingMissing,
            result.Classification!.State
        );

        Assert.True(
            Directory.Exists(
                fixture.PathFor(
                    "Final"
                )
            )
        );

        Assert.Null(
            result.RemoveResult
        );
    }

    [Fact]
    public void HeldJournalLock_PreventsDestructiveDirectoryRollback()
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

        fixture.PersistRollbackRequestedWithFinal();

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

        DataRelativePathRepairDirectoryRollbackRecovery result =
            DataRelativePathRepairDirectoryRollbackRecoveryAction
                .Recover(
                    fixture.JournalDirectory,
                    "journal.json",
                    T0.AddSeconds(4)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRollbackRecoveryState
                .LockUnavailable,
            result.State
        );

        Assert.True(
            Directory.Exists(
                fixture.PathFor(
                    "Final"
                )
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
                    "casecompat-directory-rollback-recovery-tests",
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

        public void PersistRollbackRequestedWithFinal()
        {
            DataRelativePathRepairDirectoryJournalRecord prepared =
                CreatePreparedWithFinal();

            DataRelativePathRepairDirectoryJournalRecord applied =
                RequireRecord(
                    DataRelativePathRepairDirectoryJournal.MarkApplied(
                        prepared,
                        T0.AddSeconds(2)
                    )
                );

            DataRelativePathRepairDirectoryJournalRecord requested =
                RequireRecord(
                    DataRelativePathRepairDirectoryJournal
                        .RequestRollback(
                            applied,
                            T0.AddSeconds(3)
                        )
                );

            PersistLifecycle(
                prepared,
                applied,
                requested
            );
        }

        public void PersistRollbackRequestedMissingFinal()
        {
            DataRelativePathRepairDirectoryJournalRecord intent =
                CreateIntent();

            DataRelativePathRepairDirectoryJournalRecord prepared =
                RequireRecord(
                    DataRelativePathRepairDirectoryJournal.MarkPrepared(
                        intent,
                        ".stage",
                        SyntheticDirectoryJournalIncarnation.FromPhysical(

                            SyntheticIdentity()

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

            DataRelativePathRepairDirectoryJournalRecord requested =
                RequireRecord(
                    DataRelativePathRepairDirectoryJournal
                        .RequestRollback(
                            applied,
                            T0.AddSeconds(3)
                        )
                );

            PersistInitial(
                intent
            );

            PersistReplacement(
                prepared
            );

            PersistReplacement(
                applied
            );

            PersistReplacement(
                requested
            );
        }

        public void PersistPreparedWithFinal()
        {
            DataRelativePathRepairDirectoryJournalRecord prepared =
                CreatePreparedWithFinal();

            PersistInitial(
                CreateIntentForSameTransaction(
                    prepared.JournalId,
                    prepared.CreatedUtc,
                    prepared.DestinationParentSnapshot
                )
            );

            PersistReplacement(
                prepared
            );
        }

        private DataRelativePathRepairDirectoryJournalRecord
            CreatePreparedWithFinal()
        {
            DataRelativePathRepairDirectoryJournalRecord intent =
                CreateIntent();

            Directory.CreateDirectory(
                PathFor(
                    ".stage"
                )
            );

            LinuxDirectoryIncarnationIdentity identity =
                CaptureDirectoryIdentity(
                    ".stage"
                );

            DataRelativePathRepairDirectoryJournalRecord prepared =
                RequireRecord(
                    DataRelativePathRepairDirectoryJournal.MarkPrepared(
                        intent,
                        ".stage",
                        identity,
                        T0.AddSeconds(1)
                    )
                );

            Directory.Move(
                PathFor(
                    ".stage"
                ),
                PathFor(
                    "Final"
                )
            );

            return prepared;
        }

        private void PersistLifecycle(
            DataRelativePathRepairDirectoryJournalRecord prepared,
            DataRelativePathRepairDirectoryJournalRecord applied,
            DataRelativePathRepairDirectoryJournalRecord requested)
        {
            PersistInitial(
                CreateIntentForSameTransaction(
                    prepared.JournalId,
                    prepared.CreatedUtc,
                    prepared.DestinationParentSnapshot
                )
            );

            PersistReplacement(
                prepared
            );

            PersistReplacement(
                applied
            );

            PersistReplacement(
                requested
            );
        }

        private
            DataRelativePathRepairDirectoryJournalRecord
            CreateIntentForSameTransaction(
                Guid journalId,
                DateTimeOffset createdUtc,
                DataRelativePathRepairDestinationParentSnapshot
                    parentSnapshot)
        {
            DataRelativePathRepairDirectoryJournalTransitionResult result =
                DataRelativePathRepairDirectoryJournal.CreateIntent(
                    journalId,
                    createdUtc,
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
                    parentSnapshot
                ,
                    LiveDirectoryJournalIncarnation.Capture(
                        Parent
                    ));

            return RequireRecord(
                result
            );
        }

        private DataRelativePathRepairDirectoryJournalRecord
            CreateIntent()
        {
            DataRelativePathRepairDirectoryJournalTransitionResult result =
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
                    CaptureParentSnapshot()
                ,
                    LiveDirectoryJournalIncarnation.Capture(
                        Parent
                    ));

            return RequireRecord(
                result
            );
        }

        private LinuxFileIdentityResult SyntheticIdentity()
        {
            DataRelativePathRepairDestinationParentSnapshot parent =
                CaptureParentSnapshot();

            return new(
                FullPath:
                    PathFor(
                        ".stage"
                    ),
                DeviceMajor:
                    parent.Identity.DeviceMajor,
                DeviceMinor:
                    parent.Identity.DeviceMinor,
                Inode:
                    ulong.MaxValue - 600UL,
                LinkCount:
                    2U,
                MountId:
                    parent.Identity.MountId,
                Error:
                    null
            );
        }

        private LinuxDirectoryIncarnationIdentity CaptureDirectoryIdentity(
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

            LinuxOpenedDirectorySnapshotResult snapshot =
                LinuxOpenedDirectorySnapshot.Capture(
                    child,
                    PathFor(
                        childName
                    )
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

        private void PersistInitial(
            DataRelativePathRepairDirectoryJournalRecord record)
        {
            DataRelativePathRepairDirectoryJournalWriterResult result =
                DataRelativePathRepairDirectoryJournalWriter
                    .CreateInitial(
                        JournalDirectory,
                        "journal.json",
                        record
                    );

            Assert.True(
                result.Success,
                result.Error
            );
        }

        private void PersistReplacement(
            DataRelativePathRepairDirectoryJournalRecord record)
        {
            DataRelativePathRepairDirectoryJournalReaderResult before =
                ReadJournal();

            DataRelativePathRepairDirectoryJournalWriterResult result =
                DataRelativePathRepairDirectoryJournalWriter
                    .ReplaceExisting(
                        JournalDirectory,
                        "journal.json",
                        before.JournalIdentity!,
                        record
                    );

            Assert.True(
                result.Success,
                result.Error
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
