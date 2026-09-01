using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxExclusiveChildFileLockTests
{
    [Fact]
    public void
        Acquire_FirstLeaseBlocksSecondUntilDisposedAndEntryPersists()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        using LinuxNoFollowPathHandle firstParent =
            OpenRoot(
                temp.RootPath
            );

        using LinuxNoFollowPathHandle secondParent =
            OpenRoot(
                temp.RootPath
            );

        const string childName =
            "plan.execution-lock";

        LinuxExclusiveChildFileLockResult first =
            LinuxExclusiveChildFileLock.Acquire(
                firstParent,
                childName
            );

        Assert.True(
            first.Success,
            first.Error
        );

        using LinuxExclusiveChildFileLockLease firstLease =
            Assert.IsType<
                LinuxExclusiveChildFileLockLease
            >(
                first.Lease
            );

        Assert.True(
            firstLease.IsHeld
        );

        Assert.True(
            File.Exists(
                Path.Combine(
                    temp.RootPath,
                    childName
                )
            )
        );

        LinuxExclusiveChildFileLockResult blocked =
            LinuxExclusiveChildFileLock.Acquire(
                secondParent,
                childName
            );

        Assert.False(
            blocked.Success
        );

        Assert.Equal(
            LinuxExclusiveChildFileLockState
                .AlreadyLocked,
            blocked.State
        );

        firstLease.Dispose();

        Assert.False(
            firstLease.IsHeld
        );

        LinuxExclusiveChildFileLockResult reacquired =
            LinuxExclusiveChildFileLock.Acquire(
                secondParent,
                childName
            );

        Assert.True(
            reacquired.Success,
            reacquired.Error
        );

        using LinuxExclusiveChildFileLockLease secondLease =
            Assert.IsType<
                LinuxExclusiveChildFileLockLease
            >(
                reacquired.Lease
            );

        Assert.True(
            secondLease.IsHeld
        );

        secondLease.Dispose();

        /*
         * Releasing flock never unlinks the persistent lock child.
         */
        Assert.True(
            File.Exists(
                Path.Combine(
                    temp.RootPath,
                    childName
                )
            )
        );
    }

    [Fact]
    public void Acquire_SymbolicLinkChild_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string target =
            Path.Combine(
                temp.RootPath,
                "target"
            );

        File.WriteAllText(
            target,
            "fixture"
        );

        const string childName =
            "plan.execution-lock";

        File.CreateSymbolicLink(
            Path.Combine(
                temp.RootPath,
                childName
            ),
            target
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        LinuxExclusiveChildFileLockResult result =
            LinuxExclusiveChildFileLock.Acquire(
                parent,
                childName
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxExclusiveChildFileLockState
                .ChildSymbolicLinkRejected,
            result.State
        );

        Assert.Null(
            result.Lease
        );
    }

    [Fact]
    public void Acquire_DirectoryChild_IsRejectedAsNonRegular()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        const string childName =
            "plan.execution-lock";

        Directory.CreateDirectory(
            Path.Combine(
                temp.RootPath,
                childName
            )
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        LinuxExclusiveChildFileLockResult result =
            LinuxExclusiveChildFileLock.Acquire(
                parent,
                childName
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxExclusiveChildFileLockState
                .ChildNotRegularFile,
            result.State
        );

        Assert.Null(
            result.Lease
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("child/file")]
    [InlineData(@"child\file")]
    public void Acquire_InvalidChildName_IsRejected(
        string childName)
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

        LinuxExclusiveChildFileLockResult result =
            LinuxExclusiveChildFileLock.Acquire(
                parent,
                childName
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxExclusiveChildFileLockState
                .InvalidName,
            result.State
        );

        Assert.Null(
            result.Lease
        );
    }

    private static LinuxNoFollowPathHandle OpenRoot(
        string root)
    {
        LinuxNoFollowPathOpenResult opened =
            LinuxNoFollowPath.OpenRootReadOnly(
                root
            );

        Assert.True(
            opened.Success,
            opened.Error
        );

        return Assert.IsType<
            LinuxNoFollowPathHandle
        >(
            opened.OpenedPath
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
                    "casecompat-child-file-lock-tests",
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
