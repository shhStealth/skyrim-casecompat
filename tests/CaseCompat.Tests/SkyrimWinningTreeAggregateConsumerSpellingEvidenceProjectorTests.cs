using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Repair;

namespace CaseCompat.Tests;

public sealed class
    SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjectorTests
{
    [Fact]
    public void Project_CompleteSingleConsumerPublishesUniqueSpelling()
    {
        const string requestedPath =
            "meshes/Tree/Common/Chair01.nif";

        SkyrimWinningTreeInventoryResult inventory =
            Inventory(
                Winner(
                    "FurnA",
                    "FurnAEditor",
                    requestedPath
                )
            );

        SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjector
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
            SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjectionState
                .Complete,
            result.State
        );

        DataRelativePathAggregateConsumerSpellingEvidence evidence =
            Assert.Single(
                result.Evidence
            );

        Assert.Equal(
            "MESHES/TREE/COMMON/CHAIR01.NIF",
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
            "meshes/Tree/Common/Chair01.nif";

        const string second =
            "meshes/tree/common/Chair01.nif";

        SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjector
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
        SkyrimWinningTreeInventoryResult inventory =
            Inventory(
                searchComplete:
                    false,
                Winner(
                    "FurnA",
                    "FurnAEditor",
                    "Meshes//Broken.nif"
                )
            );

        SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        inventory
                    );

        Assert.False(result.WinnerSearchComplete);
        Assert.False(result.ConsumerPathEvidenceComplete);

        Assert.Equal(
            SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjectionState
                .IncompleteWinnerSearch,
            result.State
        );

        Assert.Empty(result.Evidence);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Project_CompleteWinnerSearchWithInvalidPathIsIndeterminate()
    {
        SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjector
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
            SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjectionState
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
            new SkyrimTreeModelReference(
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
            new SkyrimWinningTreeRecord(
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

        SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        Inventory(
                            winner
                        )
                    );

        Assert.Equal(
            SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjectionState
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
            new SkyrimWinningTreeRecord(
                FormKey:
                    "FurnA",
                EditorId:
                    "FurnAEditor",
                WinningPluginName:
                    "Winner.esp",
                WinningLoadOrderIndex:
                    1,
                ModelReferences:
                    new SkyrimTreeModelReference[]
                    {
                        null!
                    }
            );

        SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        Inventory(
                            winner
                        )
                    );

        Assert.Equal(
            SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjectionState
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
        SkyrimWinningTreeInventoryResult inventory =
            Inventory();

        SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        inventory
                    );

        Assert.True(result.WinnerSearchComplete);
        Assert.True(result.ConsumerPathEvidenceComplete);

        Assert.Equal(
            SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjectionState
                .Complete,
            result.State
        );

        Assert.Empty(result.Evidence);
        Assert.Equal(0, result.EvidenceCount);
        Assert.Null(result.Error);
    }

    private static SkyrimWinningTreeInventoryResult Inventory(
        params SkyrimWinningTreeRecord[] winners)
    {
        return Inventory(
            searchComplete:
                true,
            winners
        );
    }

    private static SkyrimWinningTreeInventoryResult Inventory(
        bool searchComplete,
        params SkyrimWinningTreeRecord[] winners)
    {
        return new SkyrimWinningTreeInventoryResult(
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

    private static SkyrimWinningTreeRecord Winner(
        string formKey,
        string? editorId,
        params string[] requestedPaths)
    {
        SkyrimTreeModelReference[] references =
            requestedPaths
                .Select(
                    requestedPath =>
                        new SkyrimTreeModelReference(
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

        return new SkyrimWinningTreeRecord(
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
