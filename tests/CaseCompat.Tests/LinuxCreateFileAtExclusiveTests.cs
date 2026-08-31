using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxCreateFileAtExclusiveTests
{
    [Fact]
    public void Create_MissingChild_CreatesZeroByteFileAndReturnsOpenDescriptor()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string parent =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "parent"
                )
            ).FullName;

        using LinuxNoFollowPathHandle openedParent =
            OpenParent(
                temp.RootPath,
                "parent"
            );

        LinuxCreateFileAtExclusiveResult result =
            LinuxCreateFileAtExclusive.Create(
                openedParent,
                "fixture.nif"
            );

        Assert.True(
            result.Success
        );

        Assert.Equal(
            LinuxCreateFileAtExclusiveState.Created,
            result.State
        );

        LinuxNoFollowPathHandle created =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                result.OpenedPath
            );

        using (created)
        {
            string physicalPath =
                Path.Combine(
                    parent,
                    "fixture.nif"
                );

            Assert.True(
                File.Exists(
                    physicalPath
                )
            );

            Assert.Equal(
                0L,
                new FileInfo(
                    physicalPath
                ).Length
            );

            Assert.False(
                created.Handle.IsInvalid
            );

            Assert.False(
                created.Handle.IsClosed
            );

            LinuxOpenedFileSnapshotResult snapshot =
                LinuxOpenedFileSnapshot.Capture(
                    created
                );

            Assert.True(
                snapshot.Success
            );

            Assert.Equal(
                0L,
                snapshot.Size
            );

            LinuxFileIdentityResult pathnameIdentity =
                LinuxFileIdentity.Inspect(
                    physicalPath
                );

            Assert.True(
                snapshot.Identity!
                    .SameObjectAs(
                        pathnameIdentity
                    )
            );
        }
    }

    [Fact]
    public void Create_ExistingFile_IsConflictAndFileIsUntouched()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string parent =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "parent"
                )
            ).FullName;

        string existing =
            Path.Combine(
                parent,
                "fixture.nif"
            );

        File.WriteAllText(
            existing,
            "do-not-touch"
        );

        using LinuxNoFollowPathHandle openedParent =
            OpenParent(
                temp.RootPath,
                "parent"
            );

        LinuxCreateFileAtExclusiveResult result =
            LinuxCreateFileAtExclusive.Create(
                openedParent,
                "fixture.nif"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxCreateFileAtExclusiveState
                .DestinationExists,
            result.State
        );

        Assert.Null(
            result.OpenedPath
        );

        Assert.Equal(
            "do-not-touch",
            File.ReadAllText(
                existing
            )
        );
    }

    [Fact]
    public void Create_ExistingDirectory_IsConflictAndDirectoryIsUntouched()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string parent =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "parent"
                )
            ).FullName;

        string existing =
            Directory.CreateDirectory(
                Path.Combine(
                    parent,
                    "fixture.nif"
                )
            ).FullName;

        using LinuxNoFollowPathHandle openedParent =
            OpenParent(
                temp.RootPath,
                "parent"
            );

        LinuxCreateFileAtExclusiveResult result =
            LinuxCreateFileAtExclusive.Create(
                openedParent,
                "fixture.nif"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxCreateFileAtExclusiveState
                .DestinationExists,
            result.State
        );

        Assert.True(
            Directory.Exists(
                existing
            )
        );
    }

    [Fact]
    public void Create_ExistingSymbolicLink_IsConflictAndLinkIsUntouched()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string parent =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "parent"
                )
            ).FullName;

        string target =
            Path.Combine(
                temp.RootPath,
                "target.nif"
            );

        File.WriteAllText(
            target,
            "target"
        );

        string link =
            Path.Combine(
                parent,
                "fixture.nif"
            );

        File.CreateSymbolicLink(
            link,
            target
        );

        using LinuxNoFollowPathHandle openedParent =
            OpenParent(
                temp.RootPath,
                "parent"
            );

        LinuxCreateFileAtExclusiveResult result =
            LinuxCreateFileAtExclusive.Create(
                openedParent,
                "fixture.nif"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxCreateFileAtExclusiveState
                .DestinationExists,
            result.State
        );

        FileAttributes attributes =
            File.GetAttributes(
                link
            );

        Assert.True(
            (attributes &
             FileAttributes.ReparsePoint) != 0
        );

        Assert.Equal(
            "target",
            File.ReadAllText(
                target
            )
        );
    }

    [Fact]
    public void Create_CaseDifferentSiblingOnStrictParent_CreatesDistinctFile()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string parent =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "parent"
                )
            ).FullName;

        string existing =
            Path.Combine(
                parent,
                "fixture.nif"
            );

        File.WriteAllText(
            existing,
            "existing"
        );

        LinuxFileIdentityResult existingIdentityBefore =
            LinuxFileIdentity.Inspect(
                existing
            );

        using LinuxNoFollowPathHandle openedParent =
            OpenParent(
                temp.RootPath,
                "parent"
            );

        LinuxCreateFileAtExclusiveResult result =
            LinuxCreateFileAtExclusive.Create(
                openedParent,
                "Fixture.nif"
            );

        Assert.True(
            result.Success
        );

        LinuxNoFollowPathHandle created =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                result.OpenedPath
            );

        using (created)
        {
            string createdPath =
                Path.Combine(
                    parent,
                    "Fixture.nif"
                );

            Assert.Equal(
                "existing",
                File.ReadAllText(
                    existing
                )
            );

            Assert.True(
                File.Exists(
                    createdPath
                )
            );

            LinuxFileIdentityResult existingIdentityAfter =
                LinuxFileIdentity.Inspect(
                    existing
                );

            LinuxFileIdentityResult createdIdentity =
                LinuxFileIdentity.Inspect(
                    createdPath
                );

            Assert.True(
                existingIdentityBefore.SameObjectAs(
                    existingIdentityAfter
                )
            );

            Assert.False(
                existingIdentityAfter.SameObjectAs(
                    createdIdentity
                )
            );
        }
    }

    [Fact]
    public void Create_ParentPathReplacedAfterOpen_CreatesUnderOriginalDirectory()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string originalParent =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "parent"
                )
            ).FullName;

        using LinuxNoFollowPathHandle openedParent =
            OpenParent(
                temp.RootPath,
                "parent"
            );

        string movedParent =
            Path.Combine(
                temp.RootPath,
                "parent-original"
            );

        Directory.Move(
            originalParent,
            movedParent
        );

        string replacementParent =
            Directory.CreateDirectory(
                originalParent
            ).FullName;

        LinuxCreateFileAtExclusiveResult result =
            LinuxCreateFileAtExclusive.Create(
                openedParent,
                "fixture.nif"
            );

        Assert.True(
            result.Success
        );

        LinuxNoFollowPathHandle created =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                result.OpenedPath
            );

        using (created)
        {
            Assert.True(
                File.Exists(
                    Path.Combine(
                        movedParent,
                        "fixture.nif"
                    )
                )
            );

            Assert.False(
                File.Exists(
                    Path.Combine(
                        replacementParent,
                        "fixture.nif"
                    )
                )
            );
        }
    }

    [Fact]
    public void Create_PathReplacedAfterCreation_DescriptorStillReferencesCreatedFile()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string parent =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "parent"
                )
            ).FullName;

        using LinuxNoFollowPathHandle openedParent =
            OpenParent(
                temp.RootPath,
                "parent"
            );

        LinuxCreateFileAtExclusiveResult result =
            LinuxCreateFileAtExclusive.Create(
                openedParent,
                "fixture.nif"
            );

        LinuxNoFollowPathHandle created =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                result.OpenedPath
            );

        using (created)
        {
            string originalPath =
                Path.Combine(
                    parent,
                    "fixture.nif"
                );

            string movedPath =
                Path.Combine(
                    parent,
                    "fixture-original.nif"
                );

            File.Move(
                originalPath,
                movedPath
            );

            File.WriteAllText(
                originalPath,
                "replacement"
            );

            LinuxOpenedFileSnapshotResult descriptorSnapshot =
                LinuxOpenedFileSnapshot.Capture(
                    created
                );

            Assert.True(
                descriptorSnapshot.Success
            );

            LinuxFileIdentityResult movedIdentity =
                LinuxFileIdentity.Inspect(
                    movedPath
                );

            LinuxFileIdentityResult replacementIdentity =
                LinuxFileIdentity.Inspect(
                    originalPath
                );

            Assert.True(
                descriptorSnapshot.Identity!
                    .SameObjectAs(
                        movedIdentity
                    )
            );

            Assert.False(
                descriptorSnapshot.Identity!
                    .SameObjectAs(
                        replacementIdentity
                    )
            );

            Assert.Equal(
                0L,
                descriptorSnapshot.Size
            );
        }
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../escape.nif")]
    [InlineData("child/file.nif")]
    [InlineData(@"child\file.nif")]
    [InlineData("")]
    [InlineData("\0")]
    public void Create_InvalidChildName_IsRejectedWithoutCreatingAnything(
        string childName)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string parent =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "parent"
                )
            ).FullName;

        using LinuxNoFollowPathHandle openedParent =
            OpenParent(
                temp.RootPath,
                "parent"
            );

        string[] before =
            Directory
                .EnumerateFileSystemEntries(
                    parent
                )
                .ToArray();

        LinuxCreateFileAtExclusiveResult result =
            LinuxCreateFileAtExclusive.Create(
                openedParent,
                childName
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxCreateFileAtExclusiveState.InvalidName,
            result.State
        );

        Assert.Null(
            result.OpenedPath
        );

        string[] after =
            Directory
                .EnumerateFileSystemEntries(
                    parent
                )
                .ToArray();

        Assert.Equal(
            before,
            after
        );

        Assert.False(
            File.Exists(
                Path.Combine(
                    temp.RootPath,
                    "escape.nif"
                )
            )
        );
    }

    private static LinuxNoFollowPathHandle OpenParent(
        string root,
        string relativePath)
    {
        LinuxNoFollowPathOpenResult result =
            LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                root,
                relativePath
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
                    "casecompat-openat-exclusive-tests",
                    Guid.NewGuid()
                        .ToString("N")
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
