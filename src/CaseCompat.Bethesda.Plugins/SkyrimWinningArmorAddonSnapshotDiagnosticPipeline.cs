using CaseCompat.Core.Analysis;

namespace CaseCompat.Bethesda.Plugins;

/*
 * Pure end-to-end composition of the approved descriptor-bound winning
 * ArmorAddon snapshot diagnostic layers.
 *
 * Inputs are already-produced evidence. This coordinator does not scan the
 * filesystem, parse plugins or archives, resolve paths independently, perform
 * archive lookup independently, calculate archive precedence independently,
 * introduce a new diagnostic state, infer canonical spelling, or infer repair
 * eligibility.
 *
 * Authority remains in the approved layers:
 *
 *   10C   snapshot evidence composition
 *   10E   consumer projection, including one 10D interpretation per path
 *   10F   consumer diagnostic classification
 *   10G-B shared path-level archive evidence
 *   10G-C consumer archive projection
 *   10G-D final provider diagnostic classification
 *
 * The exact existing 10G-D result is returned; no duplicate aggregate wrapper
 * is introduced.
 */
public static class SkyrimWinningArmorAddonSnapshotDiagnosticPipeline
{
    public static
        SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticResult
        Compose(
            SkyrimWinningArmorAddonInventoryResult inventory,
            IReadOnlyList<WindowsNamespaceAnalysis> analyses,
            SkyrimArchiveCandidateIndexResult archiveIndex,
            SkyrimRuntimeArchiveEvidenceResult runtimeArchiveEvidence)
    {
        ArgumentNullException.ThrowIfNull(
            inventory
        );

        ArgumentNullException.ThrowIfNull(
            analyses
        );

        ArgumentNullException.ThrowIfNull(
            archiveIndex
        );

        ArgumentNullException.ThrowIfNull(
            runtimeArchiveEvidence
        );

        SkyrimWinningArmorAddonSnapshotEvidenceScanResult scan =
            SkyrimWinningArmorAddonSnapshotEvidenceScanner.Inspect(
                inventory,
                analyses
            );

        SkyrimWinningArmorAddonSnapshotConsumerProjectionResult
            consumerProjection =
                SkyrimWinningArmorAddonSnapshotConsumerProjector.Project(
                    scan
                );

        SkyrimWinningArmorAddonSnapshotConsumerDiagnosticResult
            consumerDiagnostics =
                SkyrimWinningArmorAddonSnapshotConsumerDiagnosticClassifier
                    .Classify(
                        consumerProjection
                    );

        SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceResult
            pathArchiveEvidence =
                SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceComposer
                    .Compose(
                        consumerDiagnostics,
                        archiveIndex,
                        runtimeArchiveEvidence
                    );

        SkyrimWinningArmorAddonSnapshotConsumerArchiveProjectionResult
            consumerArchiveProjection =
                SkyrimWinningArmorAddonSnapshotConsumerArchiveProjector
                    .Project(
                        pathArchiveEvidence
                    );

        return
            SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticClassifier
                .Classify(
                    consumerArchiveProjection
                );
    }
}
