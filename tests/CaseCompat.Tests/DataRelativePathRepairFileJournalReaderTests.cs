using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class DataRelativePathRepairFileJournalReaderTests
{
    private static readonly DateTimeOffset T0 =
        new(
            2026,
            8,
            30,
            20,
            0,
            0,
            TimeSpan.Zero
        );

    [Fact]
    public void Read_DurableInitialJournal_ReturnsRecordAndIdentity()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        using LinuxNoFollowPathHandle directory =
            OpenRoot(
                temp.RootPath
            );

        DataRelativePathRepairFileJournalRecord intent =
            Intent();

        DataRelativePathRepairFileJournalWriterResult write =
            DataRelativePathRepairFileJournalWriter
                .CreateInitial(
                    directory,
                    "journal.json",
                    intent
                );

        Assert.True(
            write.Success,
            write.Error
        );

        DataRelativePathRepairFileJournalReaderResult read =
            DataRelativePathRepairFileJournalReader.Read(
                directory,
                "journal.json"
            );

        Assert.True(
            read.Success,
            read.Error
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalReadState.Loaded,
            read.State
        );

        Assert.Equal(
            intent,
            read.Record
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

        Assert.True(
            read.Length > 0
        );

        if (
            write.WrittenJournalIdentity is
                LinuxOpenedFileIdentityResult writtenIdentity &&
            writtenIdentity.Success)
        {
            Assert.True(
                writtenIdentity.SameObjectAs(
                    read.JournalIdentity
                )
            );
        }
    }

    [Fact]
    public void Read_DurablyReplacedJournal_ReturnsLatestRevision()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        using LinuxNoFollowPathHandle directory =
            OpenRoot(
                temp.RootPath
            );

        DataRelativePathRepairFileJournalRecord intent =
            Intent();

        DataRelativePathRepairFileJournalWriterResult initial =
            DataRelativePathRepairFileJournalWriter
                .CreateInitial(
                    directory,
                    "journal.json",
                    intent
                );

        Assert.True(
            initial.Success,
            initial.Error
        );

        LinuxFileIncarnationIdentity currentIncarnation =
            RequireJournalIncarnation(
                directory,
                initial,
                "journal.json"
            );

        DataRelativePathRepairFileJournalRecord prepared =
            RequireRecord(
                DataRelativePathRepairFileJournal
                    .MarkPrepared(
                        intent,

                        SyntheticFileJournalIncarnation.FromPhysical(PreparedIdentity()),
                        T0.AddSeconds(1)
                    )
            );

        DataRelativePathRepairFileJournalWriterResult update =
            DataRelativePathRepairFileJournalWriter
                .ReplaceExisting(
                    directory,
                    "journal.json",
                    currentIncarnation,
                    prepared
                );

        Assert.True(
            update.Success,
            update.Error
        );

        DataRelativePathRepairFileJournalReaderResult read =
            DataRelativePathRepairFileJournalReader.Read(
                directory,
                "journal.json"
            );

        Assert.True(
            read.Success,
            read.Error
        );

        Assert.Equal(
            prepared,
            read.Record
        );

        Assert.Equal(
            1,
            read.Record!.Revision
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalState.Prepared,
            read.Record.State
        );

        if (
            update.WrittenJournalIdentity is
                LinuxOpenedFileIdentityResult writtenIdentity &&
            writtenIdentity.Success)
        {
            Assert.True(
                writtenIdentity.SameObjectAs(
                    read.JournalIdentity!
                )
            );
        }
    }

    [Fact]
    public void Read_MissingJournal_IsReportedUnavailable()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        using LinuxNoFollowPathHandle directory =
            OpenRoot(
                temp.RootPath
            );

        DataRelativePathRepairFileJournalReaderResult result =
            DataRelativePathRepairFileJournalReader.Read(
                directory,
                "missing.json"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalReadState
                .JournalUnavailable,
            result.State
        );
    }

    [Fact]
    public void Read_SymbolicLinkJournal_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string target =
            Path.Combine(
                temp.RootPath,
                "actual.json"
            );

        File.WriteAllBytes(
            target,
            DataRelativePathRepairFileJournalJson.Serialize(
                Intent()
            )
        );

        File.CreateSymbolicLink(
            Path.Combine(
                temp.RootPath,
                "journal.json"
            ),
            target
        );

        using LinuxNoFollowPathHandle directory =
            OpenRoot(
                temp.RootPath
            );

        DataRelativePathRepairFileJournalReaderResult result =
            DataRelativePathRepairFileJournalReader.Read(
                directory,
                "journal.json"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalReadState
                .JournalSymbolicLinkRejected,
            result.State
        );
    }

    [Fact]
    public void Read_InvalidJson_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        File.WriteAllText(
            Path.Combine(
                temp.RootPath,
                "journal.json"
            ),
            "{ definitely-not-valid-json"
        );

        using LinuxNoFollowPathHandle directory =
            OpenRoot(
                temp.RootPath
            );

        DataRelativePathRepairFileJournalReaderResult result =
            DataRelativePathRepairFileJournalReader.Read(
                directory,
                "journal.json"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalReadState
                .DeserializeFailed,
            result.State
        );

        Assert.NotNull(
            result.JournalIdentity
        );

        Assert.True(
            result.JournalIdentity!.Success
        );
    }

    [Fact]
    public void Read_DeserializedButInvalidRecord_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        DataRelativePathRepairFileJournalRecord invalid =
            Intent() with
            {
                DataRoot =
                    "relative/Data"
            };

        File.WriteAllBytes(
            Path.Combine(
                temp.RootPath,
                "journal.json"
            ),
            DataRelativePathRepairFileJournalJson.Serialize(
                invalid
            )
        );

        using LinuxNoFollowPathHandle directory =
            OpenRoot(
                temp.RootPath
            );

        DataRelativePathRepairFileJournalReaderResult result =
            DataRelativePathRepairFileJournalReader.Read(
                directory,
                "journal.json"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalReadState
                .InvalidRecord,
            result.State
        );

        Assert.Equal(
            invalid,
            result.Record
        );
    }

    [Fact]
    public void Read_ParentPathReplaced_UsesOpenedDirectory()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string journalDirectoryPath =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "journal-dir"
                )
            ).FullName;

        DataRelativePathRepairFileJournalRecord intent =
            Intent();

        File.WriteAllBytes(
            Path.Combine(
                journalDirectoryPath,
                "journal.json"
            ),
            DataRelativePathRepairFileJournalJson.Serialize(
                intent
            )
        );

        LinuxNoFollowPathOpenResult directoryOpen =
            LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                temp.RootPath,
                "journal-dir"
            );

        using LinuxNoFollowPathHandle directory =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                directoryOpen.OpenedPath
            );

        string movedDirectory =
            Path.Combine(
                temp.RootPath,
                "journal-dir-original"
            );

        Directory.Move(
            journalDirectoryPath,
            movedDirectory
        );

        Directory.CreateDirectory(
            journalDirectoryPath
        );

        File.WriteAllText(
            Path.Combine(
                journalDirectoryPath,
                "journal.json"
            ),
            "decoy"
        );

        DataRelativePathRepairFileJournalReaderResult result =
            DataRelativePathRepairFileJournalReader.Read(
                directory,
                "journal.json"
            );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            intent,
            result.Record
        );

        Assert.Equal(
            "decoy",
            File.ReadAllText(
                Path.Combine(
                    journalDirectoryPath,
                    "journal.json"
                )
            )
        );
    }

    [Fact]
    public void Read_OversizedJournal_IsRejectedBeforeAllocation()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string journalPath =
            Path.Combine(
                temp.RootPath,
                "journal.json"
            );

        using (
            FileStream stream =
                new(
                    journalPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None
                ))
        {
            stream.SetLength(
                DataRelativePathRepairFileJournalReader
                    .MaxJournalBytes +
                1
            );
        }

        using LinuxNoFollowPathHandle directory =
            OpenRoot(
                temp.RootPath
            );

        DataRelativePathRepairFileJournalReaderResult result =
            DataRelativePathRepairFileJournalReader.Read(
                directory,
                "journal.json"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalReadState
                .JournalTooLarge,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalReader
                .MaxJournalBytes +
            1,
            result.Length
        );
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../journal.json")]
    [InlineData("child/journal.json")]
    [InlineData(@"child\journal.json")]
    [InlineData("")]
    [InlineData("\0")]
    public void Read_InvalidJournalName_IsRejected(
        string journalChildName)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        using LinuxNoFollowPathHandle directory =
            OpenRoot(
                temp.RootPath
            );

        DataRelativePathRepairFileJournalReaderResult result =
            DataRelativePathRepairFileJournalReader.Read(
                directory,
                journalChildName
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalReadState
                .InvalidJournalName,
            result.State
        );
    }

    private static LinuxFileIncarnationIdentity
        RequireJournalIncarnation(
            LinuxNoFollowPathHandle directory,
            DataRelativePathRepairFileJournalWriterResult result,
            string childName)
    {
        if (
            result.WrittenJournalIncarnationIdentity is
                LinuxFileIncarnationIdentity identity &&
            identity.Success)
        {
            return identity;
        }

        LinuxOpenChildReadOnlyAtResult opened =
            LinuxOpenChildReadOnlyAt.Open(
                directory,
                childName
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

        LinuxOpenedFileIncarnationResult incarnation =
            LinuxOpenedFileIncarnation.Capture(
                child
            );

        Assert.True(
            incarnation.Success,
            incarnation.Error
        );

        return incarnation.Identity!;
    }

    private static DataRelativePathRepairFileJournalRecord
        Intent()
    {
        return RequireRecord(
            DataRelativePathRepairFileJournal.CreateIntent(
                Guid.Parse(
                    "11111111-2222-3333-4444-555555555555"
                ),
                T0,
                "/game/Data",
                Operation(),
                SourceSnapshot(),
                ParentSnapshot()
            )
        );
    }

    private static DataRelativePathRepairPlanOperation
        Operation()
    {
        return new DataRelativePathRepairPlanOperation(
            Kind:
                DataRelativePathRepairPlanOperationKind
                    .CreateFile,
            DestinationPath:
                "/game/Data/Meshes/Foo/Armor.nif",
            SourcePath:
                "/game/Data/meshes/foo/Armor.nif"
        );
    }

    private static DataRelativePathRepairSourceSnapshot
        SourceSnapshot()
    {
        return new DataRelativePathRepairSourceSnapshot(
            PhysicalPath:
                "/game/Data/meshes/foo/Armor.nif",
            Size:
                1234,
            Sha256:
                new string(
                    'A',
                    64
                ),
            Identity:
                new LinuxFileIdentityResult(
                    FullPath:
                        "/game/Data/meshes/foo/Armor.nif",
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
        );
    }

    private static
        DataRelativePathRepairDestinationParentSnapshot
        ParentSnapshot()
    {
        return new
            DataRelativePathRepairDestinationParentSnapshot(
                PhysicalPath:
                    "/game/Data/Meshes/Foo",
                Identity:
                    new LinuxFileIdentityResult(
                        FullPath:
                            "/game/Data/Meshes/Foo",
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
                    ),
                CasefoldEnabled:
                    false,
                RawFlags:
                    0
            );
    }

    private static LinuxOpenedFileIdentityResult
        PreparedIdentity()
    {
        return new LinuxOpenedFileIdentityResult(
            State:
                LinuxOpenedFileIdentityState.Captured,
            DeviceMajor:
                8U,
            DeviceMinor:
                1U,
            Inode:
                300UL,
            LinkCount:
                0U,
            MountId:
                55UL,
            Errno:
                null,
            Error:
                null
        );
    }

    private static DataRelativePathRepairFileJournalRecord
        RequireRecord(
            DataRelativePathRepairFileJournalTransitionResult result)
    {
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

    private sealed class TemporaryDirectory
        : IDisposable
    {
        public TemporaryDirectory()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-journal-reader-tests",
                    Guid.NewGuid().ToString("N")
                );

            Directory.CreateDirectory(
                RootPath
            );
        }

        public string RootPath { get; }

        public void Dispose()
        {
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
