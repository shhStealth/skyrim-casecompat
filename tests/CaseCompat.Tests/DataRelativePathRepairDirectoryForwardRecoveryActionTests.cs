using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairDirectoryForwardRecoveryActionTests
{
    private static readonly DateTimeOffset T0 =
        new(
            2026,
            8,
            31,
            4,
            0,
            0,
            TimeSpan.Zero
        );

    [Fact]
    public void PreparedStaging_Recover_PublishesAndPersistsApplied()
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

        fixture.PersistPreparedWithStaging();

        DataRelativePathRepairDirectoryForwardRecovery result =
            DataRelativePathRepairDirectoryForwardRecoveryAction
                .Recover(
                    fixture.JournalDirectory,
                    "journal.json",
                    T0.AddSeconds(2)
                );

        if (
            result.Publication?.State ==
            LinuxPublishOwnedDirectoryAtState.NoReplaceUnsupported)
        {
            return;
        }

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryForwardRecoveryState
                .AppliedDurably,
            result.State
        );

        Assert.False(
            Directory.Exists(
                fixture.PathFor(
                    ".stage"
                )
            )
        );

        Assert.True(
            Directory.Exists(
                fixture.PathFor(
                    "Final"
                )
            )
        );

        DataRelativePathRepairDirectoryJournalReaderResult after =
            fixture.ReadJournal();

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.Applied,
            after.Record!.State
        );

        Assert.Equal(
            2,
            after.Record.Revision
        );

        Assert.NotNull(
            result.Publication
        );

        Assert.True(
            result.Publication!.Success
        );

        Assert.NotNull(
            result.DestinationParentSync
        );

        Assert.True(
            result.DestinationParentSync!.Success
        );
    }

    [Fact]
    public void PreparedBothMissing_DoesNotCreateReplacementDirectory()
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

        fixture.PersistPrepared(
            SyntheticDirectoryJournalIncarnation.FromPhysical(
                fixture.SyntheticIdentity()
            )
        );

        DataRelativePathRepairDirectoryForwardRecovery result =
            DataRelativePathRepairDirectoryForwardRecoveryAction
                .Recover(
                    fixture.JournalDirectory,
                    "journal.json",
                    T0.AddSeconds(2)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryForwardRecoveryState
                .RecoveryStateNotEligible,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .PreparedBothMissing,
            result.Classification!.State
        );

        Assert.False(
            Directory.Exists(
                fixture.PathFor(
                    ".stage"
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
            DataRelativePathRepairDirectoryJournalState.Prepared,
            after.Record!.State
        );

        Assert.Equal(
            1,
            after.Record.Revision
        );
    }

    [Fact]
    public void AlreadyPublishedPreparedDirectory_IsLeftForJournalReconciler()
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

        fixture.PersistPreparedWithStaging();

        Directory.Move(
            fixture.PathFor(
                ".stage"
            ),
            fixture.PathFor(
                "Final"
            )
        );

        DataRelativePathRepairDirectoryForwardRecovery result =
            DataRelativePathRepairDirectoryForwardRecoveryAction
                .Recover(
                    fixture.JournalDirectory,
                    "journal.json",
                    T0.AddSeconds(2)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryForwardRecoveryState
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

        DataRelativePathRepairDirectoryJournalReaderResult after =
            fixture.ReadJournal();

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.Prepared,
            after.Record!.State
        );
    }

    [Fact]
    public void PreparedConflict_DoesNotPublishOrOverwriteFinal()
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

        fixture.PersistPreparedWithStaging();

        fixture.CreateDirectory(
            "Final"
        );

        string marker =
            Path.Combine(
                fixture.PathFor(
                    "Final"
                ),
                "existing.txt"
            );

        File.WriteAllText(
            marker,
            "existing"
        );

        DataRelativePathRepairDirectoryForwardRecovery result =
            DataRelativePathRepairDirectoryForwardRecoveryAction
                .Recover(
                    fixture.JournalDirectory,
                    "journal.json",
                    T0.AddSeconds(2)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryForwardRecoveryState
                .RecoveryStateNotEligible,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .PreparedConflict,
            result.Classification!.State
        );

        Assert.True(
            Directory.Exists(
                fixture.PathFor(
                    ".stage"
                )
            )
        );

        Assert.Equal(
            "existing",
            File.ReadAllText(
                marker
            )
        );
    }

    [Fact]
    public void HeldJournalLock_PreventsDirectoryForwardRecovery()
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

        fixture.PersistPreparedWithStaging();

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

        DataRelativePathRepairDirectoryForwardRecovery result =
            DataRelativePathRepairDirectoryForwardRecoveryAction
                .Recover(
                    fixture.JournalDirectory,
                    "journal.json",
                    T0.AddSeconds(2)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryForwardRecoveryState
                .LockUnavailable,
            result.State
        );

        Assert.True(
            Directory.Exists(
                fixture.PathFor(
                    ".stage"
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
    }

    private sealed class Fixture
        : IDisposable
    {
        public Fixture()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-directory-forward-recovery-tests",
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

        public void PersistPreparedWithStaging()
        {
            CreateDirectory(
                ".stage"
            );

            PersistPrepared(
                CaptureDirectoryIdentity(
                    ".stage"
                )
            );
        }

        public void PersistPrepared(
            LinuxDirectoryIncarnationIdentity identity)
        {
            DataRelativePathRepairDirectoryJournalRecord intent =
                CreateIntent();

            DataRelativePathRepairDirectoryJournalTransitionResult
                preparedResult =
                    DataRelativePathRepairDirectoryJournal.MarkPrepared(
                        intent,
                        ".stage",
                        identity,
                        T0.AddSeconds(1)
                    );

            Assert.True(
                preparedResult.Success,
                preparedResult.Error
            );

            DataRelativePathRepairDirectoryJournalRecord prepared =
                Assert.IsType<
                    DataRelativePathRepairDirectoryJournalRecord
                >(
                    preparedResult.Record
                );

            DataRelativePathRepairDirectoryJournalWriterResult initial =
                DataRelativePathRepairDirectoryJournalWriter
                    .CreateInitial(
                        JournalDirectory,
                        "journal.json",
                        intent
                    );

            Assert.True(
                initial.Success,
                initial.Error
            );

            DataRelativePathRepairDirectoryJournalReaderResult before =
                ReadJournal();

            DataRelativePathRepairDirectoryJournalWriterResult update =
                DataRelativePathRepairDirectoryJournalWriter
                    .ReplaceExisting(
                        JournalDirectory,
                        "journal.json",
                        before.JournalIncarnationIdentity!,
                        prepared
                    );

            Assert.True(
                update.Success,
                update.Error
            );
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

        public LinuxFileIdentityResult SyntheticIdentity()
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
                    ulong.MaxValue - 300UL,
                LinkCount:
                    2U,
                MountId:
                    parent.Identity.MountId,
                Error:
                    null
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
                        CaptureParentSnapshot()
                    ,
                        LiveDirectoryJournalIncarnation.Capture(
                            Parent
                        ));

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
