using CaseCompat.Core.Analysis;

namespace CaseCompat.Core.Repair;

/*
 * Read-only relation between authoritative consumer spelling evidence and
 * the exact physical Data-relative spellings observed for one Windows-logical
 * aggregate regular-file leaf.
 *
 * This classification is orthogonal to provider/content classification.
 * In particular, it does not inspect or replace:
 *
 * - UniqueRepresentation;
 * - EquivalentContentMultipleRepresentations;
 * - ConflictingContentMultipleRepresentations.
 *
 * It grants no provider selection, source selection, repair planning,
 * persistence, execution, rollback, or recovery authority.
 */
public enum DataRelativePathAggregateConsumerPhysicalSpellingState
{
    NoConsumerEvidence,
    ConflictingConsumerSpellings,
    ExactPhysicalSpellingPresent,
    ConsumerCaseMismatch
}

/*
 * PhysicalRelativePaths retains the distinct normalized physical spellings
 * participating in this spelling-only relation. Separators are represented
 * as '/', while component spelling is preserved exactly.
 *
 * ConsumerSpelling remains the authority for the requested spelling.
 * Physical spellings never establish or override that authority.
 */
public sealed record
    DataRelativePathAggregateConsumerPhysicalSpellingEvidence(
        string WindowsLogicalPath,
        DataRelativePathAggregateConsumerSpellingEvidence ConsumerSpelling,
        IReadOnlyList<string> PhysicalRelativePaths,
        DataRelativePathAggregateConsumerPhysicalSpellingState State
    )
{
    public string? AuthoritativeRequestedPath =>
        ConsumerSpelling.AuthoritativeRequestedPath;

    public bool ExactPhysicalSpellingPresent =>
        State ==
        DataRelativePathAggregateConsumerPhysicalSpellingState
            .ExactPhysicalSpellingPresent;
}

/*
 * Pure spelling-only join.
 *
 * The caller supplies exact physical Data-relative spellings from already
 * established aggregate namespace evidence. Every physical spelling must map
 * to the same Windows-logical leaf as the supplied consumer evidence.
 *
 * Multiple physical representations do not by themselves create consumer
 * ambiguity. Consumer ambiguity comes only from authoritative consumers that
 * disagree on exact requested spelling.
 *
 * No filesystem access, hashing, physical-content classification, provider
 * precedence, canonical-spelling inference, or repair decision occurs here.
 */
