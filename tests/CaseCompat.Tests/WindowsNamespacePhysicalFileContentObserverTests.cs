using CaseCompat.Core.Analysis;

namespace CaseCompat.Tests;

public sealed class WindowsNamespacePhysicalFileContentObserverTests
{
    [LinuxFileInodeGenerationFact]
    public void Observe_SingleFile_ProducesStableContentEvidence()
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

            string filePath =
                WriteFile(
                    data,
                    "Meshes/Foo/Only.nif",
                    "only"
                );

            WindowsNamespaceAnalysis analysis =
                AnalyzeNamespace(
                    data
                );

            WindowsNamespaceNode node =
                Assert.Single(
                    analysis.Nodes,
                    candidate =>
                        string.Equals(
                            candidate.LogicalPath.Value,
                            "MESHES/FOO/ONLY.NIF",
                            StringComparison.Ordinal
                        )
                );

            WindowsNamespacePhysicalParticipant participant =
                Assert.Single(
                    node.Participants
                );

            Assert.Equal(
                WindowsNamespacePhysicalObjectKind.File,
                participant.Kind
            );

            Assert.Equal(
                filePath,
                participant.FullPath
            );

            WindowsNamespacePhysicalFileContentEvidence evidence =
                WindowsNamespacePhysicalFileContentObserver.Observe(
                    analysis,
                    participant
                );

            Assert.True(
                evidence.Success,
                evidence.Error
            );

            Assert.Equal(
                WindowsNamespacePhysicalFileContentEvidenceState
                    .StableContentEvidence,
                evidence.State
            );

            Assert.Equal(
                WindowsNamespacePhysicalFileReacquisitionState
                    .Reacquired,
                evidence.InitialReacquisitionState
            );

            Assert.Equal(
                WindowsNamespacePhysicalFileReacquisitionState
                    .Reacquired,
                evidence.PostObservationReacquisitionState
            );

            Assert.NotNull(
                evidence.Size
            );

            Assert.NotNull(
                evidence.Sha256
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
    public void Observe_RecreatedSingleFileAfterPassOne_FailsClosed()
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
                "original"
            );

            WindowsNamespaceAnalysis analysis =
                AnalyzeNamespace(
                    data
                );

            WindowsNamespacePhysicalParticipant participant =
                Assert.Single(
                    analysis.Nodes
                        .Where(node =>
                            string.Equals(
                                node.LogicalPath.Value,
                                "MESHES/FOO/ONLY.NIF",
                                StringComparison.Ordinal
                            ))
                        .SelectMany(node =>
                            node.Participants)
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

            WindowsNamespacePhysicalFileContentEvidence evidence =
                WindowsNamespacePhysicalFileContentObserver.Observe(
                    analysis,
                    participant
                );

            Assert.False(
                evidence.Success
            );

            Assert.Equal(
                WindowsNamespacePhysicalFileContentEvidenceState
                    .InitialReacquisitionFailed,
                evidence.State
            );

            Assert.Equal(
                WindowsNamespacePhysicalFileReacquisitionState
                    .FileIncarnationChanged,
                evidence.InitialReacquisitionState
            );

            Assert.Null(
                evidence.ContentObservation
            );

            Assert.Null(
                evidence.Size
            );

            Assert.Null(
                evidence.Sha256
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

    private static string WriteFile(
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

        return fullPath;
    }

    private static string CreateTempDirectory()
    {
        string path =
            Path.Combine(
                Path.GetTempPath(),
                "casecompat-content-observer-" +
                Guid.NewGuid().ToString("N")
            );

        Directory.CreateDirectory(
            path
        );

        return path;
    }
}
