using CaseCompat.Core.Analysis;
using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class WindowsNamespaceAggregateManifestProjectorTests
{
    [Fact]
    public void
        Project_CompleteInputs_ProducesValidDeterministicManifest()
    {
        using Fixture fixture =
            Fixture.Create();

        DateTimeOffset createdUtc =
            new(
                2026,
                9,
                5,
                12,
                0,
                0,
                TimeSpan.Zero
            );

        DataRelativePathAggregateNamespaceManifestRecord first =
            WindowsNamespaceAggregateManifestProjector.Project(
                fixture.Namespace,
                fixture.Content,
                createdUtc
            );

        DataRelativePathAggregateNamespaceManifestRecord second =
            WindowsNamespaceAggregateManifestProjector.Project(
                fixture.Namespace,
                fixture.Content,
                createdUtc
            );

        Assert.Null(
            DataRelativePathAggregateNamespaceManifest.Validate(
                first
            )
        );

        Assert.Equal(
            DataRelativePathAggregateNamespaceManifestRecord.SchemaVersion1,
            first.SchemaVersion
        );

        Assert.Equal(
            createdUtc,
            first.CreatedUtc
        );

        Assert.Equal(
            fixture.Namespace.DataRootPath,
            first.DataRoot
        );

        Assert.Equal(
            fixture.Namespace.RootLogicalPath.Value,
            first.RootWindowsLogicalPath
        );

        Assert.Equal(
            fixture.Namespace.DataRootChildNames!.ToArray(),
            first.DataRootChildNames.ToArray()
        );

        Assert.Equal(
            fixture.Namespace.DirectoryLookupObservations.ToArray(),
            first.DirectoryLookupObservations.ToArray()
        );

        Assert.Equal(
            fixture.Namespace.DirectoryIncarnationObservations.ToArray(),
            first.DirectoryIncarnationObservations.ToArray()
        );

        Assert.Equal(
            new[]
            {
                "MESHES/FOO/CONFLICT.NIF",
                "MESHES/FOO/SHARED.NIF",
                "MESHES/FOO/UNIQUE.NIF"
            },
            first.LogicalLeaves
                .Select(leaf =>
                    leaf.WindowsLogicalPath)
                .ToArray()
        );

        Assert.Equal(
            DataRelativePathAggregateLogicalLeafState
                .ConflictingContentMultipleRepresentations,
            first.LogicalLeaves[0].State
        );

        Assert.Equal(
            DataRelativePathAggregateLogicalLeafState
                .EquivalentContentMultipleRepresentations,
            first.LogicalLeaves[1].State
        );

        Assert.Equal(
            DataRelativePathAggregateLogicalLeafState
                .UniqueRepresentation,
            first.LogicalLeaves[2].State
        );

        Dictionary<
            string,
            WindowsNamespacePhysicalFileContentEvidence
        > evidenceByPath =
            fixture.Content.Nodes
                .SelectMany(node =>
                    node.Files)
                .ToDictionary(
                    evidence =>
                        evidence.Participant.FullPath,
                    StringComparer.Ordinal
                );

        foreach (
            DataRelativePathAggregateNamespaceManifestFileRepresentation
                representation
            in first.LogicalLeaves.SelectMany(
                leaf =>
                    leaf.PhysicalRepresentations))
        {
            WindowsNamespacePhysicalFileContentEvidence evidence =
                evidenceByPath[
                    representation.Snapshot.PhysicalPath
                ];

            Assert.Equal(
                evidence.Participant.RelativePath,
                representation.RelativePath
            );

            Assert.Equal(
                evidence
                    .ExpectedIncarnationObservation!
                    .InodeGeneration!
                    .Value,
                representation.InodeGeneration
            );
        }

        byte[] firstBytes =
            DataRelativePathAggregateNamespaceManifestJson.Serialize(
                first
            );

        byte[] secondBytes =
            DataRelativePathAggregateNamespaceManifestJson.Serialize(
                second
            );

        Assert.Equal(
            firstBytes,
            secondBytes
        );
    }

    [Fact]
    public void Project_IncompleteNamespaceAnalysis_IsRejected()
    {
        using Fixture fixture =
            Fixture.Create();

        WindowsNamespaceAnalysis incomplete =
            fixture.Namespace with
            {
                Errors =
                    new[]
                    {
                        "synthetic incomplete namespace"
                    }
            };

        Assert.False(
            incomplete.Complete
        );

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    WindowsNamespaceAggregateManifestProjector.Project(
                        incomplete,
                        fixture.Content,
                        DateTimeOffset.UtcNow
                    )
            );

        Assert.Contains(
            "complete Windows namespace analysis",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Project_IncompleteContentAnalysis_IsRejected()
    {
        using Fixture fixture =
            Fixture.Create();

        WindowsNamespaceRegularFileContentAnalysis incomplete =
            fixture.Content with
            {
                Errors =
                    new[]
                    {
                        "synthetic incomplete content"
                    }
            };

        Assert.False(
            incomplete.Complete
        );

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    WindowsNamespaceAggregateManifestProjector.Project(
                        fixture.Namespace,
                        incomplete,
                        DateTimeOffset.UtcNow
                    )
            );

        Assert.Contains(
            "complete stable",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Project_OmittedRegularFileLeaf_FailsClosed()
    {
        using Fixture fixture =
            Fixture.Create();

        WindowsNamespaceRegularFileContentAnalysis synthetic =
            fixture.Content with
            {
                Nodes =
                    fixture.Content.Nodes
                        .Skip(1)
                        .ToArray()
            };

        Assert.True(
            synthetic.Complete
        );

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    WindowsNamespaceAggregateManifestProjector.Project(
                        fixture.Namespace,
                        synthetic,
                        DateTimeOffset.UtcNow
                    )
            );

        Assert.Contains(
            "exactly cover",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Project_UnexpectedRegularFileLeaf_FailsClosed()
    {
        using Fixture fixture =
            Fixture.Create();

        WindowsNamespaceRegularFileContentNodeAnalysis[] nodes =
            fixture.Content.Nodes.ToArray();

        nodes[0] =
            nodes[0] with
            {
                LogicalPath =
                    WindowsLogicalPath.FromRelativePath(
                        "Meshes/Foo/Unexpected.nif"
                    )
            };

        WindowsNamespaceRegularFileContentAnalysis synthetic =
            fixture.Content with
            {
                Nodes =
                    nodes
            };

        Assert.True(
            synthetic.Complete
        );

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    WindowsNamespaceAggregateManifestProjector.Project(
                        fixture.Namespace,
                        synthetic,
                        DateTimeOffset.UtcNow
                    )
            );

        Assert.Contains(
            "missing namespace logical leaf",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Project_ContentTopologyMismatch_FailsClosed()
    {
        using Fixture fixture =
            Fixture.Create();

        int index =
            Array.FindIndex(
                fixture.Content.Nodes.ToArray(),
                node =>
                    node.Files.Count == 1
            );

        Assert.True(
            index >= 0
        );

        WindowsNamespaceRegularFileContentNodeAnalysis[] nodes =
            fixture.Content.Nodes.ToArray();

        WindowsNamespacePhysicalFileContentEvidence evidence =
            Assert.Single(
                nodes[index].Files
            );

        nodes[index] =
            nodes[index] with
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
            fixture.Content with
            {
                Nodes =
                    nodes
            };

        Assert.True(
            synthetic.Complete
        );

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    WindowsNamespaceAggregateManifestProjector.Project(
                        fixture.Namespace,
                        synthetic,
                        DateTimeOffset.UtcNow
                    )
            );

        Assert.Contains(
            "does not retain the topology",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Project_ParticipantMismatchAgainstNamespace_FailsClosed()
    {
        using Fixture fixture =
            Fixture.Create();

        WindowsNamespaceRegularFileContentAnalysis synthetic =
            ReplaceFirstEvidence(
                fixture.Content,
                evidence =>
                    evidence with
                    {
                        Participant =
                            evidence.Participant with
                            {
                                Name =
                                    evidence.Participant.Name +
                                    ".forged"
                            }
                    }
            );

        Assert.True(
            synthetic.Complete
        );

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    WindowsNamespaceAggregateManifestProjector.Project(
                        fixture.Namespace,
                        synthetic,
                        DateTimeOffset.UtcNow
                    )
            );

        Assert.Contains(
            "does not match the supplied namespace participant",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Project_ExpectedPassOneIncarnationMismatch_FailsClosed()
    {
        using Fixture fixture =
            Fixture.Create();

        WindowsNamespaceRegularFileContentAnalysis synthetic =
            ReplaceFirstEvidence(
                fixture.Content,
                evidence =>
                {
                    WindowsNamespaceFileIncarnationObservation expected =
                        evidence.ExpectedIncarnationObservation!;

                    return evidence with
                    {
                        ExpectedIncarnationObservation =
                            expected with
                            {
                                InodeGeneration =
                                    DifferentGeneration(
                                        expected.InodeGeneration!.Value
                                    )
                            }
                    };
                }
            );

        Assert.True(
            synthetic.Complete
        );

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    WindowsNamespaceAggregateManifestProjector.Project(
                        fixture.Namespace,
                        synthetic,
                        DateTimeOffset.UtcNow
                    )
            );

        Assert.Contains(
            "not bound to the supplied namespace pass-1",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Project_InitialIncarnationGenerationMismatch_FailsClosed()
    {
        using Fixture fixture =
            Fixture.Create();

        WindowsNamespaceRegularFileContentAnalysis synthetic =
            ReplaceFirstEvidence(
                fixture.Content,
                evidence =>
                {
                    LinuxOpenedFileIncarnationResult initial =
                        evidence.InitialIncarnation!;

                    LinuxFileIncarnationIdentity identity =
                        initial.Identity! with
                        {
                            InodeGeneration =
                                DifferentGeneration(
                                    initial.Identity!.InodeGeneration
                                )
                        };

                    return evidence with
                    {
                        InitialIncarnation =
                            initial with
                            {
                                Identity =
                                    identity
                            }
                    };
                }
            );

        Assert.True(
            synthetic.Complete
        );

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    WindowsNamespaceAggregateManifestProjector.Project(
                        fixture.Namespace,
                        synthetic,
                        DateTimeOffset.UtcNow
                    )
            );

        Assert.Contains(
            "initial inode generation",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Project_PostObservationGenerationMismatch_FailsClosed()
    {
        using Fixture fixture =
            Fixture.Create();

        WindowsNamespaceRegularFileContentAnalysis synthetic =
            ReplaceFirstEvidence(
                fixture.Content,
                evidence =>
                {
                    LinuxOpenedFileIncarnationResult post =
                        evidence.PostObservationIncarnation!;

                    LinuxFileIncarnationIdentity identity =
                        post.Identity! with
                        {
                            InodeGeneration =
                                DifferentGeneration(
                                    post.Identity!.InodeGeneration
                                )
                        };

                    return evidence with
                    {
                        PostObservationIncarnation =
                            post with
                            {
                                Identity =
                                    identity
                            }
                    };
                }
            );

        Assert.True(
            synthetic.Complete
        );

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    WindowsNamespaceAggregateManifestProjector.Project(
                        fixture.Namespace,
                        synthetic,
                        DateTimeOffset.UtcNow
                    )
            );

        Assert.Contains(
            "post-observation inode generation",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Project_DefaultCreationTimestamp_IsRejected()
    {
        using Fixture fixture =
            Fixture.Create();

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    WindowsNamespaceAggregateManifestProjector.Project(
                        fixture.Namespace,
                        fixture.Content,
                        default
                    )
            );

        Assert.Contains(
            "creation timestamp",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    private static WindowsNamespaceRegularFileContentAnalysis
        ReplaceFirstEvidence(
            WindowsNamespaceRegularFileContentAnalysis content,
            Func<
                WindowsNamespacePhysicalFileContentEvidence,
                WindowsNamespacePhysicalFileContentEvidence
            > transform)
    {
        WindowsNamespaceRegularFileContentNodeAnalysis[] nodes =
            content.Nodes.ToArray();

        WindowsNamespacePhysicalFileContentEvidence[] files =
            nodes[0].Files.ToArray();

        files[0] =
            transform(
                files[0]
            );

        nodes[0] =
            nodes[0] with
            {
                Files =
                    files
            };

        return content with
        {
            Nodes =
                nodes
        };
    }

    private static uint DifferentGeneration(
        uint value)
    {
        return
            value == uint.MaxValue
                ? value - 1
                : value + 1;
    }

    private sealed class Fixture : IDisposable
    {
        private Fixture(
            string root,
            string data,
            WindowsNamespaceAnalysis namespaceAnalysis,
            WindowsNamespaceRegularFileContentAnalysis content)
        {
            Root = root;
            Data = data;
            Namespace = namespaceAnalysis;
            Content = content;
        }

        public string Root
        {
            get;
        }

        public string Data
        {
            get;
        }

        public WindowsNamespaceAnalysis Namespace
        {
            get;
        }

        public WindowsNamespaceRegularFileContentAnalysis Content
        {
            get;
        }

        public static Fixture Create()
        {
            string root =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-aggregate-manifest-projector-" +
                    Guid.NewGuid().ToString("N")
                );

            Directory.CreateDirectory(
                root
            );

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

                WriteFile(
                    data,
                    "Textures/Unrelated.dds",
                    "unrelated"
                );

                WindowsNamespaceAnalysis namespaceAnalysis =
                    WindowsNamespaceAnalyzer.Analyze(
                        data,
                        "Meshes"
                    );

                Assert.True(
                    namespaceAnalysis.Complete,
                    string.Join(
                        Environment.NewLine,
                        namespaceAnalysis.Errors
                    )
                );

                WindowsNamespaceRegularFileContentAnalysis content =
                    WindowsNamespaceRegularFileContentAnalyzer.Analyze(
                        namespaceAnalysis
                    );

                Assert.True(
                    content.Complete,
                    string.Join(
                        Environment.NewLine,
                        content.Errors
                    )
                );

                return new Fixture(
                    root,
                    data,
                    namespaceAnalysis,
                    content
                );
            }
            catch
            {
                Directory.Delete(
                    root,
                    recursive:
                        true
                );

                throw;
            }
        }

        public void Dispose()
        {
            Directory.Delete(
                Root,
                recursive:
                    true
            );
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
    }
}
