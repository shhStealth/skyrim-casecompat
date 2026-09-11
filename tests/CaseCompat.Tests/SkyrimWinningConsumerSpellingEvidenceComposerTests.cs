using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Repair;

namespace CaseCompat.Tests;

public sealed class SkyrimWinningConsumerSpellingEvidenceComposerTests
{
    [Fact]
    public void Compose_CompleteEmptySourcesProducesCompleteEmptyEvidence()
    {
        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult
            armorAddon =
                ArmorAddon(
                    winnerSearchComplete:
                        true,
                    state:
                        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionState
                            .Complete,
                    evidence:
                        Array.Empty<
                            DataRelativePathAggregateConsumerSpellingEvidence
                        >()
                );

        SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult
            headPart =
                HeadPart(
                    winnerSearchComplete:
                        true,
                    state:
                        SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionState
                            .Complete,
                    evidence:
                        Array.Empty<
                            DataRelativePathAggregateConsumerSpellingEvidence
                        >()
                );

        SkyrimWinningConsumerSpellingEvidenceCompositionResult result =
            SkyrimWinningConsumerSpellingEvidenceComposer.Compose(
                armorAddon,
                headPart
            );

        Assert.Same(
            armorAddon,
            result.ArmorAddonProjection
        );

        Assert.Same(
            headPart,
            result.HeadPartProjection
        );

        Assert.True(result.WinnerSearchComplete);
        Assert.True(result.ConsumerPathEvidenceComplete);

        Assert.Equal(
            SkyrimWinningConsumerSpellingEvidenceCompositionState.Complete,
            result.State
        );

        Assert.Empty(result.Evidence);
        Assert.Equal(0, result.EvidenceCount);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Compose_CompleteSourcesUnionDifferentLogicalLeavesInOrdinalOrder()
    {
        SkyrimWinningConsumerSpellingEvidenceCompositionResult result =
            SkyrimWinningConsumerSpellingEvidenceComposer.Compose(
                CompleteArmorAddon(
                    Consumer(
                        "TEXTURES/Z.DDS",
                        "Textures/Z.dds"
                    )
                ),
                CompleteHeadPart(
                    Consumer(
                        "MESHES/A.TRI",
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
                    evidence =>
                        evidence.WindowsLogicalPath
                )
                .ToArray()
        );
    }

    [Fact]
    public void Compose_CompleteSourcesSameExactSpellingRemainsUnique()
    {
        const string logicalPath =
            "MESHES/ACTORS/CHARACTER/CHARACTER ASSETS/FACEPARTS/" +
            "MALEHEADBROWS.TRI";

        const string requestedPath =
            "Meshes/Actors/Character/Character Assets/FaceParts/" +
            "MaleHeadBrows.tri";

        SkyrimWinningConsumerSpellingEvidenceCompositionResult result =
            SkyrimWinningConsumerSpellingEvidenceComposer.Compose(
                CompleteArmorAddon(
                    Consumer(
                        logicalPath,
                        requestedPath
                    )
                ),
                CompleteHeadPart(
                    Consumer(
                        logicalPath,
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
            requestedPath,
            evidence.AuthoritativeRequestedPath
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
    public void Compose_CompleteSourcesCaseDistinctSpellingsConflict()
    {
        const string logicalPath =
            "MESHES/ACTORS/CHARACTER/CHARACTER ASSETS/FACEPARTS/" +
            "MALEHEADBROWS.TRI";

        const string armorAddonSpelling =
            "Meshes/Actors/Character/Character Assets/FaceParts/" +
            "MaleHeadbrows.tri";

        const string headPartSpelling =
            "Meshes/Actors/Character/Character Assets/FaceParts/" +
            "MaleHeadBrows.tri";

        SkyrimWinningConsumerSpellingEvidenceCompositionResult result =
            SkyrimWinningConsumerSpellingEvidenceComposer.Compose(
                CompleteArmorAddon(
                    Consumer(
                        logicalPath,
                        armorAddonSpelling
                    )
                ),
                CompleteHeadPart(
                    Consumer(
                        logicalPath,
                        headPartSpelling
                    )
                )
            );

        Assert.True(result.WinnerSearchComplete);
        Assert.True(result.ConsumerPathEvidenceComplete);

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
                armorAddonSpelling,
                headPartSpelling
            }
                .OrderBy(
                    path =>
                        path,
                    StringComparer.Ordinal
                )
                .ToArray(),
            evidence.DistinctRequestedPaths
        );
    }

    [Fact]
    public void Compose_ArmorAddonIncompleteDominatesWithoutInspectingEvidence()
    {
        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult
            armorAddon =
                ArmorAddon(
                    winnerSearchComplete:
                        false,
                    state:
                        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionState
                            .IncompleteWinnerSearch,
                    evidence:
                        null!
                );

        SkyrimWinningConsumerSpellingEvidenceCompositionResult result =
            SkyrimWinningConsumerSpellingEvidenceComposer.Compose(
                armorAddon,
                CompleteHeadPart(
                    Consumer(
                        "MESHES/A.TRI",
                        "Meshes/A.tri"
                    )
                )
            );

        Assert.False(result.WinnerSearchComplete);
        Assert.False(result.ConsumerPathEvidenceComplete);

        Assert.Equal(
            SkyrimWinningConsumerSpellingEvidenceCompositionState
                .IncompleteWinnerSearch,
            result.State
        );

        Assert.Empty(result.Evidence);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Compose_IncompleteWinnerSearchDominatesOtherSourceIndeterminate()
    {
        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult
            armorAddon =
                ArmorAddon(
                    winnerSearchComplete:
                        true,
                    state:
                        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionState
                            .IndeterminateConsumerPathEvidence,
                    evidence:
                        Array.Empty<
                            DataRelativePathAggregateConsumerSpellingEvidence
                        >(),
                    error:
                        "ArmorAddon consumer evidence is indeterminate."
                );

        SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult
            headPart =
                HeadPart(
                    winnerSearchComplete:
                        false,
                    state:
                        SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionState
                            .IncompleteWinnerSearch,
                    evidence:
                        null!
                );

        SkyrimWinningConsumerSpellingEvidenceCompositionResult result =
            SkyrimWinningConsumerSpellingEvidenceComposer.Compose(
                armorAddon,
                headPart
            );

        Assert.Equal(
            SkyrimWinningConsumerSpellingEvidenceCompositionState
                .IncompleteWinnerSearch,
            result.State
        );

        Assert.False(result.WinnerSearchComplete);
        Assert.False(result.ConsumerPathEvidenceComplete);
        Assert.Empty(result.Evidence);
        Assert.Null(result.Error);

        Assert.Same(
            armorAddon,
            result.ArmorAddonProjection
        );

        Assert.Same(
            headPart,
            result.HeadPartProjection
        );
    }

    [Fact]
    public void Compose_CompleteWinnerSearchWithIndeterminateSourceIsIndeterminate()
    {
        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult
            armorAddon =
                ArmorAddon(
                    winnerSearchComplete:
                        true,
                    state:
                        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionState
                            .IndeterminateConsumerPathEvidence,
                    evidence:
                        null!,
                    error:
                        "Malformed ArmorAddon consumer evidence."
                );

        SkyrimWinningConsumerSpellingEvidenceCompositionResult result =
            SkyrimWinningConsumerSpellingEvidenceComposer.Compose(
                armorAddon,
                CompleteHeadPart(
                    Consumer(
                        "MESHES/A.TRI",
                        "Meshes/A.tri"
                    )
                )
            );

        Assert.True(result.WinnerSearchComplete);
        Assert.False(result.ConsumerPathEvidenceComplete);

        Assert.Equal(
            SkyrimWinningConsumerSpellingEvidenceCompositionState
                .IndeterminateConsumerPathEvidence,
            result.State
        );

        Assert.Empty(result.Evidence);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Compose_CompleteSourcesWithNonCanonicalEvidenceIsIndeterminate()
    {
        var malformed =
            new DataRelativePathAggregateConsumerSpellingEvidence(
                WindowsLogicalPath:
                    "MESHES/A.TRI",
                DistinctRequestedPaths:
                    new[]
                    {
                        "Meshes/A.tri"
                    },
                State:
                    DataRelativePathAggregateConsumerSpellingState
                        .ConflictingConsumerSpellings
            );

        SkyrimWinningConsumerSpellingEvidenceCompositionResult result =
            SkyrimWinningConsumerSpellingEvidenceComposer.Compose(
                CompleteArmorAddon(
                    malformed
                ),
                CompleteHeadPart()
            );

        Assert.True(result.WinnerSearchComplete);
        Assert.False(result.ConsumerPathEvidenceComplete);

        Assert.Equal(
            SkyrimWinningConsumerSpellingEvidenceCompositionState
                .IndeterminateConsumerPathEvidence,
            result.State
        );

        Assert.Empty(result.Evidence);
        Assert.NotNull(result.Error);

        Assert.Contains(
            "rejected",
            result.Error!.ToLowerInvariant()
        );
    }

    [Fact]
    public void Compose_NullProjectionIsRejected()
    {
        SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult
            headPart =
                CompleteHeadPart();

        Assert.Throws<ArgumentNullException>(
            () =>
                SkyrimWinningConsumerSpellingEvidenceComposer.Compose(
                    null!,
                    headPart
                )
        );

        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult
            armorAddon =
                CompleteArmorAddon();

        Assert.Throws<ArgumentNullException>(
            () =>
                SkyrimWinningConsumerSpellingEvidenceComposer.Compose(
                    armorAddon,
                    null!
                )
        );
    }

    private static
        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult
        CompleteArmorAddon(
            params DataRelativePathAggregateConsumerSpellingEvidence[] evidence)
    {
        return ArmorAddon(
            winnerSearchComplete:
                true,
            state:
                SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionState
                    .Complete,
            evidence:
                evidence
        );
    }

    private static
        SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult
        CompleteHeadPart(
            params DataRelativePathAggregateConsumerSpellingEvidence[] evidence)
    {
        return HeadPart(
            winnerSearchComplete:
                true,
            state:
                SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionState
                    .Complete,
            evidence:
                evidence
        );
    }

    private static
        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult
        ArmorAddon(
            bool winnerSearchComplete,
            SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionState
                state,
            IReadOnlyList<DataRelativePathAggregateConsumerSpellingEvidence>
                evidence,
            string? error = null)
    {
        SkyrimWinningArmorAddonInventoryResult inventory =
            new(
                DataRoot:
                    "/fixture/Data",
                RuntimeActivePluginCount:
                    winnerSearchComplete
                        ? 1
                        : 2,
                PluginsOpened:
                    1,
                MissingPluginFiles:
                    winnerSearchComplete
                        ? Array.Empty<string>()
                        : new[]
                        {
                            "MissingArmorAddon.esp"
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

        var scan =
            new SkyrimWinningArmorAddonSnapshotEvidenceScanResult(
                Inventory:
                    inventory,
                Paths:
                    Array.Empty<
                        SkyrimWinningArmorAddonSnapshotPathEvidence
                    >()
            );

        return new
            SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult(
                Scan:
                    scan,
                State:
                    state,
                Evidence:
                    evidence,
                Error:
                    error
            );
    }

    private static
        SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult
        HeadPart(
            bool winnerSearchComplete,
            SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionState
                state,
            IReadOnlyList<DataRelativePathAggregateConsumerSpellingEvidence>
                evidence,
            string? error = null)
    {
        SkyrimWinningHeadPartInventoryResult inventory =
            new(
                DataRoot:
                    "/fixture/Data",
                RuntimeActivePluginCount:
                    winnerSearchComplete
                        ? 1
                        : 2,
                PluginsOpened:
                    1,
                MissingPluginFiles:
                    winnerSearchComplete
                        ? Array.Empty<string>()
                        : new[]
                        {
                            "MissingHeadPart.esp"
                        },
                ReadErrors:
                    Array.Empty<
                        SkyrimPluginReadError
                    >(),
                Winners:
                    Array.Empty<
                        SkyrimWinningHeadPartRecord
                    >()
            );

        return new
            SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult(
                Inventory:
                    inventory,
                State:
                    state,
                Evidence:
                    evidence,
                Error:
                    error
            );
    }

    private static
        DataRelativePathAggregateConsumerSpellingEvidence
        Consumer(
            string logicalPath,
            params string[] requestedPaths)
    {
        return DataRelativePathAggregateConsumerSpellingClassifier.Classify(
            logicalPath,
            requestedPaths
        );
    }

    private static
        SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjectionResult
        CompleteFurniture(
            params DataRelativePathAggregateConsumerSpellingEvidence[] evidence)
    {
        SkyrimWinningFurnitureInventoryResult inventory =
            new(
                DataRoot:
                    "/fixture/Data",
                RuntimeActivePluginCount:
                    1,
                PluginsOpened:
                    1,
                MissingPluginFiles:
                    Array.Empty<string>(),
                ReadErrors:
                    Array.Empty<SkyrimPluginReadError>(),
                Winners:
                    Array.Empty<SkyrimWinningFurnitureRecord>()
            );

        return new
            SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjectionResult(
                Inventory:
                    inventory,
                State:
                    SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjectionState
                        .Complete,
                Evidence:
                    evidence,
                Error:
                    null
            );
    }

    private static
        SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjectionResult
        CompleteStatic(
            params DataRelativePathAggregateConsumerSpellingEvidence[] evidence)
    {
        SkyrimWinningStaticInventoryResult inventory =
            new(
                DataRoot:
                    "/fixture/Data",
                RuntimeActivePluginCount:
                    1,
                PluginsOpened:
                    1,
                MissingPluginFiles:
                    Array.Empty<string>(),
                ReadErrors:
                    Array.Empty<SkyrimPluginReadError>(),
                Winners:
                    Array.Empty<SkyrimWinningStaticRecord>()
            );

        return new
            SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjectionResult(
                Inventory:
                    inventory,
                State:
                    SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjectionState
                        .Complete,
                Evidence:
                    evidence,
                Error:
                    null
            );
    }

    private static
        SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionResult
        CompleteContainer(
            params DataRelativePathAggregateConsumerSpellingEvidence[] evidence)
    {
        SkyrimWinningContainerInventoryResult inventory =
            new(
                DataRoot:
                    "/fixture/Data",
                RuntimeActivePluginCount:
                    1,
                PluginsOpened:
                    1,
                MissingPluginFiles:
                    Array.Empty<string>(),
                ReadErrors:
                    Array.Empty<SkyrimPluginReadError>(),
                Winners:
                    Array.Empty<SkyrimWinningContainerRecord>()
            );

        return new
            SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionResult(
                Inventory:
                    inventory,
                State:
                    SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionState
                        .Complete,
                Evidence:
                    evidence,
                Error:
                    null
            );
    }

    private static
        SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjectionResult
        CompleteTree(
            params DataRelativePathAggregateConsumerSpellingEvidence[] evidence)
    {
        SkyrimWinningTreeInventoryResult inventory =
            new(
                DataRoot:
                    "/fixture/Data",
                RuntimeActivePluginCount:
                    1,
                PluginsOpened:
                    1,
                MissingPluginFiles:
                    Array.Empty<string>(),
                ReadErrors:
                    Array.Empty<SkyrimPluginReadError>(),
                Winners:
                    Array.Empty<SkyrimWinningTreeRecord>()
            );

        return new
            SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjectionResult(
                Inventory:
                    inventory,
                State:
                    SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjectionState
                        .Complete,
                Evidence:
                    evidence,
                Error:
                    null
            );
    }

    [Fact]
    public void Compose_SixSourcesUnionDifferentLogicalLeavesInOrdinalOrder()
    {
        SkyrimWinningConsumerSpellingEvidenceCompositionResult result =
            SkyrimWinningConsumerSpellingEvidenceComposer.Compose(
                CompleteArmorAddon(
                    Consumer(
                        "MESHES/ARMORADDON.NIF",
                        "Meshes/ArmorAddon.nif"
                    )
                ),
                CompleteHeadPart(
                    Consumer(
                        "MESHES/HEADPART.TRI",
                        "Meshes/HeadPart.tri"
                    )
                ),
                CompleteFurniture(
                    Consumer(
                        "MESHES/FURNITURE.NIF",
                        "Meshes/Furniture.nif"
                    )
                ),
                CompleteStatic(
                    Consumer(
                        "MESHES/STATIC.NIF",
                        "Meshes/Static.nif"
                    )
                ),
                CompleteContainer(
                    Consumer(
                        "MESHES/CONTAINER.NIF",
                        "Meshes/Container.nif"
                    )
                ),
                CompleteTree(
                    Consumer(
                        "MESHES/TREE.NIF",
                        "Meshes/Tree.nif"
                    )
                )
            );

        Assert.True(result.WinnerSearchComplete);

        Assert.Equal(
            SkyrimWinningConsumerSpellingEvidenceCompositionState.Complete,
            result.State
        );

        Assert.Equal(
            new[]
            {
                "MESHES/ARMORADDON.NIF",
                "MESHES/CONTAINER.NIF",
                "MESHES/FURNITURE.NIF",
                "MESHES/HEADPART.TRI",
                "MESHES/STATIC.NIF",
                "MESHES/TREE.NIF"
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
    public void Compose_AnySingleIncompleteSourceAmongSixDominates()
    {
        SkyrimWinningConsumerSpellingEvidenceCompositionResult result =
            SkyrimWinningConsumerSpellingEvidenceComposer.Compose(
                CompleteArmorAddon(),
                CompleteHeadPart(),
                CompleteFurniture(),
                CompleteStatic(),
                Container(
                    winnerSearchComplete:
                        false,
                    state:
                        SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionState
                            .IncompleteWinnerSearch,
                    evidence:
                        Array.Empty<
                            DataRelativePathAggregateConsumerSpellingEvidence
                        >()
                ),
                CompleteTree()
            );

        Assert.False(result.WinnerSearchComplete);

        Assert.Equal(
            SkyrimWinningConsumerSpellingEvidenceCompositionState
                .IncompleteWinnerSearch,
            result.State
        );

        Assert.Empty(result.Evidence);
    }

    private static
        SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionResult
        Container(
            bool winnerSearchComplete,
            SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionState
                state,
            IReadOnlyList<DataRelativePathAggregateConsumerSpellingEvidence>
                evidence,
            string? error = null)
    {
        SkyrimWinningContainerInventoryResult inventory =
            new(
                DataRoot:
                    "/fixture/Data",
                RuntimeActivePluginCount:
                    winnerSearchComplete
                        ? 1
                        : 2,
                PluginsOpened:
                    1,
                MissingPluginFiles:
                    winnerSearchComplete
                        ? Array.Empty<string>()
                        : new[]
                        {
                            "MissingContainer.esp"
                        },
                ReadErrors:
                    Array.Empty<SkyrimPluginReadError>(),
                Winners:
                    Array.Empty<SkyrimWinningContainerRecord>()
            );

        return new
            SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionResult(
                Inventory:
                    inventory,
                State:
                    state,
                Evidence:
                    evidence,
                Error:
                    error
            );
    }
}
