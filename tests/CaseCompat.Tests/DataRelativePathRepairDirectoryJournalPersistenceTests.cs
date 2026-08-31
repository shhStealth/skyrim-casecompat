using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairDirectoryJournalPersistenceTests
{
    private static readonly DateTimeOffset T0 =
        new(
            2026,
            8,
            31,
            1,
            0,
            0,
            TimeSpan.Zero
        );

    [Fact]
    public void CreateInitial_ThenRead_RoundTripsIntentDurably()
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

        DataRelativePathRepairDirectoryJournalWriterResult write =
            DataRelativePathRepairDirectoryJournalWriter
                .CreateInitial(
                    fixture.JournalDirectory,
                    "journal.json",
                    intent
                );

        Assert.True(
            write.Success,
            write.Error
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalWriteState
                .CreatedDurably,
            write.State
        );

        Assert.NotNull(
            write.WrittenJournalIncarnation
        );

        Assert.True(
            write.WrittenJournalIncarnation!.Success,
            write.WrittenJournalIncarnation.Error
        );

        Assert.NotNull(
            write.WrittenJournalIncarnationIdentity
        );

        Assert.True(
            write.WrittenJournalIncarnationIdentity!
                .SameIncarnationAs(
                    write.WrittenJournalIncarnation.Identity!
                )
        );

        Assert.NotNull(
            write.WrittenJournalIdentity
        );

        Assert.True(
            write.WrittenJournalIdentity!.SameObjectAs(
                write.WrittenJournalIncarnationIdentity
                    .PhysicalIdentity
            )
        );

        DataRelativePathRepairDirectoryJournalReaderResult read =
            fixture.ReadJournal();

        Assert.Equal(
            intent.JournalId,
            read.Record!.JournalId
        );

        Assert.Equal(
            0,
            read.Record.Revision
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState
                .IntentRecorded,
            read.Record.State
        );

        Assert.NotNull(
            read.JournalIdentity
        );

        Assert.True(
            read.JournalIdentity!.Success
        );

        Assert.NotNull(
            read.JournalIncarnation
        );

        Assert.True(
            read.JournalIncarnation!.Success,
            read.JournalIncarnation.Error
        );

        Assert.NotNull(
            read.JournalIncarnationIdentity
        );

        Assert.True(
            read.JournalIncarnationIdentity!
                .SameIncarnationAs(
                    read.JournalIncarnation.Identity!
                )
        );

        Assert.True(
            read.JournalIdentity.SameObjectAs(
                read.JournalIncarnationIdentity
                    .PhysicalIdentity
            )
        );
    }

    [Fact]
    public void CreateInitial_ExistingJournal_IsNeverOverwritten()
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

        DataRelativePathRepairDirectoryJournalRecord first =
            fixture.CreateIntent();

        DataRelativePathRepairDirectoryJournalWriterResult initial =
            DataRelativePathRepairDirectoryJournalWriter
                .CreateInitial(
                    fixture.JournalDirectory,
                    "journal.json",
                    first
                );

        Assert.True(
            initial.Success,
            initial.Error
        );

        DataRelativePathRepairDirectoryJournalRecord second =
            fixture.CreateIntent();

        Assert.NotEqual(
            first.JournalId,
            second.JournalId
        );

        DataRelativePathRepairDirectoryJournalWriterResult duplicate =
            DataRelativePathRepairDirectoryJournalWriter
                .CreateInitial(
                    fixture.JournalDirectory,
                    "journal.json",
                    second
                );

        Assert.False(
            duplicate.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalWriteState
                .JournalAlreadyExists,
            duplicate.State
        );

        DataRelativePathRepairDirectoryJournalReaderResult read =
            fixture.ReadJournal();

        Assert.Equal(
            first.JournalId,
            read.Record!.JournalId
        );
    }

    [Fact]
    public void ReplaceExisting_PreparedRevision_RoundTripsEvidence()
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

        fixture.WriteInitial(
            intent
        );

        DataRelativePathRepairDirectoryJournalReaderResult before =
            fixture.ReadJournal();

        DataRelativePathRepairDirectoryJournalRecord prepared =
            RequireRecord(
                DataRelativePathRepairDirectoryJournal.MarkPrepared(
                    intent,
                    ".casecompat-stage-1",
                    SyntheticDirectoryJournalIncarnation.FromPhysical(

                        DirectoryIdentity()

                    ),
                    T0.AddSeconds(1)
                )
            );

        DataRelativePathRepairDirectoryJournalWriterResult update =
            DataRelativePathRepairDirectoryJournalWriter
                .ReplaceExisting(
                    fixture.JournalDirectory,
                    "journal.json",
                    before.JournalIncarnationIdentity!,
                    prepared
                );

        Assert.True(
            update.Success,
            update.Error
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalWriteState
                .ReplacedDurably,
            update.State
        );

        Assert.NotNull(
            update.WrittenJournalIncarnation
        );

        Assert.True(
            update.WrittenJournalIncarnation!.Success,
            update.WrittenJournalIncarnation.Error
        );

        Assert.NotNull(
            update.WrittenJournalIncarnationIdentity
        );

        Assert.True(
            update.WrittenJournalIncarnationIdentity!
                .SameIncarnationAs(
                    update.WrittenJournalIncarnation.Identity!
                )
        );

        Assert.NotNull(
            update.WrittenJournalIdentity
        );

        Assert.True(
            update.WrittenJournalIdentity!.SameObjectAs(
                update.WrittenJournalIncarnationIdentity
                    .PhysicalIdentity
            )
        );

        DataRelativePathRepairDirectoryJournalReaderResult after =
            fixture.ReadJournal();

        Assert.Equal(
            1,
            after.Record!.Revision
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.Prepared,
            after.Record.State
        );

        Assert.Equal(
            ".casecompat-stage-1",
            after.Record.PreparedStagingChildName
        );

        Assert.Equal(
            prepared.PreparedDirectoryIdentity,
            after.Record.PreparedDirectoryIdentity
        );
    }

    [Fact]
    public void ReplaceExisting_StaleJournalIdentity_IsRefused()
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

        fixture.WriteInitial(
            intent
        );

        DataRelativePathRepairDirectoryJournalReaderResult initialRead =
            fixture.ReadJournal();

        LinuxFileIncarnationIdentity staleIncarnation =
            initialRead.JournalIncarnationIdentity!;

        DataRelativePathRepairDirectoryJournalRecord prepared =
            RequireRecord(
                DataRelativePathRepairDirectoryJournal.MarkPrepared(
                    intent,
                    ".casecompat-stage-1",
                    SyntheticDirectoryJournalIncarnation.FromPhysical(

                        DirectoryIdentity()

                    ),
                    T0.AddSeconds(1)
                )
            );

        DataRelativePathRepairDirectoryJournalWriterResult firstUpdate =
            DataRelativePathRepairDirectoryJournalWriter
                .ReplaceExisting(
                    fixture.JournalDirectory,
                    "journal.json",
                    staleIncarnation,
                    prepared
                );

        Assert.True(
            firstUpdate.Success,
            firstUpdate.Error
        );

        DataRelativePathRepairDirectoryJournalRecord applied =
            RequireRecord(
                DataRelativePathRepairDirectoryJournal.MarkApplied(
                    prepared,
                    T0.AddSeconds(2)
                )
            );

        DataRelativePathRepairDirectoryJournalWriterResult staleUpdate =
            DataRelativePathRepairDirectoryJournalWriter
                .ReplaceExisting(
                    fixture.JournalDirectory,
                    "journal.json",
                    staleIncarnation,
                    applied
                );

        Assert.False(
            staleUpdate.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalWriteState
                .CurrentJournalIdentityMismatch,
            staleUpdate.State
        );

        DataRelativePathRepairDirectoryJournalReaderResult after =
            fixture.ReadJournal();

        Assert.Equal(
            1,
            after.Record!.Revision
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalState.Prepared,
            after.Record.State
        );
    }

    [Fact]
    public void CreateInitial_InvalidRecord_PerformsNoFilesystemMutation()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairDirectoryJournalRecord invalid =
            fixture.CreateIntent() with
            {
                Operation =
                    new DataRelativePathRepairPlanOperation(
                        Kind:
                            DataRelativePathRepairPlanOperationKind
                                .CreateFile,
                        DestinationPath:
                            "/game/Data/Meshes/Final",
                        SourcePath:
                            "/game/Data/source.nif"
                    )
            };

        DataRelativePathRepairDirectoryJournalWriterResult result =
            DataRelativePathRepairDirectoryJournalWriter
                .CreateInitial(
                    fixture.JournalDirectory,
                    "journal.json",
                    invalid
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalWriteState
                .InvalidRecord,
            result.State
        );

        Assert.False(
            File.Exists(
                fixture.JournalPath
            )
        );

        Assert.Empty(
            Directory.EnumerateFileSystemEntries(
                fixture.JournalDirectoryPath
            )
        );
    }

    [Fact]
    public void Read_SymbolicLinkJournal_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        string target =
            Path.Combine(
                fixture.RootPath,
                "target.json"
            );

        File.WriteAllText(
            target,
            "{}"
        );

        File.CreateSymbolicLink(
            fixture.JournalPath,
            target
        );

        DataRelativePathRepairDirectoryJournalReaderResult result =
            DataRelativePathRepairDirectoryJournalReader.Read(
                fixture.JournalDirectory,
                "journal.json"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDirectoryJournalReadState
                .JournalSymbolicLinkRejected,
            result.State
        );

        Assert.Equal(
            "{}",
            File.ReadAllText(
                target
            )
        );
    }

    private static LinuxFileIdentityResult DirectoryIdentity()
    {
        return new(
            FullPath:
                "/game/Data/Meshes/.casecompat-stage-1",
            DeviceMajor:
                8U,
            DeviceMinor:
                1U,
            Inode:
                200UL,
            LinkCount:
                2U,
            MountId:
                55UL,
            Error:
                null
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
                    "casecompat-directory-journal-persistence-tests",
                    Guid.NewGuid().ToString("N")
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
                JournalDirectoryPath
            );

            JournalDirectory =
                OpenRoot(
                    JournalDirectoryPath
                );
        }

        public string RootPath { get; }

        public string JournalDirectoryPath { get; }

        public string JournalPath { get; }

        public LinuxNoFollowPathHandle JournalDirectory { get; }

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

        public DataRelativePathRepairDirectoryJournalRecord
            CreateIntent()
        {
            DataRelativePathRepairDirectoryJournalTransitionResult
                result =
                    DataRelativePathRepairDirectoryJournal.CreateIntent(
                        Guid.NewGuid(),
                        T0,
                        "/game/Data",
                        new DataRelativePathRepairPlanOperation(
                            Kind:
                                DataRelativePathRepairPlanOperationKind
                                    .CreateDirectory,
                            DestinationPath:
                                "/game/Data/Meshes/Final",
                            SourcePath:
                                null
                        ),
                        new
                            DataRelativePathRepairDestinationParentSnapshot(
                                PhysicalPath:
                                    "/game/Data/Meshes",
                                Identity:
                                    new LinuxFileIdentityResult(
                                        FullPath:
                                            "/game/Data/Meshes",
                                        DeviceMajor:
                                            8U,
                                        DeviceMinor:
                                            1U,
                                        Inode:
                                            100UL,
                                        LinkCount:
                                            2U,
                                        MountId:
                                            55UL,
                                        Error:
                                            null
                                    ),
                                CasefoldEnabled:
                                    false,
                                RawFlags:
                                    0
                            )
                    ,
                        SyntheticDirectoryJournalIncarnation.FromPhysical(
                            (new
                            DataRelativePathRepairDestinationParentSnapshot(
                                PhysicalPath:
                                    "/game/Data/Meshes",
                                Identity:
                                    new LinuxFileIdentityResult(
                                        FullPath:
                                            "/game/Data/Meshes",
                                        DeviceMajor:
                                            8U,
                                        DeviceMinor:
                                            1U,
                                        Inode:
                                            100UL,
                                        LinkCount:
                                            2U,
                                        MountId:
                                            55UL,
                                        Error:
                                            null
                                    ),
                                CasefoldEnabled:
                                    false,
                                RawFlags:
                                    0
                            )).Identity
                        ));

            return RequireRecord(
                result
            );
        }

        public void WriteInitial(
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
