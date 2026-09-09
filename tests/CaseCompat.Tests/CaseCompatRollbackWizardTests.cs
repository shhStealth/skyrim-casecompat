using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class CaseCompatRollbackWizardTests
{
    private static readonly DateTimeOffset T0 =
        new(
            2026,
            9,
            9,
            0,
            0,
            0,
            TimeSpan.Zero
        );

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
            CaseCompatRollbackWizard.Run(
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
    public void Run_NoJournalForThisInstall_ReportsNothingToRollBack()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string dataRoot =
            Directory.CreateTempSubdirectory(
                "casecompat-rollback-wizard-test-"
            ).FullName;

        (string? previousXdgDataHome, string isolatedStateHome) =
            IsolateStateHome();

        try
        {
            var input =
                new StringReader(
                    "y\n"
                );

            var output =
                new StringWriter();

            int exitCode =
                CaseCompatRollbackWizard.Run(
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
                0,
                exitCode
            );

            Assert.Contains(
                "Nothing has been applied here",
                output.ToString()
            );
        }
        finally
        {
            RestoreStateHome(
                previousXdgDataHome,
                isolatedStateHome
            );

            Directory.Delete(
                dataRoot,
                recursive: true
            );
        }
    }

    [Fact]
    public void Run_RealAppliedFix_RollsItBackToOriginalName()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string dataRoot =
            RealPipelineDataRoot();

        string casedDirectory =
            Path.Combine(
                dataRoot,
                "Meshes"
            );

        Directory.CreateDirectory(
            casedDirectory
        );

        string originalFilePath =
            Path.Combine(
                casedDirectory,
                "readme.txt"
            );

        File.WriteAllText(
            originalFilePath,
            "original contents"
        );

        string requestedPath =
            "Meshes/Readme.txt";

        (string? previousXdgDataHome, string isolatedStateHome) =
            IsolateStateHome();

        try
        {
            string journalDirectoryPath =
                Path.Combine(
                    CaseCompatStateDirectory.Resolve(
                        dataRoot
                    ),
                    "journal"
                );

            Directory.CreateDirectory(
                journalDirectoryPath
            );

            DataRelativePathTargetedConsumerCaseRepairCandidate candidate =
                RealPipelineCandidate(
                    dataRoot,
                    requestedPath
                );

            using LinuxNoFollowPathHandle root =
                RealPipelineOpenRoot(
                    dataRoot
                );

            DataRelativePathTargetedConsumerCaseRepairPlanProjection
                projection =
                    DataRelativePathTargetedConsumerCaseRepairPlanProjector
                        .Project(
                            root,
                            candidate
                        );

            Assert.True(
                projection.HasPlan,
                projection.Error
            );

            // No directory-rename should be required here - "Meshes"
            // already physically exists under the correct case, so this
            // exercises a pure terminal-file rename, same as what the
            // rollback wizard needs to reverse.
            Assert.Empty(
                projection.DirectoryRenameSources
            );

            DataRelativePathTargetedConsumerCaseRepairDurablePlanCreation
                creation =
                    DataRelativePathTargetedConsumerCaseRepairDurablePlan
                        .Create(
                            root,
                            Guid.NewGuid(),
                            T0,
                            projection
                        );

            Assert.True(
                creation.Success,
                creation.Error
            );

            using LinuxNoFollowPathHandle journalDirectory =
                RealPipelineOpenRoot(
                    journalDirectoryPath
                );

            DataRelativePathTargetedConsumerCaseRepairApplyExecution
                execution =
                    DataRelativePathTargetedConsumerCaseRepairApplyExecutor
                        .Execute(
                            root,
                            journalDirectory,
                            creation.Record!,
                            T0
                        );

            Assert.True(
                execution.Success,
                $"State={execution.State} Error={execution.Error}"
            );

            string newCasedFilePath =
                Path.Combine(
                    casedDirectory,
                    "Readme.txt"
                );

            Assert.True(
                File.Exists(
                    newCasedFilePath
                )
            );

            Assert.False(
                File.Exists(
                    originalFilePath
                )
            );

            var input =
                new StringReader(
                    "y\ny\n"
                );

            var output =
                new StringWriter();

            int exitCode =
                CaseCompatRollbackWizard.Run(
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
                0,
                exitCode
            );

            string transcript =
                output.ToString();

            Assert.Contains(
                "Found 1 applied fix(es)",
                transcript
            );

            Assert.Contains(
                "Rolled back:        1",
                transcript
            );

            Assert.True(
                File.Exists(
                    originalFilePath
                ),
                "The original-case file should have been renamed back."
            );

            Assert.False(
                File.Exists(
                    newCasedFilePath
                ),
                "The corrected-case file should no longer exist after rollback."
            );

            Assert.Equal(
                "original contents",
                File.ReadAllText(
                    originalFilePath
                )
            );
        }
        finally
        {
            RestoreStateHome(
                previousXdgDataHome,
                isolatedStateHome
            );
        }
    }

    private static (string? PreviousXdgDataHome, string IsolatedStateHome)
        IsolateStateHome()
    {
        string? previousXdgDataHome =
            Environment.GetEnvironmentVariable(
                "XDG_DATA_HOME"
            );

        string isolatedStateHome =
            Directory.CreateTempSubdirectory(
                "casecompat-rollback-wizard-state-"
            ).FullName;

        Environment.SetEnvironmentVariable(
            "XDG_DATA_HOME",
            isolatedStateHome
        );

        return (previousXdgDataHome, isolatedStateHome);
    }

    private static void RestoreStateHome(
        string? previousXdgDataHome,
        string isolatedStateHome)
    {
        Environment.SetEnvironmentVariable(
            "XDG_DATA_HOME",
            previousXdgDataHome
        );

        Directory.Delete(
            isolatedStateHome,
            recursive: true
        );
    }

    private static string RealPipelineDataRoot()
    {
        string path =
            Path.Combine(
                Path.GetTempPath(),
                "casecompat-rollback-wizard-real-pipeline-tests",
                Guid.NewGuid().ToString("N"),
                "Data"
            );

        Directory.CreateDirectory(
            path
        );

        return Path.GetFullPath(
            path
        );
    }

    private static
        DataRelativePathTargetedConsumerCaseRepairCandidate
        RealPipelineCandidate(
            string dataRoot,
            string requestedPath)
    {
        using LinuxNoFollowPathHandle root =
            RealPipelineOpenRoot(
                dataRoot
            );

        string rootLogical =
            requestedPath
                .Split('/')[0]
                .ToUpperInvariant();

        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis
            current =
                DataRelativePathRepairAggregateNamespaceCurrentLeafAnalyzer
                    .Analyze(
                        root,
                        rootLogical,
                        requestedPath
                    );

        DataRelativePathAggregateConsumerSpellingEvidence consumer =
            DataRelativePathAggregateConsumerSpellingClassifier.Classify(
                requestedPath.ToUpperInvariant(),
                new[]
                {
                    requestedPath
                }
            );

        DataRelativePathTargetedConsumerCaseRepairCandidateProjection
            projection =
                DataRelativePathTargetedConsumerCaseRepairCandidateProjector
                    .Project(
                        dataRoot,
                        consumer,
                        current
                    );

        Assert.True(
            projection.HasCandidate,
            projection.Error
        );

        return Assert.IsType<
            DataRelativePathTargetedConsumerCaseRepairCandidate
        >(
            projection.Candidate
        );
    }

    private static LinuxNoFollowPathHandle RealPipelineOpenRoot(
        string path)
    {
        LinuxNoFollowPathOpenResult opened =
            LinuxNoFollowPath.OpenRootReadOnly(
                path
            );

        Assert.True(
            opened.Success,
            opened.Error
        );

        return Assert.IsType<
            LinuxNoFollowPathHandle
        >(
            opened.OpenedPath
        );
    }
}
