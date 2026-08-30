using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxFsyncTests
{
    [Fact]
    public void Sync_NewlyCreatedWritableFile_Succeeds()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        using LinuxNoFollowPathHandle root =
            OpenRoot(
                temp.RootPath
            );

        LinuxCreateFileAtExclusiveResult create =
            LinuxCreateFileAtExclusive.Create(
                root,
                "fixture.bin"
            );

        using LinuxNoFollowPathHandle created =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                create.OpenedPath
            );

        LinuxFsyncResult result =
            LinuxFsync.Sync(
                created
            );

        Assert.True(
            result.Success
        );

        Assert.Equal(
            LinuxFsyncState.Synced,
            result.State
        );

        Assert.Null(
            result.Errno
        );
    }

    [Fact]
    public void Sync_CopiedAndVerifiedFile_Succeeds()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        byte[] content =
            Encoding.UTF8.GetBytes(
                "durable-copy-fixture"
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

        using LinuxNoFollowPathHandle root =
            OpenRoot(
                temp.RootPath
            );

        LinuxCreateFileAtExclusiveResult create =
            LinuxCreateFileAtExclusive.Create(
                root,
                "destination.bin"
            );

        using LinuxNoFollowPathHandle destination =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                create.OpenedPath
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

        LinuxFsyncResult sync =
            LinuxFsync.Sync(
                destination
            );

        Assert.True(
            sync.Success
        );

        LinuxOpenedFileSnapshotResult snapshot =
            LinuxOpenedFileSnapshot.Capture(
                destination
            );

        Assert.True(
            snapshot.Success
        );

        Assert.Equal(
            expectedHash,
            snapshot.Sha256
        );
    }

    [Fact]
    public void Sync_ClosedDescriptor_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        using LinuxNoFollowPathHandle root =
            OpenRoot(
                temp.RootPath
            );

        LinuxCreateFileAtExclusiveResult create =
            LinuxCreateFileAtExclusive.Create(
                root,
                "fixture.bin"
            );

        LinuxNoFollowPathHandle created =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                create.OpenedPath
            );

        created.Dispose();

        LinuxFsyncResult result =
            LinuxFsync.Sync(
                created
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxFsyncState.InvalidHandle,
            result.State
        );
    }

    [Fact]
    public void Sync_PathReplacedAfterOpen_StillUsesOriginalDescriptor()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        using LinuxNoFollowPathHandle root =
            OpenRoot(
                temp.RootPath
            );

        LinuxCreateFileAtExclusiveResult create =
            LinuxCreateFileAtExclusive.Create(
                root,
                "fixture.bin"
            );

        using LinuxNoFollowPathHandle created =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                create.OpenedPath
            );

        RandomAccess.Write(
            created.Handle,
            "original"u8,
            0
        );

        string originalPath =
            Path.Combine(
                temp.RootPath,
                "fixture.bin"
            );

        string movedPath =
            Path.Combine(
                temp.RootPath,
                "fixture-original.bin"
            );

        File.Move(
            originalPath,
            movedPath
        );

        File.WriteAllText(
            originalPath,
            "replacement"
        );

        LinuxFsyncResult result =
            LinuxFsync.Sync(
                created
            );

        Assert.True(
            result.Success
        );

        LinuxOpenedFileSnapshotResult descriptorSnapshot =
            LinuxOpenedFileSnapshot.Capture(
                created
            );

        LinuxFileIdentityResult movedIdentity =
            LinuxFileIdentity.Inspect(
                movedPath
            );

        LinuxFileIdentityResult replacementIdentity =
            LinuxFileIdentity.Inspect(
                originalPath
            );

        Assert.True(
            descriptorSnapshot.Identity!
                .SameObjectAs(
                    movedIdentity
                )
        );

        Assert.False(
            descriptorSnapshot.Identity!
                .SameObjectAs(
                    replacementIdentity
                )
        );
    }

    [Fact]
    public void Sync_OpenedDirectoryDescriptor_Succeeds()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string directory =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "parent"
                )
            ).FullName;

        LinuxNoFollowPathOpenResult open =
            LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                temp.RootPath,
                "parent"
            );

        using LinuxNoFollowPathHandle opened =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                open.OpenedPath
            );

        LinuxOpenedDirectorySnapshotResult before =
            LinuxOpenedDirectorySnapshot.Capture(
                opened
            );

        Assert.True(
            before.Success
        );

        LinuxFsyncResult result =
            LinuxFsync.Sync(
                opened
            );

        Assert.True(
            result.Success
        );

        LinuxFileIdentityResult pathnameIdentity =
            LinuxFileIdentity.Inspect(
                directory
            );

        Assert.True(
            before.Identity!
                .SameObjectAs(
                    pathnameIdentity
                )
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
                    "casecompat-fsync-tests",
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
