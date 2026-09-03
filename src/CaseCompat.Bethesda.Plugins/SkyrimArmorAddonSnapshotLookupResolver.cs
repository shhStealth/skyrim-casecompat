using CaseCompat.Core.Analysis;

namespace CaseCompat.Bethesda.Plugins;

/*
 * Pure adapter from an ArmorAddon model reference to checkpoint-10A
 * snapshot lookup evidence.
 *
 * No filesystem access is performed here. The original requested
 * DataRelativePath spelling is passed unchanged to
 * WindowsNamespaceSnapshotFileResolver.
 */
public static class SkyrimArmorAddonSnapshotLookupResolver
{
    public static SkyrimArmorAddonSnapshotLookupEvidence Resolve(
        SkyrimArmorAddonModelReference reference,
        IReadOnlyList<WindowsNamespaceAnalysis> analyses)
    {
        ArgumentNullException.ThrowIfNull(
            reference
        );

        ArgumentNullException.ThrowIfNull(
            analyses
        );

        if (!WindowsDataRelativePathParser.TryParse(
                reference.DataRelativePath,
                out string[] requestedComponents,
                out string? requestError))
        {
            return new SkyrimArmorAddonSnapshotLookupEvidence(
                Reference:
                    reference,
                RequestedRootLogicalPath:
                    null,
                State:
                    SkyrimArmorAddonSnapshotLookupEvidenceState
                        .InvalidRequestedPath,
                MatchingAnalysisCount:
                    0,
                SelectedAnalysis:
                    null,
                Lookup:
                    null,
                Error:
                    requestError
            );
        }

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

        WindowsLogicalPath requestedRoot =
            WindowsLogicalPath.FromRelativePath(
                requestedComponents[0]
            );

        WindowsNamespaceAnalysis[] matching =
            analyses
                .Where(
                    analysis =>
                        analysis.RootLogicalPath ==
                        requestedRoot
                )
                .ToArray();

        if (matching.Length == 0)
        {
            return new SkyrimArmorAddonSnapshotLookupEvidence(
                Reference:
                    reference,
                RequestedRootLogicalPath:
                    requestedRoot,
                State:
                    SkyrimArmorAddonSnapshotLookupEvidenceState
                        .NoMatchingNamespaceAnalysis,
                MatchingAnalysisCount:
                    0,
                SelectedAnalysis:
                    null,
                Lookup:
                    null,
                Error:
                    $"No Windows-namespace analysis matches requested " +
                    $"logical root \"{requestedRoot.Value}\"."
            );
        }

        if (matching.Length != 1)
        {
            return new SkyrimArmorAddonSnapshotLookupEvidence(
                Reference:
                    reference,
                RequestedRootLogicalPath:
                    requestedRoot,
                State:
                    SkyrimArmorAddonSnapshotLookupEvidenceState
                        .AmbiguousMatchingNamespaceAnalysis,
                MatchingAnalysisCount:
                    matching.Length,
                SelectedAnalysis:
                    null,
                Lookup:
                    null,
                Error:
                    $"Multiple Windows-namespace analyses match requested " +
                    $"logical root \"{requestedRoot.Value}\"."
            );
        }

        WindowsNamespaceAnalysis selected =
            matching[0];

        WindowsNamespaceSnapshotFileLookup lookup =
            WindowsNamespaceSnapshotFileResolver.Resolve(
                selected,
                reference.DataRelativePath
            );

        return new SkyrimArmorAddonSnapshotLookupEvidence(
            Reference:
                reference,
            RequestedRootLogicalPath:
                requestedRoot,
            State:
                SkyrimArmorAddonSnapshotLookupEvidenceState
                    .LookupProduced,
            MatchingAnalysisCount:
                1,
            SelectedAnalysis:
                selected,
            Lookup:
                lookup,
            Error:
                null
        );
    }
}