public static class
    DataRelativePathAggregateConsumerPhysicalSpellingClassifier
{
    public static
        DataRelativePathAggregateConsumerPhysicalSpellingEvidence Classify(
            DataRelativePathAggregateConsumerSpellingEvidence
                consumerSpelling,
            IReadOnlyList<string> physicalRelativePaths)
    {
        ArgumentNullException.ThrowIfNull(
            consumerSpelling
        );

        ArgumentNullException.ThrowIfNull(
            physicalRelativePaths
        );

        if (consumerSpelling.DistinctRequestedPaths is null)
        {
            throw new ArgumentException(
                "Consumer spelling evidence has no requested-path " +
                "collection.",
                nameof(consumerSpelling)
            );
        }

        DataRelativePathAggregateConsumerSpellingEvidence
            validatedConsumerSpelling =
                DataRelativePathAggregateConsumerSpellingClassifier
                    .Classify(
                        consumerSpelling.WindowsLogicalPath,
                        consumerSpelling.DistinctRequestedPaths
                    );

        if (
            validatedConsumerSpelling.State !=
                consumerSpelling.State ||
            !validatedConsumerSpelling
                .DistinctRequestedPaths
                .SequenceEqual(
                    consumerSpelling.DistinctRequestedPaths,
                    StringComparer.Ordinal
                ))
        {
            throw new ArgumentException(
                "Consumer spelling evidence is not in the canonical form " +
                "produced by the aggregate consumer spelling classifier.",
                nameof(consumerSpelling)
            );
        }

        if (physicalRelativePaths.Count == 0)
        {
            throw new ArgumentException(
                "At least one physical representation is required for an " +
                "aggregate physical-leaf spelling relation.",
                nameof(physicalRelativePaths)
            );
        }

        var distinctPhysicalRelativePaths =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        foreach (string? physicalRelativePath in physicalRelativePaths)
        {
            if (!WindowsDataRelativePathParser.TryParse(
                    physicalRelativePath,
                    out string[] components,
                    out string? parseError))
            {
                throw new ArgumentException(
                    $"Physical relative path is invalid: " +
                    $"{parseError ?? "unknown parse error"}",
                    nameof(physicalRelativePaths)
                );
            }

            string normalizedPhysicalRelativePath =
                string.Join(
                    '/',
                    components
                );

            WindowsLogicalPath physicalLogicalPath;

            try
            {
                physicalLogicalPath =
                    WindowsLogicalPath.FromRelativePath(
                        normalizedPhysicalRelativePath
                    );
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"Physical relative path cannot be mapped to the " +
                    $"Windows namespace: {ex.Message}",
                    nameof(physicalRelativePaths),
                    ex
                );
            }

            if (!string.Equals(
                    physicalLogicalPath.Value,
                    consumerSpelling.WindowsLogicalPath,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Physical relative path " +
                    $"'{normalizedPhysicalRelativePath}' does not belong " +
                    $"to Windows-logical leaf " +
                    $"'{consumerSpelling.WindowsLogicalPath}'.",
                    nameof(physicalRelativePaths)
                );
            }

            distinctPhysicalRelativePaths.Add(
                normalizedPhysicalRelativePath
            );
        }

        string[] orderedPhysicalRelativePaths =
            distinctPhysicalRelativePaths
                .OrderBy(
                    path =>
                        path,
                    StringComparer.Ordinal
                )
                .ToArray();

        DataRelativePathAggregateConsumerPhysicalSpellingState state =
            consumerSpelling.State switch
            {
                DataRelativePathAggregateConsumerSpellingState
                    .NoConsumerEvidence =>
                        DataRelativePathAggregateConsumerPhysicalSpellingState
                            .NoConsumerEvidence,

                DataRelativePathAggregateConsumerSpellingState
                    .ConflictingConsumerSpellings =>
                        DataRelativePathAggregateConsumerPhysicalSpellingState
                            .ConflictingConsumerSpellings,

                DataRelativePathAggregateConsumerSpellingState
                    .UniqueConsumerSpelling =>
                        ClassifyUniqueConsumerSpelling(
                            consumerSpelling.AuthoritativeRequestedPath!,
                            orderedPhysicalRelativePaths
                        ),

                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(consumerSpelling),
                        consumerSpelling.State,
                        "Unsupported aggregate consumer spelling state."
                    )
            };

        return new
            DataRelativePathAggregateConsumerPhysicalSpellingEvidence(
                WindowsLogicalPath:
                    consumerSpelling.WindowsLogicalPath,
                ConsumerSpelling:
                    consumerSpelling,
                PhysicalRelativePaths:
                    orderedPhysicalRelativePaths,
                State:
                    state
            );
    }

    private static
        DataRelativePathAggregateConsumerPhysicalSpellingState
            ClassifyUniqueConsumerSpelling(
                string authoritativeRequestedPath,
                IReadOnlyList<string> physicalRelativePaths)
    {
        bool exactPhysicalSpellingPresent =
            physicalRelativePaths.Any(
                physicalRelativePath =>
                    string.Equals(
                        physicalRelativePath,
                        authoritativeRequestedPath,
                        StringComparison.Ordinal
                    )
            );

        return exactPhysicalSpellingPresent
            ? DataRelativePathAggregateConsumerPhysicalSpellingState
                .ExactPhysicalSpellingPresent
            : DataRelativePathAggregateConsumerPhysicalSpellingState
                .ConsumerCaseMismatch;
    }
}
