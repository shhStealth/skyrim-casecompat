using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class LinuxInspectChildAtTests
{
    [Fact]
    public void
        Inspect_ClassifiesFileDirectoryAndSymbolicLinkWithoutFollowing()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string root =
            CreateTemporaryRoot();

        try
        {
            string directoryPath =
                Path.Combine(
                    root,
                    "Directory"
                );

            Directory.CreateDirectory(
                directoryPath
            );

            File.WriteAllText(
                Path.Combine(
                    root,
                    "File.nif"
                ),
                "fixture"
            );

            Directory.CreateSymbolicLink(
                Path.Combine(
                    root,
                    "Link"
                ),
                directoryPath
            );

            using LinuxNoFollowPathHandle parent =
                OpenRoot(root);

            LinuxInspectChildAtResult directory =
                LinuxInspectChildAt.Inspect(
                    parent,
                    "Directory"
                );

            LinuxInspectChildAtResult file =
                LinuxInspectChildAt.Inspect(
                    parent,
                    "File.nif"
                );

            LinuxInspectChildAtResult link =
                LinuxInspectChildAt.Inspect(
                    parent,
                    "Link"
                );

            Assert.True(
                directory.Success,
                directory.Error
            );

            Assert.True(
                file.Success,
                file.Error
            );

            Assert.True(
                link.Success,
                link.Error
            );

            Assert.Equal(
                LinuxChildObjectKind.Directory,
                directory.Kind
            );

            Assert.Equal(
                LinuxChildObjectKind.RegularFile,
                file.Kind
            );

            Assert.Equal(
                LinuxChildObjectKind.SymbolicLink,
                link.Kind
            );

            Assert.NotNull(
                directory.Inode
            );

            Assert.NotNull(
                file.Inode
            );

            Assert.NotNull(
                link.Inode
            );

            Assert.NotEqual(
                directory.Inode,
                link.Inode
            );
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public void
        Inspect_RetainedParentDescriptorIgnoresPathReplacement()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string root =
            CreateTemporaryRoot();

        try
        {
            string parentPath =
                Path.Combine(
                    root,
                    "parent"
                );

            Directory.CreateDirectory(
                parentPath
            );

            File.WriteAllText(
                Path.Combine(
                    parentPath,
                    "original.nif"
                ),
                "original"
            );

            LinuxNoFollowPathOpenResult opened =
                LinuxNoFollowPath
                    .OpenReadOnlyUnderRoot(
                        root,
                        "parent"
                    );

            Assert.True(
                opened.Success,
                opened.Error
            );

            using LinuxNoFollowPathHandle parent =
                Assert.IsType<
                    LinuxNoFollowPathHandle
                >(
                    opened.OpenedPath
                );

            string movedParent =
                Path.Combine(
                    root,
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
                    "replacement.nif"
                ),
                "replacement"
            );

            LinuxInspectChildAtResult original =
                LinuxInspectChildAt.Inspect(
                    parent,
                    "original.nif"
                );

            LinuxInspectChildAtResult replacement =
                LinuxInspectChildAt.Inspect(
                    parent,
                    "replacement.nif"
                );

            Assert.True(
                original.Success,
                original.Error
            );

            Assert.Equal(
                LinuxChildObjectKind.RegularFile,
                original.Kind
            );

            Assert.False(
                replacement.Success
            );

            Assert.Equal(
                LinuxInspectChildAtState
                    .ChildUnavailable,
                replacement.State
            );
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
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

    private static string CreateTemporaryRoot()
    {
        string path =
            Path.Combine(
                Path.GetTempPath(),
                "casecompat-inspect-child-" +
                Guid.NewGuid().ToString("N")
            );

        Directory.CreateDirectory(
            path
        );

        return path;
    }

    private static void DeleteTemporaryRoot(
        string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(
                path,
                recursive:
                    true
            );
        }
    }
}
