using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairFileLifecycleIntegrationTests
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
    public void FullLifecycle_ExecuteRequestRollbackRecover_PreservesTransactionEvidence()
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
         * Start with one real revision-zero intent derived from
         * the actual source and destination-parent filesystem
         * evidence.
         */
        DataRelativePathRepairFileJournalRecord intent =
            fixture.CreateIntent();

        Guid journalId =
            intent.JournalId;

        Assert.Equal(
            0,
            intent.Revision
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalState.IntentRecorded,
            intent.State
        );

        Assert.False(
            File.Exists(
                fixture.DestinationPath
            )
        );

        /*
         * Forward execution:
         *
         * IntentRecorded r0
         *   -> Prepared r1
         *   -> Applied r2
         */
        DataRelativePathRepairFileExecution execution =
            DataRelativePathRepairFileExecutor.Execute(
                fixture.JournalDirectory,
                "journal.json",
                intent,
                T0.AddSeconds(10)
            );

        Assert.True(
            execution.Success,
            execution.Error
        );

        Assert.Equal(
            DataRelativePathRepairFileExecutionState
                .AppliedDurably,
            execution.State
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

        Assert.NotNull(
            execution.PreparedIdentity
        );

        LinuxOpenedFileIdentityResult preparedIdentity =
            execution.PreparedIdentity!;

        Assert.True(
            preparedIdentity.Success
        );

        Assert.Equal(
            0U,
            preparedIdentity.LinkCount
        );

        DataRelativePathRepairFileJournalReaderResult
            appliedRead =
                fixture.ReadJournal();

        DataRelativePathRepairFileJournalRecord applied =
            appliedRead.Record!;

        Assert.Equal(
            journalId,
            applied.JournalId
        );

        Assert.Equal(
            2,
            applied.Revision
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalState.Applied,
            applied.State
        );

        Assert.NotNull(
            applied.PreparedFileIdentity
        );

        Assert.True(
            preparedIdentity.SameObjectAs(
                applied.PreparedFileIdentity!
            )
        );

        /*
         * Prove the durable Prepared identity describes the
         * actual published destination inode.
         */
        using (
            LinuxNoFollowPathHandle parent =
                fixture.OpenParent())
        {
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

            LinuxOpenedFileIdentityResult liveIdentity =
                LinuxOpenedFileIdentity.Capture(
                    child
                );

            Assert.True(
                liveIdentity.Success,
                liveIdentity.Error
            );

            Assert.True(
                preparedIdentity.SameObjectAs(
                    liveIdentity
                )
            );

            Assert.True(
                liveIdentity.LinkCount >=
                1U
            );
        }

        /*
         * Rollback request:
         *
         * Applied r2
         *   -> RollbackRequested r3
         *
         * This step must not remove or modify the asset.
         */
        DataRelativePathRepairFileRollbackRequest request =
            DataRelativePathRepairFileRollbackRequestAction
                .Request(
                    fixture.JournalDirectory,
                    "journal.json",
                    T0.AddSeconds(20)
                );

        Assert.True(
            request.Success,
            request.Error
        );

        Assert.Equal(
            DataRelativePathRepairFileRollbackRequestState
                .RequestedDurably,
            request.State
        );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .AppliedDestinationMatches,
            request.Classification!.State
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

        DataRelativePathRepairFileJournalReaderResult
            requestedRead =
                fixture.ReadJournal();

        DataRelativePathRepairFileJournalRecord requested =
            requestedRead.Record!;

        Assert.Equal(
            journalId,
            requested.JournalId
        );

        Assert.Equal(
            3,
            requested.Revision
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalState
                .RollbackRequested,
            requested.State
        );

        Assert.NotNull(
            requested.PreparedFileIdentity
        );

        Assert.True(
            preparedIdentity.SameObjectAs(
                requested.PreparedFileIdentity!
            )
        );

        /*
         * Destructive recovery:
         *
         * RollbackRequested r3
         *   -> identity/content revalidation
         *   -> one identity-gated unlink
         *   -> parent fsync
         *   -> RolledBack r4
         */
        DataRelativePathRepairFileRollbackRecovery recovery =
            DataRelativePathRepairFileRollbackRecoveryAction
                .Recover(
                    fixture.JournalDirectory,
                    "journal.json",
                    T0.AddSeconds(30)
                );

        Assert.True(
            recovery.Success,
            recovery.Error
        );

        Assert.Equal(
            DataRelativePathRepairFileRollbackRecoveryState
                .RolledBackDurably,
            recovery.State
        );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .RollbackRequestedDestinationMatches,
            recovery.Classification!.State
        );

        Assert.NotNull(
            recovery.RemoveResult
        );

        Assert.Equal(
            LinuxRemoveOwnedFileAtState.Removed,
            recovery.RemoveResult!.State
        );

        Assert.NotNull(
            recovery.RemoveResult.ActualIdentity
        );

        /*
         * The actual inode inspected immediately before unlink
         * must be the same object recorded while the repair was
         * still an anonymous Prepared file.
         */
        Assert.True(
            preparedIdentity.SameObjectAs(
                recovery.RemoveResult.ActualIdentity!
                    .PhysicalIdentity
            )
        );

        Assert.False(
            File.Exists(
                fixture.DestinationPath
            )
        );

        DataRelativePathRepairFileJournalReaderResult
            rolledBackRead =
                fixture.ReadJournal();

        DataRelativePathRepairFileJournalRecord rolledBack =
            rolledBackRead.Record!;

        Assert.Equal(
            journalId,
            rolledBack.JournalId
        );

        Assert.Equal(
            4,
            rolledBack.Revision
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalState.RolledBack,
            rolledBack.State
        );

        Assert.True(
            rolledBack.IsTerminal
        );

        /*
         * RolledBack retains the historical Prepared identity.
         * That evidence is not reused as a live-file claim; it is
         * retained as transaction history.
         */
        Assert.NotNull(
            rolledBack.PreparedFileIdentity
        );

        Assert.True(
            preparedIdentity.SameObjectAs(
                rolledBack.PreparedFileIdentity!
            )
        );

        DataRelativePathRepairFileRecoveryClassification
            finalClassification =
                DataRelativePathRepairFileRecoveryClassifier
                    .Classify(
                        rolledBack
                    );

        Assert.Equal(
            DataRelativePathRepairFileRecoveryState
                .RolledBackDestinationMissing,
            finalClassification.State
        );

        /*
         * Immutable transaction evidence also survives every
         * journal revision.
         */
        Assert.Equal(
            intent.Operation,
            rolledBack.Operation
        );

        Assert.Equal(
            intent.SourceSnapshot,
            rolledBack.SourceSnapshot
        );

        Assert.Equal(
            intent.DestinationParentSnapshot,
            rolledBack.DestinationParentSnapshot
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
                    "casecompat-file-lifecycle-tests",
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

        public DataRelativePathRepairFileJournalRecord
            CreateIntent()
        {
            DataRelativePathRepairSourceSnapshot sourceSnapshot =
                CaptureSourceSnapshot();

            DataRelativePathRepairDestinationParentSnapshot
                parentSnapshot =
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
                    sourceSnapshot,
                    parentSnapshot
                );

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
