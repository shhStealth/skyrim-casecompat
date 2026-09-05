using CaseCompat.Core.Analysis;

namespace CaseCompat.Tests;

public sealed class WindowsNamespaceRegularFileContentAnalyzerTests
{
    [LinuxFileInodeGenerationFact]
    public void Analyze_HashesSingleAndMultipleFileLeaves_AndSkipsDirectories()
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

            WriteFile(
                data,
                "Meshes/Foo/Sword.nif",
                "same"
            );

            WriteFile(
                data,
                "Meshes/Foo/sword.NIF",
                "same"
            );

            Directory.CreateDirectory(
                Path.Combine(
                    data,
                    "Meshes",
                    "Foo",
                    "DirectoryOnly"
                )
            );

            WindowsNamespaceAnalysis analysis =
                AnalyzeNamespace(
                    data
                );

            WindowsNamespaceRegularFileContentAnalysis result =
                WindowsNamespaceRegularFileContentAnalyzer.Analyze(
                    analysis
                );

            Assert.True(
                result.Complete,
                string.Join(
                    Environment.NewLine,
                    result.Errors
                )
            );

            Assert.Equal(
                2,
                result.Nodes.Count
            );

            Assert.Equal(
                new[]
                {
                    "MESHES/FOO/ONLY.NIF",
                    "MESHES/FOO/SWORD.NIF"
                },
                result.Nodes
                    .Select(
                        node =>
                            node.LogicalPath.Value
                    )
                    .ToArray()
            );

            WindowsNamespaceRegularFileContentNodeAnalysis single =
                result.Nodes[0];

            Assert.Equal(
                WindowsNamespaceNodeTopology.SinglePhysicalObject,
                single.Topology
            );

            WindowsNamespacePhysicalFileContentEvidence singleEvidence =
                Assert.Single(
                    single.Files
                );

            Assert.True(
                singleEvidence.Success,
                singleEvidence.Error
            );

            Assert.NotNull(
                singleEvidence.Size
            );

            Assert.NotNull(
                singleEvidence.Sha256
            );

            WindowsNamespaceRegularFileContentNodeAnalysis multiple =
                result.Nodes[1];

            Assert.Equal(
                WindowsNamespaceNodeTopology.MultipleFiles,
                multiple.Topology
            );

            Assert.Equal(
                2,
                multiple.Files.Count
            );

