using CaseCompat.Core.Analysis;
using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class
    WindowsNamespaceRegularFileAggregateLogicalLeafProjectorTests
{
    [Fact]
    public void
        Project_ClassifiesAndPreservesAllRepresentationsDeterministically()
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
                "Meshes/Foo/Unique.nif",
                "unique"
            );

            WriteFile(
                data,
                "Meshes/Foo/Shared.nif",
                "same"
            );

            WriteFile(
                data,
                "meshes/foo/shared.nif",
                "same"
            );

            WriteFile(
                data,
                "Meshes/Foo/Conflict.nif",
                "left"
            );

            WriteFile(
                data,
                "meshes/foo/conflict.nif",
                "right"
            );

            WindowsNamespaceRegularFileContentAnalysis content =
                AnalyzeRegularFiles(
                    data
                );

            IReadOnlyList<DataRelativePathAggregateLogicalLeaf> leaves =
                WindowsNamespaceRegularFileAggregateLogicalLeafProjector
                    .Project(
                        content
                    );

            Assert.Equal(
                3,
                leaves.Count
            );

            Assert.Equal(
                new[]
                {
                    "MESHES/FOO/CONFLICT.NIF",
                    "MESHES/FOO/SHARED.NIF",
                    "MESHES/FOO/UNIQUE.NIF"
                },
                leaves
                    .Select(leaf =>
                        leaf.WindowsLogicalPath)
                    .ToArray()
            );

            DataRelativePathAggregateLogicalLeaf conflict =
                Assert.Single(
                    leaves,
                    leaf =>
                        string.Equals(
                            leaf.WindowsLogicalPath,
                            "MESHES/FOO/CONFLICT.NIF",
                            StringComparison.Ordinal
                        )
                );

            DataRelativePathAggregateLogicalLeaf shared =
                Assert.Single(
                    leaves,
                    leaf =>
                        string.Equals(
                            leaf.WindowsLogicalPath,
                            "MESHES/FOO/SHARED.NIF",
                            StringComparison.Ordinal
                        )
                );

            DataRelativePathAggregateLogicalLeaf unique =
                Assert.Single(
                    leaves,
                    leaf =>
                        string.Equals(
                            leaf.WindowsLogicalPath,
                            "MESHES/FOO/UNIQUE.NIF",
                            StringComparison.Ordinal
                        )
                );

            Assert.Equal(
                DataRelativePathAggregateLogicalLeafState
                    .ConflictingContentMultipleRepresentations,
                conflict.State
            );

            Assert.Equal(
                DataRelativePathAggregateLogicalLeafState
                    .EquivalentContentMultipleRepresentations,
                shared.State
            );

            Assert.Equal(
                DataRelativePathAggregateLogicalLeafState
                    .UniqueRepresentation,
                unique.State
            );

            Assert.Equal(
                2,
                conflict.PhysicalRepresentations.Count
            );

            Assert.Equal(
                2,
                shared.PhysicalRepresentations.Count
            );

            Assert.Single(
                unique.PhysicalRepresentations
            );

            foreach (
                DataRelativePathAggregateLogicalLeaf leaf
                in leaves)
            {
                string[] observedPaths =
                    leaf.PhysicalRepresentations
                        .Select(snapshot =>
                            snapshot.PhysicalPath)
                        .ToArray();

                string[] sortedPaths =
                    observedPaths
                        .OrderBy(
                            path =>
                                path,
                            StringComparer.Ordinal
                        )
                        .ToArray();

                Assert.Equal(
                    sortedPaths,
                    observedPaths
                );
            }

            Dictionary<
                string,
                WindowsNamespacePhysicalFileContentEvidence
            > evidenceByPath =
                content.Nodes
                    .SelectMany(node =>
                        node.Files)
                    .ToDictionary(
                        evidence =>
                            evidence.Participant.FullPath,
                        StringComparer.Ordinal
                    );

            foreach (
                DataRelativePathRepairSourceSnapshot snapshot
                in leaves.SelectMany(leaf =>
                    leaf.PhysicalRepresentations))
            {
                WindowsNamespacePhysicalFileContentEvidence evidence =
                    evidenceByPath[
                        snapshot.PhysicalPath
                    ];

                LinuxOpenedFileIdentityResult expectedIdentity =
                    evidence
                        .PostObservationIncarnation!
                        .Identity!
                        .PhysicalIdentity;

                Assert.Equal(
                    snapshot.PhysicalPath,
                    snapshot.Identity.FullPath
                );

                Assert.Equal(
                    expectedIdentity.DeviceMajor,
                    snapshot.Identity.DeviceMajor
                );

                Assert.Equal(
                    expectedIdentity.DeviceMinor,
                    snapshot.Identity.DeviceMinor
                );

                Assert.Equal(
                    expectedIdentity.Inode,
                    snapshot.Identity.Inode
                );

                Assert.Equal(
                    expectedIdentity.LinkCount,
                    snapshot.Identity.LinkCount
                );

                Assert.Equal(
                    expectedIdentity.MountId,
                    snapshot.Identity.MountId
                );

                Assert.Equal(
                    evidence.Size,
                    snapshot.Size
                );

                Assert.Equal(
                    evidence.Sha256,
                    snapshot.Sha256
                );
            }
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

    [Fact]
    public void Project_DuplicatePhysicalPath_FailsClosed()
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

            WindowsNamespaceRegularFileContentAnalysis content =
                AnalyzeRegularFiles(
                    data
                );

            WindowsNamespaceRegularFileContentNodeAnalysis node =
                Assert.Single(
                    content.Nodes
                );

            WindowsNamespacePhysicalFileContentEvidence evidence =
                Assert.Single(
                    node.Files
                );

            WindowsNamespaceRegularFileContentNodeAnalysis duplicate =
                node with
                {
                    Topology =
                        WindowsNamespaceNodeTopology.MultipleFiles,
                    Files =
                        new[]
                        {
                            evidence,
                            evidence
                        }
                };

            WindowsNamespaceRegularFileContentAnalysis synthetic =
                content with
                {
                    Nodes =
                        new[]
                        {
                            duplicate
                        }
                };

            Assert.True(
                synthetic.Complete
            );

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(
                    () =>
                        WindowsNamespaceRegularFileAggregateLogicalLeafProjector
                            .Project(
                                synthetic
                            )
                );

            Assert.Contains(
                "duplicate physical path",
                exception.Message,
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

    [Fact]
    public void Project_DuplicateLogicalLeaf_FailsClosed()
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

            WindowsNamespaceRegularFileContentAnalysis content =
                AnalyzeRegularFiles(
                    data
                );

            WindowsNamespaceRegularFileContentNodeAnalysis node =
                Assert.Single(
                    content.Nodes
                );

            WindowsNamespaceRegularFileContentAnalysis synthetic =
                content with
                {
                    Nodes =
                        new[]
                        {
                            node,
                            node
                        }
                };

            Assert.True(
                synthetic.Complete
            );

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(
                    () =>
                        WindowsNamespaceRegularFileAggregateLogicalLeafProjector
                            .Project(
                                synthetic
                            )
                );

            Assert.Contains(
                "occurs more than once",
                exception.Message,
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

    [Fact]
    public void Project_MissingDescriptorLinkCount_FailsClosed()
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

            WindowsNamespaceRegularFileContentAnalysis content =
                AnalyzeRegularFiles(
                    data
                );

            WindowsNamespaceRegularFileContentNodeAnalysis node =
                Assert.Single(
                    content.Nodes
                );

            WindowsNamespacePhysicalFileContentEvidence evidence =
                Assert.Single(
                    node.Files
                );

            LinuxOpenedFileIncarnationResult post =
                evidence.PostObservationIncarnation!;

            LinuxFileIncarnationIdentity incarnationIdentity =
                post.Identity!;

            LinuxOpenedFileIdentityResult missingLinkCount =
                incarnationIdentity.PhysicalIdentity with
                {
                    LinkCount =
                        null
                };

            LinuxFileIncarnationIdentity malformedIncarnationIdentity =
                incarnationIdentity with
                {
                    PhysicalIdentity =
                        missingLinkCount
                };

            LinuxOpenedFileIncarnationResult malformedPost =
                post with
                {
                    PhysicalIdentity =
                        missingLinkCount,
                    Identity =
                        malformedIncarnationIdentity
                };

            WindowsNamespacePhysicalFileContentEvidence malformedEvidence =
                evidence with
                {
                    PostObservationIncarnation =
                        malformedPost
                };

            Assert.True(
                malformedEvidence.Success
            );

            WindowsNamespaceRegularFileContentNodeAnalysis malformedNode =
                node with
                {
                    Files =
                        new[]
                        {
                            malformedEvidence
                        }
                };

            WindowsNamespaceRegularFileContentAnalysis synthetic =
                content with
                {
                    Nodes =
                        new[]
                        {
                            malformedNode
                        }
                };

            Assert.True(
                synthetic.Complete
            );

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(
                    () =>
                        WindowsNamespaceRegularFileAggregateLogicalLeafProjector
                            .Project(
                                synthetic
                            )
                );

            Assert.Contains(
                "link count",
                exception.Message,
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

    [Fact]
    public void Project_IncompleteAnalysis_IsRejected()
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

            WindowsNamespaceRegularFileContentAnalysis content =
                AnalyzeRegularFiles(
                    data
                );

            WindowsNamespaceRegularFileContentAnalysis incomplete =
                content with
                {
                    Errors =
                        new[]
                        {
                            "synthetic incomplete analysis"
                        }
                };

            Assert.False(
                incomplete.Complete
            );

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(
                    () =>
                        WindowsNamespaceRegularFileAggregateLogicalLeafProjector
                            .Project(
                                incomplete
                            )
                );

            Assert.Contains(
                "requires complete",
                exception.Message,
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

    private static WindowsNamespaceRegularFileContentAnalysis
        AnalyzeRegularFiles(
            string data)
    {
        WindowsNamespaceAnalysis analysis =
            WindowsNamespaceAnalyzer.Analyze(
                data,
                "Meshes"
            );

        Assert.True(
            analysis.Complete,
            string.Join(
                Environment.NewLine,
                analysis.Errors
            )
        );

        WindowsNamespaceRegularFileContentAnalysis content =
            WindowsNamespaceRegularFileContentAnalyzer.Analyze(
                analysis
            );

        Assert.True(
            content.Complete,
            string.Join(
                Environment.NewLine,
                content.Errors
            )
        );

        return content;
    }

    private static void WriteFile(
        string data,
        string relativePath,
        string content)
    {
        string path =
            Path.Combine(
                data,
                relativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar
                )
            );

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                path
            )!
        );

        File.WriteAllText(
            path,
            content
        );
    }

    private static string CreateTempDirectory()
    {
        string path =
            Path.Combine(
                Path.GetTempPath(),
                "casecompat-aggregate-projector-" +
                Guid.NewGuid().ToString("N")
            );

        Directory.CreateDirectory(
            path
        );

        return path;
    }
}
