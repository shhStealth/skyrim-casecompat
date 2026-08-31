using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairFileForwardRecoveryActionTests
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
    public void IntentMissing_Recover_PublishesAndPersistsApplied()
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

        DataRelativePathRepairFileForwardRecovery result =
            DataRelativePathRepairFileForwardRecoveryAction
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
            DataRelativePathRepairFileRecoveryState
                .IntentDestinationMissing,
            result.Classification!.State
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
            DataRelativePathRepairFileJournalState.Applied,
            after.Record!.State
        );

        Assert.Equal(
            2,
            after.Record.Revision
        );
    }

    [Fact]
    public void PreparedMissing_Recover_ReplacesDeadIdentityBeforePublication()
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

        LinuxOpenedFileIdentityResult oldIdentity =
            fixture.FakePreparedIdentity();

        fixture.PersistPrepared(
            oldIdentity
        );

        DataRelativePathRepairFileForwardRecovery result =
            DataRelativePathRepairFileForwardRecoveryAction
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
            DataRelativePathRepairFileRecoveryState
                .PreparedDestinationMissing,
            result.Classification!.State
        );

        Assert.NotNull(
            result.PreparedIdentity
        );

        Assert.False(
            oldIdentity.SameObjectAs(
                result.PreparedIdentity!
            )
        );

        Assert.Equal(
            0U,
            result.PreparedIdentity!.LinkCount
        );

        DataRelativePathRepairFileJournalReaderResult after =
            fixture.ReadJournal();

        Assert.Equal(
            DataRelativePathRepairFileJournalState.Applied,
            after.Record!.State
        );

        Assert.Equal(
            3,
            after.Record.Revision
        );

        Assert.True(
            result.PreparedIdentity.SameObjectAs(
                after.Record.PreparedFileIdentity!
            )
        );

        using LinuxNoFollowPathHandle parent =
            fixture.OpenParent();

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

        LinuxOpenedFileIdentityResult finalIdentity =
            LinuxOpenedFileIdentity.Capture(
                child
            );

        Assert.True(
            after.Record.PreparedFileIdentity!
                .SameObjectAs(
                    finalIdentity
                )
        );
    }

    [Fact]
    public void PreparedMatchingDestination_IsNotReprepared()
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

        DataRelativePathRepairFileJournalReaderResult before =
            fixture.ReadJournal();

        DataRelativePathRepairFileForwardRecovery result =
            DataRelativePathRepairFileForwardRecoveryAction
                .Recover(
                    fixture.JournalDirectory,
                    "journal.json",
                    T0.AddSeconds(10)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileForwardRecoveryState
                .RecoveryStateNotEligible,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .PreparedDestinationMatches,
            result.Classification!.State
        );

        DataRelativePathRepairFileJournalReaderResult after =
            fixture.ReadJournal();

        Assert.Equal(
            before.Record,
            after.Record
        );

        Assert.Equal(
            "source",
            File.ReadAllText(
                fixture.DestinationPath
            )
        );
    }

    [Fact]
    public void HeldJournalLock_PreventsForwardRecovery()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

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
            Assert.IsType<
                LinuxExclusiveDirectoryLockLease
            >(
                held.Lease
            );

        DataRelativePathRepairFileForwardRecovery result =
            DataRelativePathRepairFileForwardRecoveryAction
                .Recover(
                    fixture.JournalDirectory,
                    "journal.json",
                    T0.AddSeconds(10)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileForwardRecoveryState
                .LockUnavailable,
            result.State
        );

        Assert.Equal(
            LinuxExclusiveDirectoryLockState.AlreadyLocked,
            result.LockState
        );

        Assert.False(
            File.Exists(
                fixture.DestinationPath
            )
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalState
                .IntentRecorded,
            fixture.ReadJournal().Record!.State
        );
    }

    [Fact]
    public void ChangedSource_DoesNotReprepareOrPublish()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.PersistIntent();

        DataRelativePathRepairFileJournalReaderResult before =
            fixture.ReadJournal();

        fixture.OverwriteSourceInPlace(
            "mutant"
        );

        DataRelativePathRepairFileForwardRecovery result =
            DataRelativePathRepairFileForwardRecoveryAction
                .Recover(
                    fixture.JournalDirectory,
                    "journal.json",
                    T0.AddSeconds(10)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileForwardRecoveryState
                .SourceValidationFailed,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairSourceValidationState
                .HashChanged,
            result.SourceValidation!.State
        );

        Assert.False(
            File.Exists(
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
            before.JournalIdentity!.SameObjectAs(
                after.JournalIdentity!
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
                    "casecompat-forward-recovery-tests",
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

        public bool SupportsUnnamedFiles()
        {
            using LinuxNoFollowPathHandle parent =
                OpenParent();

            LinuxCreateUnnamedFileAtResult probe =
                LinuxCreateUnnamedFileAt.Create(
                    parent
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

        public void PersistIntent()
        {
            DataRelativePathRepairFileJournalWriterResult write =
                DataRelativePathRepairFileJournalWriter
                    .CreateInitial(
                        JournalDirectory,
                        "journal.json",
                        CreateIntent()
                    );

            Assert.True(
                write.Success,
                write.Error
            );
        }

        public void PersistPrepared(
            LinuxOpenedFileIdentityResult identity)
        {
            PersistPrepared(
                SyntheticFileJournalIncarnation.FromPhysical(
                    identity
                )
            );
        }

        public void PersistPrepared(
            LinuxFileIncarnationIdentity identity)
        {
            PersistIntent();

            DataRelativePathRepairFileJournalReaderResult current =
                ReadJournal();

            DataRelativePathRepairFileJournalTransitionResult
                transition =
                    DataRelativePathRepairFileJournal
                        .MarkPrepared(
                            current.Record!,
                            identity,
                            T0.AddSeconds(1)
                        );

            Assert.True(
                transition.Success,
                transition.Error
            );

            DataRelativePathRepairFileJournalWriterResult write =
                DataRelativePathRepairFileJournalWriter
                    .ReplaceExisting(
                        JournalDirectory,
                        "journal.json",
                        current.JournalIdentity!,
                        transition.Record!
                    );

            Assert.True(
                write.Success,
                write.Error
            );
        }

        public void WriteDestination(
            string text)
        {
            File.WriteAllText(
                DestinationPath,
                text
            );
        }

        public void OverwriteSourceInPlace(
            string text)
        {
            byte[] bytes =
                System.Text.Encoding.UTF8.GetBytes(
                    text
                );

            using FileStream stream =
                new(
                    SourcePath,
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

        public LinuxFileIncarnationIdentity
            PreparedIdentityFromDestination()
        {
            using LinuxNoFollowPathHandle parent =
                OpenParent();

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

        public LinuxNoFollowPathHandle OpenParent()
        {
            return OpenRoot(
                ParentPath
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

        private DataRelativePathRepairFileJournalRecord
            CreateIntent()
        {
            DataRelativePathRepairSourceSnapshot source =
                CaptureSourceSnapshot();

            DataRelativePathRepairDestinationParentSnapshot parent =
                CaptureParentSnapshot();

            DataRelativePathRepairFileJournalTransitionResult result =
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
                    source,
                    parent
                );

            Assert.True(
                result.Success,
                result.Error
            );

            return result.Record!;
        }

        private DataRelativePathRepairSourceSnapshot
            CaptureSourceSnapshot()
        {
            LinuxNoFollowPathOpenResult opened =
                LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                    DataRoot,
                    "source.nif"
                );

            Assert.True(
                opened.Success
            );

            using LinuxNoFollowPathHandle source =
                Assert.IsType<
                    LinuxNoFollowPathHandle
                >(
                    opened.OpenedPath
                );

            LinuxOpenedFileSnapshotResult snapshot =
                LinuxOpenedFileSnapshot.Capture(
                    source
                );

            Assert.True(
                snapshot.Success,
                snapshot.Error
            );

            return new DataRelativePathRepairSourceSnapshot(
                PhysicalPath:
                    SourcePath,
                Size:
                    snapshot.Size!.Value,
                Sha256:
                    snapshot.Sha256!,
                Identity:
                    snapshot.Identity!
            );
        }

        private
            DataRelativePathRepairDestinationParentSnapshot
            CaptureParentSnapshot()
        {
            using LinuxNoFollowPathHandle parent =
                OpenParent();

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
