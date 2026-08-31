using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class DataRelativePathRepairFileExecutorTests
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
    public void Execute_DirectChildRepair_PublishesAndPersistsApplied()
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

        DataRelativePathRepairFileJournalRecord intent =
            fixture.CreateIntent();

        DataRelativePathRepairFileExecution result =
            DataRelativePathRepairFileExecutor.Execute(
                fixture.JournalDirectory,
                "journal.json",
                intent,
                T0.AddSeconds(10)
            );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            DataRelativePathRepairFileExecutionState
                .AppliedDurably,
            result.State
        );

        Assert.Equal(
            "source",
            File.ReadAllText(
                fixture.DestinationPath
            )
        );

        Assert.NotNull(
            result.PreparedIdentity
        );

        Assert.Equal(
            0U,
            result.PreparedIdentity!.LinkCount
        );

        DataRelativePathRepairFileJournalReaderResult read =
            fixture.ReadJournal();

        Assert.Equal(
            DataRelativePathRepairFileJournalState.Applied,
            read.Record!.State
        );

        Assert.Equal(
            2,
            read.Record.Revision
        );

        Assert.NotNull(
            read.Record.PreparedFileIdentity
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
            finalIdentity.Success
        );

        Assert.True(
            read.Record.PreparedFileIdentity!
                .SameObjectAs(
                    finalIdentity
                )
        );

        Assert.True(
            finalIdentity.LinkCount >=
            1U
        );
    }

    [Fact]
    public void Execute_HeldJournalLock_DoesNotStartTransaction()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairFileJournalRecord intent =
            fixture.CreateIntent();

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

        DataRelativePathRepairFileExecution result =
            DataRelativePathRepairFileExecutor.Execute(
                fixture.JournalDirectory,
                "journal.json",
                intent,
                T0.AddSeconds(10)
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileExecutionState
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

        Assert.False(
            fixture.JournalExists()
        );
    }

    [Fact]
    public void Execute_ExistingDestination_DoesNotJournalOrOverwrite()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairFileJournalRecord intent =
            fixture.CreateIntent();

        File.WriteAllText(
            fixture.DestinationPath,
            "external"
        );

        DataRelativePathRepairFileExecution result =
            DataRelativePathRepairFileExecutor.Execute(
                fixture.JournalDirectory,
                "journal.json",
                intent,
                T0.AddSeconds(10)
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileExecutionState
                .DestinationExists,
            result.State
        );

        Assert.Equal(
            "external",
            File.ReadAllText(
                fixture.DestinationPath
            )
        );

        Assert.False(
            fixture.JournalExists()
        );
    }

    [Fact]
    public void Execute_SourceChangedAfterIntent_DoesNotJournalOrPublish()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairFileJournalRecord intent =
            fixture.CreateIntent();

        fixture.OverwriteSourceInPlace(
            "mutant"
        );

        DataRelativePathRepairFileExecution result =
            DataRelativePathRepairFileExecutor.Execute(
                fixture.JournalDirectory,
                "journal.json",
                intent,
                T0.AddSeconds(10)
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileExecutionState
                .SourceValidationFailed,
            result.State
        );

        Assert.NotNull(
            result.SourceValidation
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

        Assert.False(
            fixture.JournalExists()
        );
    }

    [Fact]
    public void Execute_ExistingJournal_DoesNotPublishAsset()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairFileJournalRecord intent =
            fixture.CreateIntent();

        string journalPath =
            Path.Combine(
                fixture.JournalDirectoryPath,
                "journal.json"
            );

        File.WriteAllText(
            journalPath,
            "external journal"
        );

        DataRelativePathRepairFileExecution result =
            DataRelativePathRepairFileExecutor.Execute(
                fixture.JournalDirectory,
                "journal.json",
                intent,
                T0.AddSeconds(10)
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileExecutionState
                .InitialJournalWriteFailed,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalWriteState
                .JournalAlreadyExists,
            result.InitialJournalWrite!.State
        );

        Assert.Equal(
            "external journal",
            File.ReadAllText(
                journalPath
            )
        );

        Assert.False(
            File.Exists(
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
                    "casecompat-file-executor-tests",
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

        public bool JournalExists()
        {
            return File.Exists(
                Path.Combine(
                    JournalDirectoryPath,
                    "journal.json"
                )
            );
        }

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

        public void OverwriteSourceInPlace(
            string text)
        {
            using FileStream stream =
                new(
                    SourcePath,
                    FileMode.Open,
                    FileAccess.Write,
                    FileShare.None
                );

            byte[] bytes =
                System.Text.Encoding.UTF8.GetBytes(
                    text
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
