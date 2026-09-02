using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxOpenedFileIncarnationTests
{
    [LinuxFileInodeGenerationFact]
    public void Capture_SameDescriptor_IsSameIncarnation()
    {

        using Fixture fixture =
            new();

        fixture.CreateFile(
            "Owned.bin",
            "first"
        );

        using LinuxNoFollowPathHandle opened =
            fixture.OpenFile(
                "Owned.bin"
            );

        LinuxOpenedFileIncarnationResult first =
            LinuxOpenedFileIncarnation.Capture(
                opened
            );


        Assert.True(
            first.Success,
            first.Error
        );

        LinuxOpenedFileIncarnationResult second =
            LinuxOpenedFileIncarnation.Capture(
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

    [LinuxFileInodeGenerationFact]
    public void Capture_TwoDescriptorsForSameFile_AreSameIncarnation()
    {

        using Fixture fixture =
            new();

        fixture.CreateFile(
            "Owned.bin",
            "first"
        );

        using LinuxNoFollowPathHandle firstHandle =
            fixture.OpenFile(
                "Owned.bin"
            );

        using LinuxNoFollowPathHandle secondHandle =
            fixture.OpenFile(
                "Owned.bin"
            );

        LinuxOpenedFileIncarnationResult first =
            LinuxOpenedFileIncarnation.Capture(
                firstHandle
            );


        LinuxOpenedFileIncarnationResult second =
            LinuxOpenedFileIncarnation.Capture(
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

    [LinuxFileInodeGenerationFact]
    public void Capture_RecreatedFile_IsDifferentIncarnation()
    {

        using Fixture fixture =
            new();

        fixture.CreateFile(
            "Owned.bin",
            "first"
        );

        LinuxFileIncarnationIdentity firstIdentity;

        using (
            LinuxNoFollowPathHandle first =
                fixture.OpenFile(
                    "Owned.bin"
                ))
        {
            LinuxOpenedFileIncarnationResult capture =
                LinuxOpenedFileIncarnation.Capture(
                    first
                );


            Assert.True(
                capture.Success,
                capture.Error
            );

            firstIdentity =
                capture.Identity!;
        }

        File.Delete(
            fixture.PathFor(
                "Owned.bin"
            )
        );

        fixture.CreateFile(
            "Owned.bin",
            "second"
        );

        LinuxFileIncarnationIdentity secondIdentity;

        using (
            LinuxNoFollowPathHandle second =
                fixture.OpenFile(
                    "Owned.bin"
                ))
        {
            LinuxOpenedFileIncarnationResult capture =
                LinuxOpenedFileIncarnation.Capture(
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
         * If that happens, generation must still prove that these
         * are different regular-file incarnations.
         */
        LinuxOpenedFileIdentityResult firstPhysical =
            firstIdentity.PhysicalIdentity;

        LinuxOpenedFileIdentityResult secondPhysical =
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

    [Fact]
    public void Capture_Directory_IsRejectedAsNotRegularFile()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        Directory.CreateDirectory(
            fixture.PathFor(
                "Owned"
            )
        );

        LinuxNoFollowPathOpenResult open =
            LinuxNoFollowPath.OpenRootReadOnly(
                fixture.PathFor(
                    "Owned"
                )
            );

        Assert.True(
            open.Success,
            open.Error
        );

        using LinuxNoFollowPathHandle opened =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                open.OpenedPath
            );

        LinuxOpenedFileIncarnationResult result =
            LinuxOpenedFileIncarnation.Capture(
                opened
            );

        Assert.Equal(
            LinuxOpenedFileIncarnationState
                .NotRegularFile,
            result.State
        );

        Assert.False(
            result.Success
        );
    }

    private sealed class Fixture
        : IDisposable
    {
        public Fixture()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-file-incarnation-tests",
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

        public void CreateFile(
            string childName,
            string contents)
        {
            File.WriteAllText(
                PathFor(
                    childName
                ),
                contents
            );
        }

        public LinuxNoFollowPathHandle OpenFile(
            string childName)
        {
            LinuxNoFollowPathOpenResult result =
                LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                    RootPath,
                    childName
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
