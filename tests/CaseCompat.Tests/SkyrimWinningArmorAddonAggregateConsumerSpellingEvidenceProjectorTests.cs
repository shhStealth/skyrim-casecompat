using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Analysis;
using CaseCompat.Core.Repair;

namespace CaseCompat.Tests;

public sealed class
    SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectorTests
{
    [Fact]
    public void Project_CompleteSingleConsumerPublishesUniqueSpelling()
    {
        const string requestedPath =
            "meshes/Actors/Character/Character Assets/FaceParts/" +
            "MaleHeadBrows.tri";

        SkyrimWinningArmorAddonSnapshotEvidenceScanResult scan =
            Scan(
                Path(
                    requestedPath,
                    requestedPath
                )
            );

        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        scan
                    );

        Assert.Same(
            scan,
            result.Scan
        );

        Assert.True(result.WinnerSearchComplete);
        Assert.True(result.ConsumerPathEvidenceComplete);
        Assert.Null(result.Error);

        Assert.Equal(
            SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionState
                .Complete,
            result.State
        );

        DataRelativePathAggregateConsumerSpellingEvidence evidence =
            Assert.Single(
                result.Evidence
            );

        Assert.Equal(
            "MESHES/ACTORS/CHARACTER/CHARACTER ASSETS/FACEPARTS/" +
            "MALEHEADBROWS.TRI",
            evidence.WindowsLogicalPath
        );

        Assert.Equal(
            DataRelativePathAggregateConsumerSpellingState
                .UniqueConsumerSpelling,
            evidence.State
        );

        Assert.Equal(
            requestedPath,
            evidence.AuthoritativeRequestedPath
        );
    }

    [Fact]
    public void Project_RepeatedExactConsumersRemainUnique()
    {
        const string requestedPath =
            "Meshes/Actors/Character/Body.nif";

        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        Scan(
                            Path(
                                requestedPath,
                                requestedPath,
                                requestedPath
                            )
                        )
                    );

        DataRelativePathAggregateConsumerSpellingEvidence evidence =
            Assert.Single(
                result.Evidence
            );

        Assert.Equal(
            DataRelativePathAggregateConsumerSpellingState
                .UniqueConsumerSpelling,
            evidence.State
        );

        Assert.Equal(
            new[]
            {
                requestedPath
            },
            evidence.DistinctRequestedPaths
        );
    }

    [Fact]
    public void Project_CaseDistinctConsumersOnSameLogicalLeafConflict()
    {
        const string first =
            "meshes/Actors/Character/Character Assets/FaceParts/" +
            "MaleHeadBrows.tri";

        const string second =
            "meshes/actors/character/character assets/faceparts/" +
            "MaleHeadbrows.tri";

        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        Scan(
                            Path(
                                second,
                                second
                            ),
                            Path(
                                first,
                                first
                            )
                        )
                    );

        DataRelativePathAggregateConsumerSpellingEvidence evidence =
            Assert.Single(
                result.Evidence
            );

        Assert.Equal(
            DataRelativePathAggregateConsumerSpellingState
                .ConflictingConsumerSpellings,
            evidence.State
        );

        Assert.Null(
            evidence.AuthoritativeRequestedPath
        );

        Assert.Equal(
            new[]
            {
                first,
                second
            },
            evidence.DistinctRequestedPaths
        );
    }

    [Fact]
    public void Project_MultipleLogicalLeavesAreOrdinallyOrdered()
    {
        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        Scan(
                            Path(
                                "textures/Z.dds",
                                "textures/Z.dds"
                            ),
                            Path(
                                "Meshes/A.nif",
                                "Meshes/A.nif"
                            )
                        )
                    );

        Assert.Equal(
            new[]
            {
                "MESHES/A.NIF",
                "TEXTURES/Z.DDS"
            },
            result.Evidence
                .Select(
                    item =>
                        item.WindowsLogicalPath
                )
                .ToArray()
        );
    }

    [Fact]
    public void Project_IncompleteWinnerSearchDominatesInvalidPath()
    {
        SkyrimWinningArmorAddonSnapshotEvidenceScanResult scan =
            Scan(
                searchComplete:
                    false,
                Path(
                    "Meshes//Broken.nif",
                    "Meshes//Broken.nif"
                )
            );

        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        scan
                    );

        Assert.False(result.WinnerSearchComplete);
        Assert.False(result.ConsumerPathEvidenceComplete);

        Assert.Equal(
            SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionState
                .IncompleteWinnerSearch,
            result.State
        );

        Assert.Empty(result.Evidence);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Project_CompleteWinnerSearchWithInvalidPathIsIndeterminate()
    {
        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        Scan(
                            Path(
                                "Meshes//Broken.nif",
                                "Meshes//Broken.nif"
                            )
                        )
                    );

        Assert.True(result.WinnerSearchComplete);
        Assert.False(result.ConsumerPathEvidenceComplete);

        Assert.Equal(
            SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionState
                .IndeterminateConsumerPathEvidence,
            result.State
        );

        Assert.Empty(result.Evidence);
        Assert.NotNull(result.Error);

        Assert.Contains(
            "invalid requested path",
            result.Error!.ToLowerInvariant()
        );
    }

    [Fact]
    public void Project_PathGroupAndConsumerMismatchIsIndeterminate()
    {
        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        Scan(
                            Path(
                                "Meshes/A.nif",
                                "Meshes/B.nif"
                            )
                        )
                    );

        Assert.Equal(
            SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionState
                .IndeterminateConsumerPathEvidence,
            result.State
        );

        Assert.False(result.ConsumerPathEvidenceComplete);
        Assert.Empty(result.Evidence);
        Assert.NotNull(result.Error);

        Assert.Contains(
            "does not exactly match",
            result.Error!
        );
    }

    [Fact]
    public void Project_ValidConsumerPathDoesNotRequireLooseLookup()
    {
        const string requestedPath =
            "Meshes/Actors/Character/Body.nif";

        SkyrimWinningArmorAddonSnapshotPathEvidence path =
            Path(
                requestedPath,
                requestedPath
            );

        Assert.Equal(
            SkyrimArmorAddonSnapshotLookupEvidenceState
                .NoMatchingNamespaceAnalysis,
            path.State
        );

        Assert.Null(path.Lookup);

        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        Scan(
                            path
                        )
                    );

        Assert.Equal(
            SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionState
                .Complete,
            result.State
        );

        DataRelativePathAggregateConsumerSpellingEvidence evidence =
            Assert.Single(
                result.Evidence
            );

        Assert.Equal(
            DataRelativePathAggregateConsumerSpellingState
                .UniqueConsumerSpelling,
            evidence.State
        );

        Assert.Equal(
            requestedPath,
            evidence.AuthoritativeRequestedPath
        );
    }

    [Fact]
    public void Project_CompleteEmptyScanProducesCompleteEmptyEvidence()
    {
        SkyrimWinningArmorAddonSnapshotEvidenceScanResult scan =
            Scan();

        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        scan
                    );

        Assert.True(result.WinnerSearchComplete);
        Assert.True(result.ConsumerPathEvidenceComplete);

        Assert.Equal(
            SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionState
                .Complete,
            result.State
        );

        Assert.Empty(result.Evidence);
        Assert.Equal(0, result.EvidenceCount);
        Assert.Null(result.Error);
    }

    private static SkyrimWinningArmorAddonSnapshotEvidenceScanResult Scan(
        params SkyrimWinningArmorAddonSnapshotPathEvidence[] paths)
    {
        return Scan(
            searchComplete:
                true,
            paths
        );
    }

    private static SkyrimWinningArmorAddonSnapshotEvidenceScanResult Scan(
        bool searchComplete,
        params SkyrimWinningArmorAddonSnapshotPathEvidence[] paths)
    {
        SkyrimWinningArmorAddonInventoryResult inventory =
            new(
                DataRoot:
                    "/fixture/Data",
                RuntimeActivePluginCount:
                    searchComplete
                        ? 1
                        : 2,
                PluginsOpened:
                    1,
                MissingPluginFiles:
                    searchComplete
                        ? Array.Empty<string>()
                        : new[]
                        {
                            "Missing.esp"
                        },
                ReadErrors:
                    Array.Empty<
                        SkyrimPluginReadError
                    >(),
                Winners:
                    Array.Empty<
                        SkyrimWinningArmorAddonRecord
                    >()
            );

        return new SkyrimWinningArmorAddonSnapshotEvidenceScanResult(
            Inventory:
                inventory,
            Paths:
                paths
        );
    }

    private static SkyrimWinningArmorAddonSnapshotPathEvidence Path(
        string requestedPath,
        params string[] consumerRequestedPaths)
    {
        bool valid =
            WindowsDataRelativePathParser.TryParse(
                requestedPath,
                out string[] components,
                out _
            );

        SkyrimWinningArmorAddonSnapshotReferenceContext[] references =
            consumerRequestedPaths
                .Select(
                    (consumerRequestedPath, index) =>
                        new SkyrimWinningArmorAddonSnapshotReferenceContext(
                            WinningPluginName:
                                $"Winner{index}.esp",
                            WinningLoadOrderIndex:
                                index,
                            Reference:
                                new SkyrimArmorAddonModelReference(
                                    FormKey:
                                        $"Armor{index}",
                                    EditorId:
                                        $"Armor{index}Editor",
                                    Field:
                                        "WorldModel.Male",
                                    GivenPath:
                                        consumerRequestedPath,
                                    DataRelativePath:
                                        consumerRequestedPath
                                )
                        )
                )
                .ToArray();

        return new SkyrimWinningArmorAddonSnapshotPathEvidence(
            RequestedPath:
                requestedPath,
            References:
                references,
            RequestedRootLogicalPath:
                valid
                    ? WindowsLogicalPath.FromRelativePath(
                        components[0]
                    )
                    : null,
            State:
                valid
                    ? SkyrimArmorAddonSnapshotLookupEvidenceState
                        .NoMatchingNamespaceAnalysis
                    : SkyrimArmorAddonSnapshotLookupEvidenceState
                        .InvalidRequestedPath,
            MatchingAnalysisCount:
                0,
            SelectedAnalysis:
                null,
            Lookup:
                null,
            Error:
                valid
                    ? "fixture: no matching analysis"
                    : "fixture: invalid requested path"
        );
    }
}
