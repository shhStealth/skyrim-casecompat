namespace CaseCompat.Core.Repair;

// Pure source-agnostic composition of already-classified C4D-1 consumer
// spelling evidence.
//
// Every incoming evidence row is first reclassified and required to be in the
// canonical form produced by DataRelativePathAggregateConsumerSpellingClassifier.
//
// Canonical requested spellings are then unioned by Windows-logical leaf and
// classified again. This second classification is essential: two independent
// sources can each contain UniqueConsumerSpelling while disagreeing with each
// other on the exact requested spelling.
//
// The output universe is the union of logical leaves explicitly represented by
// the supplied source collections. NoConsumerEvidence therefore survives when
// it is explicitly supplied, but this composer does not invent absent leaves.
//
// This type has no knowledge of Bethesda record kinds, winner-search
// completeness, filesystem state, physical spelling, provider/source
// selection, content identity, repair eligibility, persistence, execution,
// rollback, or recovery.
public static class
    DataRelativePathAggregateConsumerSpellingEvidenceComposer
{
    public static
        IReadOnlyList<DataRelativePathAggregateConsumerSpellingEvidence>
        Compose(
            IReadOnlyList<
                IReadOnlyList<
                    DataRelativePathAggregateConsumerSpellingEvidence
                >
            > sources)
    {
        ArgumentNullException.ThrowIfNull(
            sources
        );

        var requestedPathsByLogicalPath =
            new Dictionary<string, List<string>>(
                StringComparer.Ordinal
            );

        foreach (
            IReadOnlyList<
                DataRelativePathAggregateConsumerSpellingEvidence
            >? source
            in sources)
        {
            if (source is null)
            {
                throw new ArgumentException(
                    "The consumer-spelling source collection contains a " +
                    "null source.",
                    nameof(sources)
                );
            }

            foreach (
                DataRelativePathAggregateConsumerSpellingEvidence? evidence
                in source)
            {
                if (evidence is null)
                {
                    throw new ArgumentException(
                        "A consumer-spelling source contains a null evidence " +
                        "entry.",
                        nameof(sources)
                    );
                }

                if (evidence.DistinctRequestedPaths is null)
                {
                    throw new ArgumentException(
                        $"Consumer spelling evidence for " +
                        $"'{evidence.WindowsLogicalPath}' has no " +
                        "requested-path collection.",
                        nameof(sources)
                    );
                }

                // Public record instances can be manually constructed.
                // Re-run C4D-1 before allowing an input row to contribute to
                // the composed authority.
                DataRelativePathAggregateConsumerSpellingEvidence canonical =
                    DataRelativePathAggregateConsumerSpellingClassifier
                        .Classify(
                            evidence.WindowsLogicalPath,
                            evidence.DistinctRequestedPaths
                        );

                if (
                    canonical.State !=
                        evidence.State ||
                    !canonical
                        .DistinctRequestedPaths
                        .SequenceEqual(
                            evidence.DistinctRequestedPaths,
                            StringComparer.Ordinal
                        ))
                {
                    throw new ArgumentException(
                        "Consumer spelling evidence is not in the canonical " +
                        "form produced by the aggregate consumer spelling " +
                        "classifier.",
                        nameof(sources)
                    );
                }

                if (!requestedPathsByLogicalPath.TryGetValue(
                        canonical.WindowsLogicalPath,
                        out List<string>? requestedPaths))
                {
                    requestedPaths =
                        new List<string>();

                    requestedPathsByLogicalPath.Add(
                        canonical.WindowsLogicalPath,
                        requestedPaths
                    );
                }

                requestedPaths.AddRange(
                    canonical.DistinctRequestedPaths
                );
            }
        }

        return requestedPathsByLogicalPath
            .OrderBy(
                pair =>
                    pair.Key,
                StringComparer.Ordinal
            )
            .Select(
                pair =>
                    DataRelativePathAggregateConsumerSpellingClassifier
                        .Classify(
                            pair.Key,
                            pair.Value.ToArray()
                        )
            )
            .ToArray();
    }
}
