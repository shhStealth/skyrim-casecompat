using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public class LinuxFileIdentityTests
{
    [Fact]
    public void Inspect_ExistingDirectory_ReturnsPhysicalIdentity()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"casecompat-statx-{Guid.NewGuid():N}"
        );

        Directory.CreateDirectory(directory);

        try
        {
            LinuxFileIdentityResult result =
                LinuxFileIdentity.Inspect(directory);

            Assert.True(result.Success);
            Assert.NotNull(result.DeviceMajor);
            Assert.NotNull(result.DeviceMinor);
            Assert.NotNull(result.Inode);
            Assert.True(result.Inode > 0);
        }
        finally
        {
            Directory.Delete(directory);
        }
    }

    [Fact]
    public void SameObjectAs_ReturnsTrue_ForSamePhysicalDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"casecompat-statx-{Guid.NewGuid():N}"
        );

        Directory.CreateDirectory(directory);

        try
        {
            LinuxFileIdentityResult first =
                LinuxFileIdentity.Inspect(directory);

            LinuxFileIdentityResult second =
                LinuxFileIdentity.Inspect(directory);

            Assert.True(first.SameObjectAs(second));
        }
        finally
        {
            Directory.Delete(directory);
        }
    }

    [Fact]
    public void Inspect_MissingPath_ReturnsFailure()
    {
        string missing = Path.Combine(
            Path.GetTempPath(),
            $"casecompat-statx-missing-{Guid.NewGuid():N}"
        );

        LinuxFileIdentityResult result =
            LinuxFileIdentity.Inspect(missing);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }
}
