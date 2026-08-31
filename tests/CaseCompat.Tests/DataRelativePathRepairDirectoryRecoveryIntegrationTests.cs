using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairDirectoryRecoveryIntegrationTests
{
    private static readonly DateTimeOffset T0 =
        new(
            2026,
            8,
            31,
            6,
            0,
            0,
            TimeSpan.Zero
        );

    [Fact]
    public void MissingPreparedDirectory_ReprepareThenPublish_ReachesApplied()
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

        /*
         * Begin with a durable Prepared journal whose historical
         * staging inode no longer exists.
         */
        fixture.PersistMissingPrepared();

        DataRelativePathRepairDirectoryJournalReaderResult initial =
            fixture.ReadJournal();

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.Prepared,
            initial.Record!.State
        );

        Assert.Equal(
            1,
            initial.Record.Revision
        );

        Guid journalId =
            initial.Record.JournalId;

        DateTimeOffset createdUtc =
            initial.Record.CreatedUtc;

        DataRelativePathRepairDirectoryRecoveryClassification
            initialClassification =
                DataRelativePathRepairDirectoryRecoveryClassifier
                    .Classify(
                        initial.Record
                    );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .PreparedBothMissing,
            initialClassification.State
        );

        /*
         * Recovery call #1:
         *
         * create a fresh staging directory,
         * capture its physical identity,
         * fsync the parent,
         * durably Reprepare the journal,
         * then STOP.
         */
        DataRelativePathRepairDirectoryReprepareRecovery reprepare =
            DataRelativePathRepairDirectoryReprepareRecoveryAction
                .Recover(
                    fixture.JournalDirectory,
                    "journal.json",
                    T0.AddSeconds(2)
                );

        Assert.True(
            reprepare.Success,
            reprepare.Error
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryReprepareRecoveryState
                .RepreparedDurably,
            reprepare.State
        );

        Assert.NotNull(
            reprepare.FreshStagingChildName
        );

        Assert.NotNull(
            reprepare.Preparation
        );

        Assert.True(
            reprepare.Preparation!.Success
        );

        Assert.False(
            reprepare.UnjournaledStagingEntryMayRemain
        );

        string freshStagingName =
            reprepare.FreshStagingChildName!;

        DataRelativePathRepairDirectoryJournalReaderResult
            afterReprepare =
                fixture.ReadJournal();

        Assert.Equal(
            journalId,
            afterReprepare.Record!.JournalId
        );

        Assert.Equal(
            createdUtc,
            afterReprepare.Record.CreatedUtc
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.Prepared,
            afterReprepare.Record.State
        );

        Assert.Equal(
            2,
            afterReprepare.Record.Revision
        );

        Assert.Equal(
            freshStagingName,
            afterReprepare.Record.PreparedStagingChildName
        );

        Assert.NotNull(
            afterReprepare.Record.PreparedDirectoryIdentity
        );

        LinuxFileIdentityResult repreparedIdentity =
            afterReprepare.Record.PreparedDirectoryIdentity!;

        Assert.True(
            Directory.Exists(
                fixture.PathFor(
                    freshStagingName
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

        DataRelativePathRepairDirectoryRecoveryClassification
            afterReprepareClassification =
                DataRelativePathRepairDirectoryRecoveryClassifier
                    .Classify(
                        afterReprepare.Record
                    );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .PreparedStagingMatchesFinalMissing,
            afterReprepareClassification.State
        );

        /*
         * Recovery call #2:
         *
         * consume the now-proven Prepared staging state and publish
         * exactly that recorded inode under the final name.
         */
        DataRelativePathRepairDirectoryForwardRecovery forward =
            DataRelativePathRepairDirectoryForwardRecoveryAction
                .Recover(
                    fixture.JournalDirectory,
                    "journal.json",
                    T0.AddSeconds(3)
                );

        if (
            forward.Publication?.State ==
            LinuxPublishOwnedDirectoryAtState.NoReplaceUnsupported)
        {
            return;
        }

        Assert.True(
            forward.Success,
            forward.Error
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryForwardRecoveryState
                .AppliedDurably,
            forward.State
        );

        Assert.NotNull(
            forward.Publication
        );

        Assert.True(
            forward.Publication!.Success
        );

        Assert.NotNull(
            forward.DestinationParentSync
        );

        Assert.True(
            forward.DestinationParentSync!.Success
        );

        Assert.False(
            Directory.Exists(
                fixture.PathFor(
                    freshStagingName
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

        Assert.True(
            Directory.Exists(
                fixture.PathFor(
                    "Final"
                )
            )
        );

        DataRelativePathRepairDirectoryJournalReaderResult final =
            fixture.ReadJournal();

        Assert.Equal(
            journalId,
            final.Record!.JournalId
        );

        Assert.Equal(
            createdUtc,
            final.Record.CreatedUtc
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.Applied,
            final.Record.State
        );

        Assert.Equal(
            3,
            final.Record.Revision
        );

        Assert.Equal(
            freshStagingName,
            final.Record.PreparedStagingChildName
        );

        Assert.Equal(
            repreparedIdentity,
            final.Record.PreparedDirectoryIdentity
        );

        /*
         * renameat2() must have published the same physical
         * directory. The final name must therefore identify the
         * exact inode recorded by the durable re-preparation.
         */
        LinuxFileIdentityResult finalIdentity =
            fixture.CaptureDirectoryIdentity(
                "Final"
            ).PhysicalIdentity;

        AssertSameIdentity(
            repreparedIdentity,
            finalIdentity
        );

        DataRelativePathRepairDirectoryRecoveryClassification
            finalClassification =
                DataRelativePathRepairDirectoryRecoveryClassifier
                    .Classify(
                        final.Record
                    );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .AppliedFinalMatches,
            finalClassification.State
        );

        Assert.True(
            finalClassification.FinalMatchesPreparedIdentity
        );
    }

    private static void AssertSameIdentity(
        LinuxFileIdentityResult expected,
        LinuxFileIdentityResult actual)
    {
        Assert.Equal(
            expected.DeviceMajor,
            actual.DeviceMajor
        );

        Assert.Equal(
            expected.DeviceMinor,
            actual.DeviceMinor
        );

        Assert.Equal(
            expected.Inode,
            actual.Inode
        );

        Assert.Equal(
            expected.MountId,
            actual.MountId
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
                    "casecompat-directory-recovery-integration-tests",
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

        public void PersistMissingPrepared()
        {
            DataRelativePathRepairDirectoryJournalRecord intent =
                CreateIntent();

            DataRelativePathRepairDirectoryJournalTransitionResult
                preparedResult =
                    DataRelativePathRepairDirectoryJournal.MarkPrepared(
                        intent,
                        ".stage",
                        SyntheticDirectoryJournalIncarnation.FromPhysical(

                            SyntheticIdentity()

                        ),
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

            DataRelativePathRepairDirectoryJournalReaderResult current =
                ReadJournal();

            DataRelativePathRepairDirectoryJournalWriterResult update =
                DataRelativePathRepairDirectoryJournalWriter
                    .ReplaceExisting(
                        JournalDirectory,
                        "journal.json",
                        current.JournalIncarnationIdentity!,
                        prepared
                    );

            Assert.True(
                update.Success,
                update.Error
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
                    ulong.MaxValue - 500UL,
                LinkCount:
                    2U,
                MountId:
                    parent.Identity.MountId,
                Error:
                    null
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