            Assert.All(
                multiple.Files,
                file =>
                {
                    Assert.True(
                        file.Success,
                        file.Error
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
                multiple.Files
                    .Select(file => file.Sha256)
                    .Distinct(StringComparer.Ordinal)
            );

            Assert.DoesNotContain(
                result.Nodes,
                node =>
                    node.LogicalPath.Value.Contains(
                        "DIRECTORYONLY",
                        StringComparison.Ordinal
                    )
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
    public void Analyze_FileDirectoryCollision_FailsClosedWithoutHashingNode()
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

            WindowsNamespaceNode fileNode =
                Assert.Single(
                    analysis.Nodes,
                    node =>
                        string.Equals(
                            node.LogicalPath.Value,
                            "MESHES/FOO/ONLY.NIF",
                            StringComparison.Ordinal
                        )
                );

            WindowsNamespacePhysicalParticipant file =
                Assert.Single(
                    fileNode.Participants
                );

            WindowsNamespacePhysicalParticipant directory =
                file with
                {
                    FullPath =
                        file.FullPath +
                        ".synthetic-directory",
                    RelativePath =
                        file.RelativePath +
                        ".synthetic-directory",
                    Name =
                        file.Name.ToUpperInvariant(),
                    Kind =
                        WindowsNamespacePhysicalObjectKind.Directory
                };

            WindowsNamespaceNode collision =
                new(
                    LogicalPath:
                        fileNode.LogicalPath,
                    Participants:
                        new[]
                        {
                            file,
                            directory
                        }
                );

            WindowsNamespaceAnalysis synthetic =
                analysis with
                {
                    Nodes =
                        new[]
                        {
                            collision
                        }
                };

            Assert.True(
                synthetic.Complete
            );

            WindowsNamespaceRegularFileContentAnalysis result =
                WindowsNamespaceRegularFileContentAnalyzer.Analyze(
                    synthetic
                );

            Assert.False(
                result.Complete
            );

            Assert.Empty(
                result.Nodes
            );

            string error =
                Assert.Single(
                    result.Errors
                );

            Assert.Contains(
                "FileDirectoryCollision",
                error,
                StringComparison.Ordinal
            );

            Assert.Contains(
                fileNode.LogicalPath.Value,
                error,
                StringComparison.Ordinal
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
    public void Analyze_UnsupportedObject_FailsClosedWithoutHashingNode()
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

            WindowsNamespaceNode fileNode =
                Assert.Single(
                    analysis.Nodes,
                    node =>
                        string.Equals(
                            node.LogicalPath.Value,
                            "MESHES/FOO/ONLY.NIF",
                            StringComparison.Ordinal
                        )
                );

            WindowsNamespacePhysicalParticipant file =
                Assert.Single(
                    fileNode.Participants
                );

            WindowsNamespacePhysicalParticipant unsupported =
                file with
                {
                    Kind =
                        WindowsNamespacePhysicalObjectKind.SymbolicLink
                };

            WindowsNamespaceNode unsupportedNode =
                new(
                    LogicalPath:
                        fileNode.LogicalPath,
                    Participants:
                        new[]
                        {
                            unsupported
                        }
                );

            WindowsNamespaceAnalysis synthetic =
                analysis with
                {
                    Nodes =
                        new[]
                        {
                            unsupportedNode
                        }
                };

            Assert.True(
                synthetic.Complete
            );

            WindowsNamespaceRegularFileContentAnalysis result =
                WindowsNamespaceRegularFileContentAnalyzer.Analyze(
                    synthetic
                );

            Assert.False(
                result.Complete
            );

            Assert.Empty(
                result.Nodes
            );

            string error =
                Assert.Single(
                    result.Errors
                );

            Assert.Contains(
                "UnsupportedObject",
                error,
                StringComparison.Ordinal
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
    public void Analyze_IncompleteNamespaceAnalysis_IsRejectedBeforeObservation()
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

            WindowsNamespaceRegularFileContentAnalysis result =
                WindowsNamespaceRegularFileContentAnalyzer.Analyze(
                    incomplete
                );

            Assert.False(
                result.Complete
            );

            Assert.Empty(
                result.Nodes
            );

            string error =
                Assert.Single(
                    result.Errors
                );

            Assert.Contains(
                "complete",
                error,
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
    public void Analyze_RecreatedSingleFileAfterPassOne_FailsClosed()
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
                        .Where(
                            node =>
                                string.Equals(
                                    node.LogicalPath.Value,
                                    "MESHES/FOO/ONLY.NIF",
                                    StringComparison.Ordinal
                                )
                        )
                        .SelectMany(
                            node =>
                                node.Participants
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

            WindowsNamespaceRegularFileContentAnalysis result =
                WindowsNamespaceRegularFileContentAnalyzer.Analyze(
                    analysis
                );

            Assert.False(
                result.Complete
            );

            WindowsNamespaceRegularFileContentNodeAnalysis node =
                Assert.Single(
                    result.Nodes
                );

            Assert.Equal(
                WindowsNamespaceNodeTopology.SinglePhysicalObject,
                node.Topology
            );

            WindowsNamespacePhysicalFileContentEvidence evidence =
                Assert.Single(
                    node.Files
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

            Assert.Single(
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
    public void Analyze_MultipleDirectories_AreSkippedAsNonFileLeaves()
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

            WindowsNamespaceNode directoryNode =
                Assert.Single(
                    analysis.Nodes,
                    node =>
                        string.Equals(
                            node.LogicalPath.Value,
                            "MESHES/FOO",
                            StringComparison.Ordinal
                        )
                );

            WindowsNamespacePhysicalParticipant first =
                Assert.Single(
                    directoryNode.Participants
                );

            Assert.Equal(
                WindowsNamespacePhysicalObjectKind.Directory,
                first.Kind
            );

            WindowsNamespacePhysicalParticipant second =
                first with
                {
                    FullPath =
                        first.FullPath +
                        ".synthetic-equivalent",
                    RelativePath =
                        first.RelativePath +
                        ".synthetic-equivalent",
                    Name =
                        first.Name.ToUpperInvariant()
                };

            WindowsNamespaceNode multipleDirectories =
                new(
                    LogicalPath:
                        directoryNode.LogicalPath,
                    Participants:
                        new[]
                        {
                            first,
                            second
                        }
                );

            Assert.Equal(
                WindowsNamespaceNodeTopology.MultipleDirectories,
                WindowsNamespaceNodeTopologyClassifier.Classify(
                    multipleDirectories
                )
            );

            WindowsNamespaceAnalysis synthetic =
                analysis with
                {
                    Nodes =
                        new[]
                        {
                            multipleDirectories
                        }
                };

            Assert.True(
                synthetic.Complete
            );

            WindowsNamespaceRegularFileContentAnalysis result =
                WindowsNamespaceRegularFileContentAnalyzer.Analyze(
                    synthetic
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
    public void Analyze_NoPhysicalParticipants_FailsClosedWithoutObservation()
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

            WindowsNamespaceNode fileNode =
                Assert.Single(
                    analysis.Nodes,
                    node =>
                        string.Equals(
                            node.LogicalPath.Value,
                            "MESHES/FOO/ONLY.NIF",
                            StringComparison.Ordinal
                        )
                );

            WindowsNamespaceNode emptyNode =
                new(
                    LogicalPath:
                        fileNode.LogicalPath,
                    Participants:
                        Array.Empty<
                            WindowsNamespacePhysicalParticipant
                        >()
                );

            Assert.Equal(
                WindowsNamespaceNodeTopology.NoPhysicalParticipants,
                WindowsNamespaceNodeTopologyClassifier.Classify(
                    emptyNode
                )
            );

            WindowsNamespaceAnalysis synthetic =
                analysis with
                {
                    Nodes =
                        new[]
                        {
                            emptyNode
                        }
                };

            Assert.True(
                synthetic.Complete
            );

            WindowsNamespaceRegularFileContentAnalysis result =
                WindowsNamespaceRegularFileContentAnalyzer.Analyze(
                    synthetic
                );

            Assert.False(
                result.Complete
            );

            Assert.Empty(
                result.Nodes
            );

            string error =
                Assert.Single(
                    result.Errors
                );

            Assert.Contains(
                "NoPhysicalParticipants",
                error,
                StringComparison.Ordinal
            );

            Assert.Contains(
                fileNode.LogicalPath.Value,
                error,
                StringComparison.Ordinal
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
                "casecompat-regular-file-content-" +
                Guid.NewGuid().ToString("N")
            );

        Directory.CreateDirectory(
            path
        );

        return path;
    }
}
