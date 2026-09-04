using CaseCompat.Core.Analysis;

namespace CaseCompat.Bethesda.Plugins;

/*
 * Read-only acquisition of the descriptor-bound Windows namespace analyses
 * required by an already-produced winning ArmorAddon inventory.
 *
 * Requested Data-relative paths are validated with the same shared parser
 * used by snapshot lookup. Invalid requested paths require no filesystem
 * acquisition here; checkpoint 10B-B retains authority for representing
 * InvalidRequestedPath.
 *
 * Valid first components are converted to WindowsLogicalPath before
 * deduplication. Therefore spelling variants such as "Meshes" and "meshes"
 * request one logical namespace analysis.
 *
 * WindowsNamespaceAnalyzer retains all Windows-equivalent physical root
 * participants and all analyzer incompleteness/errors. This producer does not
 * choose a physical spelling or reinterpret analyzer evidence.
 *
 * No asset resolution, archive lookup, archive precedence, diagnostic
 * classification, canonical spelling inference, repair eligibility, or
 * mutation occurs here.
 */
public static class
    SkyrimWinningArmorAddonSnapshotNamespaceAnalysisProducer
{
    public static IReadOnlyList<WindowsNamespaceAnalysis> Produce(
        SkyrimWinningArmorAddonInventoryResult inventory)
    {
        ArgumentNullException.ThrowIfNull(
            inventory
        );

        var requestedRoots =
            new HashSet<WindowsLogicalPath>();

        foreach (
            SkyrimWinningArmorAddonRecord record
            in inventory.Winners)
        {
            foreach (
                SkyrimArmorAddonModelReference reference
                in record.ModelReferences)
            {
                if (!WindowsDataRelativePathParser.TryParse(
                        reference.DataRelativePath,
                        out string[] components,
                        out _))
                {
                    continue;
                }

                requestedRoots.Add(
                    WindowsLogicalPath.FromRelativePath(
                        components[0]
                    )
                );
            }
        }

        return requestedRoots
            .OrderBy(
                root =>
                    root.Value,
                StringComparer.Ordinal
            )
            .Select(
                root =>
                    WindowsNamespaceAnalyzer.Analyze(
                        inventory.DataRoot,
                        root.Value
                    )
            )
            .ToArray();
    }
}
