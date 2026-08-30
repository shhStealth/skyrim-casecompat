using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxOpenedDirectorySnapshotTests
{
    [Fact]
    public void Capture_OpenedDirectory_CapturesIdentityAndFlags()
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

        string directory =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes"
                )
            ).FullName;

        LinuxNoFollowPathOpenResult openResult =
            LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                dataRoot,
                "meshes"
            );

        Assert.True(
            openResult.Success
        );

        LinuxNoFollowPathHandle opened =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                openResult.OpenedPath
            );

        using (opened)
        {
            LinuxOpenedDirectorySnapshotResult snapshot =
                LinuxOpenedDirectorySnapshot.Capture(
                    opened
                );

            Assert.True(
                snapshot.Success
            );

            Assert.Equal(
                LinuxOpenedDirectorySnapshotState
                    .Captured,
                snapshot.State
            );

            LinuxFileIdentityResult descriptorIdentity =
                Assert.IsType<
                    LinuxFileIdentityResult
                >(
                    snapshot.Identity
                );

            LinuxFileIdentityResult pathnameIdentity =
                LinuxFileIdentity.Inspect(
                    directory
                );

            Assert.True(
                descriptorIdentity.SameObjectAs(
                    pathnameIdentity
                )
            );

            DirectoryCasefoldResult pathnameFlags =
                LinuxDirectoryFlags.Inspect(
                    directory
                );

            Assert.Null(
                pathnameFlags.Error
            );

            Assert.Equal(
                pathnameFlags.CasefoldEnabled,
                snapshot.CasefoldEnabled
            );

            Assert.Equal(
                pathnameFlags.RawFlags,
                snapshot.RawFlags
            );

            Assert.Null(
                snapshot.Error
            );
        }
    }

    [Fact]
    public void Capture_RegularFile_IsRejectedAsNotDirectory()
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

        using (opened)
        {
            LinuxOpenedDirectorySnapshotResult snapshot =
                LinuxOpenedDirectorySnapshot.Capture(
                    opened
                );

            Assert.False(
                snapshot.Success
            );

            Assert.Equal(
                LinuxOpenedDirectorySnapshotState
                    .NotDirectory,
                snapshot.State
            );

            Assert.Null(
                snapshot.CasefoldEnabled
            );

            Assert.Null(
                snapshot.RawFlags
            );
        }
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

        string dataRoot =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Data"
                )
            ).FullName;

        string originalPath =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes"
                )
            ).FullName;

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
            string movedPath =
                Path.Combine(
                    dataRoot,
                    "meshes-original"
                );

            Directory.Move(
                originalPath,
                movedPath
            );

            Directory.CreateDirectory(
                originalPath
            );

            LinuxOpenedDirectorySnapshotResult snapshot =
                LinuxOpenedDirectorySnapshot.Capture(
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

        opened.Dispose();

        LinuxOpenedDirectorySnapshotResult snapshot =
            LinuxOpenedDirectorySnapshot.Capture(
                opened
            );

        Assert.False(
            snapshot.Success
        );

        Assert.Equal(
            LinuxOpenedDirectorySnapshotState
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
                    "casecompat-opened-directory-tests",
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
