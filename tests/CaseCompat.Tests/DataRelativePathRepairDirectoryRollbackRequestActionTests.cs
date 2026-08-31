using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairDirectoryRollbackRequestActionTests
{
    private static readonly DateTimeOffset T0 =
        new(
            2026,
            8,
            31,
            8,
            0,
            0,
            TimeSpan.Zero
        );

    [Fact]
    public void AppliedMatchingDirectory_PersistsRollbackRequested()
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

        fixture.PersistAppliedWithFinal();

        DataRelativePathRepairDirectoryRollbackRequest result =
            DataRelativePathRepairDirectoryRollbackRequestAction
                .Request(
                    fixture.JournalDirectory,
                    "journal.json",
                    T0.AddSeconds(3)
                );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRollbackRequestState
                .RequestedDurably,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .AppliedFinalMatches,
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
            DataRelativePathRepairDirectoryJournalState
                .RollbackRequested,
            after.Record!.State
        );

        Assert.Equal(
            3,
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
                .RollbackRequestedFinalMatches,
            classification.State
        );
    }

    [Fact]
    public void AppliedMissingDirectory_DoesNotRequestRollback()
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

        fixture.PersistAppliedMissingFinal();

        DataRelativePathRepairDirectoryRollbackRequest result =
            DataRelativePathRepairDirectoryRollbackRequestAction
                .Request(
                    fixture.JournalDirectory,
                    "journal.json",
                    T0.AddSeconds(3)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRollbackRequestState
                .RecoveryStateNotEligible,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .AppliedFinalMissing,
            result.Classification!.State
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
    }

    [Fact]
    public void AppliedReplacedDirectory_DoesNotRequestRollback()
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

        fixture.PersistAppliedWithFinal();

        Directory.Delete(
            fixture.PathFor(
                "Final"
            )
        );

        Directory.CreateDirectory(
            fixture.PathFor(
                "Final"
            )
        );

        DataRelativePathRepairDirectoryRollbackRequest result =
            DataRelativePathRepairDirectoryRollbackRequestAction
                .Request(
                    fixture.JournalDirectory,
                    "journal.json",
                    T0.AddSeconds(3)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRollbackRequestState
                .RecoveryStateNotEligible,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .AppliedConflict,
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
            DataRelativePathRepairDirectoryJournalState.Applied,
            after.Record!.State
        );
    }

    [Fact]
    public void HeldJournalLock_PreventsDirectoryRollbackRequest()
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

        fixture.PersistAppliedWithFinal();

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

        DataRelativePathRepairDirectoryRollbackRequest result =
            DataRelativePathRepairDirectoryRollbackRequestAction
                .Request(
                    fixture.JournalDirectory,
                    "journal.json",
                    T0.AddSeconds(3)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRollbackRequestState
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

        DataRelativePathRepairDirectoryJournalReaderResult after =
            fixture.ReadJournal();

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.Applied,
            after.Record!.State
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
                    "casecompat-directory-rollback-request-tests",
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

        public void PersistAppliedWithFinal()
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

            DataRelativePathRepairDirectoryJournalRecord applied =
                RequireRecord(
                    DataRelativePathRepairDirectoryJournal.MarkApplied(
                        prepared,
                        T0.AddSeconds(2)
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
        }

        public void PersistAppliedMissingFinal()
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

            PersistInitial(
                intent
            );

            PersistReplacement(
                prepared
            );

            PersistReplacement(
                applied
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
                    ulong.MaxValue - 700UL,
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
            DataRelativePathRepairDirectoryJournalReaderResult current =
                ReadJournal();

            DataRelativePathRepairDirectoryJournalWriterResult result =
                DataRelativePathRepairDirectoryJournalWriter
                    .ReplaceExisting(
                        JournalDirectory,
                        "journal.json",
                        current.JournalIdentity!,
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
