using CaseCompat.Core.Analysis;
using CaseCompat.Filesystem.Linux;
using System.Reflection;

namespace CaseCompat.Tests;

public sealed class WindowsNamespaceMultipleFileContentAnalyzerTests
{
    [LinuxFileInodeGenerationFact]
    public void Analyze_NoMultipleFileNodes_ProducesNoContentEvidence()
    {
        string root =
            CreateTempDirectory();

        try
        {
            string data =
                Path.Combine(
                    root,
                    "Data"
                );

            WriteFile(
                data,
                "Meshes/Foo/Only.nif",
                "only"
            );

            WindowsNamespaceAnalysis analysis =
                AnalyzeNamespace(
                    data
                );

            WindowsNamespaceMultipleFileContentAnalysis result =
                WindowsNamespaceMultipleFileContentAnalyzer.Analyze(
                    analysis
                );

            Assert.True(
                result.Complete,
                string.Join(
                    Environment.NewLine,
                    result.Errors
                )
            );

            Assert.Empty(
                result.Nodes
            );

            Assert.Empty(
                result.Errors
            );
        }
        finally
        {
            Directory.Delete(
                root,
                recursive:
                    true
            );
        }
    }

    [LinuxFileInodeGenerationFact]
    public void Analyze_MultipleFiles_HashesOnlyThatLogicalNode()
    {
        string root =
            CreateTempDirectory();

        try
        {
            string data =
                CreateMultipleFileFixture(
                    root,
                    firstContent:
                        "same",
                    secondContent:
                        "same",
                    includeSingleFile:
                        true
                );

            WindowsNamespaceAnalysis analysis =
                AnalyzeNamespace(
                    data
                );

            WindowsNamespaceMultipleFileContentAnalysis result =
                WindowsNamespaceMultipleFileContentAnalyzer.Analyze(
                    analysis
                );

            Assert.True(
                result.Complete,
                string.Join(
                    Environment.NewLine,
                    result.Errors
                )
            );

            WindowsNamespaceMultipleFileContentNodeAnalysis node =
                Assert.Single(
                    result.Nodes
                );

            Assert.Equal(
                2,
                node.Files.Count
            );

            Assert.DoesNotContain(
                node.Files,
                file =>
                    string.Equals(
                        file.Participant.Name,
                        "Unique.nif",
                        StringComparison.Ordinal
                    )
            );

            Assert.All(
                node.Files,
                file =>
                {
                    Assert.True(
                        file.Success,
                        file.Error
                    );

                    Assert.Equal(
                        WindowsNamespacePhysicalFileContentEvidenceState
                            .StableContentEvidence,
                        file.State
                    );

                    Assert.True(
                        file.ContentObservation!.Success,
                        file.ContentObservation.Error
                    );

                    Assert.NotNull(
                        file.Size
                    );

                    Assert.NotNull(
                        file.Sha256
                    );
                }
            );

            Assert.Single(
                node.Files
                    .Select(file => file.Sha256)
                    .Distinct(StringComparer.Ordinal)
            );
        }
        finally
        {
            Directory.Delete(
                root,
                recursive:
                    true
            );
        }
    }

    [LinuxFileInodeGenerationFact]
    public void Analyze_MultipleFiles_RecordsDistinctStableHashes()
    {
        string root =
            CreateTempDirectory();

        try
        {
            string data =
                CreateMultipleFileFixture(
                    root,
                    firstContent:
                        "first",
                    secondContent:
                        "second",
                    includeSingleFile:
                        false
                );

            WindowsNamespaceAnalysis analysis =
                AnalyzeNamespace(
                    data
                );

            WindowsNamespaceMultipleFileContentAnalysis result =
                WindowsNamespaceMultipleFileContentAnalyzer.Analyze(
                    analysis
                );

            Assert.True(
                result.Complete,
                string.Join(
                    Environment.NewLine,
                    result.Errors
                )
            );

            WindowsNamespaceMultipleFileContentNodeAnalysis node =
                Assert.Single(
                    result.Nodes
                );

            Assert.Equal(
                2,
                node.Files
                    .Select(file => file.Sha256)
                    .Distinct(StringComparer.Ordinal)
                    .Count()
            );
        }
        finally
        {
            Directory.Delete(
                root,
                recursive:
                    true
            );
        }
    }

    [LinuxFileInodeGenerationFact]
    public void Analyze_IncompleteNamespaceAnalysis_IsRejected()
    {
        string root =
            CreateTempDirectory();

        try
        {
            string data =
                CreateMultipleFileFixture(
                    root,
                    firstContent:
                        "first",
                    secondContent:
                        "second",
                    includeSingleFile:
                        false
                );

            WindowsNamespaceAnalysis analysis =
                AnalyzeNamespace(
                    data
                );

            WindowsNamespaceAnalysis incomplete =
                analysis with
                {
                    Errors =
                        new[]
                        {
                            "Synthetic pass-1 failure."
                        }
                };

            Assert.False(
                incomplete.Complete
            );

            WindowsNamespaceMultipleFileContentAnalysis result =
                WindowsNamespaceMultipleFileContentAnalyzer.Analyze(
                    incomplete
                );

            Assert.False(
                result.Complete
            );

            Assert.Empty(
                result.Nodes
            );

            Assert.Single(
                result.Errors
            );

            Assert.Contains(
                "complete",
                result.Errors[0],
                StringComparison.OrdinalIgnoreCase
            );
        }
        finally
        {
            Directory.Delete(
                root,
                recursive:
                    true
            );
        }
    }

