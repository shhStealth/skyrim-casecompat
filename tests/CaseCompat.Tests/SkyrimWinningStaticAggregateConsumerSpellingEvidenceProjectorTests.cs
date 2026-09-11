using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Repair;

namespace CaseCompat.Tests;

public sealed class
    SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjectorTests
{
    [Fact]
    public void Project_CompleteSingleConsumerPublishesUniqueSpelling()
    {
        const string requestedPath =
            "meshes/Static/Common/Chair01.nif";

        SkyrimWinningStaticInventoryResult inventory =
            Inventory(
                Winner(
                    "FurnA",
                    "FurnAEditor",
                    requestedPath
                )
            );

        SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjector
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
            SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjectionState
                .Complete,
            result.State
        );

        DataRelativePathAggregateConsumerSpellingEvidence evidence =
            Assert.Single(
                result.Evidence
            );

        Assert.Equal(
            "MESHES/STATIC/COMMON/CHAIR01.NIF",
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
            "meshes/Static/Common/Chair01.nif";

        const string second =
            "meshes/static/common/Chair01.nif";

        SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjector
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
        SkyrimWinningStaticInventoryResult inventory =
            Inventory(
                searchComplete:
                    false,
                Winner(
                    "FurnA",
                    "FurnAEditor",
                    "Meshes//Broken.nif"
                )
            );

        SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        inventory
                    );

        Assert.False(result.WinnerSearchComplete);
        Assert.False(result.ConsumerPathEvidenceComplete);

        Assert.Equal(
            SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjectionState
                .IncompleteWinnerSearch,
            result.State
        );

        Assert.Empty(result.Evidence);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Project_CompleteWinnerSearchWithInvalidPathIsIndeterminate()
    {
        SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjector
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
            SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjectionState
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
            new SkyrimStaticModelReference(
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
            new SkyrimWinningStaticRecord(
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

        SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        Inventory(
                            winner
                        )
                    );

        Assert.Equal(
            SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjectionState
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
            new SkyrimWinningStaticRecord(
                FormKey:
                    "FurnA",
                EditorId:
                    "FurnAEditor",
                WinningPluginName:
                    "Winner.esp",
                WinningLoadOrderIndex:
                    1,
                ModelReferences:
                    new SkyrimStaticModelReference[]
                    {
                        null!
                    }
            );

        SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        Inventory(
                            winner
                        )
                    );

        Assert.Equal(
            SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjectionState
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
        SkyrimWinningStaticInventoryResult inventory =
            Inventory();

        SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjectionResult
            result =
                SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        inventory
                    );

        Assert.True(result.WinnerSearchComplete);
        Assert.True(result.ConsumerPathEvidenceComplete);

        Assert.Equal(
            SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjectionState
                .Complete,
            result.State
        );

        Assert.Empty(result.Evidence);
        Assert.Equal(0, result.EvidenceCount);
        Assert.Null(result.Error);
    }

    private static SkyrimWinningStaticInventoryResult Inventory(
        params SkyrimWinningStaticRecord[] winners)
    {
        return Inventory(
            searchComplete:
                true,
            winners
        );
    }

    private static SkyrimWinningStaticInventoryResult Inventory(
        bool searchComplete,
        params SkyrimWinningStaticRecord[] winners)
    {
        return new SkyrimWinningStaticInventoryResult(
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

    private static SkyrimWinningStaticRecord Winner(
        string formKey,
        string? editorId,
        params string[] requestedPaths)
    {
        SkyrimStaticModelReference[] references =
            requestedPaths
                .Select(
                    requestedPath =>
                        new SkyrimStaticModelReference(
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

        return new SkyrimWinningStaticRecord(
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
