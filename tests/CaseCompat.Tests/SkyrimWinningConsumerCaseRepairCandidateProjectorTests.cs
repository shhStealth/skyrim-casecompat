using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Analysis;
using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class
    SkyrimWinningConsumerCaseRepairCandidateProjectorTests
{
    [Fact]
    public void
        Project_CompleteUniqueMismatchPublishesSourceBoundCandidate()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        SkyrimWinningConsumerSpellingEvidenceCompositionResult composition =
            Composition(
                SkyrimWinningConsumerSpellingEvidenceCompositionState.Complete,
                new[]
                {
                    Consumer(
                        "meshes/body.tri"
                    )
                }
            );

        SkyrimWinningConsumerCaseRepairCandidateProjectionResult result =
            SkyrimWinningConsumerCaseRepairCandidateProjector.Project(
                manifest,
                composition
            );

        Assert.Equal(
            SkyrimWinningConsumerCaseRepairCandidateProjectionState.Complete,
            result.State
        );

        Assert.True(
            result.CandidateEvidenceComplete
        );

        Assert.Null(
            result.Error
        );

        Assert.Same(
            manifest,
            result.ConsumerPhysicalSpellingProjection.Manifest
        );

        Assert.Same(
            composition,
            result
                .ConsumerPhysicalSpellingProjection
                .ConsumerSpellingComposition
        );

        DataRelativePathAggregateConsumerCaseRepairCandidate candidate =
            Assert.Single(
                result.Candidates
            );

        Assert.Same(
            manifest.LogicalLeaves[0].PhysicalRepresentations[0],
            candidate.SourceRepresentation
        );

        Assert.Equal(
            "meshes/body.tri",
            candidate.AuthoritativeRequestedPath
        );

        Assert.Equal(
            10u,
            candidate.SourceInodeGeneration
        );
    }

    [Fact]
    public void
        Project_CompleteEmptyConsumerPopulationRemainsCompleteWithZeroCandidates()
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

        SkyrimWinningConsumerCaseRepairCandidateProjectionResult result =
            SkyrimWinningConsumerCaseRepairCandidateProjector.Project(
                manifest,
                composition
            );

        Assert.Equal(
            SkyrimWinningConsumerCaseRepairCandidateProjectionState.Complete,
            result.State
        );

        Assert.True(
            result.CandidateEvidenceComplete
        );

        Assert.Empty(
            result.Candidates
        );

        Assert.Equal(
            manifest.LogicalLeaves.Count,
            result
                .ConsumerPhysicalSpellingProjection
                .EvidenceCount
        );

        Assert.All(
            result.ConsumerPhysicalSpellingProjection.Evidence,
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
        Project_CompleteEquivalentContentMismatchRemainsCompleteButBlocked()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        SkyrimWinningConsumerSpellingEvidenceCompositionResult composition =
            Composition(
                SkyrimWinningConsumerSpellingEvidenceCompositionState.Complete,
                new[]
                {
                    Consumer(
                        "MESHES/HEAD.tri"
                    )
                }
            );

        SkyrimWinningConsumerCaseRepairCandidateProjectionResult result =
            SkyrimWinningConsumerCaseRepairCandidateProjector.Project(
                manifest,
                composition
            );

        Assert.Equal(
            SkyrimWinningConsumerCaseRepairCandidateProjectionState.Complete,
            result.State
        );

        Assert.True(
            result.CandidateEvidenceComplete
        );

        Assert.Empty(
            result.Candidates
        );

        DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingEvidence
            head =
                Assert.Single(
                    result.ConsumerPhysicalSpellingProjection.Evidence,
                    item =>
                        item.ManifestLeaf.WindowsLogicalPath ==
                        "MESHES/HEAD.TRI"
                );

        Assert.Equal(
            DataRelativePathAggregateConsumerPhysicalSpellingState
                .ConsumerCaseMismatch,
            head.SpellingEvidence.State
        );
    }

    [Fact]
    public void
        Project_IncompleteWinnerSearchPublishesNoCandidatesWithoutManifestValidation()
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
                    "winner search incomplete"
            );

        SkyrimWinningConsumerCaseRepairCandidateProjectionResult result =
            SkyrimWinningConsumerCaseRepairCandidateProjector.Project(
                invalidManifest,
                composition
            );

        Assert.Equal(
            SkyrimWinningConsumerCaseRepairCandidateProjectionState
                .IncompleteWinnerSearch,
            result.State
        );

        Assert.False(
            result.CandidateEvidenceComplete
        );

        Assert.Empty(
            result.Candidates
        );

        Assert.Empty(
            result.ConsumerPhysicalSpellingProjection.Evidence
        );

        Assert.Equal(
            "winner search incomplete",
            result.Error
        );
    }

    [Fact]
    public void
        Project_IndeterminateConsumerPathEvidencePublishesNoCandidatesWithoutManifestValidation()
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
                    "consumer paths indeterminate"
            );

        SkyrimWinningConsumerCaseRepairCandidateProjectionResult result =
            SkyrimWinningConsumerCaseRepairCandidateProjector.Project(
                invalidManifest,
                composition
            );

        Assert.Equal(
            SkyrimWinningConsumerCaseRepairCandidateProjectionState
                .IndeterminateConsumerPathEvidence,
            result.State
        );

        Assert.False(
            result.CandidateEvidenceComplete
        );

        Assert.Empty(
            result.Candidates
        );

        Assert.Empty(
            result.ConsumerPhysicalSpellingProjection.Evidence
        );

        Assert.Equal(
            "consumer paths indeterminate",
            result.Error
        );
    }

    [Fact]
    public void
        Project_CompleteInvalidManifestFailsClosedBeforeCandidatePolicy()
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
                SkyrimWinningConsumerSpellingEvidenceCompositionState.Complete,
                Array.Empty<
                    DataRelativePathAggregateConsumerSpellingEvidence
                >()
            );

        SkyrimWinningConsumerCaseRepairCandidateProjectionResult result =
            SkyrimWinningConsumerCaseRepairCandidateProjector.Project(
                invalidManifest,
                composition
            );

        Assert.Equal(
            SkyrimWinningConsumerCaseRepairCandidateProjectionState
                .IndeterminateAggregateEvidence,
            result.State
        );

        Assert.False(
            result.CandidateEvidenceComplete
        );

        Assert.Empty(
            result.Candidates
        );

        Assert.Empty(
            result.ConsumerPhysicalSpellingProjection.Evidence
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
        Project_UnrecognizedCompositionStateFailsClosedBeforeCandidatePolicy()
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

        SkyrimWinningConsumerCaseRepairCandidateProjectionResult result =
            SkyrimWinningConsumerCaseRepairCandidateProjector.Project(
                invalidManifest,
                composition
            );

        Assert.Equal(
            SkyrimWinningConsumerCaseRepairCandidateProjectionState
                .IndeterminateAggregateEvidence,
            result.State
        );

        Assert.Empty(
            result.Candidates
        );

        Assert.Empty(
            result.ConsumerPhysicalSpellingProjection.Evidence
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
                SkyrimWinningConsumerCaseRepairCandidateProjector.Project(
                    null!,
                    composition
                )
        );

        Assert.Throws<ArgumentNullException>(
            () =>
                SkyrimWinningConsumerCaseRepairCandidateProjector.Project(
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
