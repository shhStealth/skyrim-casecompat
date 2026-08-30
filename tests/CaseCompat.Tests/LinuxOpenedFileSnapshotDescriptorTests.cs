using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxOpenedFileSnapshotDescriptorTests
{
    [Fact]
    public void Capture_DirectChildHandle_CapturesDescriptorEvidence()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        byte[] content =
            Encoding.UTF8.GetBytes(
                "descriptor-child"
            );

        File.WriteAllBytes(
            Path.Combine(
                temp.RootPath,
                "Final.nif"
            ),
            content
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        LinuxOpenChildReadOnlyAtResult opened =
            LinuxOpenChildReadOnlyAt.Open(
                parent,
                "Final.nif"
            );

        Assert.True(
            opened.Success
        );

        using LinuxOpenedChildHandle child =
            Assert.IsType<
                LinuxOpenedChildHandle
            >(
                opened.OpenedChild
            );

        const string displayPath =
            "/diagnostic/Data/Final.nif";

        LinuxOpenedFileSnapshotResult snapshot =
            LinuxOpenedFileSnapshot.Capture(
                child,
                displayPath
            );

        Assert.True(
            snapshot.Success,
            snapshot.Error
        );

        Assert.Equal(
            displayPath,
            snapshot.FullPath
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

        Assert.NotNull(
            snapshot.Identity
        );

        Assert.Equal(
            displayPath,
            snapshot.Identity!.FullPath
        );
    }

    [Fact]
    public void Capture_UnnamedFileHandle_CapturesWithoutPathHandle()
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

        LinuxCreateUnnamedFileAtResult create =
            LinuxCreateUnnamedFileAt.Create(
                parent
            );

        if (
            create.State ==
            LinuxCreateUnnamedFileAtState
                .TmpfileUnsupported)
        {
            return;
        }

        Assert.True(
            create.Success,
            create.Error
        );

        using LinuxUnnamedFileHandle unnamed =
            Assert.IsType<
                LinuxUnnamedFileHandle
            >(
                create.OpenedFile
            );

        byte[] content =
            Encoding.UTF8.GetBytes(
                "anonymous-snapshot"
            );

        RandomAccess.Write(
            unnamed.Handle,
            content,
            0
        );

        LinuxOpenedFileSnapshotResult snapshot =
            LinuxOpenedFileSnapshot.Capture(
                unnamed,
                "<unnamed-test-file>"
            );

        Assert.True(
            snapshot.Success,
            snapshot.Error
        );

        Assert.Equal(
            "<unnamed-test-file>",
            snapshot.FullPath
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

        Assert.NotNull(
            snapshot.Identity
        );

        Assert.Equal(
            0U,
            snapshot.Identity!.LinkCount
        );

        Assert.Empty(
            Directory.EnumerateFileSystemEntries(
                temp.RootPath
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
                    "casecompat-opened-snapshot-descriptor-tests",
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
