using CaseCompat.Filesystem.Linux;
using System.Text;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxRenameChildAtTests
{
    [Fact]
    public void Rename_SameDirectoryCaseOnlyChange_MovesTheOneDirectoryEntry()
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

        File.WriteAllText(
            Path.Combine(
                parent,
                "femalehead.tri"
            ),
            "content"
        );

        using LinuxNoFollowPathHandle opened =
            OpenParent(
                temp.RootPath,
                "parent"
            );

        LinuxRenameChildAtResult result =
            LinuxRenameChildAt.Rename(
                opened,
                "femalehead.tri",
                opened,
                "FemaleHead.tri"
            );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            LinuxRenameChildAtState.Renamed,
            result.State
        );

        Assert.False(
            File.Exists(
                Path.Combine(
                    parent,
                    "femalehead.tri"
                )
            )
        );

        Assert.True(
            File.Exists(
                Path.Combine(
                    parent,
                    "FemaleHead.tri"
                )
            )
        );

        Assert.Equal(
            "content",
            File.ReadAllText(
                Path.Combine(
                    parent,
                    "FemaleHead.tri"
                )
            )
        );

        // No duplicate: exactly one entry for this asset, not two
        // case-variant names resolving side by side.
        Assert.Single(
            Directory.GetFileSystemEntries(
                parent
            )
        );
    }

    [Fact]
    public void Rename_PreservesTheSameInode()
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

        string sourcePath =
            Path.Combine(
                parent,
                "femalehead.tri"
            );

        File.WriteAllText(
            sourcePath,
            "content"
        );

        LinuxFileIdentityResult beforeIdentity =
            LinuxFileIdentity.Inspect(
                sourcePath
            );

        Assert.True(
            beforeIdentity.Success,
            beforeIdentity.Error
        );

        using LinuxNoFollowPathHandle opened =
            OpenParent(
                temp.RootPath,
                "parent"
            );

        LinuxRenameChildAtResult result =
            LinuxRenameChildAt.Rename(
                opened,
                "femalehead.tri",
                opened,
                "FemaleHead.tri"
            );

        Assert.True(
            result.Success,
            result.Error
        );

        LinuxFileIdentityResult afterIdentity =
            LinuxFileIdentity.Inspect(
                Path.Combine(
                    parent,
                    "FemaleHead.tri"
                )
            );

        Assert.True(
            afterIdentity.Success,
            afterIdentity.Error
        );

        Assert.True(
            beforeIdentity.SameObjectAs(
                afterIdentity
            )
        );
    }

    [Fact]
    public void Rename_AcrossDirectories_MovesToTheNewParent()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string sourceParentPath =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "highpolyhead"
                )
            ).FullName;

        string destinationParentPath =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "HighPolyHead"
                )
            ).FullName;

        File.WriteAllText(
            Path.Combine(
                sourceParentPath,
                "femalehead.tri"
            ),
            "content"
        );

        using LinuxNoFollowPathHandle sourceParent =
            OpenParent(
                temp.RootPath,
                "highpolyhead"
            );

        using LinuxNoFollowPathHandle destinationParent =
            OpenParent(
                temp.RootPath,
                "HighPolyHead"
            );

        LinuxRenameChildAtResult result =
            LinuxRenameChildAt.Rename(
                sourceParent,
                "femalehead.tri",
                destinationParent,
                "FemaleHead.tri"
            );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.False(
            File.Exists(
                Path.Combine(
                    sourceParentPath,
                    "femalehead.tri"
                )
            )
        );

        Assert.True(
            File.Exists(
                Path.Combine(
                    destinationParentPath,
                    "FemaleHead.tri"
                )
            )
        );
    }

    [Fact]
    public void Rename_DestinationAlreadyExists_RefusesWithoutClobbering()
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

        File.WriteAllText(
            Path.Combine(
                parent,
                "source.tri"
            ),
            "source content"
        );

        File.WriteAllText(
            Path.Combine(
                parent,
                "Destination.tri"
            ),
            "pre-existing destination content"
        );

        using LinuxNoFollowPathHandle opened =
            OpenParent(
                temp.RootPath,
                "parent"
            );

        LinuxRenameChildAtResult result =
            LinuxRenameChildAt.Rename(
                opened,
                "source.tri",
                opened,
                "Destination.tri"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxRenameChildAtState.DestinationExists,
            result.State
        );

        Assert.True(
            File.Exists(
                Path.Combine(
                    parent,
                    "source.tri"
                )
            )
        );

        Assert.Equal(
            "pre-existing destination content",
            File.ReadAllText(
                Path.Combine(
                    parent,
                    "Destination.tri"
                )
            )
        );
    }

    [Fact]
    public void Rename_MissingSource_Refuses()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        Directory.CreateDirectory(
            Path.Combine(
                temp.RootPath,
                "parent"
            )
        );

        using LinuxNoFollowPathHandle opened =
            OpenParent(
                temp.RootPath,
                "parent"
            );

        LinuxRenameChildAtResult result =
            LinuxRenameChildAt.Rename(
                opened,
                "does-not-exist.tri",
                opened,
                "Whatever.tri"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxRenameChildAtState.SourceUnavailable,
            result.State
        );
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a/b")]
    public void Rename_InvalidSourceName_IsRejected(
        string invalidName)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        Directory.CreateDirectory(
            Path.Combine(
                temp.RootPath,
                "parent"
            )
        );

        using LinuxNoFollowPathHandle opened =
            OpenParent(
                temp.RootPath,
                "parent"
            );

        LinuxRenameChildAtResult result =
            LinuxRenameChildAt.Rename(
                opened,
                invalidName,
                opened,
                "Whatever.tri"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxRenameChildAtState.InvalidSourceName,
            result.State
        );
    }

    [Fact]
    public void Rename_SameNameSameParent_IsRejected()
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

        File.WriteAllText(
            Path.Combine(
                parent,
                "same.tri"
            ),
            "content"
        );

        using LinuxNoFollowPathHandle opened =
            OpenParent(
                temp.RootPath,
                "parent"
            );

        LinuxRenameChildAtResult result =
            LinuxRenameChildAt.Rename(
                opened,
                "same.tri",
                opened,
                "same.tri"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxRenameChildAtState.SameNameSameParent,
            result.State
        );

        Assert.True(
            File.Exists(
                Path.Combine(
                    parent,
                    "same.tri"
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
                    "casecompat-renameat2-tests",
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
