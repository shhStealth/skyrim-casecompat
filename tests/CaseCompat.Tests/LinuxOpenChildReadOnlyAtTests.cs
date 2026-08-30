using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxOpenChildReadOnlyAtTests
{
    [Fact]
    public void Open_ExistingRegularFile_ReturnsDescriptorForExactChild()
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
                "Final.nif"
            ),
            "fixture"
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        LinuxOpenChildReadOnlyAtResult result =
            LinuxOpenChildReadOnlyAt.Open(
                parent,
                "Final.nif"
            );

        Assert.True(
            result.Success
        );

        using LinuxOpenedChildHandle child =
            Assert.IsType<
                LinuxOpenedChildHandle
            >(
                result.OpenedChild
            );

        Assert.Equal(
            "Final.nif",
            child.ChildName
        );

        LinuxOpenedFileIdentityResult identity =
            LinuxOpenedFileIdentity.Capture(
                child
            );

        Assert.True(
            identity.Success
        );
    }

    [Fact]
    public void Open_MissingChild_IsReportedUnavailable()
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

        LinuxOpenChildReadOnlyAtResult result =
            LinuxOpenChildReadOnlyAt.Open(
                parent,
                "Missing.nif"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxOpenChildReadOnlyAtState
                .ChildUnavailable,
            result.State
        );

        Assert.Null(
            result.OpenedChild
        );
    }

    [Fact]
    public void Open_SymbolicLinkChild_IsRejected()
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

        File.CreateSymbolicLink(
            Path.Combine(
                temp.RootPath,
                "Final.nif"
            ),
            target
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        LinuxOpenChildReadOnlyAtResult result =
            LinuxOpenChildReadOnlyAt.Open(
                parent,
                "Final.nif"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxOpenChildReadOnlyAtState
                .ChildSymbolicLinkRejected,
            result.State
        );

        Assert.Null(
            result.OpenedChild
        );
    }

    [Fact]
    public void Open_ParentPathReplacedAfterOpen_UsesOriginalDirectory()
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

        string originalChild =
            Path.Combine(
                parentPath,
                "Final.nif"
            );

        File.WriteAllText(
            originalChild,
            "original"
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

        File.WriteAllText(
            Path.Combine(
                parentPath,
                "Final.nif"
            ),
            "replacement"
        );

        LinuxOpenChildReadOnlyAtResult result =
            LinuxOpenChildReadOnlyAt.Open(
                parent,
                "Final.nif"
            );

        Assert.True(
            result.Success
        );

        using LinuxOpenedChildHandle child =
            Assert.IsType<
                LinuxOpenedChildHandle
            >(
                result.OpenedChild
            );

        byte[] bytes =
            new byte["original".Length];

        int read =
            RandomAccess.Read(
                child.Handle,
                bytes,
                0
            );

        Assert.Equal(
            bytes.Length,
            read
        );

        Assert.Equal(
            "original",
            System.Text.Encoding.UTF8.GetString(
                bytes
            )
        );
    }

    [Fact]
    public void Open_ClosedParent_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        parent.Dispose();

        LinuxOpenChildReadOnlyAtResult result =
            LinuxOpenChildReadOnlyAt.Open(
                parent,
                "Final.nif"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxOpenChildReadOnlyAtState
                .InvalidParentHandle,
            result.State
        );
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../escape.nif")]
    [InlineData("child/file.nif")]
    [InlineData(@"child\file.nif")]
    [InlineData("")]
    public void Open_InvalidChildName_IsRejected(
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

        LinuxOpenChildReadOnlyAtResult result =
            LinuxOpenChildReadOnlyAt.Open(
                parent,
                childName
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxOpenChildReadOnlyAtState
                .InvalidName,
            result.State
        );

        Assert.Null(
            result.OpenedChild
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
                    "casecompat-open-child-tests",
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
