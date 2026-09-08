namespace CaseCompat.Tests;

public sealed class CaseCompatWizardCommandTests
{
    [Fact]
    public void Run_TooManyArguments_ReturnsUsageError()
    {
        int exitCode =
            CaseCompatWizardCommand.Run(
                new[]
                {
                    "run",
                    "unexpected"
                }
            );

        Assert.Equal(
            2,
            exitCode
        );
    }
}
