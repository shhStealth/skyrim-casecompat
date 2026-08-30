using CaseCompat.Filesystem.Linux;
using System.Text;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxPublishUnnamedFileAtTests
{
    [Fact]
    public void Publish_MissingDestination_MakesExactUnnamedInodeVisible()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        LinuxCreateUnnamedFileAtResult create =
            LinuxCreateUnnamedFileAt.Create(
                parent
            );

        if (
            create.State ==
            LinuxCreateUnnamedFileAtState
                .TmpfileUnsupported)
        {
            return;
        }

        using LinuxUnnamedFileHandle source =
            Assert.IsType<
                LinuxUnnamedFileHandle
            >(
                create.OpenedFile
            );

        byte[] content =
            Encoding.UTF8.GetBytes(
                "published-unnamed-inode"
            );

        RandomAccess.Write(
            source.Handle,
            content,
            0
        );

        Assert.True(
            LinuxFsync.Sync(
                source
            ).Success
        );

        Assert.Empty(
            Directory.EnumerateFileSystemEntries(
                temp.RootPath
            )
        );

        LinuxPublishUnnamedFileAtResult publish =
            LinuxPublishUnnamedFileAt.Publish(
                source,
                parent,
                "Final.nif"
            );

        Assert.True(
            publish.Success
        );

        Assert.Equal(
            LinuxPublishUnnamedFileAtState.Published,
            publish.State
        );

        string finalPath =
            Path.Combine(
                temp.RootPath,
                "Final.nif"
            );

        Assert.True(
            File.Exists(
                finalPath
            )
        );

        Assert.Equal(
            content,
            File.ReadAllBytes(
                finalPath
            )
        );

        // Prove the still-open descriptor and final pathname
        // reference the same inode by modifying through the fd.
        RandomAccess.Write(
            source.Handle,
            "X"u8,
            0
        );

        byte[] after =
            File.ReadAllBytes(
                finalPath
            );

        Assert.Equal(
            (byte)'X',
            after[0]
        );
    }

    [Fact]
    public void Publish_ExistingFile_IsConflictAndExistingFileIsUntouched()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string finalPath =
            Path.Combine(
                temp.RootPath,
                "Final.nif"
            );

        File.WriteAllText(
            finalPath,
            "existing"
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        using LinuxUnnamedFileHandle source =
            CreateUnnamedOrReturn(
                parent
            );

        if (source is null)
        {
            return;
        }

        RandomAccess.Write(
            source.Handle,
            "replacement"u8,
            0
        );

        LinuxPublishUnnamedFileAtResult publish =
            LinuxPublishUnnamedFileAt.Publish(
                source,
                parent,
                "Final.nif"
            );

        Assert.False(
            publish.Success
        );

        Assert.Equal(
            LinuxPublishUnnamedFileAtState
                .DestinationExists,
            publish.State
        );

        Assert.Equal(
            "existing",
            File.ReadAllText(
                finalPath
            )
        );
    }

    [Fact]
    public void Publish_ExistingDirectory_IsConflict()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string existing =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Final.nif"
                )
            ).FullName;

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        using LinuxUnnamedFileHandle source =
            CreateUnnamedOrReturn(
                parent
            );

        if (source is null)
        {
            return;
        }

        LinuxPublishUnnamedFileAtResult publish =
            LinuxPublishUnnamedFileAt.Publish(
                source,
                parent,
                "Final.nif"
            );

        Assert.False(
            publish.Success
        );

        Assert.Equal(
            LinuxPublishUnnamedFileAtState
                .DestinationExists,
            publish.State
        );

        Assert.True(
            Directory.Exists(
                existing
            )
        );
    }

    [Fact]
    public void Publish_ExistingSymbolicLink_IsConflictAndLinkIsUntouched()
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
                "target.nif"
            );

        File.WriteAllText(
            target,
            "target"
        );

        string link =
            Path.Combine(
                temp.RootPath,
                "Final.nif"
            );

        File.CreateSymbolicLink(
            link,
            target
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        using LinuxUnnamedFileHandle source =
            CreateUnnamedOrReturn(
                parent
            );

        if (source is null)
        {
            return;
        }

        LinuxPublishUnnamedFileAtResult publish =
            LinuxPublishUnnamedFileAt.Publish(
                source,
                parent,
                "Final.nif"
            );

        Assert.False(
            publish.Success
        );

        Assert.Equal(
            LinuxPublishUnnamedFileAtState
                .DestinationExists,
            publish.State
        );

        Assert.True(
            (File.GetAttributes(
                link
            ) &
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
    public void Publish_CaseDifferentSiblingOnStrictParent_CreatesDistinctName()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string existing =
            Path.Combine(
                temp.RootPath,
                "final.nif"
            );

        File.WriteAllText(
            existing,
            "existing"
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        using LinuxUnnamedFileHandle source =
            CreateUnnamedOrReturn(
                parent
            );

        if (source is null)
        {
            return;
        }

        RandomAccess.Write(
            source.Handle,
            "new"u8,
            0
        );

        LinuxPublishUnnamedFileAtResult publish =
            LinuxPublishUnnamedFileAt.Publish(
                source,
                parent,
                "Final.nif"
            );

        Assert.True(
            publish.Success
        );

        Assert.Equal(
            "existing",
            File.ReadAllText(
                existing
            )
        );

        Assert.Equal(
            "new",
            File.ReadAllText(
                Path.Combine(
                    temp.RootPath,
                    "Final.nif"
                )
            )
        );
    }

    [Fact]
    public void Publish_ParentPathReplacedAfterOpen_PublishesUnderOriginalDirectory()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string parentPath =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "parent"
                )
            ).FullName;

        LinuxNoFollowPathOpenResult parentOpen =
            LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                temp.RootPath,
                "parent"
            );

        using LinuxNoFollowPathHandle parent =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                parentOpen.OpenedPath
            );

        using LinuxUnnamedFileHandle source =
            CreateUnnamedOrReturn(
                parent
            );

        if (source is null)
        {
            return;
        }

        RandomAccess.Write(
            source.Handle,
            "anchored"u8,
            0
        );

        string movedParent =
            Path.Combine(
                temp.RootPath,
                "parent-original"
            );

        Directory.Move(
            parentPath,
            movedParent
        );

        string replacementParent =
            Directory.CreateDirectory(
                parentPath
            ).FullName;

        LinuxPublishUnnamedFileAtResult publish =
            LinuxPublishUnnamedFileAt.Publish(
                source,
                parent,
                "Final.nif"
            );

        Assert.True(
            publish.Success
        );

        Assert.True(
            File.Exists(
                Path.Combine(
                    movedParent,
                    "Final.nif"
                )
            )
        );

        Assert.False(
            File.Exists(
                Path.Combine(
                    replacementParent,
                    "Final.nif"
                )
            )
        );
    }

    [Fact]
    public void Publish_ClosedSource_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        LinuxUnnamedFileHandle source =
            CreateUnnamedOrReturn(
                parent
            );

        if (source is null)
        {
            return;
        }

        source.Dispose();

        LinuxPublishUnnamedFileAtResult publish =
            LinuxPublishUnnamedFileAt.Publish(
                source,
                parent,
                "Final.nif"
            );

        Assert.False(
            publish.Success
        );

        Assert.Equal(
            LinuxPublishUnnamedFileAtState
                .InvalidSourceHandle,
            publish.State
        );
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../escape.nif")]
    [InlineData("child/file.nif")]
    [InlineData(@"child\file.nif")]
    [InlineData("")]
    public void Publish_InvalidChildName_IsRejectedWithoutCreatingAnything(
        string childName)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        using LinuxUnnamedFileHandle source =
            CreateUnnamedOrReturn(
                parent
            );

        if (source is null)
        {
            return;
        }

        LinuxPublishUnnamedFileAtResult publish =
            LinuxPublishUnnamedFileAt.Publish(
                source,
                parent,
                childName
            );

        Assert.False(
            publish.Success
        );

        Assert.Equal(
            LinuxPublishUnnamedFileAtState.InvalidName,
            publish.State
        );

        Assert.Empty(
            Directory.EnumerateFileSystemEntries(
                temp.RootPath
            )
        );
    }

    private static LinuxUnnamedFileHandle
        CreateUnnamedOrReturn(
            LinuxNoFollowPathHandle parent)
    {
        LinuxCreateUnnamedFileAtResult create =
            LinuxCreateUnnamedFileAt.Create(
                parent
            );

        if (
            create.State ==
            LinuxCreateUnnamedFileAtState
                .TmpfileUnsupported)
        {
            return null!;
        }

        Assert.True(
            create.Success
        );

        return Assert.IsType<
            LinuxUnnamedFileHandle
        >(
            create.OpenedFile
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
                    "casecompat-publish-unnamed-tests",
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
