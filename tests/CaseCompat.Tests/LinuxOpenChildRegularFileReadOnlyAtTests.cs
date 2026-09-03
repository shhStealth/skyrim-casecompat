using CaseCompat.Filesystem.Linux;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxOpenChildRegularFileReadOnlyAtTests
{
    [Fact]
    public void Open_RegularFile_ReturnsReadableExactChild()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        byte[] content =
            Encoding.UTF8.GetBytes(
                "descriptor-safe-regular-file"
            );

        File.WriteAllBytes(
            Path.Combine(
                temp.RootPath,
                "Fixture.nif"
            ),
            content
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        LinuxOpenChildRegularFileReadOnlyAtResult result =
            LinuxOpenChildRegularFileReadOnlyAt.Open(
                parent,
                "Fixture.nif"
            );

        Assert.True(
            result.Success,
            result.Error
        );

        using LinuxOpenedChildHandle opened =
            Assert.IsType<
                LinuxOpenedChildHandle
            >(
                result.OpenedFile
            );

        LinuxOpenedFileSnapshotResult snapshot =
            LinuxOpenedFileSnapshot.Capture(
                opened,
                "/diagnostic/Data/Fixture.nif"
            );

        Assert.True(
            snapshot.Success,
            snapshot.Error
        );

        Assert.Equal(
            content.LongLength,
            snapshot.Size
        );

        Assert.Equal(
            Convert.ToHexString(
                SHA256.HashData(
                    content
                )
            ),
            snapshot.Sha256
        );
    }

    [Fact]
    public void Open_SymbolicLink_IsRejectedWithoutFollowing()
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
                "Target.nif"
            );

        string link =
            Path.Combine(
                temp.RootPath,
                "Link.nif"
            );

        File.WriteAllText(
            target,
            "target"
        );

        File.CreateSymbolicLink(
            link,
            target
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        LinuxOpenChildRegularFileReadOnlyAtResult result =
            LinuxOpenChildRegularFileReadOnlyAt.Open(
                parent,
                "Link.nif"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxOpenChildRegularFileReadOnlyAtState
                .ChildNotRegularFile,
            result.State
        );

        Assert.Null(
            result.OpenedFile
        );
    }

    [Fact]
    public void Open_UnixSocket_IsRejectedBeforeReadableOpen()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string socketPath =
            Path.Combine(
                temp.RootPath,
                "Special.sock"
            );

        using var socket =
            new Socket(
                AddressFamily.Unix,
                SocketType.Stream,
                ProtocolType.Unspecified
            );

        socket.Bind(
            new UnixDomainSocketEndPoint(
                socketPath
            )
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        LinuxOpenChildRegularFileReadOnlyAtResult result =
            LinuxOpenChildRegularFileReadOnlyAt.Open(
                parent,
                "Special.sock"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxOpenChildRegularFileReadOnlyAtState
                .ChildNotRegularFile,
            result.State
        );

        Assert.Null(
            result.OpenedFile
        );
    }

    [Fact]
    public void Open_RetainedParentIgnoresPathReplacement()
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
                    "Parent"
                )
            ).FullName;

        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "original-retained-parent"
            );

        File.WriteAllBytes(
            Path.Combine(
                parentPath,
                "Original.nif"
            ),
            originalContent
        );

        using LinuxNoFollowPathHandle retainedParent =
            OpenRoot(
                parentPath
            );

        string movedParent =
            Path.Combine(
                temp.RootPath,
                "Parent-Original"
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
                "Replacement.nif"
            ),
            "replacement"
        );

        LinuxOpenChildRegularFileReadOnlyAtResult original =
            LinuxOpenChildRegularFileReadOnlyAt.Open(
                retainedParent,
                "Original.nif"
            );

        Assert.True(
            original.Success,
            original.Error
        );

        using (
            LinuxOpenedChildHandle opened =
                Assert.IsType<
                    LinuxOpenedChildHandle
                >(
                    original.OpenedFile
                ))
        {
            LinuxOpenedFileSnapshotResult snapshot =
                LinuxOpenedFileSnapshot.Capture(
                    opened,
                    "/diagnostic/Data/Original.nif"
                );

            Assert.True(
                snapshot.Success,
                snapshot.Error
            );

            Assert.Equal(
                Convert.ToHexString(
                    SHA256.HashData(
                        originalContent
                    )
                ),
                snapshot.Sha256
            );
        }

        LinuxOpenChildRegularFileReadOnlyAtResult replacement =
            LinuxOpenChildRegularFileReadOnlyAt.Open(
                retainedParent,
                "Replacement.nif"
            );

        Assert.False(
            replacement.Success
        );

        Assert.Equal(
            LinuxOpenChildRegularFileReadOnlyAtState
                .ChildUnavailable,
            replacement.State
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
                    "casecompat-regular-open-tests",
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
