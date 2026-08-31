using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairFileRollbackRequestActionTests
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
    public void TrustedDataRootMismatch_DoesNotRequestRollback()
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

        fixture.PersistApplied(
            fixture.PreparedIdentityFromDestination()
        );

        DataRelativePathRepairFileJournalReaderResult before =
            fixture.ReadJournal();

        string trustedDataRoot =
            Path.Combine(
                fixture.RootPath,
                "OtherData"
            );

        DataRelativePathRepairFileRollbackRequest result =
            DataRelativePathRepairFileRollbackRequestAction
                .Request(
                    fixture.JournalDirectory,
                    "journal.json",
                    trustedDataRoot,
                    T0.AddSeconds(10)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileRollbackRequestState
                .RecoveryStateNotEligible,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .DataRootMismatch,
            result.Classification!.State
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
            before.Record,
            after.Record
        );
    }

    [Fact]
    public void AppliedMatchingDestination_PersistsRollbackRequested()
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

        fixture.PersistApplied(
            fixture.PreparedIdentityFromDestination()
        );

        DataRelativePathRepairFileRollbackRequest result =
            DataRelativePathRepairFileRollbackRequestAction
                .Request(
                    fixture.JournalDirectory,
                    "journal.json",
                    fixture.DataRoot,
                    T0.AddSeconds(10)
                );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            DataRelativePathRepairFileRollbackRequestState
                .RequestedDurably,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .AppliedDestinationMatches,
            result.Classification!.State
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

        Assert.Equal(
            3,
            after.Record.Revision
        );
    }

    [Fact]
    public void AppliedMissingDestination_DoesNotRequestRollback()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.PersistApplied(
            fixture.FakePreparedIdentity()
        );

        DataRelativePathRepairFileJournalReaderResult before =
            fixture.ReadJournal();

        DataRelativePathRepairFileRollbackRequest result =
            DataRelativePathRepairFileRollbackRequestAction
                .Request(
                    fixture.JournalDirectory,
                    "journal.json",
                    fixture.DataRoot,
                    T0.AddSeconds(10)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileRollbackRequestState
                .RecoveryStateNotEligible,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .AppliedDestinationMissing,
            result.Classification!.State
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
    public void AppliedMutatedSameInode_DoesNotRequestRollback()
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

        fixture.PersistApplied(
            fixture.PreparedIdentityFromDestination()
        );

        fixture.OverwriteDestinationInPlace(
            "mutant"
        );

        DataRelativePathRepairFileRollbackRequest result =
            DataRelativePathRepairFileRollbackRequestAction
                .Request(
                    fixture.JournalDirectory,
                    "journal.json",
                    fixture.DataRoot,
                    T0.AddSeconds(10)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileRollbackRequestState
                .RecoveryStateNotEligible,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .AppliedDestinationConflict,
            result.Classification!.State
        );

        Assert.Equal(
            "mutant",
            File.ReadAllText(
                fixture.DestinationPath
            )
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalState.Applied,
            fixture.ReadJournal().Record!.State
        );
    }

    [Fact]
    public void HeldJournalLock_PreventsRollbackRequest()
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

        fixture.PersistApplied(
            fixture.PreparedIdentityFromDestination()
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

        DataRelativePathRepairFileRollbackRequest result =
            DataRelativePathRepairFileRollbackRequestAction
                .Request(
                    fixture.JournalDirectory,
                    "journal.json",
                    fixture.DataRoot,
                    T0.AddSeconds(10)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileRollbackRequestState
                .LockUnavailable,
            result.State
        );

        Assert.Equal(
            LinuxExclusiveDirectoryLockState.AlreadyLocked,
            result.LockState
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalState.Applied,
            fixture.ReadJournal().Record!.State
        );

        Assert.True(
            File.Exists(
                fixture.DestinationPath
            )
        );
    }

    [Fact]
    public void
        ExpectedJournalIncarnationGuards_RejectBeforeRollbackRequest()
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

        DataRelativePathRepairFileRollbackRequest invalid =
            DataRelativePathRepairFileRollbackRequestAction.Request(
                fixture.JournalDirectory,
                "missing-journal.json",
                fixture.DataRoot,
                T0.AddSeconds(20),
                invalidExpected
            );

        Assert.False(
            invalid.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileRollbackRequestState
                .InvalidExpectedJournalIdentity,
            invalid.State
        );

        Assert.False(
            invalid.LockState.HasValue
        );

        Assert.Null(
            invalid.JournalRead
        );

        Assert.Null(
            invalid.Classification
        );

        Assert.Null(
            invalid.JournalTransition
        );

        Assert.Null(
            invalid.JournalWrite
        );

        fixture.WriteDestination(
            "source"
        );

        fixture.PersistApplied(
            fixture.PreparedIdentityFromDestination()
        );

        DataRelativePathRepairFileJournalReaderResult before =
            fixture.ReadJournal();

        LinuxFileIncarnationIdentity actual =
            Assert.IsType<LinuxFileIncarnationIdentity>(
                before.JournalIncarnationIdentity
            );

        uint differentGeneration =
            actual.InodeGeneration == uint.MaxValue
                ? 0U
                : actual.InodeGeneration + 1U;

        var changedExpected =
            new LinuxFileIncarnationIdentity(
                actual.PhysicalIdentity,
                differentGeneration
            );

        Assert.True(
            changedExpected.Success
        );

        Assert.False(
            actual.SameIncarnationAs(
                changedExpected
            )
        );

        DataRelativePathRepairFileRollbackRequest changed =
            DataRelativePathRepairFileRollbackRequestAction.Request(
                fixture.JournalDirectory,
                "journal.json",
                fixture.DataRoot,
                T0.AddSeconds(21),
                changedExpected
            );

        Assert.False(
            changed.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileRollbackRequestState
                .JournalIncarnationChanged,
            changed.State
        );

        Assert.True(
            changed.LockState.HasValue
        );

        Assert.NotNull(
            changed.JournalRead
        );

        Assert.Null(
            changed.Classification
        );

        Assert.Null(
            changed.JournalTransition
        );

        Assert.Null(
            changed.JournalWrite
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
            before.Record,
            after.Record
        );

        Assert.True(
            actual.SameIncarnationAs(
                after.JournalIncarnationIdentity!
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
                    "casecompat-rollback-request-tests",
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
                    uint.MaxValue,
                DeviceMinor:
                    uint.MaxValue,
                Inode:
                    ulong.MaxValue - 1,
                LinkCount:
                    0U,
                MountId:
                    ulong.MaxValue - 2,
                Errno:
                    null,
                Error:
                    null
            );
        }

        public void PersistApplied(
            LinuxOpenedFileIdentityResult preparedIdentity)
        {
            PersistApplied(
                SyntheticFileJournalIncarnation.FromPhysical(
                    preparedIdentity
                )
            );
        }

        public void PersistApplied(
            LinuxFileIncarnationIdentity preparedIdentity)
        {
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

            DataRelativePathRepairFileJournalRecord prepared =
                RequireRecord(
                    DataRelativePathRepairFileJournal
                        .MarkPrepared(
                            intent,
                            preparedIdentity,
                            T0.AddSeconds(1)
                        )
                );

            DataRelativePathRepairFileJournalWriterResult
                preparedWrite =
                    DataRelativePathRepairFileJournalWriter
                        .ReplaceExisting(
                            JournalDirectory,
                            "journal.json",
                            initial.WrittenJournalIncarnationIdentity!,
                            prepared
                        );

            Assert.True(
                preparedWrite.Success,
                preparedWrite.Error
            );

            DataRelativePathRepairFileJournalRecord applied =
                RequireRecord(
                    DataRelativePathRepairFileJournal
                        .MarkApplied(
                            prepared,
                            T0.AddSeconds(2)
                        )
                );

            DataRelativePathRepairFileJournalWriterResult
                appliedWrite =
                    DataRelativePathRepairFileJournalWriter
                        .ReplaceExisting(
                            JournalDirectory,
                            "journal.json",
                            preparedWrite.WrittenJournalIncarnationIdentity!,
                            applied
                        );

            Assert.True(
                appliedWrite.Success,
                appliedWrite.Error
            );
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
            DataRelativePathRepairFileJournalRecord RequireRecord(
                DataRelativePathRepairFileJournalTransitionResult
                    result)
        {
            Assert.True(
                result.Success,
                result.Error
            );

            return result.Record!;
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

            return result.OpenedPath!;
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
