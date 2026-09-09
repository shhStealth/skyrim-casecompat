using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace CaseCompat.Tests;

public sealed class CliHelpOutputRegressionTests
{
    private const string ExpectedHelpSha256 =
        "359b4049c4656fa232dfc0e4a867d6675bd6a01c1491a3cba4f72fcb2f463dd5";

    private const int ExpectedHelpUtf8ByteCount = 2604;

    private const int ExpectedHelpNewlineCount = 40;

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
            "Guided setup",
            result.StandardOutput
        );

        Assert.Contains(
            "casecompat run       it, scans for case-mismatch fixes, " +
            "and applies them.",
            result.StandardOutput
        );

        Assert.Contains(
            "casecompat rollback  Detects your Skyrim install, finds " +
            "its journal, and rolls",
            result.StandardOutput
        );

        Assert.Contains(
            "casecompat targeted-consumer-batch-apply " +
            "<Data root> <Plugins.txt> <loadorder.txt> <Skyrim.ccc> " +
            "<plan directory> <journal directory> <report file path> " +
            "<max candidates>",
            result.StandardOutput
        );

        Assert.Contains(
            "casecompat targeted-consumer-rollback " +
            "<journal directory> <plan ID>",
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
