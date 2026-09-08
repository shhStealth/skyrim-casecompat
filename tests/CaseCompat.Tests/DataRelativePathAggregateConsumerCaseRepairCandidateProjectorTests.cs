using CaseCompat.Core.Analysis;
using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathAggregateConsumerCaseRepairCandidateProjectorTests
{
    [Fact]
    public void
        Project_UniqueConsumerCaseMismatchBindsSoleManifestRepresentation()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        IReadOnlyList<
            DataRelativePathAggregateConsumerCaseRepairCandidate
        > result =
            DataRelativePathAggregateConsumerCaseRepairCandidateProjector
                .Project(
                    manifest,
                    new[]
                    {
                        Consumer(
                            "meshes/body.tri"
                        )
                    }
                );

        DataRelativePathAggregateConsumerCaseRepairCandidate candidate =
            Assert.Single(
                result
            );

        Assert.Same(
            manifest.LogicalLeaves[0],
            candidate.Evidence.ManifestLeaf
        );

        Assert.Same(
            manifest.LogicalLeaves[0].PhysicalRepresentations[0],
            candidate.SourceRepresentation
        );

        Assert.Equal(
            "MESHES/BODY.TRI",
            candidate.WindowsLogicalPath
        );

        Assert.Equal(
            "meshes/body.tri",
            candidate.AuthoritativeRequestedPath
        );

        Assert.Equal(
            "Meshes/Body.tri",
            candidate.SourceRepresentation.RelativePath
        );

        Assert.Same(
            candidate.SourceRepresentation.Snapshot,
            candidate.SourceSnapshot
        );

        Assert.Equal(
            10u,
            candidate.SourceInodeGeneration
        );
    }

    [Fact]
    public void Project_ExactPhysicalSpellingDoesNotProjectCandidate()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        IReadOnlyList<
            DataRelativePathAggregateConsumerCaseRepairCandidate
        > result =
            DataRelativePathAggregateConsumerCaseRepairCandidateProjector
                .Project(
                    manifest,
                    new[]
                    {
                        Consumer(
                            "Meshes/Body.tri"
                        )
                    }
                );

        Assert.Empty(
            result
        );
    }

    [Fact]
    public void Project_NoConsumerEvidenceDoesNotProjectCandidate()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        IReadOnlyList<
            DataRelativePathAggregateConsumerCaseRepairCandidate
        > result =
            DataRelativePathAggregateConsumerCaseRepairCandidateProjector
                .Project(
                    manifest,
                    Array.Empty<
                        DataRelativePathAggregateConsumerSpellingEvidence
                    >()
                );

        Assert.Empty(
            result
        );
    }

    [Fact]
    public void
        Project_ConflictingConsumerSpellingsDoNotProjectCandidate()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        IReadOnlyList<
            DataRelativePathAggregateConsumerCaseRepairCandidate
        > result =
            DataRelativePathAggregateConsumerCaseRepairCandidateProjector
                .Project(
                    manifest,
                    new[]
                    {
                        Consumer(
                            "meshes/body.tri",
                            "MESHES/body.tri"
                        )
                    }
                );

        Assert.Empty(
            result
        );
    }

    [Fact]
    public void
        Project_EquivalentContentMismatchRemainsBlockedBySourcePolicy()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        IReadOnlyList<
            DataRelativePathAggregateConsumerCaseRepairCandidate
        > result =
            DataRelativePathAggregateConsumerCaseRepairCandidateProjector
                .Project(
                    manifest,
                    new[]
                    {
                        Consumer(
                            "MESHES/HEAD.tri"
                        )
                    }
                );

        Assert.Empty(
            result
        );
    }

    [Fact]
    public void
        Project_ConflictingContentMismatchDoesNotProjectCandidate()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateConflictingContentManifest();

        IReadOnlyList<
            DataRelativePathAggregateConsumerCaseRepairCandidate
        > result =
            DataRelativePathAggregateConsumerCaseRepairCandidateProjector
                .Project(
                    manifest,
                    new[]
                    {
                        Consumer(
                            "MESHES/HEAD.tri"
                        )
                    }
                );

        Assert.Empty(
            result
        );
    }

    [Fact]
    public void Project_InvalidManifestFailsClosedBeforeCandidateBinding()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest() with
            {
                SchemaVersion =
                    DataRelativePathAggregateNamespaceManifestRecord
                        .SchemaVersion1 + 1
            };

        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () =>
                    DataRelativePathAggregateConsumerCaseRepairCandidateProjector
                        .Project(
                            manifest,
                            new[]
                            {
                                Consumer(
                                    "meshes/body.tri"
                                )
                            }
                        )
            );

        Assert.Equal(
            "manifest",
            exception.ParamName
        );
    }

    [Fact]
    public void Project_NullInputsAreRejected()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        Assert.Throws<ArgumentNullException>(
            () =>
                DataRelativePathAggregateConsumerCaseRepairCandidateProjector
                    .Project(
                        null!,
                        Array.Empty<
                            DataRelativePathAggregateConsumerSpellingEvidence
                        >()
                    )
        );

        Assert.Throws<ArgumentNullException>(
            () =>
                DataRelativePathAggregateConsumerCaseRepairCandidateProjector
                    .Project(
                        manifest,
                        null!
                    )
        );
    }

    private static DataRelativePathAggregateConsumerSpellingEvidence Consumer(
        params string[] requestedPaths)
    {
        if (requestedPaths.Length == 0)
        {
            throw new ArgumentException(
                "At least one requested path is required.",
                nameof(requestedPaths)
            );
        }

        string windowsLogicalPath =
            WindowsLogicalPath
                .FromRelativePath(
                    requestedPaths[0]
                )
                .Value;

        return
            DataRelativePathAggregateConsumerSpellingClassifier
                .Classify(
                    windowsLogicalPath,
                    requestedPaths
                );
    }

    private static DataRelativePathAggregateNamespaceManifestRecord
        CreateConflictingContentManifest()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        DataRelativePathAggregateNamespaceManifestLogicalLeaf body =
            manifest.LogicalLeaves[0];

        DataRelativePathAggregateNamespaceManifestLogicalLeaf head =
            manifest.LogicalLeaves[1];

        DataRelativePathAggregateNamespaceManifestFileRepresentation
            firstHeadRepresentation =
                head.PhysicalRepresentations[0];

        DataRelativePathAggregateNamespaceManifestFileRepresentation
            secondHeadRepresentation =
                head.PhysicalRepresentations[1];

        DataRelativePathRepairSourceSnapshot conflictingSnapshot =
            secondHeadRepresentation.Snapshot with
            {
                Sha256 =
                    "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC" +
                    "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC"
            };

        DataRelativePathAggregateNamespaceManifestLogicalLeaf
            conflictingHead =
                head with
                {
                    State =
                        DataRelativePathAggregateLogicalLeafState
                            .ConflictingContentMultipleRepresentations,
                    PhysicalRepresentations =
                        new[]
                        {
                            firstHeadRepresentation,
                            secondHeadRepresentation with
                            {
                                Snapshot =
                                    conflictingSnapshot
                            }
                        }
                };

        return manifest with
        {
            LogicalLeaves =
                new[]
                {
                    body,
                    conflictingHead
                }
        };
    }

    private static DataRelativePathAggregateNamespaceManifestRecord
        CreateValidManifest()
    {
        const string dataRoot =
            "/game/Data";

        const string hashA =
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

        const string hashB =
            "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB" +
            "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

        var lookupObservations =
            new[]
            {
                Lookup(
                    dataRoot,
                    ".",
                    casefold:
                        true
                ),
                Lookup(
                    $"{dataRoot}/Meshes",
                    "Meshes",
                    casefold:
                        false
                ),
                Lookup(
                    $"{dataRoot}/meshes",
                    "meshes",
                    casefold:
                        false
                )
            };

        var incarnationObservations =
            new[]
            {
                DirectoryIncarnation(
                    dataRoot,
                    ".",
                    inode:
                        100,
                    generation:
                        1
                ),
                DirectoryIncarnation(
                    $"{dataRoot}/Meshes",
                    "Meshes",
                    inode:
                        200,
                    generation:
                        2
                ),
                DirectoryIncarnation(
                    $"{dataRoot}/meshes",
                    "meshes",
                    inode:
                        201,
                    generation:
                        3
                )
            };

        var uniqueLeaf =
            new DataRelativePathAggregateNamespaceManifestLogicalLeaf(
                WindowsLogicalPath:
                    "MESHES/BODY.TRI",
                State:
                    DataRelativePathAggregateLogicalLeafState
                        .UniqueRepresentation,
                PhysicalRepresentations:
                    new[]
                    {
                        Representation(
                            relativePath:
                                "Meshes/Body.tri",
                            physicalPath:
                                $"{dataRoot}/Meshes/Body.tri",
                            size:
                                10,
                            sha256:
                                hashA,
                            inode:
                                400,
                            generation:
                                10
                        )
                    }
            );

        var equivalentLeaf =
            new DataRelativePathAggregateNamespaceManifestLogicalLeaf(
                WindowsLogicalPath:
                    "MESHES/HEAD.TRI",
                State:
                    DataRelativePathAggregateLogicalLeafState
                        .EquivalentContentMultipleRepresentations,
                PhysicalRepresentations:
                    new[]
                    {
                        Representation(
                            relativePath:
                                "Meshes/Head.tri",
                            physicalPath:
                                $"{dataRoot}/Meshes/Head.tri",
                            size:
                                20,
                            sha256:
                                hashB,
                            inode:
                                401,
                            generation:
                                11
                        ),
                        Representation(
                            relativePath:
                                "meshes/head.tri",
                            physicalPath:
                                $"{dataRoot}/meshes/head.tri",
                            size:
                                20,
                            sha256:
                                hashB,
                            inode:
                                402,
                            generation:
                                12
                        )
                    }
            );

        return new(
            SchemaVersion:
                DataRelativePathAggregateNamespaceManifestRecord
                    .SchemaVersion1,
            CreatedUtc:
                new DateTimeOffset(
                    2026,
                    9,
                    5,
                    0,
                    0,
                    0,
                    TimeSpan.Zero
                ),
            DataRoot:
                dataRoot,
            RootWindowsLogicalPath:
                "MESHES",
            DataRootChildNames:
                new[]
                {
                    "Meshes",
                    "meshes"
                },
            DirectoryLookupObservations:
                lookupObservations,
            DirectoryIncarnationObservations:
                incarnationObservations,
            LogicalLeaves:
                new[]
                {
                    uniqueLeaf,
                    equivalentLeaf
                }
        );
    }

    private static WindowsNamespaceDirectoryLookupObservation Lookup(
        string fullPath,
        string relativePath,
        bool casefold)
    {
        return new(
            FullPath:
                fullPath,
            RelativePath:
                relativePath,
            CasefoldEnabled:
                casefold,
            RawFlags:
                casefold
                    ? 0x40000000L
                    : 0L,
            Error:
                null
        );
    }

    private static WindowsNamespaceDirectoryIncarnationObservation
        DirectoryIncarnation(
            string fullPath,
            string relativePath,
            ulong inode,
            uint generation)
    {
        return new(
            FullPath:
                fullPath,
            RelativePath:
                relativePath,
            DeviceMajor:
                8,
            DeviceMinor:
                1,
            Inode:
                inode,
            MountId:
                50,
            InodeGeneration:
                generation,
            Error:
                null
        );
    }

    private static
        DataRelativePathAggregateNamespaceManifestFileRepresentation
        Representation(
            string relativePath,
            string physicalPath,
            long size,
            string sha256,
            ulong inode,
            uint generation)
    {
        return new(
            RelativePath:
                relativePath,
            Snapshot:
                new DataRelativePathRepairSourceSnapshot(
                    PhysicalPath:
                        physicalPath,
                    Size:
                        size,
                    Sha256:
                        sha256,
                    Identity:
                        new LinuxFileIdentityResult(
                            FullPath:
                                physicalPath,
                            DeviceMajor:
                                8,
                            DeviceMinor:
                                1,
                            Inode:
                                inode,
                            LinkCount:
                                1,
                            MountId:
                                50,
                            Error:
                                null
                        )
                ),
            InodeGeneration:
                generation
        );
    }
}
