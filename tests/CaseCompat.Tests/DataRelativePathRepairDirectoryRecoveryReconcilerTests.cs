using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairDirectoryRecoveryReconcilerTests
{
    private static readonly DateTimeOffset T0 =
        new(
            2026,
            8,
            31,
            3,
            0,
            0,
            TimeSpan.Zero
        );

    [Fact]
    public void TrustedDataRootMismatch_DoesNotReconcileDirectoryJournal()
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

        DataRelativePathRepairDirectoryJournalRecord intent =
            fixture.CreateIntent();

        fixture.PersistInitial(
            intent
        );

        DataRelativePathRepairDirectoryJournalReaderResult before =
            fixture.ReadJournal();

        string trustedDataRoot =
            Path.Combine(
                fixture.RootPath,
                "OtherData"
            );

        DataRelativePathRepairDirectoryRecoveryReconciliation result =
            DataRelativePathRepairDirectoryRecoveryReconciler
                .Reconcile(
                    fixture.JournalDirectory,
                    "journal.json",
                    before.JournalIncarnationIdentity!,
                    before.Record!,
                    trustedDataRoot,
                    T0.AddSeconds(2)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryReconciliationState
                .NoAutomaticReconciliation,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .DataRootMismatch,
            result.Classification.State
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
            before.Record,
            after.Record
        );
    }

    [Fact]
    public void PreparedPublishedDirectory_PersistsApplied()
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

        fixture.CreateDirectory(
            ".stage"
        );

        LinuxDirectoryIncarnationIdentity identity =
            fixture.CaptureDirectoryIdentity(
                ".stage"
            );

        DataRelativePathRepairDirectoryJournalRecord intent =
            fixture.CreateIntent();

        DataRelativePathRepairDirectoryJournalRecord prepared =
            RequireRecord(
                DataRelativePathRepairDirectoryJournal.MarkPrepared(
                    intent,
                    ".stage",
                    identity,
                    T0.AddSeconds(1)
                )
            );

        fixture.PersistInitial(
            intent
        );

        fixture.PersistReplacement(
            prepared
        );

        Directory.Move(
            fixture.PathFor(
                ".stage"
            ),
            fixture.PathFor(
                "Final"
            )
        );

        DataRelativePathRepairDirectoryJournalReaderResult before =
            fixture.ReadJournal();

        DataRelativePathRepairDirectoryRecoveryReconciliation result =
            DataRelativePathRepairDirectoryRecoveryReconciler
                .Reconcile(
                    fixture.JournalDirectory,
                    "journal.json",
                    before.JournalIncarnationIdentity!,
                    before.Record!,
                    fixture.DataRoot,
                    T0.AddSeconds(2)
                );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryReconciliationState
                .AppliedDurably,
            result.State
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
    }

    [Fact]
    public void RollbackRequestedMissingDirectory_PersistsRolledBack()
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

        DataRelativePathRepairDirectoryJournalRecord intent =
            fixture.CreateIntent();

        DataRelativePathRepairDirectoryJournalRecord prepared =
            RequireRecord(
                DataRelativePathRepairDirectoryJournal.MarkPrepared(
                    intent,
                    ".stage",
                    SyntheticDirectoryJournalIncarnation.FromPhysical(

                        fixture.SyntheticIdentity()

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
                DataRelativePathRepairDirectoryJournal.RequestRollback(
                    applied,
                    T0.AddSeconds(3)
                )
            );

        fixture.PersistInitial(
            intent
        );

        fixture.PersistReplacement(
            prepared
        );

        fixture.PersistReplacement(
            applied
        );

        fixture.PersistReplacement(
            requested
        );

        DataRelativePathRepairDirectoryJournalReaderResult before =
            fixture.ReadJournal();

        DataRelativePathRepairDirectoryRecoveryReconciliation result =
            DataRelativePathRepairDirectoryRecoveryReconciler
                .Reconcile(
                    fixture.JournalDirectory,
                    "journal.json",
                    before.JournalIncarnationIdentity!,
                    before.Record!,
                    fixture.DataRoot,
                    T0.AddSeconds(4)
                );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryReconciliationState
                .RolledBackDurably,
            result.State
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
    }

    [Fact]
    public void PreparedStagingDirectory_DoesNotPublish()
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

        fixture.CreateDirectory(
            ".stage"
        );

        LinuxDirectoryIncarnationIdentity identity =
            fixture.CaptureDirectoryIdentity(
                ".stage"
            );

        DataRelativePathRepairDirectoryJournalRecord intent =
            fixture.CreateIntent();

        DataRelativePathRepairDirectoryJournalRecord prepared =
            RequireRecord(
                DataRelativePathRepairDirectoryJournal.MarkPrepared(
                    intent,
                    ".stage",
                    identity,
                    T0.AddSeconds(1)
                )
            );

        fixture.PersistInitial(
            intent
        );

        fixture.PersistReplacement(
            prepared
        );

        DataRelativePathRepairDirectoryJournalReaderResult before =
            fixture.ReadJournal();

        DataRelativePathRepairDirectoryRecoveryReconciliation result =
            DataRelativePathRepairDirectoryRecoveryReconciler
                .Reconcile(
                    fixture.JournalDirectory,
                    "journal.json",
                    before.JournalIncarnationIdentity!,
                    before.Record!,
                    fixture.DataRoot,
                    T0.AddSeconds(2)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryReconciliationState
                .NoAutomaticReconciliation,
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
    public void RollbackRequestedMatchingDirectory_DoesNotDelete()
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

        fixture.CreateDirectory(
            ".stage"
        );

        LinuxDirectoryIncarnationIdentity identity =
            fixture.CaptureDirectoryIdentity(
                ".stage"
            );

        DataRelativePathRepairDirectoryJournalRecord intent =
            fixture.CreateIntent();

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
            fixture.PathFor(
                ".stage"
            ),
            fixture.PathFor(
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

        DataRelativePathRepairDirectoryJournalRecord requested =
            RequireRecord(
                DataRelativePathRepairDirectoryJournal.RequestRollback(
                    applied,
                    T0.AddSeconds(3)
                )
            );

        fixture.PersistInitial(
            intent
        );

        fixture.PersistReplacement(
            prepared
        );

        fixture.PersistReplacement(
            applied
        );

        fixture.PersistReplacement(
            requested
        );

        DataRelativePathRepairDirectoryJournalReaderResult before =
            fixture.ReadJournal();

        DataRelativePathRepairDirectoryRecoveryReconciliation result =
            DataRelativePathRepairDirectoryRecoveryReconciler
                .Reconcile(
                    fixture.JournalDirectory,
                    "journal.json",
                    before.JournalIncarnationIdentity!,
                    before.Record!,
                    fixture.DataRoot,
                    T0.AddSeconds(4)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryReconciliationState
                .NoAutomaticReconciliation,
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
    public void CurrentJournalReplacedBeforeReconcile_RefusesJournalAdvance()
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

        fixture.CreateDirectory(
            ".stage"
        );

        LinuxDirectoryIncarnationIdentity identity =
            fixture.CaptureDirectoryIdentity(
                ".stage"
            );

        DataRelativePathRepairDirectoryJournalRecord intent =
            fixture.CreateIntent();

        DataRelativePathRepairDirectoryJournalRecord prepared =
            RequireRecord(
                DataRelativePathRepairDirectoryJournal.MarkPrepared(
                    intent,
                    ".stage",
                    identity,
                    T0.AddSeconds(1)
                )
            );

        fixture.PersistInitial(
            intent
        );

        fixture.PersistReplacement(
            prepared
        );

        Directory.Move(
            fixture.PathFor(
                ".stage"
            ),
            fixture.PathFor(
                "Final"
            )
        );

        DataRelativePathRepairDirectoryJournalReaderResult stale =
            fixture.ReadJournal();

        DataRelativePathRepairDirectoryJournalRecord alreadyApplied =
            RequireRecord(
                DataRelativePathRepairDirectoryJournal.MarkApplied(
                    prepared,
                    T0.AddSeconds(2)
                )
            );

        fixture.PersistReplacement(
            alreadyApplied
        );

        DataRelativePathRepairDirectoryRecoveryReconciliation result =
            DataRelativePathRepairDirectoryRecoveryReconciler
                .Reconcile(
                    fixture.JournalDirectory,
                    "journal.json",
                    stale.JournalIncarnationIdentity!,
                    stale.Record!,
                    fixture.DataRoot,
                    T0.AddSeconds(3)
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryReconciliationState
                .JournalWriteFailed,
            result.State
        );

        Assert.NotNull(
            result.JournalWrite
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalWriteState
                .CurrentJournalIdentityMismatch,
            result.JournalWrite!.State
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

    private sealed class Fixture
        : IDisposable
    {
        public Fixture()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-directory-reconciler-tests",
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
                    ulong.MaxValue - 200UL,
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

            return RequireRecord(
                result
            );
        }

        public void PersistInitial(
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

        public void PersistReplacement(
            DataRelativePathRepairDirectoryJournalRecord record)
        {
            DataRelativePathRepairDirectoryJournalReaderResult current =
                ReadJournal();

            DataRelativePathRepairDirectoryJournalWriterResult result =
                DataRelativePathRepairDirectoryJournalWriter
                    .ReplaceExisting(
                        JournalDirectory,
                        "journal.json",
                        current.JournalIncarnationIdentity!,
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
