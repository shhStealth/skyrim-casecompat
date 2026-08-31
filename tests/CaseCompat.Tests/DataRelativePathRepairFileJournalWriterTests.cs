using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class DataRelativePathRepairFileJournalWriterTests
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
    public void CreateInitial_ValidIntent_CreatesDurableJsonJournal()
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

        DataRelativePathRepairFileJournalWriterResult result =
            DataRelativePathRepairFileJournalWriter
                .CreateInitial(
                    directory,
                    "journal.json",
                    intent
                );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalWriteState
                .CreatedDurably,
            result.State
        );

        Assert.True(
            result.JournalEntryChanged
        );

        Assert.False(
            result.StagingEntryMayRemain
        );

        Assert.NotNull(
            result.WrittenJournalIncarnation
        );

        Assert.True(
            result.WrittenJournalIncarnation!.Success,
            result.WrittenJournalIncarnation.Error
        );

        Assert.NotNull(
            result.WrittenJournalIncarnationIdentity
        );

        Assert.True(
            result.WrittenJournalIncarnationIdentity!
                .SameIncarnationAs(
                    result.WrittenJournalIncarnation.Identity!
                )
        );

        Assert.NotNull(
            result.WrittenJournalIdentity
        );

        Assert.True(
            result.WrittenJournalIdentity!.SameObjectAs(
                result.WrittenJournalIncarnationIdentity
                    .PhysicalIdentity
            )
        );

        Assert.True(
            File.Exists(
                Path.Combine(
                    temp.RootPath,
                    "journal.json"
                )
            )
        );

        byte[] bytes =
            File.ReadAllBytes(
                Path.Combine(
                    temp.RootPath,
                    "journal.json"
                )
            );

        DataRelativePathRepairFileJournalRecord restored =
            Assert.IsType<
                DataRelativePathRepairFileJournalRecord
            >(
                DataRelativePathRepairFileJournalJson
                    .Deserialize(
                        bytes
                    )
            );

        Assert.Equal(
            intent,
            restored
        );

        Assert.DoesNotContain(
            Directory.EnumerateFileSystemEntries(
                temp.RootPath
            ),
            path =>
                Path.GetFileName(path)
                    .StartsWith(
                        ".casecompat-journal-",
                        StringComparison.Ordinal
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

        using var temp =
            new TemporaryDirectory();

        string journalPath =
            Path.Combine(
                temp.RootPath,
                "journal.json"
            );

        File.WriteAllText(
            journalPath,
            "existing"
        );

        using LinuxNoFollowPathHandle directory =
            OpenRoot(
                temp.RootPath
            );

        DataRelativePathRepairFileJournalWriterResult result =
            DataRelativePathRepairFileJournalWriter
                .CreateInitial(
                    directory,
                    "journal.json",
                    Intent()
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalWriteState
                .JournalAlreadyExists,
            result.State
        );

        Assert.False(
            result.JournalEntryChanged
        );

        Assert.Equal(
            "existing",
            File.ReadAllText(
                journalPath
            )
        );
    }

    [Fact]
    public void ReplaceExisting_NextRevision_ReplacesDurably()
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

        Assert.Equal(
            DataRelativePathRepairFileJournalWriteState
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

        Assert.True(
            update.JournalEntryChanged
        );

        Assert.False(
            update.StagingEntryMayRemain
        );

        byte[] bytes =
            File.ReadAllBytes(
                Path.Combine(
                    temp.RootPath,
                    "journal.json"
                )
            );

        DataRelativePathRepairFileJournalRecord restored =
            Assert.IsType<
                DataRelativePathRepairFileJournalRecord
            >(
                DataRelativePathRepairFileJournalJson
                    .Deserialize(
                        bytes
                    )
            );

        Assert.Equal(
            prepared,
            restored
        );

        Assert.DoesNotContain(
            Directory.EnumerateFileSystemEntries(
                temp.RootPath
            ),
            path =>
                Path.GetFileName(path)
                    .StartsWith(
                        ".casecompat-journal-",
                        StringComparison.Ordinal
                    )
        );
    }

    [Fact]
    public void ReplaceExisting_CurrentJournalReplacedBeforeCall_Refuses()
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

        LinuxFileIncarnationIdentity expectedIncarnation =
            RequireJournalIncarnation(
                directory,
                initial,
                "journal.json"
            );

        string journalPath =
            Path.Combine(
                temp.RootPath,
                "journal.json"
            );

        string movedPath =
            Path.Combine(
                temp.RootPath,
                "journal-original.json"
            );

        File.Move(
            journalPath,
            movedPath
        );

        File.WriteAllText(
            journalPath,
            "replacement"
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

        DataRelativePathRepairFileJournalWriterResult result =
            DataRelativePathRepairFileJournalWriter
                .ReplaceExisting(
                    directory,
                    "journal.json",
                    expectedIncarnation,
                    prepared
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalWriteState
                .CurrentJournalIdentityMismatch,
            result.State
        );

        Assert.False(
            result.JournalEntryChanged
        );

        Assert.Equal(
            "replacement",
            File.ReadAllText(
                journalPath
            )
        );

        Assert.DoesNotContain(
            Directory.EnumerateFileSystemEntries(
                temp.RootPath
            ),
            path =>
                Path.GetFileName(path)
                    .StartsWith(
                        ".casecompat-journal-",
                        StringComparison.Ordinal
                    )
        );
    }

    [Fact]
    public void ReplaceExisting_SymbolicLinkJournal_IsRejected()
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

        File.WriteAllText(
            target,
            "actual"
        );

        string journalPath =
            Path.Combine(
                temp.RootPath,
                "journal.json"
            );

        File.CreateSymbolicLink(
            journalPath,
            target
        );

        using LinuxNoFollowPathHandle directory =
            OpenRoot(
                temp.RootPath
            );

        LinuxFileIncarnationIdentity targetIncarnation =
            CaptureIncarnation(
                directory,
                "actual.json"
            );

        DataRelativePathRepairFileJournalRecord prepared =
            RequireRecord(
                DataRelativePathRepairFileJournal
                    .MarkPrepared(
                        Intent(),

                        SyntheticFileJournalIncarnation.FromPhysical(PreparedIdentity()),
                        T0.AddSeconds(1)
                    )
            );

        DataRelativePathRepairFileJournalWriterResult result =
            DataRelativePathRepairFileJournalWriter
                .ReplaceExisting(
                    directory,
                    "journal.json",
                    targetIncarnation,
                    prepared
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalWriteState
                .CurrentJournalOpenFailed,
            result.State
        );

        Assert.Equal(
            "actual",
            File.ReadAllText(
                target
            )
        );
    }

    [Fact]
    public void CreateInitial_InvalidRecord_PerformsNoFilesystemMutation()
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

        DataRelativePathRepairFileJournalRecord invalid =
            Intent() with
            {
                DataRoot =
                    "relative/Data"
            };

        DataRelativePathRepairFileJournalWriterResult result =
            DataRelativePathRepairFileJournalWriter
                .CreateInitial(
                    directory,
                    "journal.json",
                    invalid
                );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairFileJournalWriteState
                .InvalidRecord,
            result.State
        );

        Assert.Empty(
            Directory.EnumerateFileSystemEntries(
                temp.RootPath
            )
        );
    }

    [Fact]
    public void ReplaceExisting_ParentPathReplaced_UsesOpenedDirectory()
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

        byte[] originalBytes =
            File.ReadAllBytes(
                Path.Combine(
                    movedDirectory,
                    "journal.json"
                )
            );

        DataRelativePathRepairFileJournalRecord restored =
            Assert.IsType<
                DataRelativePathRepairFileJournalRecord
            >(
                DataRelativePathRepairFileJournalJson
                    .Deserialize(
                        originalBytes
                    )
            );

        Assert.Equal(
            prepared,
            restored
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

        return CaptureIncarnation(
            directory,
            childName
        );
    }

    private static LinuxFileIncarnationIdentity
        CaptureIncarnation(
            LinuxNoFollowPathHandle directory,
            string childName)
    {
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
                    "casecompat-journal-writer-tests",
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
