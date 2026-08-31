using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxCreateDirectoryAtTests
{
    [Fact]
    public void Create_MissingChild_CreatesExactlyOneDirectory()
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

        LinuxCreateDirectoryAtResult result =
            LinuxCreateDirectoryAt.Create(
                opened,
                "Created"
            );

        Assert.True(
            result.Success
        );

        Assert.Equal(
            LinuxCreateDirectoryAtState.Created,
            result.State
        );

        Assert.True(
            Directory.Exists(
                Path.Combine(
                    parent,
                    "Created"
                )
            )
        );

        Assert.Null(
            result.Error
        );
    }

    [Fact]
    public void Create_ExistingDirectory_IsConflict()
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
                "Existing"
            )
        );

        using LinuxNoFollowPathHandle opened =
            OpenParent(
                temp.RootPath,
                "parent"
            );

        LinuxCreateDirectoryAtResult result =
            LinuxCreateDirectoryAt.Create(
                opened,
                "Existing"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxCreateDirectoryAtState
                .DestinationExists,
            result.State
        );
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
                "Existing"
            );

        File.WriteAllText(
            existing,
            "do-not-touch"
        );

        using LinuxNoFollowPathHandle opened =
            OpenParent(
                temp.RootPath,
                "parent"
            );

        LinuxCreateDirectoryAtResult result =
            LinuxCreateDirectoryAt.Create(
                opened,
                "Existing"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxCreateDirectoryAtState
                .DestinationExists,
            result.State
        );

        Assert.True(
            File.Exists(
                existing
            )
        );

        Assert.Equal(
            "do-not-touch",
            File.ReadAllText(
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
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "target"
                )
            ).FullName;

        string link =
            Path.Combine(
                parent,
                "Existing"
            );

        Directory.CreateSymbolicLink(
            link,
            target
        );

        using LinuxNoFollowPathHandle opened =
            OpenParent(
                temp.RootPath,
                "parent"
            );

        LinuxCreateDirectoryAtResult result =
            LinuxCreateDirectoryAt.Create(
                opened,
                "Existing"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxCreateDirectoryAtState
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

        using LinuxNoFollowPathHandle opened =
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

        LinuxCreateDirectoryAtResult result =
            LinuxCreateDirectoryAt.Create(
                opened,
                "Created"
            );

        Assert.True(
            result.Success
        );

        Assert.True(
            Directory.Exists(
                Path.Combine(
                    movedParent,
                    "Created"
                )
            )
        );

        Assert.False(
            Directory.Exists(
                Path.Combine(
                    replacementParent,
                    "Created"
                )
            )
        );
    }

    [Fact]
    public void Create_CaseDifferentSiblingOnStrictParent_CreatesDistinctDirectory()
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
                    "freehorse"
                )
            ).FullName;

        LinuxFileIdentityResult existingIdentityBefore =
            LinuxFileIdentity.Inspect(
                existing
            );

        Assert.True(
            existingIdentityBefore.Success
        );

        using LinuxNoFollowPathHandle opened =
            OpenParent(
                temp.RootPath,
                "parent"
            );

        LinuxCreateDirectoryAtResult result =
            LinuxCreateDirectoryAt.Create(
                opened,
                "FreeHorse"
            );

        Assert.True(
            result.Success
        );

        string created =
            Path.Combine(
                parent,
                "FreeHorse"
            );

        Assert.True(
            Directory.Exists(
                existing
            )
        );

        Assert.True(
            Directory.Exists(
                created
            )
        );

        LinuxFileIdentityResult existingIdentityAfter =
            LinuxFileIdentity.Inspect(
                existing
            );

        LinuxFileIdentityResult createdIdentity =
            LinuxFileIdentity.Inspect(
                created
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

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../escape")]
    [InlineData("child/grandchild")]
    [InlineData(@"child\\grandchild")]
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

        using LinuxNoFollowPathHandle opened =
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

        LinuxCreateDirectoryAtResult result =
            LinuxCreateDirectoryAt.Create(
                opened,
                childName
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxCreateDirectoryAtState.InvalidName,
            result.State
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
            Directory.Exists(
                Path.Combine(
                    temp.RootPath,
                    "escape"
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
                    "casecompat-mkdirat-tests",
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
