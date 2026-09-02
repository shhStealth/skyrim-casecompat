using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxOpenedDirectoryIdentityTests
{
    [Fact]
    public void Capture_TwoDescriptorsForSameDirectory_HaveSameIdentity()
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
                "fixture"
            )
        );

        using LinuxNoFollowPathHandle first =
            OpenDirectory(
                temp.RootPath,
                "fixture"
            );

        using LinuxNoFollowPathHandle second =
            OpenDirectory(
                temp.RootPath,
                "fixture"
            );

        LinuxOpenedDirectoryIdentityResult firstIdentity =
            LinuxOpenedDirectoryIdentity.Capture(
                first
            );

        LinuxOpenedDirectoryIdentityResult secondIdentity =
            LinuxOpenedDirectoryIdentity.Capture(
                second
            );

        Assert.True(
            firstIdentity.Success
        );

        Assert.True(
            secondIdentity.Success
        );

        Assert.True(
            firstIdentity.SameObjectAs(
                secondIdentity
            )
        );
    }

    [Fact]
    public void Capture_TwoDifferentDirectories_HaveDifferentIdentity()
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
                "first"
            )
        );

        Directory.CreateDirectory(
            Path.Combine(
                temp.RootPath,
                "second"
            )
        );

        using LinuxNoFollowPathHandle first =
            OpenDirectory(
                temp.RootPath,
                "first"
            );

        using LinuxNoFollowPathHandle second =
            OpenDirectory(
                temp.RootPath,
                "second"
            );

        LinuxOpenedDirectoryIdentityResult firstIdentity =
            LinuxOpenedDirectoryIdentity.Capture(
                first
            );

        LinuxOpenedDirectoryIdentityResult secondIdentity =
            LinuxOpenedDirectoryIdentity.Capture(
                second
            );

        Assert.True(
            firstIdentity.Success
        );

        Assert.True(
            secondIdentity.Success
        );

        Assert.False(
            firstIdentity.SameObjectAs(
                secondIdentity
            )
        );
    }

    [Fact]
    public void Capture_PathReplacedAfterOpen_StillIdentifiesOriginalDirectory()
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
                    "fixture"
                )
            ).FullName;

        using LinuxNoFollowPathHandle original =
            OpenDirectory(
                temp.RootPath,
                "fixture"
            );

        LinuxOpenedDirectoryIdentityResult originalIdentity =
            LinuxOpenedDirectoryIdentity.Capture(
                original
            );

        Assert.True(
            originalIdentity.Success
        );

        string movedPath =
            Path.Combine(
                temp.RootPath,
                "fixture-original"
            );

        Directory.Move(
            originalPath,
            movedPath
        );

        Directory.CreateDirectory(
            originalPath
        );

        using LinuxNoFollowPathHandle moved =
            OpenDirectory(
                temp.RootPath,
                "fixture-original"
            );

        using LinuxNoFollowPathHandle replacement =
            OpenDirectory(
                temp.RootPath,
                "fixture"
            );

        LinuxOpenedDirectoryIdentityResult movedIdentity =
            LinuxOpenedDirectoryIdentity.Capture(
                moved
            );

        LinuxOpenedDirectoryIdentityResult replacementIdentity =
            LinuxOpenedDirectoryIdentity.Capture(
                replacement
            );

        Assert.True(
            originalIdentity.SameObjectAs(
                movedIdentity
            )
        );

        Assert.False(
            originalIdentity.SameObjectAs(
                replacementIdentity
            )
        );
    }

    [Fact]
    public void Capture_RegularFileDescriptor_IsRejected()
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
                "fixture.bin"
            ),
            "fixture"
        );

        using LinuxNoFollowPathHandle opened =
            OpenDirectory(
                temp.RootPath,
                "fixture.bin"
            );

        LinuxOpenedDirectoryIdentityResult result =
            LinuxOpenedDirectoryIdentity.Capture(
                opened
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxOpenedDirectoryIdentityState
                .NotDirectory,
            result.State
        );
    }

    [Fact]
    public void Capture_ClosedDescriptor_IsRejected()
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
                "fixture"
            )
        );

        LinuxNoFollowPathHandle opened =
            OpenDirectory(
                temp.RootPath,
                "fixture"
            );

        opened.Dispose();

        LinuxOpenedDirectoryIdentityResult result =
            LinuxOpenedDirectoryIdentity.Capture(
                opened
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxOpenedDirectoryIdentityState
                .InvalidHandle,
            result.State
        );
    }

    private static LinuxNoFollowPathHandle OpenDirectory(
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
                    "casecompat-opened-directory-identity-tests",
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
