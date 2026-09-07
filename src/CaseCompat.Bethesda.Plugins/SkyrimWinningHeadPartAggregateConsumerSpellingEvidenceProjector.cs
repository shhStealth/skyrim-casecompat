using CaseCompat.Core.Analysis;
using CaseCompat.Core.Repair;

namespace CaseCompat.Bethesda.Plugins;

/*
 * Authority state for projecting genuine winning HeadPart consumer requests
 * into the generic aggregate consumer-spelling model.
 *
 * Complete means winner discovery was complete and every retained winning
 * HeadPart/reference relationship required for this projection was internally
 * valid.
 *
 * IncompleteWinnerSearch deliberately dominates path-local evidence. A partial
 * winning-record population cannot establish authoritative consumer spelling.
 *
 * IndeterminateConsumerPathEvidence means winner discovery was complete, but
 * retained requested-path or winner/reference provenance was malformed or
 * internally inconsistent.
 *
 * Only Complete may publish consumer-spelling evidence.
 */
public enum
    SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionState
{
    Complete,
    IncompleteWinnerSearch,
    IndeterminateConsumerPathEvidence
}

/*
 * Read-only Bethesda -> Core authority projection.
 *
 * Inventory is retained by reference so winning-record provenance and
 * completeness evidence remain recoverable.
 *
 * Evidence contains only generic Core consumer-spelling classification. It
 * grants no filesystem, loose/provider/archive, physical-spelling, source
 * selection, repair planning, persistence, execution, rollback, or recovery
 * authority.
 *
 * This projection intentionally does not emit NoConsumerEvidence for logical
 * leaves absent from the HeadPart consumer population. Establishing that
 * relation requires an external logical-leaf universe and belongs to aggregate
 * composition.
 */
public sealed record
    SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult(
        SkyrimWinningHeadPartInventoryResult Inventory,
        SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionState
            State,
        IReadOnlyList<DataRelativePathAggregateConsumerSpellingEvidence>
            Evidence,
        string? Error
    )
{
    public bool WinnerSearchComplete =>
        Inventory.SearchComplete;

    public bool ConsumerPathEvidenceComplete =>
        State ==
        SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionState
            .Complete;

    public int EvidenceCount =>
        Evidence.Count;
}

/*
 * Pure projection of winning HeadPart Parts[*].FileName consumer requests into
 * C4D aggregate consumer-spelling evidence.
 *
 * Consumer authority comes from the genuine retained
 * SkyrimHeadPartPartReference.DataRelativePath values. Winner/reference FormKey
 * and EditorId provenance is validated before any authority is published.
 *
 * Case-distinct requested paths mapping to the same Windows-logical leaf are
 * combined and delegated to the generic C4D-1 classifier.
 *
 * No filesystem access, namespace acquisition, provider/archive precedence,
 * physical comparison, hashing, repair eligibility, or mutation occurs here.
 */
public static class
    SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjector
{
    public static
        SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult
        Project(
            SkyrimWinningHeadPartInventoryResult inventory)
    {
        ArgumentNullException.ThrowIfNull(
            inventory
        );

        if (inventory.Winners is null)
        {
            throw new ArgumentException(
                "The winning HeadPart inventory must retain its winner " +
                "collection.",
                nameof(inventory)
            );
        }

        /*
         * Incomplete winner discovery outranks every winner/reference-local
         * condition. Never inspect, salvage, or publish partial consumer
         * authority.
         */
        if (!inventory.SearchComplete)
        {
            return new
                SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult(
                    Inventory:
                        inventory,
                    State:
                        SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionState
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
            SkyrimWinningHeadPartRecord? winner
            in inventory.Winners)
        {
            if (winner is null)
            {
                return Indeterminate(
                    inventory,
                    "The winning HeadPart inventory contains a null winner."
                );
            }

            if (string.IsNullOrWhiteSpace(
                    winner.FormKey))
            {
                return Indeterminate(
                    inventory,
                    "A winning HeadPart has no FormKey."
                );
            }

            if (winner.PartReferences is null)
            {
                return Indeterminate(
                    inventory,
                    $"Winning HeadPart '{winner.FormKey}' has no retained " +
                    "part-reference collection."
                );
            }

            foreach (
                SkyrimHeadPartPartReference? reference
                in winner.PartReferences)
            {
                if (reference is null)
                {
                    return Indeterminate(
                        inventory,
                        $"Winning HeadPart '{winner.FormKey}' contains a null " +
                        "part reference."
                    );
                }

                if (!string.Equals(
                        reference.FormKey,
                        winner.FormKey,
                        StringComparison.Ordinal))
                {
                    return Indeterminate(
                        inventory,
                        $"Winning HeadPart '{winner.FormKey}' contains part " +
                        $"reference FormKey '{reference.FormKey}'."
                    );
                }

                if (!string.Equals(
                        reference.EditorId,
                        winner.EditorId,
                        StringComparison.Ordinal))
                {
                    return Indeterminate(
                        inventory,
                        $"Winning HeadPart '{winner.FormKey}' contains part " +
                        "reference EditorId provenance that does not match " +
                        "the winning record."
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
                        $"Winning HeadPart '{winner.FormKey}' contains invalid " +
                        $"consumer requested path '{consumerRequestedPath}': " +
                        $"{parseError}"
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
                        $"Winning HeadPart '{winner.FormKey}' consumer path " +
                        $"'{consumerRequestedPath}' cannot be mapped to the " +
                        $"Windows namespace: {ex.Message}"
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

                /*
                 * Keep the genuine consumer occurrence. C4D-1 owns separator
                 * normalization, exact-spelling deduplication, logical-leaf
                 * verification, and case-conflict classification.
                 */
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
            SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult(
                Inventory:
                    inventory,
                State:
                    SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionState
                        .Complete,
                Evidence:
                    evidence,
                Error:
                    null
            );
    }

    private static
        SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult
        Indeterminate(
            SkyrimWinningHeadPartInventoryResult inventory,
            string error)
    {
        return new
            SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult(
                Inventory:
                    inventory,
                State:
                    SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionState
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
