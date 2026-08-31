using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    LinuxDescriptorRelativeDirectoryChainTests
{
    [Fact]
    public void OpenedChildDirectory_CanAnchorNestedCreateAndOpen()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory temp =
            new();

        using LinuxNoFollowPathHandle root =
            OpenRoot(
                temp.RootPath
            );

        LinuxCreateDirectoryAtResult createA =
            LinuxCreateDirectoryAt.Create(
                root,
                "A"
            );

        Assert.True(
            createA.Success,
            createA.Error
        );

        LinuxOpenChildReadOnlyAtResult openA =
            LinuxOpenChildReadOnlyAt.Open(
                root,
                "A"
            );

        Assert.True(
            openA.Success,
            openA.Error
        );

        using LinuxOpenedChildHandle a =
            Assert.IsType<
                LinuxOpenedChildHandle
            >(
                openA.OpenedChild
            );

        /*
         * The important part of this test:
         * no pathname reopen of A occurs here. Its already-open
         * descriptor is the parent for mkdirat("B").
         */
        LinuxCreateDirectoryAtResult createB =
            LinuxCreateDirectoryAt.Create(
                a,
                "B"
            );

        Assert.True(
            createB.Success,
            createB.Error
        );

        LinuxOpenChildReadOnlyAtResult openB =
            LinuxOpenChildReadOnlyAt.Open(
                a,
                "B"
            );

        Assert.True(
            openB.Success,
            openB.Error
        );

        using LinuxOpenedChildHandle b =
            Assert.IsType<
                LinuxOpenedChildHandle
            >(
                openB.OpenedChild
            );

        string bPath =
            Path.Combine(
                temp.RootPath,
                "A",
                "B"
            );

        LinuxOpenedDirectorySnapshotResult snapshot =
            LinuxOpenedDirectorySnapshot.Capture(
                b,
                bPath
            );

        Assert.True(
            snapshot.Success,
            snapshot.Error
        );

        Assert.NotNull(
            snapshot.Identity
        );

        Assert.True(
            Directory.Exists(
                bPath
            )
        );
    }

    [Fact]
    public void RegularFileDescriptor_CannotAnchorChildOperations()
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

        using LinuxNoFollowPathHandle root =
            OpenRoot(
                temp.RootPath
            );

        LinuxOpenChildReadOnlyAtResult opened =
            LinuxOpenChildReadOnlyAt.Open(
                root,
                "file.txt"
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

        LinuxCreateDirectoryAtResult create =
            LinuxCreateDirectoryAt.Create(
                file,
                "Child"
            );

        Assert.False(
            create.Success
        );

        Assert.Equal(
            LinuxCreateDirectoryAtState.ParentNotDirectory,
            create.State
        );

        LinuxOpenChildReadOnlyAtResult open =
            LinuxOpenChildReadOnlyAt.Open(
                file,
                "Child"
            );

        Assert.False(
            open.Success
        );

        Assert.Equal(
            LinuxOpenChildReadOnlyAtState.ParentNotDirectory,
            open.State
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
                    "casecompat-descriptor-directory-chain-tests",
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
