namespace CaseCompat.Tests;

public sealed class TargetedConsumerPlanCommandTests
{
    [Fact]
    public void Run_TooFewArguments_ReturnsUsageErrorWithoutAcquisition()
    {
        int exitCode =
            TargetedConsumerPlanCommand.Run(
                new[]
                {
                    "targeted-consumer-plan",
                    "/unused/Data",
                    "/unused/Plugins.txt",
                    "/unused/loadorder.txt",
                    "/unused/Skyrim.ccc",
                    "Meshes/Some/Path.nif"
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
            TargetedConsumerPlanCommand.Run(
                new[]
                {
                    "targeted-consumer-plan",
                    "/unused/Data",
                    "/unused/Plugins.txt",
                    "/unused/loadorder.txt",
                    "/unused/Skyrim.ccc",
                    "Meshes/Some/Path.nif",
                    "/unused/plan-dir",
                    "plan.json",
                    "unexpected"
                }
            );

        Assert.Equal(
            2,
            exitCode
        );
    }
}
