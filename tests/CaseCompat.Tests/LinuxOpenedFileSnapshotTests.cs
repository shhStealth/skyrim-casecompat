using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxOpenedFileSnapshotTests
{
    [Fact]
    public void Capture_RegularOpenedFile_CapturesIdentitySizeAndHash()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Data"
                )
            ).FullName;

        string meshes =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes"
                )
            ).FullName;

        string file =
            Path.Combine(
                meshes,
                "fixture.nif"
            );

        const string content =
            "opened-descriptor-fixture";

        File.WriteAllText(
            file,
            content
        );

        LinuxNoFollowPathOpenResult openResult =
            LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                dataRoot,
                "meshes/fixture.nif"
            );

        LinuxNoFollowPathHandle opened =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                openResult.OpenedPath
            );

        using (opened)
        {
            LinuxOpenedFileSnapshotResult snapshot =
                LinuxOpenedFileSnapshot.Capture(
                    opened
                );

            Assert.True(
                snapshot.Success
            );

            Assert.Equal(
                LinuxOpenedFileSnapshotState
                    .Captured,
                snapshot.State
            );

            LinuxFileIdentityResult identity =
                Assert.IsType<
                    LinuxFileIdentityResult
                >(
                    snapshot.Identity
                );

            LinuxFileIdentityResult pathnameIdentity =
                LinuxFileIdentity.Inspect(
                    file
                );

            Assert.True(
                identity.SameObjectAs(
                    pathnameIdentity
                )
            );

            Assert.Equal(
                Encoding.UTF8.GetByteCount(
                    content
                ),
                snapshot.Size
            );

            string expectedHash =
                Convert.ToHexString(
                    SHA256.HashData(
                        Encoding.UTF8.GetBytes(
                            content
                        )
                    )
                );

            Assert.Equal(
                expectedHash,
                snapshot.Sha256
            );

            Assert.Null(
                snapshot.Error
            );
        }
    }

    [Fact]
    public void Capture_PathReplacedAfterOpen_StillSnapshotsOriginalDescriptor()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Data"
                )
            ).FullName;

        string meshes =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes"
                )
            ).FullName;

        string originalPath =
            Path.Combine(
                meshes,
                "fixture.nif"
            );

        const string originalContent =
            "original-descriptor-content";

        File.WriteAllText(
            originalPath,
            originalContent
        );

        LinuxNoFollowPathOpenResult openResult =
            LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                dataRoot,
                "meshes/fixture.nif"
            );

        LinuxNoFollowPathHandle opened =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                openResult.OpenedPath
            );

        using (opened)
        {
            string movedPath =
                Path.Combine(
                    meshes,
                    "moved-original.nif"
                );

            File.Move(
                originalPath,
                movedPath
            );

            File.WriteAllText(
                originalPath,
                "replacement-content"
            );

            LinuxOpenedFileSnapshotResult snapshot =
                LinuxOpenedFileSnapshot.Capture(
                    opened
                );

            Assert.True(
                snapshot.Success
            );

            LinuxFileIdentityResult descriptorIdentity =
                Assert.IsType<
                    LinuxFileIdentityResult
                >(
                    snapshot.Identity
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
                descriptorIdentity.SameObjectAs(
                    movedIdentity
                )
            );

            Assert.False(
                descriptorIdentity.SameObjectAs(
                    replacementIdentity
                )
            );

            string expectedHash =
                Convert.ToHexString(
                    SHA256.HashData(
                        Encoding.UTF8.GetBytes(
                            originalContent
                        )
                    )
                );

            Assert.Equal(
                expectedHash,
                snapshot.Sha256
            );

            Assert.Equal(
                Encoding.UTF8.GetByteCount(
                    originalContent
                ),
                snapshot.Size
            );
        }
    }

    [Fact]
    public void Capture_DirectoryTarget_IsRejectedAsNotRegularFile()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Data"
                )
            ).FullName;

        Directory.CreateDirectory(
            Path.Combine(
                dataRoot,
                "meshes"
            )
        );

        LinuxNoFollowPathOpenResult openResult =
            LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                dataRoot,
                "meshes"
            );

        LinuxNoFollowPathHandle opened =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                openResult.OpenedPath
            );

        using (opened)
        {
            LinuxOpenedFileSnapshotResult snapshot =
                LinuxOpenedFileSnapshot.Capture(
                    opened
                );

            Assert.False(
                snapshot.Success
            );

            Assert.Equal(
                LinuxOpenedFileSnapshotState
                    .NotRegularFile,
                snapshot.State
            );

            Assert.Null(
                snapshot.Sha256
            );
        }
    }

    [Fact]
    public void Capture_ClosedHandle_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Data"
                )
            ).FullName;

        File.WriteAllText(
            Path.Combine(
                dataRoot,
                "fixture.nif"
            ),
            "fixture"
        );

        LinuxNoFollowPathOpenResult openResult =
            LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                dataRoot,
                "fixture.nif"
            );

        LinuxNoFollowPathHandle opened =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                openResult.OpenedPath
            );

        opened.Dispose();

        LinuxOpenedFileSnapshotResult snapshot =
            LinuxOpenedFileSnapshot.Capture(
                opened
            );

        Assert.False(
            snapshot.Success
        );

        Assert.Equal(
            LinuxOpenedFileSnapshotState
                .InvalidHandle,
            snapshot.State
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
                    "casecompat-opened-snapshot-tests",
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
