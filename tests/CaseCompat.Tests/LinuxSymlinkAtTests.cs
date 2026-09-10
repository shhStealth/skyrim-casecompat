using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxSymlinkAtTests
{
    [Fact]
    public void Create_ValidLinkAndTarget_CreatesSymbolicLinkWithExactTarget()
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

        Directory.CreateDirectory(
            Path.Combine(
                parent,
                "Actors"
            )
        );

        using LinuxNoFollowPathHandle opened =
            OpenParent(
                temp.RootPath,
                "parent"
            );

        LinuxCreateSymlinkAtResult result =
            LinuxCreateSymlinkAt.Create(
                opened,
                "actors",
                "Actors"
            );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            LinuxCreateSymlinkAtState.Created,
            result.State
        );

        string linkPath =
            Path.Combine(
                parent,
                "actors"
            );

        Assert.True(
            File.GetAttributes(
                linkPath
            ).HasFlag(
                FileAttributes.ReparsePoint
            )
        );

        Assert.Equal(
            "Actors",
            new FileInfo(
                linkPath
            ).LinkTarget
        );
    }

    [Fact]
    public void Create_LinkNameAlreadyExists_RefusesWithDestinationExists()
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

        Directory.CreateDirectory(
            Path.Combine(
                parent,
                "Actors"
            )
        );

        Directory.CreateDirectory(
            Path.Combine(
                parent,
                "actors"
            )
        );

        using LinuxNoFollowPathHandle opened =
            OpenParent(
                temp.RootPath,
                "parent"
            );

        LinuxCreateSymlinkAtResult result =
            LinuxCreateSymlinkAt.Create(
                opened,
                "actors",
                "Actors"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxCreateSymlinkAtState.DestinationExists,
            result.State
        );

        // The existing real "actors" directory must be untouched.
        Assert.True(
            Directory.Exists(
                Path.Combine(
                    parent,
                    "actors"
                )
            )
        );

        Assert.False(
            File.GetAttributes(
                Path.Combine(
                    parent,
                    "actors"
                )
            ).HasFlag(
                FileAttributes.ReparsePoint
            )
        );
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData(".")]
    [InlineData("..")]
    public void Create_InvalidLinkName_RefusesWithoutCreatingAnything(
        string invalidLinkName)
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

        Directory.CreateDirectory(
            Path.Combine(
                parent,
                "Actors"
            )
        );

        using LinuxNoFollowPathHandle opened =
            OpenParent(
                temp.RootPath,
                "parent"
            );

        LinuxCreateSymlinkAtResult result =
            LinuxCreateSymlinkAt.Create(
                opened,
                invalidLinkName,
                "Actors"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxCreateSymlinkAtState.InvalidLinkName,
            result.State
        );
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData(".")]
    [InlineData("..")]
    public void Create_InvalidTargetName_RefusesWithoutCreatingAnything(
        string invalidTargetName)
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

        using LinuxNoFollowPathHandle opened =
            OpenParent(
                temp.RootPath,
                "parent"
            );

        LinuxCreateSymlinkAtResult result =
            LinuxCreateSymlinkAt.Create(
                opened,
                "actors",
                invalidTargetName
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxCreateSymlinkAtState.InvalidTargetName,
            result.State
        );

        Assert.False(
            File.Exists(
                Path.Combine(
                    parent,
                    "actors"
                )
            ) ||
            Directory.Exists(
                Path.Combine(
                    parent,
                    "actors"
                )
            )
        );
    }

    [Fact]
    public void Read_ExistingSymlinkCreatedIndependently_ReturnsExactTarget()
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

        Directory.CreateDirectory(
            Path.Combine(
                parent,
                "Actors"
            )
        );

        // Set up the symlink via the BCL, independent of
        // LinuxCreateSymlinkAt, so this test does not depend on the
        // create primitive being correct.
        Directory.CreateSymbolicLink(
            Path.Combine(
                parent,
                "actors"
            ),
            "Actors"
        );

        using LinuxNoFollowPathHandle opened =
            OpenParent(
                temp.RootPath,
                "parent"
            );

        LinuxReadSymlinkAtResult result =
            LinuxReadSymlinkAt.Read(
                opened,
                "actors"
            );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            "Actors",
            result.Target
        );
    }

    [Fact]
    public void Read_ChildIsRegularDirectory_ReturnsChildNotSymbolicLink()
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

        Directory.CreateDirectory(
            Path.Combine(
                parent,
                "Actors"
            )
        );

        using LinuxNoFollowPathHandle opened =
            OpenParent(
                temp.RootPath,
                "parent"
            );

        LinuxReadSymlinkAtResult result =
            LinuxReadSymlinkAt.Read(
                opened,
                "Actors"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxReadSymlinkAtState.ChildNotSymbolicLink,
            result.State
        );
    }

    [Fact]
    public void Read_ChildDoesNotExist_ReturnsChildUnavailable()
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

        using LinuxNoFollowPathHandle opened =
            OpenParent(
                temp.RootPath,
                "parent"
            );

        LinuxReadSymlinkAtResult result =
            LinuxReadSymlinkAt.Read(
                opened,
                "does-not-exist"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxReadSymlinkAtState.ChildUnavailable,
            result.State
        );
    }

    [Fact]
    public void CreateThenRead_RoundTrips_TargetMatchesExactly()
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

        Directory.CreateDirectory(
            Path.Combine(
                parent,
                "Character Assets"
            )
        );

        using LinuxNoFollowPathHandle opened =
            OpenParent(
                temp.RootPath,
                "parent"
            );

        LinuxCreateSymlinkAtResult created =
            LinuxCreateSymlinkAt.Create(
                opened,
                "character assets",
                "Character Assets"
            );

        Assert.True(
            created.Success,
            created.Error
        );

        LinuxReadSymlinkAtResult read =
            LinuxReadSymlinkAt.Read(
                opened,
                "character assets"
            );

        Assert.True(
            read.Success,
            read.Error
        );

        Assert.Equal(
            "Character Assets",
            read.Target
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
                    "casecompat-symlinkat-tests",
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
                    recursive: true
                );
            }
        }
    }
}
