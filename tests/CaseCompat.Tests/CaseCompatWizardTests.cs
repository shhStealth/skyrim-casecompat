namespace CaseCompat.Tests;

public sealed class CaseCompatWizardTests
{
    [Fact]
    public void Run_UserCancelsAtFirstPrompt_ReturnsCancelledExitCode()
    {
        var input =
            new StringReader(
                "\n"
            );

        var output =
            new StringWriter();

        int exitCode =
            CaseCompatWizard.Run(
                input,
                output,
                () =>
                    new SkyrimInstallDetectionResult(
                        DataRoot: null,
                        SkyrimCccPath: null,
                        PluginsPath: null,
                        LoadOrderPath: null,
                        Note: null
                    )
            );

        Assert.Equal(
            130,
            exitCode
        );

        Assert.Contains(
            "Cancelled.",
            output.ToString()
        );
    }

    [Fact]
    public void Run_AutoDetectedDataRootAccepted_SkipsManualPromptForIt()
    {
        string dataRoot =
            Directory.CreateTempSubdirectory(
                "casecompat-wizard-test-"
            ).FullName;

        try
        {
            var input =
                new StringReader(
                    "y\n" +
                    "\n"
                );

            var output =
                new StringWriter();

            int exitCode =
                CaseCompatWizard.Run(
                    input,
                    output,
                    () =>
                        new SkyrimInstallDetectionResult(
                            DataRoot:
                                dataRoot,
                            SkyrimCccPath:
                                null,
                            PluginsPath:
                                null,
                            LoadOrderPath:
                                null,
                            Note:
                                null
                        )
                );

            Assert.Equal(
                130,
                exitCode
            );

            string transcript =
                output.ToString();

            Assert.Contains(
                $"Skyrim Data folder [auto-detected]: {dataRoot}",
                transcript
            );

            Assert.DoesNotContain(
                "Enter path for Skyrim Data folder",
                transcript
            );

            Assert.Contains(
                "Enter path for Plugins.txt",
                transcript
            );
        }
        finally
        {
            Directory.Delete(
                dataRoot,
                recursive: true
            );
        }
    }

    [Fact]
    public void Run_InvalidManualPath_RepromptsUntilCancelled()
    {
        var input =
            new StringReader(
                "/definitely/does/not/exist\n" +
                "\n"
            );

        var output =
            new StringWriter();

        int exitCode =
            CaseCompatWizard.Run(
                input,
                output,
                () =>
                    new SkyrimInstallDetectionResult(
                        DataRoot: null,
                        SkyrimCccPath: null,
                        PluginsPath: null,
                        LoadOrderPath: null,
                        Note: null
                    )
            );

        Assert.Equal(
            130,
            exitCode
        );

        string transcript =
            output.ToString();

        Assert.Contains(
            "That folder does not exist.",
            transcript
        );

        Assert.Contains(
            "Cancelled.",
            transcript
        );
    }

    [Fact]
    public void Run_UserDeclinesAutoDetectedPath_FallsBackToManualPrompt()
    {
        string dataRoot =
            Directory.CreateTempSubdirectory(
                "casecompat-wizard-test-"
            ).FullName;

        try
        {
            var input =
                new StringReader(
                    "n\n" +
                    "\n"
                );

            var output =
                new StringWriter();

            int exitCode =
                CaseCompatWizard.Run(
                    input,
                    output,
                    () =>
                        new SkyrimInstallDetectionResult(
                            DataRoot:
                                dataRoot,
                            SkyrimCccPath:
                                null,
                            PluginsPath:
                                null,
                            LoadOrderPath:
                                null,
                            Note:
                                null
                        )
                );

            Assert.Equal(
                130,
                exitCode
            );

            string transcript =
                output.ToString();

            Assert.Contains(
                $"Skyrim Data folder [auto-detected]: {dataRoot}",
                transcript
            );

            Assert.Contains(
                "Enter path for Skyrim Data folder",
                transcript
            );

            Assert.Contains(
                "Cancelled.",
                transcript
            );
        }
        finally
        {
            Directory.Delete(
                dataRoot,
                recursive: true
            );
        }
    }

    [Fact]
    public void Run_DetectionNoteIsSurfacedToTheUser()
    {
        var input =
            new StringReader(
                "\n"
            );

        var output =
            new StringWriter();

        CaseCompatWizard.Run(
            input,
            output,
            () =>
                new SkyrimInstallDetectionResult(
                    DataRoot: null,
                    SkyrimCccPath: null,
                    PluginsPath: null,
                    LoadOrderPath: null,
                    Note:
                        "Skyrim Special Edition was not found in any " +
                        "detected Steam library."
                )
        );

        Assert.Contains(
            "Skyrim Special Edition was not found in any detected " +
            "Steam library.",
            output.ToString()
        );
    }
}
