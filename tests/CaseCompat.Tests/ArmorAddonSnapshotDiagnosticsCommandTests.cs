namespace CaseCompat.Tests;

public sealed class ArmorAddonSnapshotDiagnosticsCommandTests
{
    [Fact]
    public void Run_TooFewArguments_ReturnsUsageErrorWithoutAcquisition()
    {
        int exitCode =
            ArmorAddonSnapshotDiagnosticsCommand.Run(
                new[]
                {
                    "armor-addon-snapshot-diagnostics",
                    "/unused/Data",
                    "/unused/Plugins.txt",
                    "/unused/loadorder.txt",
                    "/unused/Skyrim.ccc"
                }
            );

        Assert.Equal(
            2,
            exitCode
        );
    }

    [Fact]
    public void Run_TooManyArguments_ReturnsUsageErrorWithoutAcquisition()
    {
        int exitCode =
            ArmorAddonSnapshotDiagnosticsCommand.Run(
                new[]
                {
                    "armor-addon-snapshot-diagnostics",
                    "/unused/Data",
                    "/unused/Plugins.txt",
                    "/unused/loadorder.txt",
                    "/unused/Skyrim.ccc",
                    "/unused/ini",
                    "filter",
                    "unexpected"
                }
            );

        Assert.Equal(
            2,
            exitCode
        );
    }
}
