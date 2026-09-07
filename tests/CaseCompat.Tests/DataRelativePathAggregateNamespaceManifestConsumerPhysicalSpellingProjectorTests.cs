using CaseCompat.Core.Analysis;
using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingProjectorTests
{
    [Fact]
    public void
        Project_NoConsumers_PairsEveryManifestLeafAndRetainsPhysicalTopology()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        IReadOnlyList<
            DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingEvidence
        > result =
            DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingProjector
                .Project(
                    manifest,
                    Array.Empty<
                        DataRelativePathAggregateConsumerSpellingEvidence
                    >()
                );

        Assert.Equal(
            2,
            result.Count
        );

        Assert.Same(
            manifest.LogicalLeaves[0],
            result[0].ManifestLeaf
        );

        Assert.Same(
            manifest.LogicalLeaves[1],
            result[1].ManifestLeaf
        );

        Assert.Equal(
            DataRelativePathAggregateLogicalLeafState.UniqueRepresentation,
            result[0].ManifestLeaf.State
        );

        Assert.Equal(
            DataRelativePathAggregateLogicalLeafState
                .EquivalentContentMultipleRepresentations,
            result[1].ManifestLeaf.State
        );

        Assert.Equal(
            DataRelativePathAggregateConsumerPhysicalSpellingState
                .NoConsumerEvidence,
            result[0].SpellingEvidence.State
        );

        Assert.Equal(
            DataRelativePathAggregateConsumerPhysicalSpellingState
                .NoConsumerEvidence,
            result[1].SpellingEvidence.State
        );
    }

    [Fact]
    public void
        Project_UniquePhysicalRepresentationWithConsumerMismatch_RetainsBothDimensions()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        DataRelativePathAggregateConsumerSpellingEvidence consumer =
            Consumer(
                "meshes/body.tri"
            );

        IReadOnlyList<
            DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingEvidence
        > result =
            DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingProjector
                .Project(
                    manifest,
                    new[]
                    {
                        consumer
                    }
                );

        DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingEvidence
            body =
                Assert.Single(
                    result,
                    item =>
                        item.ManifestLeaf.WindowsLogicalPath ==
                        "MESHES/BODY.TRI"
                );

        Assert.Equal(
            DataRelativePathAggregateLogicalLeafState.UniqueRepresentation,
            body.ManifestLeaf.State
        );

        Assert.Equal(
            DataRelativePathAggregateConsumerPhysicalSpellingState
                .ConsumerCaseMismatch,
            body.SpellingEvidence.State
        );

        Assert.Equal(
            "meshes/body.tri",
            body.SpellingEvidence.AuthoritativeRequestedPath
        );

        Assert.Equal(
            new[]
            {
                "Meshes/Body.tri"
            },
            body.SpellingEvidence.PhysicalRelativePaths.ToArray()
        );
    }

    [Fact]
    public void
        Project_EquivalentPhysicalRepresentationsWithExactConsumer_RetainsBothDimensions()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        DataRelativePathAggregateConsumerSpellingEvidence consumer =
            Consumer(
                "meshes/head.tri"
            );

        IReadOnlyList<
            DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingEvidence
        > result =
            DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingProjector
                .Project(
                    manifest,
                    new[]
                    {
                        consumer
                    }
                );

        DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingEvidence
            head =
                Assert.Single(
                    result,
                    item =>
                        item.ManifestLeaf.WindowsLogicalPath ==
                        "MESHES/HEAD.TRI"
                );

        Assert.Equal(
            DataRelativePathAggregateLogicalLeafState
                .EquivalentContentMultipleRepresentations,
            head.ManifestLeaf.State
        );

        Assert.Equal(
            DataRelativePathAggregateConsumerPhysicalSpellingState
                .ExactPhysicalSpellingPresent,
            head.SpellingEvidence.State
        );

        Assert.Equal(
            2,
            head.ManifestLeaf.PhysicalRepresentations.Count
        );

        Assert.Equal(
            new[]
            {
                "Meshes/Head.tri",
                "meshes/head.tri"
            },
            head.SpellingEvidence.PhysicalRelativePaths.ToArray()
        );
    }

    [Fact]
    public void
        Project_ConflictingConsumers_DoNotChangeEquivalentPhysicalTopology()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        DataRelativePathAggregateConsumerSpellingEvidence consumer =
            Consumer(
                "Meshes/Head.tri",
                "meshes/head.tri"
            );

        IReadOnlyList<
            DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingEvidence
        > result =
            DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingProjector
                .Project(
                    manifest,
                    new[]
                    {
                        consumer
                    }
                );

        DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingEvidence
            head =
                Assert.Single(
                    result,
                    item =>
                        item.ManifestLeaf.WindowsLogicalPath ==
                        "MESHES/HEAD.TRI"
                );

        Assert.Equal(
            DataRelativePathAggregateLogicalLeafState
                .EquivalentContentMultipleRepresentations,
            head.ManifestLeaf.State
        );

        Assert.Equal(
            DataRelativePathAggregateConsumerPhysicalSpellingState
                .ConflictingConsumerSpellings,
            head.SpellingEvidence.State
        );

        Assert.Null(
            head.SpellingEvidence.AuthoritativeRequestedPath
        );
    }

    [Fact]
    public void Project_ConsumerOutsideManifestUniverse_IsNotProjected()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        DataRelativePathAggregateConsumerSpellingEvidence consumer =
            Consumer(
                "Meshes/ArchiveOnly.tri"
            );

        IReadOnlyList<
            DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingEvidence
        > result =
            DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingProjector
                .Project(
                    manifest,
                    new[]
                    {
                        consumer
                    }
                );

        Assert.Equal(
            2,
            result.Count
        );

        Assert.DoesNotContain(
            result,
            item =>
                item.SpellingEvidence.WindowsLogicalPath ==
                "MESHES/ARCHIVEONLY.TRI"
        );

        Assert.All(
            result,
            item =>
                Assert.Equal(
                    DataRelativePathAggregateConsumerPhysicalSpellingState
                        .NoConsumerEvidence,
                    item.SpellingEvidence.State
                )
        );
    }

    [Fact]
    public void Project_InvalidManifestFailsClosedBeforePairing()
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
                    DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingProjector
                        .Project(
                            manifest,
                            Array.Empty<
                                DataRelativePathAggregateConsumerSpellingEvidence
                            >()
                        )
            );

        Assert.Equal(
            "manifest",
            exception.ParamName
        );

        Assert.Contains(
            "Unsupported",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Project_NullInputsAreRejected()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        Assert.Throws<ArgumentNullException>(
            () =>
                DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingProjector
                    .Project(
                        null!,
                        Array.Empty<
                            DataRelativePathAggregateConsumerSpellingEvidence
                        >()
                    )
        );

        Assert.Throws<ArgumentNullException>(
            () =>
                DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingProjector
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
