using CaseCompat.Filesystem.Linux;
using System.Text;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxRemoveOwnedFileAtTests
{
    [Fact]
    public void Remove_PublishedFileWithMatchingIdentity_RemovesName()
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

        using LinuxUnnamedFileHandle unnamed =
            Assert.IsType<
                LinuxUnnamedFileHandle
            >(
                create.OpenedFile
            );

        byte[] content =
            Encoding.UTF8.GetBytes(
                "rollback-owned-file"
            );

        RandomAccess.Write(
            unnamed.Handle,
            content,
            0
        );

        Assert.True(
            LinuxFsync.Sync(
                unnamed
            ).Success
        );

        LinuxFileIncarnationIdentity expected =
            CaptureIdentity(
                unnamed
            );

        Assert.True(
            LinuxPublishUnnamedFileAt.Publish(
                unnamed,
                parent,
                "Final.nif"
            ).Success
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

        LinuxRemoveOwnedFileAtResult remove =
            LinuxRemoveOwnedFileAt.Remove(
                parent,
                "Final.nif",
                expected
            );

        Assert.True(
            remove.Success
        );

        Assert.Equal(
            LinuxRemoveOwnedFileAtState.Removed,
            remove.State
        );

        Assert.False(
            File.Exists(
                finalPath
            )
        );

        // The directory entry is gone, but the still-open
        // source descriptor continues to reference the inode.
        byte[] readBuffer =
            new byte[content.Length];

        int read =
            RandomAccess.Read(
                unnamed.Handle,
                readBuffer,
                0
            );

        Assert.Equal(
            content.Length,
            read
        );

        Assert.Equal(
            content,
            readBuffer
        );

        LinuxOpenedFileIdentityResult after =
            LinuxOpenedFileIdentity.Capture(
                unnamed
            );

        Assert.True(
            expected.PhysicalIdentity.SameObjectAs(
                after
            )
        );

        Assert.Equal(
            0U,
            after.LinkCount
        );

        // Durability remains a separate operation.
        Assert.True(
            LinuxFsync.Sync(
                parent
            ).Success
        );
    }

    [Fact]
    public void Remove_SamePhysicalIdentityDifferentGeneration_RefusesFile()
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
            "owned"
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        LinuxFileIncarnationIdentity actual =
            CaptureIdentity(
                parent,
                "Final.nif"
            );

        LinuxFileIncarnationIdentity wrongGeneration =
            actual with
            {
                InodeGeneration =
                    actual.InodeGeneration ==
                        uint.MaxValue
                        ? 0U
                        : actual.InodeGeneration + 1U
            };

        Assert.True(
            wrongGeneration.PhysicalIdentity
                .SameObjectAs(
                    actual.PhysicalIdentity
                )
        );

        Assert.False(
            wrongGeneration.SameIncarnationAs(
                actual
            )
        );

        LinuxRemoveOwnedFileAtResult remove =
            LinuxRemoveOwnedFileAt.Remove(
                parent,
                "Final.nif",
                wrongGeneration
            );

        Assert.False(
            remove.Success
        );

        Assert.Equal(
            LinuxRemoveOwnedFileAtState.IdentityMismatch,
            remove.State
        );

        Assert.NotNull(
            remove.ActualIdentity
        );

        Assert.True(
            actual.SameIncarnationAs(
                remove.ActualIdentity!
            )
        );

        Assert.Equal(
            "owned",
            File.ReadAllText(
                finalPath
            )
        );
    }

    [Fact]
    public void Remove_FinalPathReplacedBeforeRollback_RefusesReplacement()
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
            "owned"
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        LinuxFileIncarnationIdentity expected =
            CaptureIdentity(
                parent,
                "Final.nif"
            );

        string movedPath =
            Path.Combine(
                temp.RootPath,
                "Final-original.nif"
            );

        File.Move(
            finalPath,
            movedPath
        );

        File.WriteAllText(
            finalPath,
            "replacement"
        );

        LinuxRemoveOwnedFileAtResult remove =
            LinuxRemoveOwnedFileAt.Remove(
                parent,
                "Final.nif",
                expected
            );

        Assert.False(
            remove.Success
        );

        Assert.Equal(
            LinuxRemoveOwnedFileAtState
                .IdentityMismatch,
            remove.State
        );

        Assert.Equal(
            "replacement",
            File.ReadAllText(
                finalPath
            )
        );

        Assert.Equal(
            "owned",
            File.ReadAllText(
                movedPath
            )
        );
    }

    [Fact]
    public void Remove_SymbolicLinkChild_IsRejectedAndTargetUntouched()
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
                "owned.nif"
            ),
            "owned"
        );

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

        LinuxFileIncarnationIdentity expected =
            CaptureIdentity(
                parent,
                "owned.nif"
            );

        LinuxRemoveOwnedFileAtResult remove =
            LinuxRemoveOwnedFileAt.Remove(
                parent,
                "Final.nif",
                expected
            );

        Assert.False(
            remove.Success
        );

        Assert.Equal(
            LinuxRemoveOwnedFileAtState
                .ChildSymbolicLinkRejected,
            remove.State
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
    public void Remove_DirectoryChild_IsRejected()
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
                "owned.nif"
            ),
            "owned"
        );

        Directory.CreateDirectory(
            Path.Combine(
                temp.RootPath,
                "Final.nif"
            )
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        LinuxFileIncarnationIdentity expected =
            CaptureIdentity(
                parent,
                "owned.nif"
            );

        LinuxRemoveOwnedFileAtResult remove =
            LinuxRemoveOwnedFileAt.Remove(
                parent,
                "Final.nif",
                expected
            );

        Assert.False(
            remove.Success
        );

        Assert.Equal(
            LinuxRemoveOwnedFileAtState
                .ChildNotRegularFile,
            remove.State
        );

        Assert.True(
            Directory.Exists(
                Path.Combine(
                    temp.RootPath,
                    "Final.nif"
                )
            )
        );
    }

    [Fact]
    public void Remove_MissingChild_IsReportedUnavailable()
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
                "owned.nif"
            ),
            "owned"
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        LinuxFileIncarnationIdentity expected =
            CaptureIdentity(
                parent,
                "owned.nif"
            );

        LinuxRemoveOwnedFileAtResult remove =
            LinuxRemoveOwnedFileAt.Remove(
                parent,
                "Missing.nif",
                expected
            );

        Assert.False(
            remove.Success
        );

        Assert.Equal(
            LinuxRemoveOwnedFileAtState
                .ChildUnavailable,
            remove.State
        );
    }

    [Fact]
    public void Remove_ParentPathReplacedAfterOpen_UsesOriginalDirectory()
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

        File.WriteAllText(
            Path.Combine(
                parentPath,
                "Final.nif"
            ),
            "owned"
        );

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

        LinuxFileIncarnationIdentity expected =
            CaptureIdentity(
                parent,
                "Final.nif"
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

        Directory.CreateDirectory(
            parentPath
        );

        string replacement =
            Path.Combine(
                parentPath,
                "Final.nif"
            );

        File.WriteAllText(
            replacement,
            "replacement"
        );

        LinuxRemoveOwnedFileAtResult remove =
            LinuxRemoveOwnedFileAt.Remove(
                parent,
                "Final.nif",
                expected
            );

        Assert.True(
            remove.Success
        );

        Assert.False(
            File.Exists(
                Path.Combine(
                    movedParent,
                    "Final.nif"
                )
            )
        );

        Assert.Equal(
            "replacement",
            File.ReadAllText(
                replacement
            )
        );
    }

    [Fact]
    public void Remove_InvalidExpectedIdentity_RefusesWithoutDeleting()
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
            "fixture"
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        var invalidExpected =
            new LinuxFileIncarnationIdentity(
                PhysicalIdentity:
                    new LinuxOpenedFileIdentityResult(
                        State:
                            LinuxOpenedFileIdentityState
                                .MetadataUnavailable,
                        DeviceMajor:
                            null,
                        DeviceMinor:
                            null,
                        Inode:
                            null,
                        LinkCount:
                            null,
                        MountId:
                            null,
                        Errno:
                            null,
                        Error:
                            "fixture"
                    ),
                InodeGeneration:
                    0U
            );

        LinuxRemoveOwnedFileAtResult remove =
            LinuxRemoveOwnedFileAt.Remove(
                parent,
                "Final.nif",
                invalidExpected
            );

        Assert.False(
            remove.Success
        );

        Assert.Equal(
            LinuxRemoveOwnedFileAtState
                .InvalidExpectedIdentity,
            remove.State
        );

        Assert.Equal(
            "fixture",
            File.ReadAllText(
                finalPath
            )
        );
    }

    [Fact]
    public void Remove_ClosedParent_IsRejectedWithoutDeleting()
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
            "fixture"
        );

        LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        LinuxFileIncarnationIdentity expected =
            CaptureIdentity(
                parent,
                "Final.nif"
            );

        parent.Dispose();

        LinuxRemoveOwnedFileAtResult remove =
            LinuxRemoveOwnedFileAt.Remove(
                parent,
                "Final.nif",
                expected
            );

        Assert.False(
            remove.Success
        );

        Assert.Equal(
            LinuxRemoveOwnedFileAtState
                .InvalidParentHandle,
            remove.State
        );

        Assert.Equal(
            "fixture",
            File.ReadAllText(
                finalPath
            )
        );
    }

    [Fact]
    public void Remove_RegularFileParent_IsRejectedAsNotDirectory()
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
                "parent.bin"
            ),
            "parent"
        );

        LinuxNoFollowPathOpenResult parentOpen =
            LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                temp.RootPath,
                "parent.bin"
            );

        using LinuxNoFollowPathHandle notDirectory =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                parentOpen.OpenedPath
            );

        LinuxFileIncarnationIdentity expected =
            CaptureIdentity(
                notDirectory
            );

        LinuxRemoveOwnedFileAtResult remove =
            LinuxRemoveOwnedFileAt.Remove(
                notDirectory,
                "Final.nif",
                expected
            );

        Assert.False(
            remove.Success
        );

        Assert.Equal(
            LinuxRemoveOwnedFileAtState
                .ParentNotDirectory,
            remove.State
        );
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../escape.nif")]
    [InlineData("child/file.nif")]
    [InlineData(@"child\file.nif")]
    [InlineData("")]
    public void Remove_InvalidChildName_IsRejectedWithoutDeleting(
        string childName)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string ownedPath =
            Path.Combine(
                temp.RootPath,
                "owned.nif"
            );

        File.WriteAllText(
            ownedPath,
            "owned"
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        LinuxFileIncarnationIdentity expected =
            CaptureIdentity(
                parent,
                "owned.nif"
            );

        LinuxRemoveOwnedFileAtResult remove =
            LinuxRemoveOwnedFileAt.Remove(
                parent,
                childName,
                expected
            );

        Assert.False(
            remove.Success
        );

        Assert.Equal(
            LinuxRemoveOwnedFileAtState
                .InvalidName,
            remove.State
        );

        Assert.Equal(
            "owned",
            File.ReadAllText(
                ownedPath
            )
        );
    }

    private static LinuxFileIncarnationIdentity CaptureIdentity(
        LinuxNoFollowPathHandle parent,
        string childName)
    {
        LinuxOpenChildReadOnlyAtResult opened =
            LinuxOpenChildReadOnlyAt.Open(
                parent,
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

        return CaptureIdentity(
            child
        );
    }

    private static LinuxFileIncarnationIdentity CaptureIdentity(
        ILinuxOpenedHandle openedFile)
    {
        LinuxOpenedFileIncarnationResult capture =
            LinuxOpenedFileIncarnation.Capture(
                openedFile
            );

        Assert.True(
            capture.Success,
            capture.Error
        );

        return Assert.IsType<
            LinuxFileIncarnationIdentity
        >(
            capture.Identity
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
                    "casecompat-remove-owned-tests",
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
