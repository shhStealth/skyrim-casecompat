using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Analysis;
using CaseCompat.Core.LoadOrder;

// Shared winning-consumer discovery orchestration for every
// targeted-consumer-* CLI command. Runs the full ArmorAddon + HeadPart
// winning-consumer pipeline once and composes it into the
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
            string cccPath)
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

        SkyrimWinningConsumerSpellingEvidenceCompositionResult composition =
            SkyrimWinningConsumerSpellingEvidenceComposer.Compose(
                armorAddonProjection,
                headPartProjection
            );

        return
            SkyrimWinningTargetedConsumerCaseRepairCandidateProjector.Project(
                composition
            );
    }
}
