using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace CaseCompat.Tests;

public sealed class CliHelpOutputRegressionTests
{
    private const string ExpectedHelpSha256 =
        "37b9353f6fcfe187a0c8843e9f54c3ac490cf942a571e40d5d9b989ae146b4cd";

    private const int ExpectedHelpUtf8ByteCount = 4421;

    private const int ExpectedHelpNewlineCount = 67;

    [Fact]
    public async Task
        Help_LongForm_PreservesFrozenOutputContract()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        CliResult result =
            await RunCliAsync(
                "--help"
            );

        Assert.Equal(
            0,
            result.ExitCode
        );

        Assert.Equal(
            string.Empty,
            result.StandardError
        );

        Assert.Equal(
            ExpectedHelpUtf8ByteCount,
            Encoding.UTF8.GetByteCount(
                result.StandardOutput
            )
        );

        Assert.Equal(
            ExpectedHelpNewlineCount,
            result.StandardOutput.Count(
                character => character == '\n'
            )
        );

        string actualSha256 =
            Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        result.StandardOutput
                    )
                )
            ).ToLowerInvariant();

        Assert.Equal(
            ExpectedHelpSha256,
            actualSha256
        );

        Assert.Contains(
            "Repair workflow",
            result.StandardOutput
        );

        Assert.Contains(
            "Default repair plan manifest file name: repair-plan.json",
            result.StandardOutput
        );

        Assert.Contains(
            "Default aggregate namespace manifest file name: " +
            "aggregate-namespace-manifest.json",
            result.StandardOutput
        );

        Assert.Contains(
            "casecompat aggregate-namespace-manifest " +
            "<Skyrim Data directory> <direct Data child namespace> " +
            "<output directory> [manifest file name]",
            result.StandardOutput
        );

        Assert.Contains(
            "casecompat repair-rollback-batch <batch directory> " +
            "<manifest file name> <Skyrim Data directory>",
            result.StandardOutput
        );
    }

    [Theory]
    [InlineData("help")]
    [InlineData("-h")]
    public async Task
        Help_Aliases_MatchLongFormOutputExactly(
            string alias
        )
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        CliResult canonical =
            await RunCliAsync(
                "--help"
            );

        CliResult aliasResult =
            await RunCliAsync(
                alias
            );

        Assert.Equal(
            0,
            canonical.ExitCode
        );

        Assert.Equal(
            0,
            aliasResult.ExitCode
        );

        Assert.Equal(
            string.Empty,
            canonical.StandardError
        );

        Assert.Equal(
            string.Empty,
            aliasResult.StandardError
        );

        Assert.Equal(
            canonical.StandardOutput,
            aliasResult.StandardOutput
        );
    }

    private static async Task<CliResult>
        RunCliAsync(
            string argument
        )
    {
        string cliPath =
            Path.Combine(
                AppContext.BaseDirectory,
                "CaseCompat.Cli"
            );

        Assert.True(
            File.Exists(
                cliPath
            ),
            $"Expected built CLI executable at: {cliPath}"
        );

        using var process =
            new Process
            {
                StartInfo =
                    new ProcessStartInfo
                    {
                        FileName = cliPath,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
            };

        process.StartInfo.ArgumentList.Add(
            argument
        );

        Assert.True(
            process.Start(),
            "Failed to start CaseCompat.Cli."
        );

        Task<string> standardOutputTask =
            process.StandardOutput.ReadToEndAsync();

        Task<string> standardErrorTask =
            process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        string standardOutput =
            await standardOutputTask;

        string standardError =
            await standardErrorTask;

        return new CliResult(
            process.ExitCode,
            standardOutput,
            standardError
        );
    }

    private sealed record CliResult(
        int ExitCode,
        string StandardOutput,
        string StandardError
    );
}
