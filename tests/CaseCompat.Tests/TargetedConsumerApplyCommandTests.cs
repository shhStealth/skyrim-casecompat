namespace CaseCompat.Tests;

public sealed class TargetedConsumerApplyCommandTests
{
    [Fact]
    public void Run_TooFewArguments_ReturnsUsageErrorWithoutAcquisition()
    {
        int exitCode =
            TargetedConsumerApplyCommand.Run(
                new[]
                {
                    "targeted-consumer-apply",
                    "/unused/Data",
                    "/unused/plan-dir",
                    "plan.json"
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
            TargetedConsumerApplyCommand.Run(
                new[]
                {
                    "targeted-consumer-apply",
                    "/unused/Data",
                    "/unused/plan-dir",
                    "plan.json",
                    "/unused/journal-dir",
                    "unexpected"
                }
            );

        Assert.Equal(
            2,
            exitCode
        );
    }
}
