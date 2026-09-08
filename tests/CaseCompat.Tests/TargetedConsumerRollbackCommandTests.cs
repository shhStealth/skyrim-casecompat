namespace CaseCompat.Tests;

public sealed class TargetedConsumerRollbackCommandTests
{
    [Fact]
    public void Run_TooFewArguments_ReturnsUsageErrorWithoutAcquisition()
    {
        int exitCode =
            TargetedConsumerRollbackCommand.Run(
                new[]
                {
                    "targeted-consumer-rollback",
                    "/unused/journal-dir"
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
            TargetedConsumerRollbackCommand.Run(
                new[]
                {
                    "targeted-consumer-rollback",
                    "/unused/journal-dir",
                    Guid.NewGuid().ToString(),
                    "unexpected"
                }
            );

        Assert.Equal(
            2,
            exitCode
        );
    }

    [Fact]
    public void Run_InvalidPlanId_ReturnsUsageErrorWithoutAcquisition()
    {
        int exitCode =
            TargetedConsumerRollbackCommand.Run(
                new[]
                {
                    "targeted-consumer-rollback",
                    "/unused/journal-dir",
                    "not-a-guid"
                }
            );

        Assert.Equal(
            2,
            exitCode
        );
    }
}
