namespace CaseCompat.Tests;

public sealed class TargetedConsumerBatchApplyCommandTests
{
    [Fact]
    public void Run_TooFewArguments_ReturnsUsageErrorWithoutAcquisition()
    {
        int exitCode =
            TargetedConsumerBatchApplyCommand.Run(
                new[]
                {
                    "targeted-consumer-batch-apply",
                    "/unused/Data",
                    "/unused/Plugins.txt",
                    "/unused/loadorder.txt",
                    "/unused/Skyrim.ccc",
                    "/unused/plan-dir",
                    "/unused/journal-dir",
                    "/unused/aliases-dir",
                    "/unused/report.csv"
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
            TargetedConsumerBatchApplyCommand.Run(
                new[]
                {
                    "targeted-consumer-batch-apply",
                    "/unused/Data",
                    "/unused/Plugins.txt",
                    "/unused/loadorder.txt",
                    "/unused/Skyrim.ccc",
                    "/unused/plan-dir",
                    "/unused/journal-dir",
                    "/unused/aliases-dir",
                    "/unused/report.csv",
                    "10",
                    "unexpected"
                }
            );

        Assert.Equal(
            2,
            exitCode
        );
    }

    [Fact]
    public void Run_NonNumericMaxCandidates_ReturnsUsageErrorWithoutAcquisition()
    {
        int exitCode =
            TargetedConsumerBatchApplyCommand.Run(
                new[]
                {
                    "targeted-consumer-batch-apply",
                    "/unused/Data",
                    "/unused/Plugins.txt",
                    "/unused/loadorder.txt",
                    "/unused/Skyrim.ccc",
                    "/unused/plan-dir",
                    "/unused/journal-dir",
                    "/unused/aliases-dir",
                    "/unused/report.csv",
                    "not-a-number"
                }
            );

        Assert.Equal(
            2,
            exitCode
        );
    }

    [Fact]
    public void Run_NonPositiveMaxCandidates_ReturnsUsageErrorWithoutAcquisition()
    {
        int exitCode =
            TargetedConsumerBatchApplyCommand.Run(
                new[]
                {
                    "targeted-consumer-batch-apply",
                    "/unused/Data",
                    "/unused/Plugins.txt",
                    "/unused/loadorder.txt",
                    "/unused/Skyrim.ccc",
                    "/unused/plan-dir",
                    "/unused/journal-dir",
                    "/unused/aliases-dir",
                    "/unused/report.csv",
                    "0"
                }
            );

        Assert.Equal(
            2,
            exitCode
        );
    }
}
