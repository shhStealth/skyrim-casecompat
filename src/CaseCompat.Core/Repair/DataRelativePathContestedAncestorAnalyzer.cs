namespace CaseCompat.Core.Repair;

// Whole-load-order aggregate: finds shared ancestor directories where
// different candidates' own winning consumers disagree about the
// correct casing.
//
// A single candidate's plan is decided in isolation from every other
// candidate, but a directory-level rename it performs moves every
// other file already beneath that directory too - including files
// belonging to a completely unrelated candidate whose own winning
// consumer expects a different casing for that same shared ancestor.
// There is no rename that satisfies both sides: whichever casing wins
// silently strands the other consumer's files. This mirrors
// ConflictingConsumerSpellings at the file level, just one level up,
// at shared ancestor directories.
//
// This analysis only tells the plan projector which ancestor prefixes
// are contested. It does not decide anything and does not touch the
// filesystem - it is a pure function over the requested paths already
// carried by the discovered candidate set.
public static class DataRelativePathContestedAncestorAnalyzer
{
    public static IReadOnlySet<string> Analyze(
        IReadOnlyList<DataRelativePathTargetedConsumerCaseRepairCandidate>
            candidates)
    {
        ArgumentNullException.ThrowIfNull(
            candidates
        );

        var variantsByLogicalPrefix =
            new Dictionary<string, HashSet<string>>(
                StringComparer.Ordinal
            );

        foreach (
            DataRelativePathTargetedConsumerCaseRepairCandidate candidate
            in candidates)
        {
            string? requestedPath =
                candidate.AuthoritativeRequestedPath;

            if (string.IsNullOrEmpty(
                    requestedPath))
            {
                continue;
            }

            string[] components =
                requestedPath.Split(
                    '/'
                );

            // Ancestor prefixes only - the final component is the
            // requested file itself, not a directory.
            for (
                int index = 0;
                index < components.Length - 1;
                index++)
            {
                string prefix =
                    string.Join(
                        '/',
                        components,
                        0,
                        index + 1
                    );

                string logicalKey =
                    prefix.ToUpperInvariant();

                if (!variantsByLogicalPrefix.TryGetValue(
                        logicalKey,
                        out HashSet<string>? variants))
                {
                    variants =
                        new HashSet<string>(
                            StringComparer.Ordinal
                        );

                    variantsByLogicalPrefix[logicalKey] =
                        variants;
                }

                variants.Add(
                    prefix
                );
            }
        }

        var contested =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        foreach (
            (string logicalKey, HashSet<string> variants)
            in variantsByLogicalPrefix)
        {
            if (variants.Count > 1)
            {
                contested.Add(
                    logicalKey
                );
            }
        }

        return contested;
    }
}
