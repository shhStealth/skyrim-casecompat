using CaseCompat.Bethesda.Assets;
using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Analysis;
using CaseCompat.Core.LoadOrder;
using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

// Shared winning-consumer discovery orchestration for every
// targeted-consumer-* CLI command. Runs the full ArmorAddon + HeadPart +
// Furniture + Static + Container + Tree winning-consumer pipeline once and
// composes it into the
// SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult that
// SkyrimWinningTargetedConsumerCaseRepairCandidateProjector.Project
// requires - candidate/conflicting-spelling classification is inherently
// a whole-load-order aggregate, so there is no per-candidate shortcut.
internal static class TargetedConsumerDiscovery
{
    public static SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult
        Discover(
            string dataRoot,
            string pluginsPath,
            string loadOrderPath,
            string cccPath,
            string? aliasesDirectoryPath = null)
    {
        SkyrimRuntimeLoadOrder loadOrder =
            SkyrimRuntimeLoadOrderReader.Read(
                pluginsPath:
                    pluginsPath,
                loadOrderPath:
                    loadOrderPath
            );

        SkyrimRuntimePluginSet runtimePluginSet =
            SkyrimRuntimePluginSetReader.Read(
                loadOrder,
                cccPath
            );

        if (!runtimePluginSet.IsConsistent)
        {
            throw new InvalidOperationException(
                "The runtime plugin set is inconsistent."
            );
        }

        SkyrimWinningArmorAddonInventoryResult armorAddonInventory =
            SkyrimWinningArmorAddonInventory.Inspect(
                dataRoot:
                    dataRoot,
                runtimePluginSet:
                    runtimePluginSet
            );

        IReadOnlyList<WindowsNamespaceAnalysis> analyses =
            SkyrimWinningArmorAddonSnapshotNamespaceAnalysisProducer
                .Produce(
                    armorAddonInventory
                );

        SkyrimWinningArmorAddonSnapshotEvidenceScanResult scan =
            SkyrimWinningArmorAddonSnapshotEvidenceScanner.Inspect(
                armorAddonInventory,
                analyses
            );

        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult
            armorAddonProjection =
                SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        scan
                    );

        SkyrimWinningHeadPartInventoryResult headPartInventory =
            SkyrimWinningHeadPartInventory.Inspect(
                dataRoot:
                    dataRoot,
                runtimePluginSet:
                    runtimePluginSet
            );

        SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult
            headPartProjection =
                SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        headPartInventory
                    );

        SkyrimWinningFurnitureInventoryResult furnitureInventory =
            SkyrimWinningFurnitureInventory.Inspect(
                dataRoot:
                    dataRoot,
                runtimePluginSet:
                    runtimePluginSet
            );

        SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjectionResult
            furnitureProjection =
                SkyrimWinningFurnitureAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        furnitureInventory
                    );

        SkyrimWinningStaticInventoryResult staticInventory =
            SkyrimWinningStaticInventory.Inspect(
                dataRoot:
                    dataRoot,
                runtimePluginSet:
                    runtimePluginSet
            );

        SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjectionResult
            staticProjection =
                SkyrimWinningStaticAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        staticInventory
                    );

        SkyrimWinningContainerInventoryResult containerInventory =
            SkyrimWinningContainerInventory.Inspect(
                dataRoot:
                    dataRoot,
                runtimePluginSet:
                    runtimePluginSet
            );

        SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionResult
            containerProjection =
                SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        containerInventory
                    );

        SkyrimWinningTreeInventoryResult treeInventory =
            SkyrimWinningTreeInventory.Inspect(
                dataRoot:
                    dataRoot,
                runtimePluginSet:
                    runtimePluginSet
            );

        SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjectionResult
            treeProjection =
                SkyrimWinningTreeAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        treeInventory
                    );

        SkyrimWinningConsumerSpellingEvidenceCompositionResult composition =
            SkyrimWinningConsumerSpellingEvidenceComposer.Compose(
                armorAddonProjection,
                headPartProjection,
                furnitureProjection,
                staticProjection,
                containerProjection,
                treeProjection
            );

        using LinuxNoFollowPathHandle? aliasesDirectory =
            OpenAliasesDirectory(
                aliasesDirectoryPath
            );

        return
            SkyrimWinningTargetedConsumerCaseRepairCandidateProjector.Project(
                composition,
                aliasesDirectory
            );
    }

    // Runs the Phase 1 mesh-path discovery in Discover, then layers two
    // further discovery rounds on top of its resolved real mesh paths:
    // round 2 resolves any external .bgsm/.bgem material-file requests
    // embedded in those meshes to real, correctly-cased material paths;
    // round 3 unions the direct in-mesh texture requests with the
    // texture requests embedded in those resolved material files and
    // resolves them to real, correctly-cased texture paths. Every real
    // mesh and material file is opened exactly once regardless of how
    // many logical consumers reference it.
    //
    // Candidate publication remains all-or-nothing, now across all three
    // rounds together - matching the single-round projector's own
    // philosophy - since a repair plan built from a partial asset scan
    // could silently omit real, fixable candidates.
    public static TargetedConsumerAssetDiscoveryResult DiscoverWithAssets(
        string dataRoot,
        string pluginsPath,
        string loadOrderPath,
        string cccPath,
        string? aliasesDirectoryPath = null)
    {
        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult
            meshRound =
                Discover(
                    dataRoot,
                    pluginsPath,
                    loadOrderPath,
                    cccPath,
                    aliasesDirectoryPath
                );

        if (!meshRound.CandidateEvidenceComplete)
        {
            return new TargetedConsumerAssetDiscoveryResult(
                MeshRound:
                    meshRound,
                MaterialPathRound:
                    null,
                TextureRound:
                    null,
                Candidates:
                    Array.Empty<
                        DataRelativePathTargetedConsumerCaseRepairCandidate
                    >()
            );
        }

        // Deliberately every resolved winning mesh, not just this
        // round's mismatched candidates - a mesh's own path being
        // already correctly cased says nothing about whether its
        // embedded texture-slot strings are. Candidates alone would
        // only scan the minority of meshes that themselves needed a
        // path fix, silently skipping every already-correctly-named
        // mesh's own texture references.
        string[] meshPhysicalPaths =
            ExtractResolvedPhysicalPaths(
                    meshRound
                )
                .Where(
                    path =>
                        path.EndsWith(
                            ".nif",
                            StringComparison.OrdinalIgnoreCase
                        )
                )
                .Distinct(
                    StringComparer.Ordinal
                )
                .ToArray();

        SkyrimWinningMeshTextureInventoryResult meshTextureInventory =
            SkyrimWinningMeshTextureInventory.Inspect(
                dataRoot,
                meshPhysicalPaths
            );

        SkyrimWinningMeshTextureAggregateConsumerSpellingEvidenceProjectionResult
            meshTextureProjection =
                SkyrimWinningMeshTextureAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        meshTextureInventory
                    );

        SkyrimWinningMeshMaterialPathAggregateConsumerSpellingEvidenceProjectionResult
            meshMaterialPathProjection =
                SkyrimWinningMeshMaterialPathAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        meshTextureInventory
                    );

        using LinuxNoFollowPathHandle? aliasesDirectory =
            OpenAliasesDirectory(
                aliasesDirectoryPath
            );

        SkyrimWinningConsumerSpellingEvidenceCompositionResult
            materialPathComposition =
                SkyrimWinningConsumerSpellingEvidenceComposer.Compose(
                    meshMaterialPathProjection
                );

        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult
            materialPathRound =
                SkyrimWinningTargetedConsumerCaseRepairCandidateProjector
                    .Project(
                        materialPathComposition,
                        aliasesDirectory
                    );

        if (!materialPathRound.CandidateEvidenceComplete)
        {
            return new TargetedConsumerAssetDiscoveryResult(
                MeshRound:
                    meshRound,
                MaterialPathRound:
                    materialPathRound,
                TextureRound:
                    null,
                Candidates:
                    Array.Empty<
                        DataRelativePathTargetedConsumerCaseRepairCandidate
                    >()
            );
        }

        // Same reasoning as meshPhysicalPaths above - every resolved
        // material file a mesh actually points at, not just the ones
        // whose own path needed a case fix.
        string[] materialPhysicalPaths =
            ExtractResolvedPhysicalPaths(
                    materialPathRound
                )
                .Distinct(
                    StringComparer.Ordinal
                )
                .ToArray();

        SkyrimWinningMaterialTextureInventoryResult materialTextureInventory =
            SkyrimWinningMaterialTextureInventory.Inspect(
                dataRoot,
                materialPhysicalPaths
            );

        SkyrimWinningMaterialTextureAggregateConsumerSpellingEvidenceProjectionResult
            materialTextureProjection =
                SkyrimWinningMaterialTextureAggregateConsumerSpellingEvidenceProjector
                    .Project(
                        materialTextureInventory
                    );

        SkyrimWinningConsumerSpellingEvidenceCompositionResult
            textureComposition =
                SkyrimWinningConsumerSpellingEvidenceComposer.Compose(
                    meshTextureProjection,
                    materialTextureProjection
                );

        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult
            textureRound =
                SkyrimWinningTargetedConsumerCaseRepairCandidateProjector
                    .Project(
                        textureComposition,
                        aliasesDirectory
                    );

        if (!textureRound.CandidateEvidenceComplete)
        {
            return new TargetedConsumerAssetDiscoveryResult(
                MeshRound:
                    meshRound,
                MaterialPathRound:
                    materialPathRound,
                TextureRound:
                    textureRound,
                Candidates:
                    Array.Empty<
                        DataRelativePathTargetedConsumerCaseRepairCandidate
                    >()
            );
        }

        DataRelativePathTargetedConsumerCaseRepairCandidate[] candidates =
            meshRound.Candidates
                .Concat(
                    materialPathRound.Candidates
                )
                .Concat(
                    textureRound.Candidates
                )
                .ToArray();

        return new TargetedConsumerAssetDiscoveryResult(
            MeshRound:
                meshRound,
            MaterialPathRound:
                materialPathRound,
            TextureRound:
                textureRound,
            Candidates:
                candidates
        );
    }

    // A missing or unopenable aliases directory degrades to no alias
    // awareness rather than failing discovery outright - most commonly,
    // this is simply the first-ever run against this install, before any
    // alias has been created and before its durable state directory
    // necessarily exists yet.
    private static LinuxNoFollowPathHandle? OpenAliasesDirectory(
        string? aliasesDirectoryPath)
    {
        return string.IsNullOrWhiteSpace(
                aliasesDirectoryPath)
            ? null
            : LinuxNoFollowPath.OpenRootReadOnly(
                    aliasesDirectoryPath
                )
                .OpenedPath;
    }

    // Every real file a winning consumer resolves to, whether or not
    // that consumer's own requested path needed a case fix - a
    // round's published Candidates only cover the mismatched subset
    // (DataRelativePathTargetedConsumerCaseRepairCandidateProjector
    // only binds a candidate for
    // UniqueRepresentationConsumerCaseMismatchCandidate), which would
    // silently skip scanning the (typically much larger)
    // already-correctly-cased subset's own embedded texture/material
    // references for a downstream round.
    private static IEnumerable<string> ExtractResolvedPhysicalPaths(
        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult
            round)
    {
        foreach (
            SkyrimWinningTargetedConsumerCaseRepairLeafProjection leaf
            in round.Leaves)
        {
            DataRelativePathTargetedPhysicalLeafProjection? physical =
                leaf.TargetedProjection?.PhysicalProjection;

            if (
                physical is null ||
                physical.State !=
                    DataRelativePathTargetedPhysicalLeafProjectionState
                        .Projected)
            {
                continue;
            }

            foreach (
                DataRelativePathTargetedPhysicalFileRepresentation
                    representation
                in physical.PhysicalRepresentations)
            {
                yield return representation.Snapshot.PhysicalPath;
            }
        }
    }
}

