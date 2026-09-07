using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Repair;
using Mutagen.Bethesda.Skyrim;

namespace CaseCompat.Tests;

public sealed class
    SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectorTests
{
    [Fact]
    public void Project_CompleteSingleConsumerPublishesUniqueSpelling()
    {
        const string requestedPath =
            "meshes/Actors/Character/Character Assets/FaceParts/" +
            "MaleHeadBrows.tri";

        SkyrimWinningHeadPartInventoryResult inventory =
            Inventory(
                Winner(
                    "HeadA",
                    "HeadAEditor",
                    requestedPath
                )
            );

        SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        inventory
                    );

        Assert.Same(
            inventory,
            result.Inventory
        );

        Assert.True(result.WinnerSearchComplete);
        Assert.True(result.ConsumerPathEvidenceComplete);
        Assert.Null(result.Error);

        Assert.Equal(
            SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionState
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
            "Meshes/Actors/Character/Character Assets/FaceParts/" +
            "FemaleHeadCharGen.tri";

        SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        Inventory(
                            Winner(
                                "HeadA",
                                "HeadAEditor",
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

        SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        Inventory(
                            Winner(
                                "HeadA",
                                "HeadAEditor",
                                first
                            ),
                            Winner(
                                "HeadB",
                                "HeadBEditor",
                                second
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
        SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        Inventory(
                            Winner(
                                "HeadZ",
                                "HeadZEditor",
                                "textures/Z.dds"
                            ),
                            Winner(
                                "HeadA",
                                "HeadAEditor",
                                "Meshes/A.tri"
                            )
                        )
                    );

        Assert.Equal(
            new[]
            {
                "MESHES/A.TRI",
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
        SkyrimWinningHeadPartInventoryResult inventory =
            Inventory(
                searchComplete:
                    false,
                Winner(
                    "HeadA",
                    "HeadAEditor",
                    "Meshes//Broken.tri"
                )
            );

        SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        inventory
                    );

        Assert.False(result.WinnerSearchComplete);
        Assert.False(result.ConsumerPathEvidenceComplete);

        Assert.Equal(
            SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionState
                .IncompleteWinnerSearch,
            result.State
        );

        Assert.Empty(result.Evidence);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Project_CompleteWinnerSearchWithInvalidPathIsIndeterminate()
    {
        SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        Inventory(
                            Winner(
                                "HeadA",
                                "HeadAEditor",
                                "Meshes//Broken.tri"
                            )
                        )
                    );

        Assert.True(result.WinnerSearchComplete);
        Assert.False(result.ConsumerPathEvidenceComplete);

        Assert.Equal(
            SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionState
                .IndeterminateConsumerPathEvidence,
            result.State
        );

        Assert.Empty(result.Evidence);
        Assert.NotNull(result.Error);

        Assert.Contains(
            "invalid consumer requested path",
            result.Error!.ToLowerInvariant()
        );
    }

    [Fact]
    public void Project_WinnerAndReferenceProvenanceMismatchIsIndeterminate()
    {
        var mismatchedReference =
            new SkyrimHeadPartPartReference(
                FormKey:
                    "DifferentHead",
                EditorId:
                    "HeadAEditor",
                PartIndex:
                    0,
                PartType:
                    Part.PartTypeEnum.Tri,
                GivenPath:
                    "Meshes/Face.tri",
                DataRelativePath:
                    "Meshes/Face.tri"
            );

        var winner =
            new SkyrimWinningHeadPartRecord(
                FormKey:
                    "HeadA",
                EditorId:
                    "HeadAEditor",
                WinningPluginName:
                    "Winner.esp",
                WinningLoadOrderIndex:
                    1,
                PartReferences:
                    new[]
                    {
                        mismatchedReference
                    }
            );

        SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        Inventory(
                            winner
                        )
                    );

        Assert.Equal(
            SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionState
                .IndeterminateConsumerPathEvidence,
            result.State
        );

        Assert.False(result.ConsumerPathEvidenceComplete);
        Assert.Empty(result.Evidence);
        Assert.NotNull(result.Error);

        Assert.Contains(
            "formkey",
            result.Error!.ToLowerInvariant()
        );
    }

    [Fact]
    public void Project_NullPartReferenceIsIndeterminate()
    {
        var winner =
            new SkyrimWinningHeadPartRecord(
                FormKey:
                    "HeadA",
                EditorId:
                    "HeadAEditor",
                WinningPluginName:
                    "Winner.esp",
                WinningLoadOrderIndex:
                    1,
                PartReferences:
                    new SkyrimHeadPartPartReference[]
                    {
                        null!
                    }
            );

        SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        Inventory(
                            winner
                        )
                    );

        Assert.Equal(
            SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionState
                .IndeterminateConsumerPathEvidence,
            result.State
        );

        Assert.False(result.ConsumerPathEvidenceComplete);
        Assert.Empty(result.Evidence);
        Assert.NotNull(result.Error);

        Assert.Contains(
            "null part reference",
            result.Error!.ToLowerInvariant()
        );
    }

    [Fact]
    public void Project_CompleteEmptyInventoryProducesCompleteEmptyEvidence()
    {
        SkyrimWinningHeadPartInventoryResult inventory =
            Inventory();

        SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        inventory
                    );

        Assert.True(result.WinnerSearchComplete);
        Assert.True(result.ConsumerPathEvidenceComplete);

        Assert.Equal(
            SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionState
                .Complete,
            result.State
        );

        Assert.Empty(result.Evidence);
        Assert.Equal(0, result.EvidenceCount);
        Assert.Null(result.Error);
    }

    private static SkyrimWinningHeadPartInventoryResult Inventory(
        params SkyrimWinningHeadPartRecord[] winners)
    {
        return Inventory(
            searchComplete:
                true,
            winners
        );
    }

    private static SkyrimWinningHeadPartInventoryResult Inventory(
        bool searchComplete,
        params SkyrimWinningHeadPartRecord[] winners)
    {
        return new SkyrimWinningHeadPartInventoryResult(
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
                winners
        );
    }

    private static SkyrimWinningHeadPartRecord Winner(
        string formKey,
        string? editorId,
        params string[] requestedPaths)
    {
        SkyrimHeadPartPartReference[] references =
            requestedPaths
                .Select(
                    (requestedPath, index) =>
                        new SkyrimHeadPartPartReference(
                            FormKey:
                                formKey,
                            EditorId:
                                editorId,
                            PartIndex:
                                index,
                            PartType:
                                Part.PartTypeEnum.Tri,
                            GivenPath:
                                requestedPath,
                            DataRelativePath:
                                requestedPath
                        )
                )
                .ToArray();

        return new SkyrimWinningHeadPartRecord(
            FormKey:
                formKey,
            EditorId:
                editorId,
            WinningPluginName:
                "Winner.esp",
            WinningLoadOrderIndex:
                1,
            PartReferences:
                references
        );
    }
}
