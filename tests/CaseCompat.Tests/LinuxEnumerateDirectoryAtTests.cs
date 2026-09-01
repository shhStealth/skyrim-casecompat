using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class LinuxEnumerateDirectoryAtTests
{
    [Fact]
    public void
        Enumerate_ReturnsSortedExactDirectChildNames()
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
                "beta"
            ),
            "b"
        );

        File.WriteAllText(
            Path.Combine(
                temp.RootPath,
                "Alpha"
            ),
            "a"
        );

        File.WriteAllText(
            Path.Combine(
                temp.RootPath,
                ".hidden"
            ),
            "h"
        );

        Directory.CreateDirectory(
            Path.Combine(
                temp.RootPath,
                "Subdir"
            )
        );

        string linkTarget =
            Path.Combine(
                temp.RootPath,
                "beta"
            );

        File.CreateSymbolicLink(
            Path.Combine(
                temp.RootPath,
                "link"
            ),
            linkTarget
        );

        using LinuxNoFollowPathHandle directory =
            OpenRoot(
                temp.RootPath
            );

        LinuxEnumerateDirectoryAtResult result =
            LinuxEnumerateDirectoryAt.Enumerate(
                directory
            );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            LinuxEnumerateDirectoryAtState.Enumerated,
            result.State
        );

        Assert.Equal(
            [
                ".hidden",
                "Alpha",
                "Subdir",
                "beta",
                "link"
            ],
            result.ChildNames
        );

        Assert.Null(
            result.Errno
        );

        Assert.Null(
            result.Error
        );
    }

    [Fact]
    public void
        Enumerate_EmptyDirectory_ReturnsEmptySuccess()
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

        LinuxEnumerateDirectoryAtResult result =
            LinuxEnumerateDirectoryAt.Enumerate(
                directory
            );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Empty(
            result.ChildNames
        );
    }

    [Fact]
    public void
        Enumerate_RepeatedCallsDoNotConsumeCallerDirectoryOffset()
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
                "one"
            ),
            "1"
        );

        File.WriteAllText(
            Path.Combine(
                temp.RootPath,
                "two"
            ),
            "2"
        );

        using LinuxNoFollowPathHandle directory =
            OpenRoot(
                temp.RootPath
            );

        LinuxEnumerateDirectoryAtResult first =
            LinuxEnumerateDirectoryAt.Enumerate(
                directory
            );

        LinuxEnumerateDirectoryAtResult second =
            LinuxEnumerateDirectoryAt.Enumerate(
                directory
            );

        Assert.True(
            first.Success,
            first.Error
        );

        Assert.True(
            second.Success,
            second.Error
        );

        Assert.Equal(
            ["one", "two"],
            first.ChildNames
        );

        Assert.Equal(
            first.ChildNames,
            second.ChildNames
        );
    }

    [Fact]
    public void
        Enumerate_ParentPathReplacedAfterOpen_UsesOriginalDirectory()
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
                "original-one"
            ),
            "1"
        );

        File.WriteAllText(
            Path.Combine(
                parentPath,
                "original-two"
            ),
            "2"
        );

        LinuxNoFollowPathOpenResult opened =
            LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                temp.RootPath,
                "parent"
            );

        Assert.True(
            opened.Success,
            opened.Error
        );

        using LinuxNoFollowPathHandle directory =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                opened.OpenedPath
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
                "replacement-only"
            ),
            "replacement"
        );

        LinuxEnumerateDirectoryAtResult result =
            LinuxEnumerateDirectoryAt.Enumerate(
                directory
            );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            [
                "original-one",
                "original-two"
            ],
            result.ChildNames
        );

        Assert.DoesNotContain(
            "replacement-only",
            result.ChildNames
        );
    }

    [Fact]
    public void
        Enumerate_RegularFileHandle_IsReportedNotDirectory()
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
                "file.nif"
            ),
            "fixture"
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        LinuxOpenChildReadOnlyAtResult opened =
            LinuxOpenChildReadOnlyAt.Open(
                parent,
                "file.nif"
            );

        Assert.True(
            opened.Success,
            opened.Error
        );

        using LinuxOpenedChildHandle file =
            Assert.IsType<
                LinuxOpenedChildHandle
            >(
                opened.OpenedChild
            );

        LinuxEnumerateDirectoryAtResult result =
            LinuxEnumerateDirectoryAt.Enumerate(
                file
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxEnumerateDirectoryAtState.NotDirectory,
            result.State
        );

        Assert.Empty(
            result.ChildNames
        );
    }

    [Fact]
    public void
        Enumerate_ClosedHandle_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        LinuxNoFollowPathHandle directory =
            OpenRoot(
                temp.RootPath
            );

        directory.Dispose();

        LinuxEnumerateDirectoryAtResult result =
            LinuxEnumerateDirectoryAt.Enumerate(
                directory
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxEnumerateDirectoryAtState
                .InvalidDirectoryHandle,
            result.State
        );

        Assert.Empty(
            result.ChildNames
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
            result.Success,
            result.Error
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
                    "casecompat-enumerate-directory-tests",
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
                    RootPath))
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
