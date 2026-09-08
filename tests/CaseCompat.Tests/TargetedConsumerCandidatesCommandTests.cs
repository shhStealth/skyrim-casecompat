namespace CaseCompat.Tests;

public sealed class TargetedConsumerCandidatesCommandTests
{
    [Fact]
    public void Run_TooFewArguments_ReturnsUsageErrorWithoutAcquisition()
    {
        int exitCode =
            TargetedConsumerCandidatesCommand.Run(
                new[]
                {
                    "targeted-consumer-candidates",
                    "/unused/Data",
                    "/unused/Plugins.txt",
                    "/unused/loadorder.txt"
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
            TargetedConsumerCandidatesCommand.Run(
                new[]
                {
                    "targeted-consumer-candidates",
                    "/unused/Data",
                    "/unused/Plugins.txt",
                    "/unused/loadorder.txt",
                    "/unused/Skyrim.ccc",
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
