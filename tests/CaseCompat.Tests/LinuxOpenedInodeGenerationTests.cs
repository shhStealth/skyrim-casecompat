using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxOpenedInodeGenerationTests
{
    [Fact]
    public void Capture_SameDescriptor_IsStable()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            "Owned"
        );

        using LinuxNoFollowPathHandle opened =
            fixture.OpenDirectory(
                "Owned"
            );

        LinuxOpenedInodeGenerationResult first =
            LinuxOpenedInodeGeneration.Capture(
                opened
            );

        if (
            first.State ==
            LinuxOpenedInodeGenerationState
                .GenerationUnavailable)
        {
            return;
        }

        Assert.True(
            first.Success,
            first.Error
        );

        LinuxOpenedInodeGenerationResult second =
            LinuxOpenedInodeGeneration.Capture(
                opened
            );

        Assert.True(
            second.Success,
            second.Error
        );

        Assert.Equal(
            first.Generation,
            second.Generation
        );
    }

    [Fact]
    public void Capture_TwoDescriptorsForSameDirectory_Agree()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            "Owned"
        );

        using LinuxNoFollowPathHandle firstHandle =
            fixture.OpenDirectory(
                "Owned"
            );

        using LinuxNoFollowPathHandle secondHandle =
            fixture.OpenDirectory(
                "Owned"
            );

        LinuxOpenedInodeGenerationResult first =
            LinuxOpenedInodeGeneration.Capture(
                firstHandle
            );

        if (
            first.State ==
            LinuxOpenedInodeGenerationState
                .GenerationUnavailable)
        {
            return;
        }

        LinuxOpenedInodeGenerationResult second =
            LinuxOpenedInodeGeneration.Capture(
                secondHandle
            );

        Assert.True(
            first.Success,
            first.Error
        );

        Assert.True(
            second.Success,
            second.Error
        );

        Assert.Equal(
            first.Generation,
            second.Generation
        );
    }

    [Fact]
    public void Capture_RecreatedDirectory_HasDifferentGeneration()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            "Owned"
        );

        LinuxFileIdentityResult firstIdentity;
        LinuxOpenedInodeGenerationResult firstGeneration;

        using (
            LinuxNoFollowPathHandle first =
                fixture.OpenDirectory(
                    "Owned"
                ))
        {
            LinuxOpenedDirectorySnapshotResult snapshot =
                LinuxOpenedDirectorySnapshot.Capture(
                    first
                );

            Assert.True(
                snapshot.Success,
                snapshot.Error
            );

            firstIdentity =
                snapshot.Identity!;

            firstGeneration =
                LinuxOpenedInodeGeneration.Capture(
                    first
                );
        }

        if (
            firstGeneration.State ==
            LinuxOpenedInodeGenerationState
                .GenerationUnavailable)
        {
            return;
        }

        Assert.True(
            firstGeneration.Success,
            firstGeneration.Error
        );

        Directory.Delete(
            fixture.PathFor(
                "Owned"
            )
        );

        Directory.CreateDirectory(
            fixture.PathFor(
                "Owned"
            )
        );

        LinuxFileIdentityResult secondIdentity;
        LinuxOpenedInodeGenerationResult secondGeneration;

        using (
            LinuxNoFollowPathHandle second =
                fixture.OpenDirectory(
                    "Owned"
                ))
        {
            LinuxOpenedDirectorySnapshotResult snapshot =
                LinuxOpenedDirectorySnapshot.Capture(
                    second
                );

            Assert.True(
                snapshot.Success,
                snapshot.Error
            );

            secondIdentity =
                snapshot.Identity!;

            secondGeneration =
                LinuxOpenedInodeGeneration.Capture(
                    second
                );
        }

        Assert.True(
            secondGeneration.Success,
            secondGeneration.Error
        );

        Assert.NotEqual(
            firstGeneration.Generation,
            secondGeneration.Generation
        );

        /*
         * On ext4 an inode number may be reused immediately.
         *
         * When that happens, this test directly proves why the
         * generation field is required for destructive ownership:
         * traditional physical identity is unchanged while inode
         * incarnation evidence differs.
         */
        if (
            firstIdentity.DeviceMajor ==
                secondIdentity.DeviceMajor &&
            firstIdentity.DeviceMinor ==
                secondIdentity.DeviceMinor &&
            firstIdentity.Inode ==
                secondIdentity.Inode &&
            firstIdentity.MountId ==
                secondIdentity.MountId)
        {
            Assert.NotEqual(
                firstGeneration.Generation,
                secondGeneration.Generation
            );
        }
    }

    private sealed class Fixture
        : IDisposable
    {
        public Fixture()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-inode-generation-tests",
                    Guid.NewGuid().ToString("N")
                );

            Directory.CreateDirectory(
                RootPath
            );
        }

        public string RootPath { get; }

        public string PathFor(
            string childName)
        {
            return Path.Combine(
                RootPath,
                childName
            );
        }

        public void CreateDirectory(
            string childName)
        {
            Directory.CreateDirectory(
                PathFor(
                    childName
                )
            );
        }

        public LinuxNoFollowPathHandle OpenDirectory(
            string childName)
        {
            LinuxNoFollowPathOpenResult result =
                LinuxNoFollowPath.OpenRootReadOnly(
                    PathFor(
                        childName
                    )
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
