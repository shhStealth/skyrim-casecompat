using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairFileRollbackRecoveryActionTests
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
    public void RollbackRequestedMatchingDestination_RemovesAndPersistsRolledBack()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.WriteDestination(
            "source"
        );

        LinuxOpenedFileIdentityResult preparedIdentity =
            fixture.PreparedIdentityFromDestination();

        fixture.PersistRollbackRequested(
            preparedIdentity
        );

        DataRelativePathRepairFileRollbackRecovery result =
            DataRelativePathRepairFileRollbackRecoveryAction
                .Recover(
                    fixture.JournalDirectory,
                    "journal.json",
                    T0.AddSeconds(10)
                );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            DataRelativePathRepairFileRollbackRecoveryState
                .RolledBackDurably,
            result.State
        );

        Assert.NotNull(
            result.RemoveResult
        );

        Assert.Equal(
            LinuxRemoveOwnedFileAtState.Removed,
            result.RemoveResult!.State
        );

        Assert.False(
            File.Exists(
                fixture.DestinationPath
            )
        );

        DataRelativePathRepairFileJournalReaderResult after =
            fixture.ReadJournal();

        Assert.Equal(
            DataRelativePathRepairFileJournalState.RolledBack,
            after.Record!.State
        );
    }

    [Fact]
    public void HeldJournalLock_PreventsDestructiveRecovery()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.WriteDestination(
            "source"
        );

        LinuxOpenedFileIdentityResult preparedIdentity =
            fixture.PreparedIdentityFromDestination();

        fixture.PersistRollbackRequested(
            preparedIdentity
        );

        LinuxExclusiveDirectoryLockResult held =
            LinuxExclusiveDirectoryLock.Acquire(
                fixture.JournalDirectory
            );

        Assert.True(
            held.Success,
            held.Error
        );

        using LinuxExclusiveDirectoryLockLease lease =
            Assert.IsType<
                LinuxExclusiveDirectoryLockLease
            >(
                held.Lease
            );

        DataRelativePathRepairFileRollbackRecovery result =
            DataRelativePathRepairFileRollbackRecoveryAction
                .Recover(
                    fixture.JournalDirectory,
                    "journal.json",
                    T0.AddSeconds(10)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileRollbackRecoveryState
                .LockUnavailable,
            result.State
        );

        Assert.Equal(
            LinuxExclusiveDirectoryLockState.AlreadyLocked,
            result.LockState
        );

        Assert.True(
            File.Exists(
                fixture.DestinationPath
            )
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalState
                .RollbackRequested,
            fixture.ReadJournal().Record!.State
        );
    }

    [Fact]
    public void PreparedMatchingDestination_IsNotEligibleForDeletion()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.WriteDestination(
            "source"
        );

        LinuxOpenedFileIdentityResult preparedIdentity =
            fixture.PreparedIdentityFromDestination();

        fixture.PersistPrepared(
            preparedIdentity
        );

        DataRelativePathRepairFileRollbackRecovery result =
            DataRelativePathRepairFileRollbackRecoveryAction
                .Recover(
                    fixture.JournalDirectory,
                    "journal.json",
                    T0.AddSeconds(10)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileRollbackRecoveryState
                .RecoveryStateNotEligible,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .PreparedDestinationMatches,
            result.Classification!.State
        );

        Assert.True(
            File.Exists(
                fixture.DestinationPath
            )
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalState.Prepared,
            fixture.ReadJournal().Record!.State
        );
    }

    [Fact]
    public void RollbackRequestedMutatedSameInode_IsNotDeleted()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.WriteDestination(
            "source"
        );

        LinuxOpenedFileIdentityResult preparedIdentity =
            fixture.PreparedIdentityFromDestination();

        fixture.PersistRollbackRequested(
            preparedIdentity
        );

        fixture.OverwriteDestinationInPlace(
            "mutant"
        );

        DataRelativePathRepairFileRollbackRecovery result =
            DataRelativePathRepairFileRollbackRecoveryAction
                .Recover(
                    fixture.JournalDirectory,
                    "journal.json",
                    T0.AddSeconds(10)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileRollbackRecoveryState
                .RecoveryStateNotEligible,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .RollbackRequestedDestinationConflict,
            result.Classification!.State
        );

        Assert.True(
            File.Exists(
                fixture.DestinationPath
            )
        );

        Assert.Equal(
            "mutant",
            File.ReadAllText(
                fixture.DestinationPath
            )
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalState
                .RollbackRequested,
            fixture.ReadJournal().Record!.State
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
                    "casecompat-rollback-recovery-tests",
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

            SourcePath =
                Path.Combine(
                    DataRoot,
                    "source.nif"
                );

            DestinationPath =
                Path.Combine(
                    ParentPath,
                    "Final.nif"
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

            File.WriteAllText(
                SourcePath,
                "source"
            );

            ParentSnapshot =
                CaptureParentSnapshot();

            JournalDirectory =
                OpenRoot(
                    JournalDirectoryPath
                );

            DataRelativePathRepairFileJournalWriterResult initial =
                DataRelativePathRepairFileJournalWriter
                    .CreateInitial(
                        JournalDirectory,
                        "journal.json",
                        Intent()
                    );

            Assert.True(
                initial.Success,
                initial.Error
            );
        }

        public string RootPath { get; }

        public string DataRoot { get; }

        public string ParentPath { get; }

        public string SourcePath { get; }

        public string DestinationPath { get; }

        public string JournalDirectoryPath { get; }

        public LinuxNoFollowPathHandle JournalDirectory { get; }

        public DataRelativePathRepairDestinationParentSnapshot
            ParentSnapshot { get; }

        public void WriteDestination(
            string text)
        {
            File.WriteAllText(
                DestinationPath,
                text
            );
        }

        public void OverwriteDestinationInPlace(
            string text)
        {
            byte[] bytes =
                Encoding.UTF8.GetBytes(
                    text
                );

            using FileStream stream =
                new(
                    DestinationPath,
                    FileMode.Open,
                    FileAccess.Write,
                    FileShare.None
                );

            stream.SetLength(
                bytes.LongLength
            );

            stream.Position =
                0;

            stream.Write(
                bytes
            );

            stream.Flush(
                flushToDisk:
                    true
            );
        }

        public LinuxOpenedFileIdentityResult
            PreparedIdentityFromDestination()
        {
            using LinuxNoFollowPathHandle parent =
                OpenRoot(
                    ParentPath
                );

            LinuxOpenChildReadOnlyAtResult opened =
                LinuxOpenChildReadOnlyAt.Open(
                    parent,
                    "Final.nif"
                );

            Assert.True(
                opened.Success
            );

            using LinuxOpenedChildHandle child =
                Assert.IsType<
                    LinuxOpenedChildHandle
                >(
                    opened.OpenedChild
                );

            LinuxOpenedFileIdentityResult identity =
                LinuxOpenedFileIdentity.Capture(
                    child
                );

            Assert.True(
                identity.Success
            );

            return identity with
            {
                LinkCount =
                    0U
            };
        }

        public DataRelativePathRepairFileJournalRecord
            PersistPrepared(
                LinuxOpenedFileIdentityResult preparedIdentity)
        {
            DataRelativePathRepairFileJournalReaderResult current =
                ReadJournal();

            DataRelativePathRepairFileJournalRecord prepared =
                RequireRecord(
                    DataRelativePathRepairFileJournal.MarkPrepared(
                        current.Record!,
                        SyntheticFileJournalIncarnation.FromPhysical(
                            preparedIdentity
                        ),
                        T0.AddSeconds(1)
                    )
                );

            Persist(
                current,
                prepared
            );

            return prepared;
        }

        public DataRelativePathRepairFileJournalRecord
            PersistRollbackRequested(
                LinuxOpenedFileIdentityResult preparedIdentity)
        {
            DataRelativePathRepairFileJournalRecord prepared =
                PersistPrepared(
                    preparedIdentity
                );

            DataRelativePathRepairFileJournalReaderResult
                preparedRead =
                    ReadJournal();

            Assert.Equal(
                prepared,
                preparedRead.Record
            );

            DataRelativePathRepairFileJournalRecord applied =
                RequireRecord(
                    DataRelativePathRepairFileJournal.MarkApplied(
                        prepared,
                        T0.AddSeconds(2)
                    )
                );

            Persist(
                preparedRead,
                applied
            );

            DataRelativePathRepairFileJournalReaderResult
                appliedRead =
                    ReadJournal();

            DataRelativePathRepairFileJournalRecord
                rollbackRequested =
                    RequireRecord(
                        DataRelativePathRepairFileJournal
                            .RequestRollback(
                                applied,
                                T0.AddSeconds(3)
                            )
                    );

            Persist(
                appliedRead,
                rollbackRequested
            );

            return rollbackRequested;
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

        private void Persist(
            DataRelativePathRepairFileJournalReaderResult current,
            DataRelativePathRepairFileJournalRecord next)
        {
            DataRelativePathRepairFileJournalWriterResult write =
                DataRelativePathRepairFileJournalWriter
                    .ReplaceExisting(
                        JournalDirectory,
                        "journal.json",
                        current.JournalIdentity!,
                        next
                    );

            Assert.True(
                write.Success,
                write.Error
            );
        }

        private DataRelativePathRepairFileJournalRecord Intent()
        {
            byte[] sourceBytes =
                Encoding.UTF8.GetBytes(
                    "source"
                );

            return RequireRecord(
                DataRelativePathRepairFileJournal.CreateIntent(
                    Guid.NewGuid(),
                    T0,
                    DataRoot,
                    new DataRelativePathRepairPlanOperation(
                        Kind:
                            DataRelativePathRepairPlanOperationKind
                                .CreateFile,
                        DestinationPath:
                            DestinationPath,
                        SourcePath:
                            SourcePath
                    ),
                    new DataRelativePathRepairSourceSnapshot(
                        PhysicalPath:
                            SourcePath,
                        Size:
                            sourceBytes.LongLength,
                        Sha256:
                            Convert.ToHexString(
                                SHA256.HashData(
                                    sourceBytes
                                )
                            ),
                        Identity:
                            new LinuxFileIdentityResult(
                                FullPath:
                                    SourcePath,
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
                    ),
                    ParentSnapshot
                )
            );
        }

        private
            DataRelativePathRepairDestinationParentSnapshot
            CaptureParentSnapshot()
        {
            using LinuxNoFollowPathHandle parent =
                OpenRoot(
                    ParentPath
                );

            LinuxOpenedDirectorySnapshotResult snapshot =
                LinuxOpenedDirectorySnapshot.Capture(
                    parent
                );

            Assert.True(
                snapshot.Success
            );

            Assert.NotNull(
                snapshot.Identity
            );

            Assert.False(
                snapshot.CasefoldEnabled
            );

            Assert.NotNull(
                snapshot.RawFlags
            );

            return new
                DataRelativePathRepairDestinationParentSnapshot(
                    PhysicalPath:
                        ParentPath,
                    Identity:
                        snapshot.Identity!,
                    CasefoldEnabled:
                        snapshot.CasefoldEnabled!.Value,
                    RawFlags:
                        snapshot.RawFlags!.Value
                );
        }

        private static
            DataRelativePathRepairFileJournalRecord
            RequireRecord(
                DataRelativePathRepairFileJournalTransitionResult
                    result)
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

        private static LinuxNoFollowPathHandle OpenRoot(
            string root)
        {
            LinuxNoFollowPathOpenResult result =
                LinuxNoFollowPath.OpenRootReadOnly(
                    root
                );

            Assert.True(
                result.Success
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
