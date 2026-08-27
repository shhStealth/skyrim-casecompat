using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public class DirectoryProbeTests
{
    [Fact]
    public void Inspect_ExistingDirectory_ReturnsExistsTrue()
    {
        string directory = Path.GetTempPath();

        DirectoryProbeResult result = DirectoryProbe.Inspect(directory);

        Assert.True(result.Exists);
        Assert.Equal(Path.GetFullPath(directory), result.FullPath);
    }

    [Fact]
    public void Inspect_MissingDirectory_ReturnsExistsFalse()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"casecompat-missing-{Guid.NewGuid():N}"
        );

        DirectoryProbeResult result = DirectoryProbe.Inspect(directory);

        Assert.False(result.Exists);
    }

    [Fact]
    public void Inspect_EmptyPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => DirectoryProbe.Inspect(""));
    }
}