    [LinuxFileInodeGenerationFact]
    public void Analyze_RecreatedParticipantAfterPassOne_FailsClosed()
    {
        string root =
            CreateTempDirectory();

        try
        {
            string data =
                CreateMultipleFileFixture(
                    root,
                    firstContent:
                        "first",
                    secondContent:
                        "second",
                    includeSingleFile:
                        false
                );

            WindowsNamespaceAnalysis analysis =
                AnalyzeNamespace(
                    data
                );

            WindowsNamespacePhysicalParticipant participant =
                Assert.Single(
                    analysis.Nodes
                        .SelectMany(node => node.Participants),
                    candidate =>
                        string.Equals(
                            candidate.Name,
                            "Sword.nif",
                            StringComparison.Ordinal
                        )
                );

            string retired =
                participant.FullPath +
                ".retired";

            File.Move(
                participant.FullPath,
                retired
            );

            File.WriteAllText(
                participant.FullPath,
                "replacement"
            );

            WindowsNamespaceMultipleFileContentAnalysis result =
                WindowsNamespaceMultipleFileContentAnalyzer.Analyze(
                    analysis
                );

            Assert.False(
                result.Complete
            );

            WindowsNamespaceMultipleFileContentNodeAnalysis node =
                Assert.Single(
                    result.Nodes
                );

            WindowsNamespacePhysicalFileContentEvidence changed =
                Assert.Single(
                    node.Files,
                    file =>
                        file.Participant ==
                        participant
                );

            Assert.False(
                changed.Success
            );

            Assert.Equal(
                WindowsNamespacePhysicalFileContentEvidenceState
                    .InitialReacquisitionFailed,
                changed.State
            );

            Assert.Equal(
                WindowsNamespacePhysicalFileReacquisitionState
                    .FileIncarnationChanged,
                changed.InitialReacquisitionState
            );

            Assert.Null(
                changed.ContentObservation
            );

            Assert.Contains(
                node.Files,
                file =>
                    file.Participant != participant &&
                    file.Success
            );
        }
        finally
        {
            Directory.Delete(
                root,
                recursive:
                    true
            );
        }
    }

    [LinuxFileInodeGenerationFact]
    public void Analyze_StableContent_ReacquiresAgainAfterHash()
    {
        string root =
            CreateTempDirectory();

        try
        {
            string data =
                CreateMultipleFileFixture(
                    root,
                    firstContent:
                        "first",
                    secondContent:
                        "second",
                    includeSingleFile:
                        false
                );

            WindowsNamespaceAnalysis analysis =
                AnalyzeNamespace(
                    data
                );

            WindowsNamespaceMultipleFileContentAnalysis result =
                WindowsNamespaceMultipleFileContentAnalyzer.Analyze(
                    analysis
                );

            Assert.True(
                result.Complete,
                string.Join(
                    Environment.NewLine,
                    result.Errors
                )
            );

            WindowsNamespaceMultipleFileContentNodeAnalysis node =
                Assert.Single(
                    result.Nodes
                );

            Assert.All(
                node.Files,
                file =>
                {
                    Assert.Equal(
                        WindowsNamespacePhysicalFileReacquisitionState
                            .Reacquired,
                        file.InitialReacquisitionState
                    );

                    Assert.Equal(
                        WindowsNamespacePhysicalFileReacquisitionState
                            .Reacquired,
                        file.PostObservationReacquisitionState
                    );

                    Assert.True(
                        file.InitialIncarnation!.Success,
                        file.InitialIncarnation.Error
                    );

                    Assert.True(
                        file.PostObservationIncarnation!.Success,
                        file.PostObservationIncarnation.Error
                    );

                    Assert.Equal(
                        file.ExpectedIncarnationObservation!
                            .InodeGeneration,
                        file.InitialIncarnation
                            .Identity!
                            .InodeGeneration
                    );

                    Assert.Equal(
                        file.ExpectedIncarnationObservation!
                            .InodeGeneration,
                        file.PostObservationIncarnation
                            .Identity!
                            .InodeGeneration
                    );
                }
            );
        }
        finally
        {
            Directory.Delete(
                root,
                recursive:
                    true
            );
        }
    }

