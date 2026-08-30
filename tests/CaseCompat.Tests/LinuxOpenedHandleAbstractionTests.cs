using CaseCompat.Filesystem.Linux;
using Microsoft.Win32.SafeHandles;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxOpenedHandleAbstractionTests
{
    [Fact]
    public void Fsync_DescriptorOnlyHandle_Succeeds()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string path =
            Path.Combine(
                temp.RootPath,
                "fixture.bin"
            );

        SafeFileHandle safeHandle =
            File.OpenHandle(
                path,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None
            );

        using var opened =
            new DescriptorOnlyHandle(
                safeHandle
            );

        RandomAccess.Write(
            opened.Handle,
            "fixture"u8,
            0
        );

        LinuxFsyncResult result =
            LinuxFsync.Sync(
                opened
            );

        Assert.True(
            result.Success
        );

        Assert.Equal(
            LinuxFsyncState.Synced,
            result.State
        );
    }

    [Fact]
    public void CopyAndVerify_DescriptorOnlyHandles_Succeeds()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        byte[] content =
            Encoding.UTF8.GetBytes(
                "descriptor-only-copy"
            );

        string sourcePath =
            Path.Combine(
                temp.RootPath,
                "source.bin"
            );

        string destinationPath =
            Path.Combine(
                temp.RootPath,
                "destination.bin"
            );

        File.WriteAllBytes(
            sourcePath,
            content
        );

        string expectedHash =
            Convert.ToHexString(
                SHA256.HashData(
                    content
                )
            );

        using (
            var source =
                new DescriptorOnlyHandle(
                    File.OpenHandle(
                        sourcePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read
                    )
                ))
        using (
            var destination =
                new DescriptorOnlyHandle(
                    File.OpenHandle(
                        destinationPath,
                        FileMode.CreateNew,
                        FileAccess.ReadWrite,
                        FileShare.None
                    )
                ))
        {
            LinuxCopyFileContentsResult result =
                LinuxCopyFileContents.CopyAndVerify(
                    source,
                    destination,
                    content.LongLength,
                    expectedHash
                );

            Assert.True(
                result.Success
            );

            Assert.Equal(
                LinuxCopyFileContentsState
                    .CopiedAndVerified,
                result.State
            );

            Assert.Equal(
                content.LongLength,
                RandomAccess.GetLength(
                    destination.Handle
                )
            );
        }

        Assert.Equal(
            content,
            File.ReadAllBytes(
                destinationPath
            )
        );
    }

    private sealed class DescriptorOnlyHandle
        : ILinuxOpenedHandle
    {
        public DescriptorOnlyHandle(
            SafeFileHandle handle)
        {
            Handle =
                handle;
        }

        public SafeFileHandle Handle { get; }

        public void Dispose()
        {
            Handle.Dispose();
        }
    }

    private sealed class TemporaryDirectory
        : IDisposable
    {
        public TemporaryDirectory()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-opened-handle-tests",
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
