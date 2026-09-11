using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Repair;

namespace CaseCompat.Tests;

public sealed class
    SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjectorTests
{
    [Fact]
    public void Project_CompleteSingleConsumerPublishesUniqueSpelling()
    {
        const string requestedPath =
            "meshes/Furniture/Common/Chair01.nif";

        SkyrimWinningFurnitureInventoryResult inventory =
            Inventory(
                Winner(
                    "FurnA",
                    "FurnAEditor",
                    requestedPath
                )
            );

        SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjector
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
            SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjectionState
                .Complete,
            result.State
        );

        DataRelativePathAggregateConsumerSpellingEvidence evidence =
            Assert.Single(
                result.Evidence
            );

        Assert.Equal(
            "MESHES/FURNITURE/COMMON/CHAIR01.NIF",
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
    public void Project_CaseDistinctConsumersOnSameLogicalLeafConflict()
    {
        const string first =
            "meshes/Furniture/Common/Chair01.nif";

        const string second =
            "meshes/furniture/common/Chair01.nif";

        SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        Inventory(
                            Winner(
                                "FurnA",
                                "FurnAEditor",
                                first
                            ),
                            Winner(
                                "FurnB",
                                "FurnBEditor",
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
    public void Project_IncompleteWinnerSearchDominatesInvalidPath()
    {
        SkyrimWinningFurnitureInventoryResult inventory =
            Inventory(
                searchComplete:
                    false,
                Winner(
                    "FurnA",
                    "FurnAEditor",
                    "Meshes//Broken.nif"
                )
            );

        SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        inventory
                    );

        Assert.False(result.WinnerSearchComplete);
        Assert.False(result.ConsumerPathEvidenceComplete);

        Assert.Equal(
            SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjectionState
                .IncompleteWinnerSearch,
            result.State
        );

        Assert.Empty(result.Evidence);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Project_CompleteWinnerSearchWithInvalidPathIsIndeterminate()
    {
        SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        Inventory(
                            Winner(
                                "FurnA",
                                "FurnAEditor",
                                "Meshes//Broken.nif"
                            )
                        )
                    );

        Assert.True(result.WinnerSearchComplete);
        Assert.False(result.ConsumerPathEvidenceComplete);

        Assert.Equal(
            SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjectionState
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
            new SkyrimFurnitureModelReference(
                FormKey:
                    "DifferentFurn",
                EditorId:
                    "FurnAEditor",
                Field:
                    "Model",
                GivenPath:
                    "Meshes/Chair.nif",
                DataRelativePath:
                    "Meshes/Chair.nif"
            );

        var winner =
            new SkyrimWinningFurnitureRecord(
                FormKey:
                    "FurnA",
                EditorId:
                    "FurnAEditor",
                WinningPluginName:
                    "Winner.esp",
                WinningLoadOrderIndex:
                    1,
                ModelReferences:
                    new[]
                    {
                        mismatchedReference
                    }
            );

        SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        Inventory(
                            winner
                        )
                    );

        Assert.Equal(
            SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjectionState
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
    public void Project_NullModelReferenceIsIndeterminate()
    {
        var winner =
            new SkyrimWinningFurnitureRecord(
                FormKey:
                    "FurnA",
                EditorId:
                    "FurnAEditor",
                WinningPluginName:
                    "Winner.esp",
                WinningLoadOrderIndex:
                    1,
                ModelReferences:
                    new SkyrimFurnitureModelReference[]
                    {
                        null!
                    }
            );

        SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        Inventory(
                            winner
                        )
                    );

        Assert.Equal(
            SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjectionState
                .IndeterminateConsumerPathEvidence,
            result.State
        );

        Assert.False(result.ConsumerPathEvidenceComplete);
        Assert.Empty(result.Evidence);
        Assert.NotNull(result.Error);

        Assert.Contains(
            "null model reference",
            result.Error!.ToLowerInvariant()
        );
    }

    [Fact]
    public void Project_CompleteEmptyInventoryProducesCompleteEmptyEvidence()
    {
        SkyrimWinningFurnitureInventoryResult inventory =
            Inventory();

        SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        inventory
                    );

        Assert.True(result.WinnerSearchComplete);
        Assert.True(result.ConsumerPathEvidenceComplete);

        Assert.Equal(
            SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjectionState
                .Complete,
            result.State
        );

        Assert.Empty(result.Evidence);
        Assert.Equal(0, result.EvidenceCount);
        Assert.Null(result.Error);
    }

    private static SkyrimWinningFurnitureInventoryResult Inventory(
        params SkyrimWinningFurnitureRecord[] winners)
    {
        return Inventory(
            searchComplete:
                true,
            winners
        );
    }

    private static SkyrimWinningFurnitureInventoryResult Inventory(
        bool searchComplete,
        params SkyrimWinningFurnitureRecord[] winners)
    {
        return new SkyrimWinningFurnitureInventoryResult(
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

    private static SkyrimWinningFurnitureRecord Winner(
        string formKey,
        string? editorId,
        params string[] requestedPaths)
    {
        SkyrimFurnitureModelReference[] references =
            requestedPaths
                .Select(
                    requestedPath =>
                        new SkyrimFurnitureModelReference(
                            FormKey:
                                formKey,
                            EditorId:
                                editorId,
                            Field:
                                "Model",
                            GivenPath:
                                requestedPath,
                            DataRelativePath:
                                requestedPath
                        )
                )
                .ToArray();

        return new SkyrimWinningFurnitureRecord(
            FormKey:
                formKey,
            EditorId:
                editorId,
            WinningPluginName:
                "Winner.esp",
            WinningLoadOrderIndex:
                1,
            ModelReferences:
                references
        );
    }
}