    [LinuxFileInodeGenerationFact]
    public void Analyze_PostHashExactSpellingChange_IsRejected()
    {
        string root =
            CreateTempDirectory();

        try
        {
            string data =
                CreateMultipleFileFixture(
                    root,
                    firstContent:
                        "first",
                    secondContent:
                        "second",
                    includeSingleFile:
                        false
                );

            WindowsNamespaceAnalysis analysis =
                AnalyzeNamespace(
                    data
                );

            WindowsNamespacePhysicalParticipant target =
                Assert.Single(
                    analysis.Nodes
                        .SelectMany(
                            node =>
                                node.Participants
                        ),
                    candidate =>
                        string.Equals(
                            candidate.Name,
                            "Sword.nif",
                            StringComparison.Ordinal
                        )
                );

            string renamedPath =
                Path.Combine(
                    Path.GetDirectoryName(
                        target.FullPath
                    )!,
                    "SWORD.NIF"
                );

            bool renamedAfterStableHash =
                false;

            WindowsNamespaceMultipleFileContentAnalysis result =
                AnalyzeWithAfterStableContentObservation(
                    analysis,
                    participant =>
                    {
                        if (
                            participant == target &&
                            !renamedAfterStableHash)
                        {
                            File.Move(
                                target.FullPath,
                                renamedPath
                            );

                            renamedAfterStableHash =
                                true;
                        }
                    }
                );

            Assert.True(
                renamedAfterStableHash
            );

            Assert.False(
                result.Complete
            );

            WindowsNamespaceMultipleFileContentNodeAnalysis node =
                Assert.Single(
                    result.Nodes
                );

            WindowsNamespacePhysicalFileContentEvidence changed =
                Assert.Single(
                    node.Files,
                    file =>
                        file.Participant ==
                        target
                );

            /*
             * The retained descriptor already produced a valid stable hash.
             * The later namespace proof is what deliberately invalidates the
             * combined evidence.
             */
            Assert.NotNull(
                changed.ContentObservation
            );

            Assert.True(
                changed.ContentObservation!.Success,
                changed.ContentObservation.Error
            );

            Assert.NotNull(
                changed.ContentObservation.Sha256
            );

            Assert.Equal(
                WindowsNamespacePhysicalFileContentEvidenceState
                    .PostObservationReacquisitionFailed,
                changed.State
            );

            Assert.Equal(
                WindowsNamespacePhysicalFileReacquisitionState
                    .Reacquired,
                changed.InitialReacquisitionState
            );

            Assert.Equal(
                WindowsNamespacePhysicalFileReacquisitionState
                    .ExactFileSpellingUnavailable,
                changed.PostObservationReacquisitionState
            );

            Assert.Equal(
                "Sword.nif",
                changed.FailedComponent
            );

            /*
             * Convenience evidence must not publish the already-computed
             * digest once the post-hash namespace proof has failed.
             */
            Assert.False(
                changed.Success
            );

            Assert.Null(
                changed.Size
            );

            Assert.Null(
                changed.Sha256
            );

            Assert.Contains(
                node.Files,
                file =>
                    file.Participant != target &&
                    file.Success
            );
        }
        finally
        {
            Directory.Delete(
                root,
                recursive:
                    true
            );
        }
    }

    private static WindowsNamespaceMultipleFileContentAnalysis
        AnalyzeWithAfterStableContentObservation(
            WindowsNamespaceAnalysis analysis,
            Action<WindowsNamespacePhysicalParticipant> callback)
    {
        ArgumentNullException.ThrowIfNull(
            analysis
        );

        ArgumentNullException.ThrowIfNull(
            callback
        );

        MethodInfo? method =
            typeof(WindowsNamespaceMultipleFileContentAnalyzer)
                .GetMethod(
                    "AnalyzeCore",
                    BindingFlags.NonPublic |
                    BindingFlags.Static
                );

        Assert.NotNull(
            method
        );

        object? value =
            method!.Invoke(
                obj:
                    null,
                parameters:
                    new object?[]
                    {
                        analysis,
                        callback
                    }
            );

        return Assert.IsType<
            WindowsNamespaceMultipleFileContentAnalysis
        >(
            value
        );
    }

    private static WindowsNamespaceAnalysis AnalyzeNamespace(
        string dataRoot)
    {
        WindowsNamespaceAnalysis analysis =
            WindowsNamespaceAnalyzer.Analyze(
                dataRoot,
                "Meshes"
            );

        Assert.True(
            analysis.Complete,
            string.Join(
                Environment.NewLine,
                analysis.Errors
            )
        );

        return analysis;
    }

    private static string CreateMultipleFileFixture(
        string root,
        string firstContent,
        string secondContent,
        bool includeSingleFile)
    {
        string data =
            Path.Combine(
                root,
                "Data"
            );

        WriteFile(
            data,
            "Meshes/Foo/Sword.nif",
            firstContent
        );

        WriteFile(
            data,
            "Meshes/Foo/sword.NIF",
            secondContent
        );

        if (includeSingleFile)
        {
            WriteFile(
                data,
                "Meshes/Foo/Unique.nif",
                "single"
            );
        }

        return data;
    }

    private static void WriteFile(
        string dataRoot,
        string relativePath,
        string content)
    {
        string fullPath =
            Path.Combine(
                dataRoot,
                relativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar
                )
            );

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                fullPath
            )!
        );

        File.WriteAllText(
            fullPath,
            content
        );
    }

    private static string CreateTempDirectory()
    {
        string path =
            Path.Combine(
                Path.GetTempPath(),
                "casecompat-multiple-file-content-" +
                Guid.NewGuid().ToString("N")
            );

        Directory.CreateDirectory(
            path
        );

        return path;
    }
}
