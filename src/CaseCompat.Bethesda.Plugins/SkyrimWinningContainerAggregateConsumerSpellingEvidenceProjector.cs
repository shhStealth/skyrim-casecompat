using CaseCompat.Core.Analysis;
using CaseCompat.Core.Repair;

namespace CaseCompat.Bethesda.Plugins;

// Authority state for projecting genuine winning Container consumer
// requests into the generic aggregate consumer-spelling model. Mirrors
// SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionState's
// precedence exactly.
public enum
    SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionState
{
    Complete,
    IncompleteWinnerSearch,
    IndeterminateConsumerPathEvidence
}

public sealed record
    SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionResult(
        SkyrimWinningContainerInventoryResult Inventory,
        SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionState
            State,
        IReadOnlyList<DataRelativePathAggregateConsumerSpellingEvidence>
            Evidence,
        string? Error
    ) : ISkyrimWinningConsumerSpellingEvidenceSource
{
    public string SourceName =>
        "Container";

    public string DataRoot =>
        Inventory.DataRoot;

    public bool WinnerSearchComplete =>
        Inventory.SearchComplete;

    public bool ConsumerPathEvidenceComplete =>
        State ==
        SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionState
            .Complete;

    public int EvidenceCount =>
        Evidence.Count;
}

// Pure projection of winning Container Model consumer requests into
// aggregate consumer-spelling evidence. Mirrors
// SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjector's logic
// exactly, adapted for a single Model reference per record instead of a
// Parts list.
public static class
    SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjector
{
    public static
        SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionResult
        Project(
            SkyrimWinningContainerInventoryResult inventory)
    {
        ArgumentNullException.ThrowIfNull(
            inventory
        );

        if (inventory.Winners is null)
        {
            throw new ArgumentException(
                "The winning Container inventory must retain its winner " +
                "collection.",
                nameof(inventory)
            );
        }

        if (!inventory.SearchComplete)
        {
            return new
                SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionResult(
                    Inventory:
                        inventory,
                    State:
                        SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionState
                            .IncompleteWinnerSearch,
                    Evidence:
                        Array.Empty<
                            DataRelativePathAggregateConsumerSpellingEvidence
                        >(),
                    Error:
                        null
                );
        }

        var requestedPathsByLogicalPath =
            new Dictionary<string, List<string>>(
                StringComparer.Ordinal
            );

        foreach (
            SkyrimWinningContainerRecord? winner
            in inventory.Winners)
        {
            if (winner is null)
            {
                return Indeterminate(
                    inventory,
                    "The winning Container inventory contains a null winner."
                );
            }

            if (string.IsNullOrWhiteSpace(
                    winner.FormKey))
            {
                return Indeterminate(
                    inventory,
                    "A winning Container has no FormKey."
                );
            }

            if (winner.ModelReferences is null)
            {
                return Indeterminate(
                    inventory,
                    $"Winning Container '{winner.FormKey}' has no retained " +
                    "model-reference collection."
                );
            }

            foreach (
                SkyrimContainerModelReference? reference
                in winner.ModelReferences)
            {
                if (reference is null)
                {
                    return Indeterminate(
                        inventory,
                        $"Winning Container '{winner.FormKey}' contains a " +
                        "null model reference."
                    );
                }

                if (!string.Equals(
                        reference.FormKey,
                        winner.FormKey,
                        StringComparison.Ordinal))
                {
                    return Indeterminate(
                        inventory,
                        $"Winning Container '{winner.FormKey}' contains a " +
                        $"model reference FormKey '{reference.FormKey}'."
                    );
                }

                if (!string.Equals(
                        reference.EditorId,
                        winner.EditorId,
                        StringComparison.Ordinal))
                {
                    return Indeterminate(
                        inventory,
                        $"Winning Container '{winner.FormKey}' contains a " +
                        "model reference EditorId provenance that does not " +
                        "match the winning record."
                    );
                }

                string consumerRequestedPath =
                    reference.DataRelativePath;

                if (!WindowsDataRelativePathParser.TryParse(
                        consumerRequestedPath,
                        out string[] components,
                        out string? parseError))
                {
                    return Indeterminate(
                        inventory,
                        $"Winning Container '{winner.FormKey}' contains " +
                        $"invalid consumer requested path " +
                        $"'{consumerRequestedPath}': {parseError}"
                    );
                }

                string normalizedRequestedPath =
                    string.Join(
                        "/",
                        components
                    );

                string windowsLogicalPath;

                try
                {
                    windowsLogicalPath =
                        WindowsLogicalPath
                            .FromRelativePath(
                                normalizedRequestedPath
                            )
                            .Value;
                }
                catch (Exception ex)
                {
                    return Indeterminate(
                        inventory,
                        $"Winning Container '{winner.FormKey}' consumer " +
                        $"path '{consumerRequestedPath}' cannot be mapped " +
                        $"to the Windows namespace: {ex.Message}"
                    );
                }

                if (!requestedPathsByLogicalPath.TryGetValue(
                        windowsLogicalPath,
                        out List<string>? requestedPaths))
                {
                    requestedPaths =
                        new List<string>();

                    requestedPathsByLogicalPath.Add(
                        windowsLogicalPath,
                        requestedPaths
                    );
                }

                requestedPaths.Add(
                    consumerRequestedPath
                );
            }
        }

        DataRelativePathAggregateConsumerSpellingEvidence[] evidence =
            requestedPathsByLogicalPath
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

        return new
            SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionResult(
                Inventory:
                    inventory,
                State:
                    SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionState
                        .Complete,
                Evidence:
                    evidence,
                Error:
                    null
            );
    }

    private static
        SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionResult
        Indeterminate(
            SkyrimWinningContainerInventoryResult inventory,
            string error)
    {
        return new
            SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionResult(
                Inventory:
                    inventory,
                State:
                    SkyrimWinningContainerAggregateConsumerSpellingEvidenceProjectionState
                        .IndeterminateConsumerPathEvidence,
                Evidence:
                    Array.Empty<
                        DataRelativePathAggregateConsumerSpellingEvidence
                    >(),
                Error:
                    error
            );
    }
}
