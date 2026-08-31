using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    LinuxOpenedDirectorySnapshotHandleTests
{
    [Fact]
    public void Capture_DirectChildDirectory_UsesOpenedDescriptor()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory temp =
            new();

        string childPath =
            Path.Combine(
                temp.RootPath,
                "Child"
            );

        Directory.CreateDirectory(
            childPath
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        LinuxOpenChildReadOnlyAtResult opened =
            LinuxOpenChildReadOnlyAt.Open(
                parent,
                "Child"
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

        LinuxOpenedDirectorySnapshotResult directSnapshot =
            LinuxOpenedDirectorySnapshot.Capture(
                child,
                childPath
            );

        Assert.True(
            directSnapshot.Success,
            directSnapshot.Error
        );

        Assert.Equal(
            childPath,
            directSnapshot.FullPath
        );

        Assert.NotNull(
            directSnapshot.Identity
        );

        /*
         * Compare against another descriptor for the same
         * physical directory. This proves the generalized
         * capture reports the descriptor's object identity.
         */
        using LinuxNoFollowPathHandle independentlyOpened =
            OpenRoot(
                childPath
            );

        LinuxOpenedDirectorySnapshotResult independentSnapshot =
            LinuxOpenedDirectorySnapshot.Capture(
                independentlyOpened
            );

        Assert.True(
            independentSnapshot.Success,
            independentSnapshot.Error
        );

        Assert.True(
            directSnapshot.Identity!
                .SameObjectAs(
                    independentSnapshot.Identity!
                )
        );

        Assert.Equal(
            independentSnapshot.CasefoldEnabled,
            directSnapshot.CasefoldEnabled
        );
    }

    [Fact]
    public void Capture_DirectChildRegularFile_IsRejectedAsNotDirectory()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory temp =
            new();

        string filePath =
            Path.Combine(
                temp.RootPath,
                "file.txt"
            );

        File.WriteAllText(
            filePath,
            "file"
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        LinuxOpenChildReadOnlyAtResult opened =
            LinuxOpenChildReadOnlyAt.Open(
                parent,
                "file.txt"
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

        LinuxOpenedDirectorySnapshotResult snapshot =
            LinuxOpenedDirectorySnapshot.Capture(
                child,
                filePath
            );

        Assert.False(
            snapshot.Success
        );

        Assert.Equal(
            LinuxOpenedDirectorySnapshotState.NotDirectory,
            snapshot.State
        );
    }

    private static LinuxNoFollowPathHandle OpenRoot(
        string path)
    {
        LinuxNoFollowPathOpenResult result =
            LinuxNoFollowPath.OpenRootReadOnly(
                path
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
                    "casecompat-directory-snapshot-handle-tests",
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
