using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Analysis;
using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class
    SkyrimWinningConsumerPhysicalSpellingEvidenceProjectorTests
{
    [Fact]
    public void
        Project_CompleteEmptyCompositionPublishesManifestLeafUniverse()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        SkyrimWinningConsumerSpellingEvidenceCompositionResult composition =
            Composition(
                SkyrimWinningConsumerSpellingEvidenceCompositionState.Complete,
                Array.Empty<
                    DataRelativePathAggregateConsumerSpellingEvidence
                >()
            );

        SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionResult result =
            SkyrimWinningConsumerPhysicalSpellingEvidenceProjector.Project(
                manifest,
                composition
            );

        Assert.Same(
            manifest,
            result.Manifest
        );

        Assert.Same(
            composition,
            result.ConsumerSpellingComposition
        );

        Assert.Equal(
            SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionState
                .Complete,
            result.State
        );

        Assert.True(
            result.ConsumerPhysicalSpellingEvidenceComplete
        );

        Assert.Null(
            result.Error
        );

        Assert.Equal(
            2,
            result.EvidenceCount
        );

        Assert.All(
            result.Evidence,
            item =>
                Assert.Equal(
                    DataRelativePathAggregateConsumerPhysicalSpellingState
                        .NoConsumerEvidence,
                    item.SpellingEvidence.State
                )
        );
    }

    [Fact]
    public void
        Project_CompleteCompositionDelegatesConsumerSpellingRelationToCore()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        DataRelativePathAggregateConsumerSpellingEvidence consumer =
            Consumer(
                "meshes/body.tri"
            );

        SkyrimWinningConsumerSpellingEvidenceCompositionResult composition =
            Composition(
                SkyrimWinningConsumerSpellingEvidenceCompositionState.Complete,
                new[]
                {
                    consumer
                }
            );

        SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionResult result =
            SkyrimWinningConsumerPhysicalSpellingEvidenceProjector.Project(
                manifest,
                composition
            );

        DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingEvidence
            body =
                Assert.Single(
                    result.Evidence,
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
        Project_IncompleteWinnerSearchDoesNotInspectConsumerEvidenceOrManifest()
    {
        DataRelativePathAggregateNamespaceManifestRecord invalidManifest =
            CreateValidManifest() with
            {
                SchemaVersion =
                    DataRelativePathAggregateNamespaceManifestRecord
                        .SchemaVersion1 + 1
            };

        SkyrimWinningConsumerSpellingEvidenceCompositionResult composition =
            Composition(
                SkyrimWinningConsumerSpellingEvidenceCompositionState
                    .IncompleteWinnerSearch,
                null!,
                error:
                    "Winning-record search was incomplete."
            );

        SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionResult result =
            SkyrimWinningConsumerPhysicalSpellingEvidenceProjector.Project(
                invalidManifest,
                composition
            );

        Assert.Equal(
            SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionState
                .IncompleteWinnerSearch,
            result.State
        );

        Assert.False(
            result.ConsumerPhysicalSpellingEvidenceComplete
        );

        Assert.Empty(
            result.Evidence
        );

        Assert.Equal(
            "Winning-record search was incomplete.",
            result.Error
        );
    }

    [Fact]
    public void
        Project_IndeterminateConsumerPathEvidenceDoesNotBecomeNoConsumerEvidence()
    {
        DataRelativePathAggregateNamespaceManifestRecord invalidManifest =
            CreateValidManifest() with
            {
                SchemaVersion =
                    DataRelativePathAggregateNamespaceManifestRecord
                        .SchemaVersion1 + 1
            };

        SkyrimWinningConsumerSpellingEvidenceCompositionResult composition =
            Composition(
                SkyrimWinningConsumerSpellingEvidenceCompositionState
                    .IndeterminateConsumerPathEvidence,
                null!,
                error:
                    "Consumer-path evidence is indeterminate."
            );

        SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionResult result =
            SkyrimWinningConsumerPhysicalSpellingEvidenceProjector.Project(
                invalidManifest,
                composition
            );

        Assert.Equal(
            SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionState
                .IndeterminateConsumerPathEvidence,
            result.State
        );

        Assert.Empty(
            result.Evidence
        );

        Assert.Equal(
            0,
            result.EvidenceCount
        );

        Assert.Equal(
            "Consumer-path evidence is indeterminate.",
            result.Error
        );
    }

    [Fact]
    public void
        Project_CompleteMalformedConsumerEvidenceFailsClosedAsAggregateIndeterminate()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        SkyrimWinningConsumerSpellingEvidenceCompositionResult composition =
            Composition(
                SkyrimWinningConsumerSpellingEvidenceCompositionState.Complete,
                null!
            );

        SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionResult result =
            SkyrimWinningConsumerPhysicalSpellingEvidenceProjector.Project(
                manifest,
                composition
            );

        Assert.Equal(
            SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionState
                .IndeterminateAggregateEvidence,
            result.State
        );

        Assert.False(
            result.ConsumerPhysicalSpellingEvidenceComplete
        );

        Assert.Empty(
            result.Evidence
        );

        Assert.NotNull(
            result.Error
        );

        Assert.Contains(
            "rejected",
            result.Error!,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void
        Project_CompleteInvalidManifestFailsClosedAsAggregateIndeterminate()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest() with
            {
                SchemaVersion =
                    DataRelativePathAggregateNamespaceManifestRecord
                        .SchemaVersion1 + 1
            };

        SkyrimWinningConsumerSpellingEvidenceCompositionResult composition =
            Composition(
                SkyrimWinningConsumerSpellingEvidenceCompositionState.Complete,
                Array.Empty<
                    DataRelativePathAggregateConsumerSpellingEvidence
                >()
            );

        SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionResult result =
            SkyrimWinningConsumerPhysicalSpellingEvidenceProjector.Project(
                manifest,
                composition
            );

        Assert.Equal(
            SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionState
                .IndeterminateAggregateEvidence,
            result.State
        );

        Assert.Empty(
            result.Evidence
        );

        Assert.NotNull(
            result.Error
        );

        Assert.Contains(
            "Unsupported",
            result.Error!,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void
        Project_UnrecognizedCompositionStateFailsClosedWithoutAggregateProjection()
    {
        DataRelativePathAggregateNamespaceManifestRecord invalidManifest =
            CreateValidManifest() with
            {
                SchemaVersion =
                    DataRelativePathAggregateNamespaceManifestRecord
                        .SchemaVersion1 + 1
            };

        SkyrimWinningConsumerSpellingEvidenceCompositionResult composition =
            Composition(
                (SkyrimWinningConsumerSpellingEvidenceCompositionState)999,
                null!
            );

        SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionResult result =
            SkyrimWinningConsumerPhysicalSpellingEvidenceProjector.Project(
                invalidManifest,
                composition
            );

        Assert.Equal(
            SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionState
                .IndeterminateAggregateEvidence,
            result.State
        );

        Assert.Empty(
            result.Evidence
        );

        Assert.NotNull(
            result.Error
        );

        Assert.Contains(
            "unrecognized",
            result.Error!,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Project_NullInputsAreRejected()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        SkyrimWinningConsumerSpellingEvidenceCompositionResult composition =
            Composition(
                SkyrimWinningConsumerSpellingEvidenceCompositionState.Complete,
                Array.Empty<
                    DataRelativePathAggregateConsumerSpellingEvidence
                >()
            );

        Assert.Throws<ArgumentNullException>(
            () =>
                SkyrimWinningConsumerPhysicalSpellingEvidenceProjector.Project(
                    null!,
                    composition
                )
        );

        Assert.Throws<ArgumentNullException>(
            () =>
                SkyrimWinningConsumerPhysicalSpellingEvidenceProjector.Project(
                    manifest,
                    null!
                )
        );
    }

    private static SkyrimWinningConsumerSpellingEvidenceCompositionResult
        Composition(
            SkyrimWinningConsumerSpellingEvidenceCompositionState state,
            IReadOnlyList<DataRelativePathAggregateConsumerSpellingEvidence>
                evidence,
            string? error =
                null)
    {
        return new SkyrimWinningConsumerSpellingEvidenceCompositionResult(
            ArmorAddonProjection:
                null!,
            HeadPartProjection:
                null!,
            State:
                state,
            Evidence:
                evidence,
            Error:
                error
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
