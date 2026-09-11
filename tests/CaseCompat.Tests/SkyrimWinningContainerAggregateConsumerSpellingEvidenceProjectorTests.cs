using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Repair;

namespace CaseCompat.Tests;

public sealed class
    SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectorTests
{
    [Fact]
    public void Project_CompleteSingleConsumerPublishesUniqueSpelling()
    {
        const string requestedPath =
            "meshes/Container/Common/Chair01.nif";

        SkyrimWinningContainerInventoryResult inventory =
            Inventory(
                Winner(
                    "FurnA",
                    "FurnAEditor",
                    requestedPath
                )
            );

        SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjector
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
            SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionState
                .Complete,
            result.State
        );

        DataRelativePathAggregateConsumerSpellingEvidence evidence =
            Assert.Single(
                result.Evidence
            );

        Assert.Equal(
            "MESHES/CONTAINER/COMMON/CHAIR01.NIF",
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
            "meshes/Container/Common/Chair01.nif";

        const string second =
            "meshes/container/common/Chair01.nif";

        SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjector
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
        SkyrimWinningContainerInventoryResult inventory =
            Inventory(
                searchComplete:
                    false,
                Winner(
                    "FurnA",
                    "FurnAEditor",
                    "Meshes//Broken.nif"
                )
            );

        SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        inventory
                    );

        Assert.False(result.WinnerSearchComplete);
        Assert.False(result.ConsumerPathEvidenceComplete);

        Assert.Equal(
            SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionState
                .IncompleteWinnerSearch,
            result.State
        );

        Assert.Empty(result.Evidence);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Project_CompleteWinnerSearchWithInvalidPathIsIndeterminate()
    {
        SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjector
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
            SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionState
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
            new SkyrimContainerModelReference(
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
            new SkyrimWinningContainerRecord(
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

        SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        Inventory(
                            winner
                        )
                    );

        Assert.Equal(
            SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionState
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
            new SkyrimWinningContainerRecord(
                FormKey:
                    "FurnA",
                EditorId:
                    "FurnAEditor",
                WinningPluginName:
                    "Winner.esp",
                WinningLoadOrderIndex:
                    1,
                ModelReferences:
                    new SkyrimContainerModelReference[]
                    {
                        null!
                    }
            );

        SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        Inventory(
                            winner
                        )
                    );

        Assert.Equal(
            SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionState
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
        SkyrimWinningContainerInventoryResult inventory =
            Inventory();

        SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        inventory
                    );

        Assert.True(result.WinnerSearchComplete);
        Assert.True(result.ConsumerPathEvidenceComplete);

        Assert.Equal(
            SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionState
                .Complete,
            result.State
        );

        Assert.Empty(result.Evidence);
        Assert.Equal(0, result.EvidenceCount);
        Assert.Null(result.Error);
    }

    private static SkyrimWinningContainerInventoryResult Inventory(
        params SkyrimWinningContainerRecord[] winners)
    {
        return Inventory(
            searchComplete:
                true,
            winners
        );
    }

    private static SkyrimWinningContainerInventoryResult Inventory(
        bool searchComplete,
        params SkyrimWinningContainerRecord[] winners)
    {
        return new SkyrimWinningContainerInventoryResult(
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

    private static SkyrimWinningContainerRecord Winner(
        string formKey,
        string? editorId,
        params string[] requestedPaths)
    {
        SkyrimContainerModelReference[] references =
            requestedPaths
                .Select(
                    requestedPath =>
                        new SkyrimContainerModelReference(
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

        return new SkyrimWinningContainerRecord(
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
