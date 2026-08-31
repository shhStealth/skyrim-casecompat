using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairFileRecoveryReconcilerTests
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
    public void TrustedDataRootMismatch_DoesNotReconcileJournal()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.PersistPrepared(
            fixture.FakePreparedIdentity()
        );

        DataRelativePathRepairFileJournalReaderResult before =
            fixture.ReadJournal();

        string trustedDataRoot =
            Path.Combine(
                fixture.RootPath,
                "OtherData"
            );

        DataRelativePathRepairFileRecoveryReconciliation result =
            DataRelativePathRepairFileRecoveryReconciler
                .Reconcile(
                    fixture.JournalDirectory,
                    "journal.json",
                    before.JournalIncarnationIdentity!,
                    before.Record!,
                    trustedDataRoot,
                    T0.AddSeconds(10)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryReconciliationState
                .NoAutomaticReconciliation,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .DataRootMismatch,
            result.Classification.State
        );

        DataRelativePathRepairFileJournalReaderResult after =
            fixture.ReadJournal();

        Assert.Equal(
            before.Record,
            after.Record
        );
    }

    [Fact]
    public void PreparedMatchingDestination_PersistsApplied()
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

        LinuxFileIncarnationIdentity preparedIdentity =
            fixture.PreparedIdentityFromDestination();

        DataRelativePathRepairFileJournalRecord prepared =
            fixture.PersistPrepared(
                preparedIdentity
            );

        DataRelativePathRepairFileJournalReaderResult before =
            fixture.ReadJournal();

        Assert.Equal(
            prepared,
            before.Record
        );

        DataRelativePathRepairFileRecoveryReconciliation result =
            DataRelativePathRepairFileRecoveryReconciler
                .Reconcile(
                    fixture.JournalDirectory,
                    "journal.json",
                    before.JournalIncarnationIdentity!,
                    before.Record!,
                    fixture.DataRoot,
                    T0.AddSeconds(10)
                );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryReconciliationState
                .AppliedDurably,
            result.State
        );

        DataRelativePathRepairFileJournalReaderResult after =
            fixture.ReadJournal();

        Assert.True(
            after.Success,
            after.Error
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalState.Applied,
            after.Record!.State
        );

        Assert.Equal(
            prepared.Revision + 1,
            after.Record.Revision
        );

        Assert.Equal(
            "source",
            File.ReadAllText(
                fixture.DestinationPath
            )
        );
    }

    [Fact]
    public void RollbackRequestedMissingDestination_PersistsRolledBack()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.PersistRollbackRequested(
            fixture.FakePreparedIdentity()
        );

        DataRelativePathRepairFileJournalReaderResult before =
            fixture.ReadJournal();

        Assert.Equal(
            DataRelativePathRepairFileJournalState
                .RollbackRequested,
            before.Record!.State
        );

        Assert.False(
            File.Exists(
                fixture.DestinationPath
            )
        );

        DataRelativePathRepairFileRecoveryReconciliation result =
            DataRelativePathRepairFileRecoveryReconciler
                .Reconcile(
                    fixture.JournalDirectory,
                    "journal.json",
                    before.JournalIncarnationIdentity!,
                    before.Record,
                    fixture.DataRoot,
                    T0.AddSeconds(10)
                );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryReconciliationState
                .RolledBackDurably,
            result.State
        );

        DataRelativePathRepairFileJournalReaderResult after =
            fixture.ReadJournal();

        Assert.Equal(
            DataRelativePathRepairFileJournalState
                .RolledBack,
            after.Record!.State
        );

        Assert.False(
            File.Exists(
                fixture.DestinationPath
            )
        );
    }

    [Fact]
    public void PreparedMissingDestination_DoesNotAdvanceJournal()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.PersistPrepared(
            fixture.FakePreparedIdentity()
        );

        DataRelativePathRepairFileJournalReaderResult before =
            fixture.ReadJournal();

        DataRelativePathRepairFileRecoveryReconciliation result =
            DataRelativePathRepairFileRecoveryReconciler
                .Reconcile(
                    fixture.JournalDirectory,
                    "journal.json",
                    before.JournalIncarnationIdentity!,
                    before.Record!,
                    fixture.DataRoot,
                    T0.AddSeconds(10)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryReconciliationState
                .NoAutomaticReconciliation,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .PreparedDestinationMissing,
            result.Classification.State
        );

        DataRelativePathRepairFileJournalReaderResult after =
            fixture.ReadJournal();

        Assert.Equal(
            before.Record,
            after.Record
        );

        Assert.True(
            before.JournalIdentity!.SameObjectAs(
                after.JournalIdentity!
            )
        );
    }

    [Fact]
    public void RollbackRequestedMatchingDestination_DoesNotDelete()
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

        LinuxFileIncarnationIdentity preparedIdentity =
            fixture.PreparedIdentityFromDestination();

        fixture.PersistRollbackRequested(
            preparedIdentity
        );

        DataRelativePathRepairFileJournalReaderResult before =
            fixture.ReadJournal();

        DataRelativePathRepairFileRecoveryReconciliation result =
            DataRelativePathRepairFileRecoveryReconciler
                .Reconcile(
                    fixture.JournalDirectory,
                    "journal.json",
                    before.JournalIncarnationIdentity!,
                    before.Record!,
                    fixture.DataRoot,
                    T0.AddSeconds(10)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryReconciliationState
                .NoAutomaticReconciliation,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .RollbackRequestedDestinationMatches,
            result.Classification.State
        );

        Assert.True(
            File.Exists(
                fixture.DestinationPath
            )
        );

        Assert.Equal(
            "source",
            File.ReadAllText(
                fixture.DestinationPath
            )
        );

        DataRelativePathRepairFileJournalReaderResult after =
            fixture.ReadJournal();

        Assert.Equal(
            DataRelativePathRepairFileJournalState
                .RollbackRequested,
            after.Record!.State
        );
    }

    [Fact]
    public void CurrentJournalReplacedBeforeReconcile_RefusesJournalAdvance()
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

        LinuxFileIncarnationIdentity preparedIdentity =
            fixture.PreparedIdentityFromDestination();

        fixture.PersistPrepared(
            preparedIdentity
        );

        DataRelativePathRepairFileJournalReaderResult stale =
            fixture.ReadJournal();

        string journalPath =
            Path.Combine(
                fixture.JournalDirectoryPath,
                "journal.json"
            );

        string moved =
            Path.Combine(
                fixture.JournalDirectoryPath,
                "journal-original.json"
            );

        File.Move(
            journalPath,
            moved
        );

        File.WriteAllText(
            journalPath,
            "external replacement"
        );

        DataRelativePathRepairFileRecoveryReconciliation result =
            DataRelativePathRepairFileRecoveryReconciler
                .Reconcile(
                    fixture.JournalDirectory,
                    "journal.json",
                    stale.JournalIncarnationIdentity!,
                    stale.Record!,
                    fixture.DataRoot,
                    T0.AddSeconds(10)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryReconciliationState
                .JournalWriteFailed,
            result.State
        );

        Assert.Equal(
            "external replacement",
            File.ReadAllText(
                journalPath
            )
        );

        Assert.Equal(
            "source",
            File.ReadAllText(
                fixture.DestinationPath
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
                    "casecompat-recovery-reconciler-tests",
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

            DataRelativePathRepairFileJournalRecord intent =
                Intent();

            DataRelativePathRepairFileJournalWriterResult initial =
                DataRelativePathRepairFileJournalWriter
                    .CreateInitial(
                        JournalDirectory,
                        "journal.json",
                        intent
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

        public LinuxFileIncarnationIdentity
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
                opened.Success,
                opened.Error
            );

            using LinuxOpenedChildHandle child =
                Assert.IsType<
                    LinuxOpenedChildHandle
                >(
                    opened.OpenedChild
                );

            LinuxOpenedFileIncarnationResult capture =
                LinuxOpenedFileIncarnation.Capture(
                    child
                );

            Assert.True(
                capture.Success,
                capture.Error
            );

            LinuxFileIncarnationIdentity identity =
                Assert.IsType<
                    LinuxFileIncarnationIdentity
                >(
                    capture.Identity
                );

            return identity with
            {
                PhysicalIdentity =
                    identity.PhysicalIdentity with
                    {
                        LinkCount =
                            0U
                    }
            };
        }

        public LinuxOpenedFileIdentityResult
            FakePreparedIdentity()
        {
            return new LinuxOpenedFileIdentityResult(
                State:
                    LinuxOpenedFileIdentityState.Captured,
                DeviceMajor:
                    8U,
                DeviceMinor:
                    1U,
                Inode:
                    999999UL,
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

        public DataRelativePathRepairFileJournalRecord
            PersistPrepared(
                LinuxOpenedFileIdentityResult preparedIdentity)
        {
            return PersistPrepared(
                SyntheticFileJournalIncarnation.FromPhysical(
                    preparedIdentity
                )
            );
        }

        public DataRelativePathRepairFileJournalRecord
            PersistPrepared(
                LinuxFileIncarnationIdentity preparedIdentity)
        {
            DataRelativePathRepairFileJournalReaderResult current =
                ReadJournal();

            DataRelativePathRepairFileJournalRecord prepared =
                RequireRecord(
                    DataRelativePathRepairFileJournal.MarkPrepared(
                        current.Record!,
                        preparedIdentity,
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
            return PersistRollbackRequested(
                SyntheticFileJournalIncarnation.FromPhysical(
                    preparedIdentity
                )
            );
        }

        public DataRelativePathRepairFileJournalRecord
            PersistRollbackRequested(
                LinuxFileIncarnationIdentity preparedIdentity)
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
                        current.JournalIncarnationIdentity!,
                        next
                    );

            Assert.True(
                write.Success,
                write.Error
            );
        }

        private DataRelativePathRepairFileJournalRecord
            Intent()
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