// Aggregate result of the multi-round mesh -> material -> texture asset
// discovery pipeline. MaterialPathRound and TextureRound are null only
// when an earlier round failed to reach CandidateEvidenceComplete -
// candidate publication is all-or-nothing across the whole pipeline, the
// same philosophy the single-round projector already applies internally.
internal sealed record TargetedConsumerAssetDiscoveryResult(
    SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult
        MeshRound,
    SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult?
        MaterialPathRound,
    SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult?
        TextureRound,
    IReadOnlyList<DataRelativePathTargetedConsumerCaseRepairCandidate>
        Candidates
)
{
    // Drop-in-compatible surface with
    // SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult so
    // callers that only knew Phase 1's single-round result (the CLI
    // commands and the wizard's batch-apply convergence loop) can adopt
    // this multi-round result with no further shape changes.
    public bool CandidateEvidenceComplete =>
        MeshRound.CandidateEvidenceComplete &&
        MaterialPathRound is not null &&
        MaterialPathRound.CandidateEvidenceComplete &&
        TextureRound is not null &&
        TextureRound.CandidateEvidenceComplete;

    public int CandidateCount =>
        Candidates.Count;

    // Whichever round first failed to reach CandidateEvidenceComplete -
    // Complete only once every round that ran did.
    public SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
        State =>
            !MeshRound.CandidateEvidenceComplete
                ? MeshRound.State
                : MaterialPathRound is null
                    ? MeshRound.State
                    : !MaterialPathRound.CandidateEvidenceComplete
                        ? MaterialPathRound.State
                        : TextureRound is null
                            ? MaterialPathRound.State
                            : TextureRound.State;

    public string? Error =>
        !MeshRound.CandidateEvidenceComplete
            ? MeshRound.Error
            : MaterialPathRound is null
                ? null
                : !MaterialPathRound.CandidateEvidenceComplete
                    ? MaterialPathRound.Error
                    : TextureRound?.Error;

    // Union of every round's leaves that actually ran - contested-ancestor
    // analysis needs every winning consumer's requested path across the
    // whole scan, texture/material consumers included, not just Phase
    // 1's mesh-path consumers.
    public IReadOnlyList<SkyrimWinningTargetedConsumerCaseRepairLeafProjection>
        Leaves =>
            MeshRound.Leaves
                .Concat(
                    MaterialPathRound?.Leaves ??
                    Array.Empty<
                        SkyrimWinningTargetedConsumerCaseRepairLeafProjection
                    >()
                )
                .Concat(
                    TextureRound?.Leaves ??
                    Array.Empty<
                        SkyrimWinningTargetedConsumerCaseRepairLeafProjection
                    >()
                )
                .ToArray();
}
