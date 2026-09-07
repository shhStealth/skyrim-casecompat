using CaseCompat.Core.Analysis;
using CaseCompat.Core.Repair;

namespace CaseCompat.Bethesda.Plugins;

/*
 * Authority state for projecting genuine winning ArmorAddon requested-path
 * evidence into the generic aggregate consumer-spelling model.
 *
 * Complete means winner discovery was complete and every retained checkpoint-
 * 10C path/reference relationship required for this projection was internally
 * valid.
 *
 * IncompleteWinnerSearch deliberately dominates path-local evidence. A partial
 * winning-record population cannot establish authoritative consumer spelling.
 *
 * IndeterminateConsumerPathEvidence means winner discovery was complete, but
 * the retained requested-path evidence was malformed or internally
 * inconsistent.
 *
 * Only Complete may publish consumer-spelling evidence.
 */
public enum
    SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionState
{
    Complete,
    IncompleteWinnerSearch,
    IndeterminateConsumerPathEvidence
}

/*
 * Read-only Bethesda -> Core authority projection.
 *
 * Scan is retained by reference so the winning-record provenance and
 * completeness evidence remain recoverable.
 *
 * Evidence contains only generic Core consumer-spelling classification. It
 * does not contain loose/provider/archive evidence and grants no physical
 * source selection, repair planning, persistence, execution, rollback, or
 * recovery authority.
 *
 * This projection intentionally does not emit NoConsumerEvidence for logical
 * leaves absent from the Bethesda consumer population. Establishing that
 * relation requires an external logical-leaf universe and belongs to the
 * later aggregate composition layer.
 */
public sealed record
    SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult(
        SkyrimWinningArmorAddonSnapshotEvidenceScanResult Scan,
        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionState
            State,
        IReadOnlyList<DataRelativePathAggregateConsumerSpellingEvidence>
            Evidence,
        string? Error
    )
{
    public bool WinnerSearchComplete =>
        Scan.WinnerSearchComplete;

    /*
     * True only when the Bethesda consumer-path population was complete and
     * structurally valid enough to publish the Evidence collection.
     *
     * This does not mean every emitted logical leaf has one authoritative
     * requested spelling. A complete projection may legitimately contain
     * ConflictingConsumerSpellings; C4D-1 retains that leaf-local authority.
     */
    public bool ConsumerPathEvidenceComplete =>
        State ==
        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionState
            .Complete;

    public int EvidenceCount =>
        Evidence.Count;
}

/*
 * Pure projection of checkpoint-10C winning ArmorAddon consumer requests into
 * C4D aggregate consumer-spelling evidence.
 *
 * Consumer authority comes from the genuine retained
 * SkyrimArmorAddonModelReference.DataRelativePath values. Path-level
 * checkpoint-10C RequestedPath is validated against every retained reference
 * rather than substituted for consumer provenance.
 *
 * Case-distinct requested paths that map to the same Windows-logical leaf are
 * intentionally combined and delegated to the generic C4D-1 classifier,
 * which decides whether those consumers agree on one exact spelling.
 *
 * Snapshot lookup state is deliberately irrelevant here. A valid authoritative
 * consumer path remains consumer evidence whether loose lookup resolved,
 * failed, or was indeterminate for some non-path-syntax reason.
 *
 * No filesystem access, namespace acquisition, loose/provider/archive
 * precedence, physical-spelling comparison, hashing, repair eligibility, or
 * mutation occurs here.
 */
public static class
    SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjector
{
    public static
        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult
        Project(
            SkyrimWinningArmorAddonSnapshotEvidenceScanResult scan)
    {
        ArgumentNullException.ThrowIfNull(
            scan
        );

        if (scan.Inventory is null)
        {
            throw new ArgumentException(
                "The checkpoint-10C scan must retain its winning inventory.",
                nameof(scan)
            );
        }

        if (scan.Paths is null)
        {
            throw new ArgumentException(
                "The checkpoint-10C scan must retain its path collection.",
                nameof(scan)
            );
        }

        /*
         * Match the established Bethesda diagnostic precedence:
         * incomplete winner discovery outranks every path-local condition.
         *
         * Do not inspect, salvage, or publish partial consumer authority.
         */
        if (!scan.WinnerSearchComplete)
        {
            return new
                SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult(
                    Scan:
                        scan,
                    State:
                        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionState
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

        /*
         * A genuine checkpoint-10C scan contains exactly one path object for
         * each exact requested DataRelativePath spelling.
         */
        var exactPathGroups =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        foreach (
            SkyrimWinningArmorAddonSnapshotPathEvidence? path
            in scan.Paths)
        {
            if (path is null)
            {
                return Indeterminate(
                    scan,
                    "The checkpoint-10C path collection contains a null path."
                );
            }

            if (string.IsNullOrWhiteSpace(path.RequestedPath))
            {
                return Indeterminate(
                    scan,
                    "A checkpoint-10C path has no requested path."
                );
            }

            if (!exactPathGroups.Add(path.RequestedPath))
            {
                return Indeterminate(
                    scan,
                    $"Checkpoint-10C contains duplicate exact requested-path " +
                    $"group '{path.RequestedPath}'."
                );
            }

            if (path.References is null ||
                path.References.Count == 0)
            {
                return Indeterminate(
                    scan,
                    $"Checkpoint-10C requested path '{path.RequestedPath}' " +
                    "does not retain any consumer reference context."
                );
            }

            if (!WindowsDataRelativePathParser.TryParse(
                    path.RequestedPath,
                    out string[] components,
                    out string? parseError))
            {
                return Indeterminate(
                    scan,
                    $"Checkpoint-10C contains invalid requested path " +
                    $"'{path.RequestedPath}': {parseError}"
                );
            }

            string normalizedRequestedPath =
                string.Join(
                    "/",
                    components
                );

            string windowsLogicalPath =
                WindowsLogicalPath
                    .FromRelativePath(
                        normalizedRequestedPath
                    )
                    .Value;

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

            foreach (
                SkyrimWinningArmorAddonSnapshotReferenceContext? context
                in path.References)
            {
                if (context is null ||
                    context.Reference is null)
                {
                    return Indeterminate(
                        scan,
                        $"Checkpoint-10C requested path " +
                        $"'{path.RequestedPath}' contains a null consumer " +
                        "reference context."
                    );
                }

                string consumerRequestedPath =
                    context.Reference.DataRelativePath;

                if (!string.Equals(
                        consumerRequestedPath,
                        path.RequestedPath,
                        StringComparison.Ordinal))
                {
                    return Indeterminate(
                        scan,
                        $"Checkpoint-10C requested-path group " +
                        $"'{path.RequestedPath}' does not exactly match " +
                        $"retained consumer path '{consumerRequestedPath}'."
                    );
                }

                /*
                 * Keep the genuine consumer occurrence here. C4D-1 owns
                 * separator normalization, exact-spelling deduplication,
                 * logical-leaf verification, and case-conflict
                 * classification.
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
            SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult(
                Scan:
                    scan,
                State:
                    SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionState
                        .Complete,
                Evidence:
                    evidence,
                Error:
                    null
            );
    }

    private static
        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult
        Indeterminate(
            SkyrimWinningArmorAddonSnapshotEvidenceScanResult scan,
            string error)
    {
        return new
            SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult(
                Scan:
                    scan,
                State:
                    SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionState
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
