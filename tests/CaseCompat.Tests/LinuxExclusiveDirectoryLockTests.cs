using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxExclusiveDirectoryLockTests
{
    [Fact]
    public void Acquire_FirstLeaseBlocksSecondUntilDisposed()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        using LinuxNoFollowPathHandle firstDirectory =
            OpenRoot(
                temp.RootPath
            );

        using LinuxNoFollowPathHandle secondDirectory =
            OpenRoot(
                temp.RootPath
            );

        LinuxExclusiveDirectoryLockResult first =
            LinuxExclusiveDirectoryLock.Acquire(
                firstDirectory
            );

        Assert.True(
            first.Success,
            first.Error
        );

        using LinuxExclusiveDirectoryLockLease firstLease =
            Assert.IsType<
                LinuxExclusiveDirectoryLockLease
            >(
                first.Lease
            );

        Assert.True(
            firstLease.IsHeld
        );

        LinuxExclusiveDirectoryLockResult blocked =
            LinuxExclusiveDirectoryLock.Acquire(
                secondDirectory
            );

        Assert.False(
            blocked.Success
        );

        Assert.Equal(
            LinuxExclusiveDirectoryLockState
                .AlreadyLocked,
            blocked.State
        );

        firstLease.Dispose();

        Assert.False(
            firstLease.IsHeld
        );

        LinuxExclusiveDirectoryLockResult afterRelease =
            LinuxExclusiveDirectoryLock.Acquire(
                secondDirectory
            );

        Assert.True(
            afterRelease.Success,
            afterRelease.Error
        );

        using LinuxExclusiveDirectoryLockLease secondLease =
            Assert.IsType<
                LinuxExclusiveDirectoryLockLease
            >(
                afterRelease.Lease
            );

        Assert.True(
            secondLease.IsHeld
        );
    }

    [Fact]
    public void Acquire_PathReplacement_RemainsBoundToPhysicalDirectory()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string originalPath =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "journal"
                )
            ).FullName;

        using LinuxNoFollowPathHandle originalDescriptor =
            OpenRoot(
                originalPath
            );

        LinuxExclusiveDirectoryLockResult originalLock =
            LinuxExclusiveDirectoryLock.Acquire(
                originalDescriptor
            );

        Assert.True(
            originalLock.Success,
            originalLock.Error
        );

        using LinuxExclusiveDirectoryLockLease originalLease =
            Assert.IsType<
                LinuxExclusiveDirectoryLockLease
            >(
                originalLock.Lease
            );

        string movedPath =
            Path.Combine(
                temp.RootPath,
                "journal-original"
            );

        Directory.Move(
            originalPath,
            movedPath
        );

        Directory.CreateDirectory(
            originalPath
        );

        using LinuxNoFollowPathHandle movedOriginal =
            OpenRoot(
                movedPath
            );

        LinuxExclusiveDirectoryLockResult samePhysical =
            LinuxExclusiveDirectoryLock.Acquire(
                movedOriginal
            );

        Assert.False(
            samePhysical.Success
        );

        Assert.Equal(
            LinuxExclusiveDirectoryLockState
                .AlreadyLocked,
            samePhysical.State
        );

        using LinuxNoFollowPathHandle replacement =
            OpenRoot(
                originalPath
            );

        LinuxExclusiveDirectoryLockResult replacementLock =
            LinuxExclusiveDirectoryLock.Acquire(
                replacement
            );

        Assert.True(
            replacementLock.Success,
            replacementLock.Error
        );

        using LinuxExclusiveDirectoryLockLease replacementLease =
            Assert.IsType<
                LinuxExclusiveDirectoryLockLease
            >(
                replacementLock.Lease
            );
    }

    [Fact]
    public void Acquire_ClosedParent_IsRejected()
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

        LinuxExclusiveDirectoryLockResult result =
            LinuxExclusiveDirectoryLock.Acquire(
                directory
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxExclusiveDirectoryLockState
                .InvalidParentHandle,
            result.State
        );
    }

    [Fact]
    public void Acquire_RegularFileHandle_IsRejectedAsNotDirectory()
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
                "file.txt"
            ),
            "file"
        );

        LinuxNoFollowPathOpenResult opened =
            LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                temp.RootPath,
                "file.txt"
            );

        Assert.True(
            opened.Success
        );

        using LinuxNoFollowPathHandle file =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                opened.OpenedPath
            );

        LinuxExclusiveDirectoryLockResult result =
            LinuxExclusiveDirectoryLock.Acquire(
                file
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxExclusiveDirectoryLockState
                .ParentNotDirectory,
            result.State
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
                    "casecompat-directory-lock-tests",
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
