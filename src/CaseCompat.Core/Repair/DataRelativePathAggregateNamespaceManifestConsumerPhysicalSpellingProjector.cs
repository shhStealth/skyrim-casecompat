namespace CaseCompat.Core.Repair;

// Read-only pairing of two orthogonal evidence dimensions for one aggregate
// Windows-logical regular-file leaf:
//
// - ManifestLeaf retains physical/content topology, exact physical
//   representations, snapshots, and generation evidence.
// - SpellingEvidence retains the authoritative consumer-to-physical spelling
//   relation produced by the generic C4D spelling classifiers.
//
// Neither evidence dimension establishes or overrides the other.
//
// This record grants no provider selection, source selection, canonical casing
// inference, repair planning, authorization, execution, rollback, or recovery
// authority.
public sealed record
    DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingEvidence(
        DataRelativePathAggregateNamespaceManifestLogicalLeaf ManifestLeaf,
        DataRelativePathAggregateConsumerPhysicalSpellingEvidence
            SpellingEvidence
    );

// Pure Core orchestration joining:
//
// 1. validated aggregate namespace manifest evidence;
// 2. C4D-6A manifest -> physical-spelling projection;
// 3. generic C4D-3B physical-leaf-left consumer spelling projection.
//
// The aggregate manifest remains the output universe. Consumer-only logical
// leaves are therefore not projected, matching C4D-3B semantics.
//
// Manifest logical leaves are retained by reference and output in manifest
// order. Pairing is performed by exact canonical WindowsLogicalPath rather than
// positional zipping so ordering changes in a downstream projector cannot
// silently associate evidence with the wrong physical/content leaf.
//
// No filesystem access, hashing, provider precedence, content classification,
// source selection, repair decision, persistence, or mutation occurs here.
public static class
    DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingProjector
{
    public static IReadOnlyList<
        DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingEvidence
    > Project(
        DataRelativePathAggregateNamespaceManifestRecord manifest,
        IReadOnlyList<DataRelativePathAggregateConsumerSpellingEvidence>
            consumerSpellings)
    {
        ArgumentNullException.ThrowIfNull(
            manifest
        );

        ArgumentNullException.ThrowIfNull(
            consumerSpellings
        );

        IReadOnlyList<DataRelativePathAggregatePhysicalSpellingLeaf>
            physicalLeaves =
                DataRelativePathAggregateNamespaceManifestPhysicalSpellingProjector
                    .Project(
                        manifest
                    );

        IReadOnlyList<DataRelativePathAggregateConsumerPhysicalSpellingEvidence>
            spellingRelations =
                DataRelativePathAggregateConsumerPhysicalSpellingProjector
                    .Project(
                        physicalLeaves,
                        consumerSpellings
                    );

        if (spellingRelations.Count != manifest.LogicalLeaves.Count)
        {
            throw new InvalidOperationException(
                "Consumer/physical spelling projection did not exactly cover " +
                "the validated aggregate manifest logical-leaf universe."
            );
        }

        var relationsByLogicalPath =
            new Dictionary<
                string,
                DataRelativePathAggregateConsumerPhysicalSpellingEvidence
            >(
                StringComparer.Ordinal
            );

        foreach (
            DataRelativePathAggregateConsumerPhysicalSpellingEvidence relation
            in spellingRelations)
        {
            if (!relationsByLogicalPath.TryAdd(
                    relation.WindowsLogicalPath,
                    relation))
            {
                throw new InvalidOperationException(
                    $"Consumer/physical spelling relation for logical leaf " +
                    $"'{relation.WindowsLogicalPath}' occurs more than once."
                );
            }
        }

        var result =
            new List<
                DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingEvidence
            >(
                manifest.LogicalLeaves.Count
            );

        foreach (
            DataRelativePathAggregateNamespaceManifestLogicalLeaf manifestLeaf
            in manifest.LogicalLeaves)
        {
            if (!relationsByLogicalPath.TryGetValue(
                    manifestLeaf.WindowsLogicalPath,
                    out DataRelativePathAggregateConsumerPhysicalSpellingEvidence?
                        spellingEvidence))
            {
                throw new InvalidOperationException(
                    $"Validated aggregate manifest logical leaf " +
                    $"'{manifestLeaf.WindowsLogicalPath}' has no " +
                    "consumer/physical spelling relation."
                );
            }

            result.Add(
                new
                    DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingEvidence(
                        ManifestLeaf:
                            manifestLeaf,
                        SpellingEvidence:
                            spellingEvidence
                    )
            );
        }

        return result.ToArray();
    }
}
