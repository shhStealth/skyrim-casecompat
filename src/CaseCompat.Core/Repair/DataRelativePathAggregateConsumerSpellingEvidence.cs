using CaseCompat.Core.Analysis;

namespace CaseCompat.Core.Repair;

/*
 * Consumer-side spelling evidence for one Windows-logical aggregate
 * namespace leaf.
 *
 * This state is deliberately orthogonal to physical/provider classification.
 * It says only whether the supplied authoritative consumers agree on one
 * exact Data-relative component spelling.
 *
 * It grants no provider selection, source selection, repair planning,
 * persistence, execution, rollback, or recovery authority.
 */
public enum DataRelativePathAggregateConsumerSpellingState
{
    NoConsumerEvidence,
    UniqueConsumerSpelling,
    ConflictingConsumerSpellings
}

/*
 * DistinctRequestedPaths contains normalized Data-relative spellings:
 * separators are represented as '/', while component spelling is preserved
 * exactly.
 *
 * Every retained path is required to map to WindowsLogicalPath.
 */
public sealed record DataRelativePathAggregateConsumerSpellingEvidence(
    string WindowsLogicalPath,
    IReadOnlyList<string> DistinctRequestedPaths,
    DataRelativePathAggregateConsumerSpellingState State
)
{
    public string? AuthoritativeRequestedPath =>
        State ==
        DataRelativePathAggregateConsumerSpellingState
            .UniqueConsumerSpelling
            ? DistinctRequestedPaths[0]
            : null;
}

/*
 * Pure consumer-spelling classification.
 *
 * Input occurrences may contain repeated references to the same exact path;
 * those repetitions do not create a conflict.
 *
 * Case-distinct component spellings that map to the same Windows-logical
 * path remain distinct evidence and therefore conflict.
 *
 * No filesystem access, namespace lookup, hashing, provider precedence,
 * canonical-spelling inference, or repair decision occurs here.
 */
public static class DataRelativePathAggregateConsumerSpellingClassifier
{
    public static DataRelativePathAggregateConsumerSpellingEvidence Classify(
        string windowsLogicalPath,
        IReadOnlyList<string> requestedPaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            windowsLogicalPath
        );

        ArgumentNullException.ThrowIfNull(
            requestedPaths
        );

        string canonicalLogicalPath;

        try
        {
            canonicalLogicalPath =
                WindowsLogicalPath
                    .FromRelativePath(
                        windowsLogicalPath
                    )
                    .Value;
        }
        catch (Exception ex)
        {
            throw new ArgumentException(
                $"The Windows-logical path is invalid: {ex.Message}",
                nameof(windowsLogicalPath),
                ex
            );
        }

        if (!string.Equals(
                canonicalLogicalPath,
                windowsLogicalPath,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The Windows-logical path must be in canonical " +
                "WindowsLogicalPath form.",
                nameof(windowsLogicalPath)
            );
        }

        var distinctRequestedPaths =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        foreach (string? requestedPath in requestedPaths)
        {
            if (!WindowsDataRelativePathParser.TryParse(
                    requestedPath,
                    out string[] components,
                    out string? parseError))
            {
                throw new ArgumentException(
                    $"Consumer requested path is invalid: " +
                    $"{parseError ?? "unknown parse error"}",
                    nameof(requestedPaths)
                );
            }

            string normalizedRequestedPath =
                string.Join(
                    '/',
                    components
                );

            WindowsLogicalPath requestedLogicalPath;

            try
            {
                requestedLogicalPath =
                    WindowsLogicalPath.FromRelativePath(
                        normalizedRequestedPath
                    );
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"Consumer requested path cannot be mapped to the " +
                    $"Windows namespace: {ex.Message}",
                    nameof(requestedPaths),
                    ex
                );
            }

            if (!string.Equals(
                    requestedLogicalPath.Value,
                    windowsLogicalPath,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Consumer requested path " +
                    $"'{normalizedRequestedPath}' does not belong to " +
                    $"Windows-logical leaf '{windowsLogicalPath}'.",
                    nameof(requestedPaths)
                );
            }

            distinctRequestedPaths.Add(
                normalizedRequestedPath
            );
        }

        string[] orderedRequestedPaths =
            distinctRequestedPaths
                .OrderBy(
                    path =>
                        path,
                    StringComparer.Ordinal
                )
                .ToArray();

        DataRelativePathAggregateConsumerSpellingState state =
            orderedRequestedPaths.Length switch
            {
                0 =>
                    DataRelativePathAggregateConsumerSpellingState
                        .NoConsumerEvidence,

                1 =>
                    DataRelativePathAggregateConsumerSpellingState
                        .UniqueConsumerSpelling,

                _ =>
                    DataRelativePathAggregateConsumerSpellingState
                        .ConflictingConsumerSpellings
            };

        return new DataRelativePathAggregateConsumerSpellingEvidence(
            WindowsLogicalPath:
                windowsLogicalPath,
            DistinctRequestedPaths:
                orderedRequestedPaths,
            State:
                state
        );
    }
}
