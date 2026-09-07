namespace CaseCompat.Core.Repair;

// Pure compatibility projection from a validated aggregate namespace manifest
// into the generic physical-spelling leaf input consumed by C4D-3B.
//
// The aggregate manifest is the authoritative boundary for this projection
// because each physical representation retains its exact Data-relative
// spelling alongside its snapshot and generation evidence.
//
// This adapter deliberately preserves manifest leaf order and representation
// order exactly. It performs no path normalization, spelling deduplication,
// sorting, physical-content classification, filesystem access, source
// selection, persistence, repair planning, authorization, or execution.
//
// Downstream C4D spelling classifiers remain responsible for canonicalizing
// and classifying spelling relationships.
public static class
    DataRelativePathAggregateNamespaceManifestPhysicalSpellingProjector
{
    public static IReadOnlyList<DataRelativePathAggregatePhysicalSpellingLeaf>
        Project(
            DataRelativePathAggregateNamespaceManifestRecord manifest)
    {
        ArgumentNullException.ThrowIfNull(
            manifest
        );

        string? validationError =
            DataRelativePathAggregateNamespaceManifest.Validate(
                manifest
            );

        if (validationError is not null)
        {
            throw new ArgumentException(
                "Aggregate namespace manifest is invalid: " +
                validationError,
                nameof(manifest)
            );
        }

        var result =
            new List<DataRelativePathAggregatePhysicalSpellingLeaf>(
                manifest.LogicalLeaves.Count
            );

        foreach (
            DataRelativePathAggregateNamespaceManifestLogicalLeaf leaf
            in manifest.LogicalLeaves)
        {
            string[] physicalRelativePaths =
                leaf.PhysicalRepresentations
                    .Select(
                        representation =>
                            representation.RelativePath
                    )
                    .ToArray();

            result.Add(
                new DataRelativePathAggregatePhysicalSpellingLeaf(
                    WindowsLogicalPath:
                        leaf.WindowsLogicalPath,
                    PhysicalRelativePaths:
                        physicalRelativePaths
                )
            );
        }

        return result.ToArray();
    }
}
