using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxCopyFileContentsTests
{
    [Fact]
    public void CopyAndVerify_ExpectedSource_CopiesExactBytes()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        byte[] content =
            Encoding.UTF8.GetBytes(
                "descriptor-copy-fixture"
            );

        using LinuxNoFollowPathHandle source =
            CreateAndOpenSource(
                temp,
                content
            );

        using LinuxNoFollowPathHandle destination =
            CreateDestination(
                temp
            );

        string expectedHash =
            Sha256(
                content
            );

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
            LinuxCopyFileContentsState.CopiedAndVerified,
            result.State
        );

        Assert.Equal(
            content.LongLength,
            result.BytesCopied
        );

        Assert.Equal(
            expectedHash,
            result.ActualSha256
        );

        LinuxOpenedFileSnapshotResult destinationSnapshot =
            LinuxOpenedFileSnapshot.Capture(
                destination
            );

        Assert.True(
            destinationSnapshot.Success
        );

        Assert.Equal(
            content.LongLength,
            destinationSnapshot.Size
        );

        Assert.Equal(
            expectedHash,
            destinationSnapshot.Sha256
        );
    }

    [Fact]
    public void CopyAndVerify_ZeroByteSource_Succeeds()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        byte[] content =
            Array.Empty<byte>();

        using LinuxNoFollowPathHandle source =
            CreateAndOpenSource(
                temp,
                content
            );

        using LinuxNoFollowPathHandle destination =
            CreateDestination(
                temp
            );

        string expectedHash =
            Sha256(
                content
            );

        LinuxCopyFileContentsResult result =
            LinuxCopyFileContents.CopyAndVerify(
                source,
                destination,
                0,
                expectedHash
            );

        Assert.True(
            result.Success
        );

        Assert.Equal(
            0L,
            result.BytesCopied
        );

        Assert.Equal(
            expectedHash,
            result.ActualSha256
        );
    }

    [Fact]
    public void CopyAndVerify_NonEmptyDestination_IsRejectedWithoutModification()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        byte[] content =
            Encoding.UTF8.GetBytes(
                "source"
            );

        using LinuxNoFollowPathHandle source =
            CreateAndOpenSource(
                temp,
                content
            );

        using LinuxNoFollowPathHandle destination =
            CreateDestination(
                temp
            );

        RandomAccess.Write(
            destination.Handle,
            "existing"u8,
            0
        );

        LinuxOpenedFileSnapshotResult before =
            LinuxOpenedFileSnapshot.Capture(
                destination
            );

        LinuxCopyFileContentsResult result =
            LinuxCopyFileContents.CopyAndVerify(
                source,
                destination,
                content.LongLength,
                Sha256(
                    content
                )
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxCopyFileContentsState.DestinationNotEmpty,
            result.State
        );

        LinuxOpenedFileSnapshotResult after =
            LinuxOpenedFileSnapshot.Capture(
                destination
            );

        Assert.Equal(
            before.Size,
            after.Size
        );

        Assert.Equal(
            before.Sha256,
            after.Sha256
        );
    }

    [Fact]
    public void CopyAndVerify_SourceSizeChangedBeforeCopy_IsRejectedBeforeWriting()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        byte[] original =
            Encoding.UTF8.GetBytes(
                "original"
            );

        string sourcePath =
            CreateSourcePath(
                temp,
                original
            );

        using LinuxNoFollowPathHandle source =
            OpenUnderRoot(
                temp.RootPath,
                "source.bin"
            );

        using LinuxNoFollowPathHandle destination =
            CreateDestination(
                temp
            );

        File.WriteAllBytes(
            sourcePath,
            "changed-size"u8.ToArray()
        );

        LinuxCopyFileContentsResult result =
            LinuxCopyFileContents.CopyAndVerify(
                source,
                destination,
                original.LongLength,
                Sha256(
                    original
                )
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxCopyFileContentsState.SourceSizeChanged,
            result.State
        );

        Assert.Equal(
            0L,
            RandomAccess.GetLength(
                destination.Handle
            )
        );
    }

    [Fact]
    public void CopyAndVerify_SameSizeSourceContentChanged_ReportsHashMismatch()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        byte[] original =
            Encoding.UTF8.GetBytes(
                "AAAA"
            );

        byte[] changed =
            Encoding.UTF8.GetBytes(
                "BBBB"
            );

        string sourcePath =
            CreateSourcePath(
                temp,
                original
            );

        using LinuxNoFollowPathHandle source =
            OpenUnderRoot(
                temp.RootPath,
                "source.bin"
            );

        using LinuxNoFollowPathHandle destination =
            CreateDestination(
                temp
            );

        File.WriteAllBytes(
            sourcePath,
            changed
        );

        LinuxCopyFileContentsResult result =
            LinuxCopyFileContents.CopyAndVerify(
                source,
                destination,
                original.LongLength,
                Sha256(
                    original
                )
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxCopyFileContentsState.HashMismatch,
            result.State
        );

        Assert.Equal(
            changed.LongLength,
            result.BytesCopied
        );

        Assert.Equal(
            Sha256(
                changed
            ),
            result.ActualSha256
        );

        LinuxOpenedFileSnapshotResult destinationSnapshot =
            LinuxOpenedFileSnapshot.Capture(
                destination
            );

        Assert.Equal(
            Sha256(
                changed
            ),
            destinationSnapshot.Sha256
        );
    }

    [Fact]
    public void CopyAndVerify_SourcePathReplacedAfterOpen_CopiesOriginalDescriptor()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        byte[] original =
            Encoding.UTF8.GetBytes(
                "original-descriptor"
            );

        string sourcePath =
            CreateSourcePath(
                temp,
                original
            );

        using LinuxNoFollowPathHandle source =
            OpenUnderRoot(
                temp.RootPath,
                "source.bin"
            );

        string movedPath =
            Path.Combine(
                temp.RootPath,
                "source-original.bin"
            );

        File.Move(
            sourcePath,
            movedPath
        );

        File.WriteAllText(
            sourcePath,
            "replacement"
        );

        using LinuxNoFollowPathHandle destination =
            CreateDestination(
                temp
            );

        string expectedHash =
            Sha256(
                original
            );

        LinuxCopyFileContentsResult result =
            LinuxCopyFileContents.CopyAndVerify(
                source,
                destination,
                original.LongLength,
                expectedHash
            );

        Assert.True(
            result.Success
        );

        Assert.Equal(
            expectedHash,
            LinuxOpenedFileSnapshot.Capture(
                destination
            ).Sha256
        );
    }

    [Fact]
    public void CopyAndVerify_DestinationPathReplacedAfterCreate_WritesOriginalDescriptor()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        byte[] content =
            Encoding.UTF8.GetBytes(
                "destination-descriptor"
            );

        using LinuxNoFollowPathHandle source =
            CreateAndOpenSource(
                temp,
                content
            );

        using LinuxNoFollowPathHandle destination =
            CreateDestination(
                temp
            );

        string originalDestination =
            Path.Combine(
                temp.RootPath,
                "destination.bin"
            );

        string movedDestination =
            Path.Combine(
                temp.RootPath,
                "destination-original.bin"
            );

        File.Move(
            originalDestination,
            movedDestination
        );

        File.WriteAllText(
            originalDestination,
            "replacement"
        );

        string expectedHash =
            Sha256(
                content
            );

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
            expectedHash,
            Sha256(
                File.ReadAllBytes(
                    movedDestination
                )
            )
        );

        Assert.Equal(
            "replacement",
            File.ReadAllText(
                originalDestination
            )
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("XYZ")]
    [InlineData("000000000000000000000000000000000000000000000000000000000000000G")]
    public void CopyAndVerify_InvalidExpectedHash_IsRejectedWithoutWriting(
        string expectedHash)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        byte[] content =
            Encoding.UTF8.GetBytes(
                "fixture"
            );

        using LinuxNoFollowPathHandle source =
            CreateAndOpenSource(
                temp,
                content
            );

        using LinuxNoFollowPathHandle destination =
            CreateDestination(
                temp
            );

        LinuxCopyFileContentsResult result =
            LinuxCopyFileContents.CopyAndVerify(
                source,
                destination,
                content.LongLength,
                expectedHash
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxCopyFileContentsState.InvalidExpectedSha256,
            result.State
        );

        Assert.Equal(
            0L,
            RandomAccess.GetLength(
                destination.Handle
            )
        );
    }

    private static LinuxNoFollowPathHandle
        CreateAndOpenSource(
            TemporaryDirectory temp,
            byte[] content)
    {
        CreateSourcePath(
            temp,
            content
        );

        return OpenUnderRoot(
            temp.RootPath,
            "source.bin"
        );
    }

    private static string CreateSourcePath(
        TemporaryDirectory temp,
        byte[] content)
    {
        string path =
            Path.Combine(
                temp.RootPath,
                "source.bin"
            );

        File.WriteAllBytes(
            path,
            content
        );

        return path;
    }

    private static LinuxNoFollowPathHandle
        CreateDestination(
            TemporaryDirectory temp)
    {
        LinuxNoFollowPathOpenResult rootOpen =
            LinuxNoFollowPath.OpenRootReadOnly(
                temp.RootPath
            );

        LinuxNoFollowPathHandle root =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                rootOpen.OpenedPath
            );

        try
        {
            LinuxCreateFileAtExclusiveResult create =
                LinuxCreateFileAtExclusive.Create(
                    root,
                    "destination.bin"
                );

            Assert.True(
                create.Success
            );

            return Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                create.OpenedPath
            );
        }
        finally
        {
            root.Dispose();
        }
    }

    private static LinuxNoFollowPathHandle OpenUnderRoot(
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

    private static string Sha256(
        byte[] bytes)
    {
        return Convert.ToHexString(
            SHA256.HashData(
                bytes
            )
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
                    "casecompat-copy-file-tests",
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
