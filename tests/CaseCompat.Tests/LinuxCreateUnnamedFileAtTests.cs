using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxCreateUnnamedFileAtTests
{
    [Fact]
    public void Create_SupportedParent_ReturnsOpenZeroByteUnnamedFile()
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

        LinuxCreateUnnamedFileAtResult result =
            LinuxCreateUnnamedFileAt.Create(
                parent
            );

        if (
            result.State ==
            LinuxCreateUnnamedFileAtState
                .TmpfileUnsupported)
        {
            Assert.Null(
                result.OpenedFile
            );

            return;
        }

        Assert.True(
            result.Success
        );

        Assert.Equal(
            LinuxCreateUnnamedFileAtState.Created,
            result.State
        );

        LinuxUnnamedFileHandle opened =
            Assert.IsType<
                LinuxUnnamedFileHandle
            >(
                result.OpenedFile
            );

        using (opened)
        {
            Assert.False(
                opened.Handle.IsInvalid
            );

            Assert.False(
                opened.Handle.IsClosed
            );

            Assert.Equal(
                0L,
                RandomAccess.GetLength(
                    opened.Handle
                )
            );
        }
    }

    [Fact]
    public void Create_UnpublishedFile_HasNoDirectoryEntryAndDisappearsOnClose()
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

        string[] before =
            Directory
                .EnumerateFileSystemEntries(
                    temp.RootPath
                )
                .ToArray();

        LinuxCreateUnnamedFileAtResult result =
            LinuxCreateUnnamedFileAt.Create(
                parent
            );

        if (
            result.State ==
            LinuxCreateUnnamedFileAtState
                .TmpfileUnsupported)
        {
            Assert.Null(
                result.OpenedFile
            );

            return;
        }

        LinuxUnnamedFileHandle opened =
            Assert.IsType<
                LinuxUnnamedFileHandle
            >(
                result.OpenedFile
            );

        RandomAccess.Write(
            opened.Handle,
            "anonymous"u8,
            0
        );

        string[] whileOpen =
            Directory
                .EnumerateFileSystemEntries(
                    temp.RootPath
                )
                .ToArray();

        Assert.Equal(
            before,
            whileOpen
        );

        opened.Dispose();

        string[] afterClose =
            Directory
                .EnumerateFileSystemEntries(
                    temp.RootPath
                )
                .ToArray();

        Assert.Equal(
            before,
            afterClose
        );
    }

    [Fact]
    public void Create_UnnamedFile_WorksWithCopyVerificationAndFsync()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        byte[] content =
            Encoding.UTF8.GetBytes(
                "unnamed-copy-fixture"
            );

        string sourcePath =
            Path.Combine(
                temp.RootPath,
                "source.bin"
            );

        File.WriteAllBytes(
            sourcePath,
            content
        );

        LinuxNoFollowPathOpenResult sourceOpen =
            LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                temp.RootPath,
                "source.bin"
            );

        using LinuxNoFollowPathHandle source =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                sourceOpen.OpenedPath
            );

        string parentPath =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "destination"
                )
            ).FullName;

        LinuxNoFollowPathOpenResult parentOpen =
            LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                temp.RootPath,
                "destination"
            );

        using LinuxNoFollowPathHandle parent =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                parentOpen.OpenedPath
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
            Assert.Empty(
                Directory.EnumerateFileSystemEntries(
                    parentPath
                )
            );

            return;
        }

        using LinuxUnnamedFileHandle destination =
            Assert.IsType<
                LinuxUnnamedFileHandle
            >(
                create.OpenedFile
            );

        string expectedHash =
            Convert.ToHexString(
                SHA256.HashData(
                    content
                )
            );

        LinuxCopyFileContentsResult copy =
            LinuxCopyFileContents.CopyAndVerify(
                source,
                destination,
                content.LongLength,
                expectedHash
            );

        Assert.True(
            copy.Success
        );

        Assert.Equal(
            expectedHash,
            copy.ActualSha256
        );

        LinuxFsyncResult sync =
            LinuxFsync.Sync(
                destination
            );

        Assert.True(
            sync.Success
        );

        Assert.Equal(
            content.LongLength,
            RandomAccess.GetLength(
                destination.Handle
            )
        );

        // Still unnamed even after verified copy + fsync.
        Assert.Empty(
            Directory.EnumerateFileSystemEntries(
                parentPath
            )
        );
    }

    [Fact]
    public void Create_ClosedParent_IsRejected()
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

        LinuxCreateUnnamedFileAtResult result =
            LinuxCreateUnnamedFileAt.Create(
                parent
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxCreateUnnamedFileAtState
                .InvalidParentHandle,
            result.State
        );

        Assert.Null(
            result.OpenedFile
        );
    }

    [Fact]
    public void Create_RegularFileParent_IsRejectedAsNotDirectory()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string filePath =
            Path.Combine(
                temp.RootPath,
                "not-a-directory.bin"
            );

        File.WriteAllText(
            filePath,
            "fixture"
        );

        LinuxNoFollowPathOpenResult fileOpen =
            LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                temp.RootPath,
                "not-a-directory.bin"
            );

        using LinuxNoFollowPathHandle file =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                fileOpen.OpenedPath
            );

        LinuxCreateUnnamedFileAtResult result =
            LinuxCreateUnnamedFileAt.Create(
                file
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxCreateUnnamedFileAtState
                .ParentNotDirectory,
            result.State
        );

        Assert.Null(
            result.OpenedFile
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
                    "casecompat-otmpfile-tests",
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
