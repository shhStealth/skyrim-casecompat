using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxOpenedDirectoryIncarnationTests
{
    [LinuxDirectoryInodeGenerationFact]
    public void Capture_SameDescriptor_IsSameIncarnation()
    {

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            "Owned"
        );

        using LinuxNoFollowPathHandle opened =
            fixture.OpenDirectory(
                "Owned"
            );

        LinuxOpenedDirectoryIncarnationResult first =
            LinuxOpenedDirectoryIncarnation.Capture(
                opened
            );


        Assert.True(
            first.Success,
            first.Error
        );

        LinuxOpenedDirectoryIncarnationResult second =
            LinuxOpenedDirectoryIncarnation.Capture(
                opened
            );

        Assert.True(
            second.Success,
            second.Error
        );

        Assert.True(
            first.Identity!
                .SameIncarnationAs(
                    second.Identity!
                )
        );
    }

    [LinuxDirectoryInodeGenerationFact]
    public void Capture_TwoDescriptorsForSameDirectory_AreSameIncarnation()
    {

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

        LinuxOpenedDirectoryIncarnationResult first =
            LinuxOpenedDirectoryIncarnation.Capture(
                firstHandle
            );


        LinuxOpenedDirectoryIncarnationResult second =
            LinuxOpenedDirectoryIncarnation.Capture(
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

        Assert.True(
            first.Identity!
                .SameIncarnationAs(
                    second.Identity!
                )
        );
    }

    [LinuxDirectoryInodeGenerationFact]
    public void Capture_RecreatedDirectory_IsDifferentIncarnation()
    {

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            "Owned"
        );

        LinuxDirectoryIncarnationIdentity firstIdentity;

        using (
            LinuxNoFollowPathHandle first =
                fixture.OpenDirectory(
                    "Owned"
                ))
        {
            LinuxOpenedDirectoryIncarnationResult capture =
                LinuxOpenedDirectoryIncarnation.Capture(
                    first
                );


            Assert.True(
                capture.Success,
                capture.Error
            );

            firstIdentity =
                capture.Identity!;
        }

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

        LinuxDirectoryIncarnationIdentity secondIdentity;

        using (
            LinuxNoFollowPathHandle second =
                fixture.OpenDirectory(
                    "Owned"
                ))
        {
            LinuxOpenedDirectoryIncarnationResult capture =
                LinuxOpenedDirectoryIncarnation.Capture(
                    second
                );

            Assert.True(
                capture.Success,
                capture.Error
            );

            secondIdentity =
                capture.Identity!;
        }

        Assert.False(
            firstIdentity.SameIncarnationAs(
                secondIdentity
            )
        );

        Assert.NotEqual(
            firstIdentity.InodeGeneration,
            secondIdentity.InodeGeneration
        );

        /*
         * ext4 may immediately reuse the same inode number.
         *
         * If that happens, the generation field must still prove
         * that these are different directory incarnations.
         */
        LinuxFileIdentityResult firstPhysical =
            firstIdentity.PhysicalIdentity;

        LinuxFileIdentityResult secondPhysical =
            secondIdentity.PhysicalIdentity;

        if (
            firstPhysical.DeviceMajor ==
                secondPhysical.DeviceMajor &&
            firstPhysical.DeviceMinor ==
                secondPhysical.DeviceMinor &&
            firstPhysical.Inode ==
                secondPhysical.Inode &&
            firstPhysical.MountId ==
                secondPhysical.MountId)
        {
            Assert.False(
                firstIdentity.SameIncarnationAs(
                    secondIdentity
                )
            );

            Assert.NotEqual(
                firstIdentity.InodeGeneration,
                secondIdentity.InodeGeneration
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
                    "casecompat-directory-incarnation-tests",
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
