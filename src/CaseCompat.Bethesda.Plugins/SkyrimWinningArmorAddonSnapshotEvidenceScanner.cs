using CaseCompat.Core.Analysis;

namespace CaseCompat.Bethesda.Plugins;

/*
 * Pure composition over an already-produced winning ArmorAddon inventory
 * and already-produced Windows-namespace analyses.
 *
 * Exact requested DataRelativePath spellings are grouped with
 * StringComparer.Ordinal. Checkpoint 10B-B is invoked once per exact group.
 *
 * No plugin parsing, filesystem lookup, hashing, provider selection,
 * archive precedence, or repair operation occurs here.
 */
public static class SkyrimWinningArmorAddonSnapshotEvidenceScanner
{
    private sealed record PendingReference(
        SkyrimWinningArmorAddonRecord Record,
        SkyrimArmorAddonModelReference Reference
    );

    public static SkyrimWinningArmorAddonSnapshotEvidenceScanResult Inspect(
        SkyrimWinningArmorAddonInventoryResult inventory,
        IReadOnlyList<WindowsNamespaceAnalysis> analyses)
    {
        ArgumentNullException.ThrowIfNull(
            inventory
        );

        ArgumentNullException.ThrowIfNull(
            analyses
        );

        foreach (
            WindowsNamespaceAnalysis? analysis
            in analyses)
        {
            if (analysis is null)
            {
                throw new ArgumentException(
                    "The namespace-analysis collection must not " +
                    "contain null entries.",
                    nameof(analyses)
                );
            }
        }

        PendingReference[] pending =
            inventory.Winners
                .SelectMany(
                    record =>
                        record.ModelReferences.Select(
                            reference =>
                                new PendingReference(
                                    Record:
                                        record,
                                    Reference:
                                        reference
                                )
                        )
                )
                .ToArray();

        IGrouping<string, PendingReference>[] pathGroups =
            pending
                .GroupBy(
                    item =>
                        item.Reference.DataRelativePath,
                    StringComparer.Ordinal
                )
                .OrderBy(
                    group =>
                        group.Key,
                    StringComparer.Ordinal
                )
                .ToArray();

        var paths =
            new List<
                SkyrimWinningArmorAddonSnapshotPathEvidence
            >(
                pathGroups.Length
            );

        foreach (
            IGrouping<string, PendingReference> group
            in pathGroups)
        {
            PendingReference[] members =
                group.ToArray();

            SkyrimArmorAddonSnapshotLookupEvidence representativeEvidence =
                SkyrimArmorAddonSnapshotLookupResolver.Resolve(
                    members[0].Reference,
                    analyses
                );

            SkyrimWinningArmorAddonSnapshotReferenceContext[] references =
                members
                    .Select(
                        item =>
                            new SkyrimWinningArmorAddonSnapshotReferenceContext(
                                WinningPluginName:
                                    item.Record.WinningPluginName,
                                WinningLoadOrderIndex:
                                    item.Record.WinningLoadOrderIndex,
                                Reference:
                                    item.Reference
                            )
                    )
                    .ToArray();

            paths.Add(
                new SkyrimWinningArmorAddonSnapshotPathEvidence(
                    RequestedPath:
                        group.Key,
                    References:
                        references,
                    RequestedRootLogicalPath:
                        representativeEvidence.RequestedRootLogicalPath,
                    State:
                        representativeEvidence.State,
                    MatchingAnalysisCount:
                        representativeEvidence.MatchingAnalysisCount,
                    SelectedAnalysis:
                        representativeEvidence.SelectedAnalysis,
                    Lookup:
                        representativeEvidence.Lookup,
                    Error:
                        representativeEvidence.Error
                )
            );
        }

        return new SkyrimWinningArmorAddonSnapshotEvidenceScanResult(
            Inventory:
                inventory,
            Paths:
                paths.ToArray()
        );
    }
}
