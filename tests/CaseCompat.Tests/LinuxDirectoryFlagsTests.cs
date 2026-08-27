using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public class LinuxDirectoryFlagsTests
{
    [Fact]
    public void HasCasefoldFlag_ReturnsTrue_WhenFlagIsPresent()
    {
        long flags = LinuxDirectoryFlags.FsCasefoldFlag;

        Assert.True(LinuxDirectoryFlags.HasCasefoldFlag(flags));
    }

    [Fact]
    public void HasCasefoldFlag_ReturnsFalse_WhenFlagIsAbsent()
    {
        long flags = 0;

        Assert.False(LinuxDirectoryFlags.HasCasefoldFlag(flags));
    }

    [Fact]
    public void HasCasefoldFlag_IgnoresUnrelatedFlags()
    {
        long flags = 0x00080000;

        Assert.False(LinuxDirectoryFlags.HasCasefoldFlag(flags));
    }
}
