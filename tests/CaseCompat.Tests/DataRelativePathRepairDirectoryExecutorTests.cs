using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class DataRelativePathRepairDirectoryExecutorTests
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
    public void Execute_CreateDirectory_ReachesAppliedDurably()
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

        DataRelativePathRepairPlanOperation operation =
            fixture.Operation();

        DataRelativePathRepairDestinationParentSnapshot
            parentSnapshot =
                fixture.CaptureParentSnapshot();

        DataRelativePathRepairDirectoryExecution result =
            DataRelativePathRepairDirectoryExecutor.Execute(
                fixture.JournalDirectory,
                "journal.json",
                operation,
                parentSnapshot,
                fixture.DataRoot,
                T0
            );

        if (
            result.ForwardRecovery?.Publication?.State ==
            LinuxPublishOwnedDirectoryAtState
                .NoReplaceUnsupported)
        {
            return;
        }

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryExecutionState
                .AppliedDurably,
            result.State
        );

        Assert.NotNull(
            result.IntentTransition
        );

        Assert.True(
            result.IntentTransition!.Success,
            result.IntentTransition.Error
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState
                .IntentRecorded,
            result.IntentTransition.Record!.State
        );

        Assert.Equal(
            0,
            result.IntentTransition.Record.Revision
        );

        Assert.NotNull(
            result.InitialJournalWrite
        );

        Assert.True(
            result.InitialJournalWrite!.Success,
            result.InitialJournalWrite.Error
        );

        Assert.NotNull(
            result.IntentRecovery
        );

        Assert.True(
            result.IntentRecovery!.Success,
            result.IntentRecovery.Error
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryIntentRecoveryState
                .PreparedDurably,
            result.IntentRecovery.State
        );

        Assert.NotNull(
            result.ForwardRecovery
        );

        Assert.True(
            result.ForwardRecovery!.Success,
            result.ForwardRecovery.Error
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryForwardRecoveryState
                .AppliedDurably,
            result.ForwardRecovery.State
        );

        string stagingName =
            result.IntentRecovery.FreshStagingChildName!;

        Assert.False(
            Directory.Exists(
                fixture.PathFor(
                    stagingName
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

        DataRelativePathRepairDirectoryJournalReaderResult journal =
            fixture.ReadJournal();

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.Applied,
            journal.Record!.State
        );

        Assert.Equal(
            2,
            journal.Record.Revision
        );

        Assert.Equal(
            stagingName,
            journal.Record.PreparedStagingChildName
        );

        Assert.NotNull(
            journal.Record.PreparedDirectoryIncarnationIdentity
        );

        LinuxDirectoryIncarnationIdentity finalIdentity =
            fixture.CaptureDirectoryIdentity(
                "Final"
            );

        Assert.True(
            journal.Record.PreparedDirectoryIncarnationIdentity!
                .SameIncarnationAs(
                    finalIdentity
                )
        );

        DataRelativePathRepairDirectoryRecoveryClassification
            classification =
                DataRelativePathRepairDirectoryRecoveryClassifier
                    .Classify(
                        journal.Record,
                        fixture.DataRoot
                    );

        Assert.Equal(
            DataRelativePathRepairDirectoryRecoveryState
                .AppliedFinalMatches,
            classification.State
        );

        Assert.True(
            classification.FinalMatchesPreparedIdentity
        );
    }

    [Fact]
    public void DestinationExists_DoesNotCreateJournalOrOverwrite()
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

        DataRelativePathRepairDestinationParentSnapshot
            parentSnapshot =
                fixture.CaptureParentSnapshot();

        fixture.CreateDirectory(
            "Final"
        );

        LinuxDirectoryIncarnationIdentity before =
            fixture.CaptureDirectoryIdentity(
                "Final"
            );

        DataRelativePathRepairDirectoryExecution result =
            DataRelativePathRepairDirectoryExecutor.Execute(
                fixture.JournalDirectory,
                "journal.json",
                fixture.Operation(),
                parentSnapshot,
                fixture.DataRoot,
                T0
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryExecutionState
                .DestinationExists,
            result.State
        );

        Assert.Null(
            result.InitialJournalWrite
        );

        Assert.Null(
            result.IntentRecovery
        );

        Assert.Null(
            result.ForwardRecovery
        );

        Assert.False(
            File.Exists(
                fixture.JournalPath
            )
        );

        Assert.True(
            Directory.Exists(
                fixture.PathFor(
                    "Final"
                )
            )
        );

        LinuxDirectoryIncarnationIdentity after =
            fixture.CaptureDirectoryIdentity(
                "Final"
            );

        Assert.True(
            before.SameIncarnationAs(
                after
            )
        );
    }

    [Fact]
    public void HeldJournalLock_PreventsExecutionBeforeIntent()
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

        DataRelativePathRepairDestinationParentSnapshot
            parentSnapshot =
                fixture.CaptureParentSnapshot();

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

        DataRelativePathRepairDirectoryExecution result =
            DataRelativePathRepairDirectoryExecutor.Execute(
                fixture.JournalDirectory,
                "journal.json",
                fixture.Operation(),
                parentSnapshot,
                fixture.DataRoot,
                T0
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryExecutionState
                .LockUnavailable,
            result.State
        );

        Assert.Null(
            result.IntentTransition
        );

        Assert.Null(
            result.InitialJournalWrite
        );

        Assert.False(
            File.Exists(
                fixture.JournalPath
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

    [Fact]
    public void TrustedDataRootMismatch_DoesNotCreateIntentOrDirectory()
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

        DataRelativePathRepairDestinationParentSnapshot
            parentSnapshot =
                fixture.CaptureParentSnapshot();

        string otherDataRoot =
            Path.Combine(
                fixture.RootPath,
                "OtherData"
            );

        Directory.CreateDirectory(
            otherDataRoot
        );

        DataRelativePathRepairDirectoryExecution result =
            DataRelativePathRepairDirectoryExecutor.Execute(
                fixture.JournalDirectory,
                "journal.json",
                fixture.Operation(),
                parentSnapshot,
                otherDataRoot,
                T0
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryExecutionState
                .DestinationParentValidationFailed,
            result.State
        );

        Assert.Null(
            result.IntentTransition
        );

        Assert.Null(
            result.InitialJournalWrite
        );

        Assert.Null(
            result.IntentRecovery
        );

        Assert.Null(
            result.ForwardRecovery
        );

        Assert.False(
            File.Exists(
                fixture.JournalPath
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
                    "casecompat-directory-executor-tests",
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

            JournalPath =
                Path.Combine(
                    JournalDirectoryPath,
                    "journal.json"
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

        public string JournalPath { get; }

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

        public DataRelativePathRepairPlanOperation Operation()
        {
            return new(
                Kind:
                    DataRelativePathRepairPlanOperationKind
                        .CreateDirectory,
                DestinationPath:
                    PathFor(
                        "Final"
                    ),
                SourcePath:
                    null
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

        public DataRelativePathRepairDestinationParentSnapshot
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

            return LiveDirectoryJournalIncarnation.Capture(
                child,
                PathFor(
                    childName
                )
            );
        }

        private static LinuxNoFollowPathHandle OpenRoot(
            string path)
        {
            LinuxNoFollowPathOpenResult opened =
                LinuxNoFollowPath.OpenRootReadOnly(
                    path
                );

            Assert.True(
                opened.Success,
                opened.Error
            );

            return Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                opened.OpenedPath
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
