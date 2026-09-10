using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Bethesda.Plugins;

// Global completeness state for consumer-first targeted Bethesda repair
// candidate discovery.
//
// Candidate publication is all-or-nothing at this layer. A targeted physical
// or candidate-evidence failure may retain already-observed leaf diagnostics,
// but it publishes zero aggregate candidates.
public enum SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
{
    Complete,

    IncompleteWinnerSearch,
    IndeterminateConsumerPathEvidence,
    IndeterminateDataRootEvidence,
    IndeterminatePhysicalEvidence,
    IndeterminateCandidateEvidence
}

// Per-logical-leaf diagnostic state.
//
// NoConsumerEvidence and ConflictingConsumerSpellings are intentionally
// resolved before any filesystem work.
//
// Complete and NoPhysicalRepresentation are complete targeted observations.
// The indeterminate states fail the global candidate projection closed.
public enum SkyrimWinningTargetedConsumerCaseRepairLeafState
{
    NoConsumerEvidence,
    ConflictingConsumerSpellings,

    Complete,
    NoPhysicalRepresentation,

    IndeterminatePhysicalEvidence,
    IndeterminateCandidateEvidence
}

// Diagnostic record for one canonical C4D-5B consumer leaf.
//
// TargetedProjection is present only when a UniqueConsumerSpelling was actually
// allowed to reach the targeted physical analyzer and C4E-4B.
public sealed record SkyrimWinningTargetedConsumerCaseRepairLeafProjection(
    DataRelativePathAggregateConsumerSpellingEvidence ConsumerSpelling,
    SkyrimWinningTargetedConsumerCaseRepairLeafState State,
    DataRelativePathTargetedConsumerCaseRepairCandidateProjection?
        TargetedProjection,
    string? Error
)
{
    public DataRelativePathTargetedConsumerCaseRepairCandidate? Candidate =>
        TargetedProjection?.Candidate;
}

// Consumer-first Bethesda candidate-discovery result.
//
// ConsumerSpellingComposition preserves the complete Bethesda consumer
// provenance.
//
// DataRoot is published only after both complete Bethesda source projections
// agree on one canonical absolute Data root.
//
// Candidates are published only when State == Complete.
public sealed record
    SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult(
        SkyrimWinningConsumerSpellingEvidenceCompositionResult
            ConsumerSpellingComposition,
        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState State,
        string? DataRoot,
        IReadOnlyList<
            SkyrimWinningTargetedConsumerCaseRepairLeafProjection
        > Leaves,
        IReadOnlyList<
            DataRelativePathTargetedConsumerCaseRepairCandidate
        > Candidates,
        string? Error
    )
{
    public bool CandidateEvidenceComplete =>
        State ==
        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
            .Complete;

    public int CandidateCount =>
        Candidates.Count;
}

// Manifest-independent Bethesda orchestration.
//
// Order of authority:
//
//   1. C4D-5B global consumer completeness.
//   2. Recomposition of supposedly complete consumer authority.
//   3. Exact canonical agreement on the Data root retained by both Bethesda
//      source projections.
//   4. Consumer state classification.
//   5. Only UniqueConsumerSpelling rows may trigger targeted filesystem work.
//   6. Each unique row uses the retained Data-root descriptor, the existing
//      current-leaf analyzer, then closed C4E-4B.
//
// Incomplete or indeterminate global consumer authority returns before
// dereferencing composed Evidence or Data-root provenance.
//
// ConflictingConsumerSpellings and NoConsumerEvidence never reach the
// filesystem.
//
// The aggregate namespace manifest, C4D-6C, old C4E-2, and old C4E-3 are
// deliberately absent.
//
// This layer grants no planning, persistence, authorization, execution,
// rollback, or recovery authority.
public static class
    SkyrimWinningTargetedConsumerCaseRepairCandidateProjector
{
    public static
        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult
        Project(
            SkyrimWinningConsumerSpellingEvidenceCompositionResult
                consumerSpellingComposition,
            LinuxNoFollowPathHandle? aliasesDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(
            consumerSpellingComposition
        );

        switch (consumerSpellingComposition.State)
        {
            case
                SkyrimWinningConsumerSpellingEvidenceCompositionState
                    .IncompleteWinnerSearch:
                return Terminal(
                    consumerSpellingComposition,
                    SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
                        .IncompleteWinnerSearch,
                    dataRoot:
                        null,
                    leaves:
                        [],
                    error:
                        consumerSpellingComposition.Error
                );

            case
                SkyrimWinningConsumerSpellingEvidenceCompositionState
                    .IndeterminateConsumerPathEvidence:
                return Terminal(
                    consumerSpellingComposition,
                    SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
                        .IndeterminateConsumerPathEvidence,
                    dataRoot:
                        null,
                    leaves:
                        [],
                    error:
                        consumerSpellingComposition.Error
                );

            case
                SkyrimWinningConsumerSpellingEvidenceCompositionState
                    .Complete:
                break;

            default:
                return Terminal(
                    consumerSpellingComposition,
                    SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
                        .IndeterminateConsumerPathEvidence,
                    dataRoot:
                        null,
                    leaves:
                        [],
                    error:
                        "The consumer-spelling composition has an unrecognized " +
                        $"state value: {consumerSpellingComposition.State}."
                );
        }

        if (!TryRecomposeCompleteAuthority(
                consumerSpellingComposition,
                out SkyrimWinningConsumerSpellingEvidenceCompositionResult?
                    canonicalComposition,
                out string? compositionError))
        {
            return Terminal(
                consumerSpellingComposition,
                SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
                    .IndeterminateConsumerPathEvidence,
                dataRoot:
                    null,
                leaves:
                    [],
                error:
                    compositionError
            );
        }

        if (!TryBindCanonicalDataRoot(
                canonicalComposition!,
                out string? dataRoot,
                out string? dataRootError))
        {
            return Terminal(
                canonicalComposition!,
                SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
                    .IndeterminateDataRootEvidence,
                dataRoot:
                    null,
                leaves:
                    [],
                error:
                    dataRootError
            );
        }

        IReadOnlyList<DataRelativePathAggregateConsumerSpellingEvidence>
            consumerEvidence =
                canonicalComposition!.Evidence;

        var rootLogicalByPath =
            new Dictionary<string, string>(
                StringComparer.Ordinal
            );

        foreach (
            DataRelativePathAggregateConsumerSpellingEvidence consumer
            in consumerEvidence)
        {
            switch (consumer.State)
            {
                case
                    DataRelativePathAggregateConsumerSpellingState
                        .NoConsumerEvidence:

                case
                    DataRelativePathAggregateConsumerSpellingState
                        .ConflictingConsumerSpellings:
                    break;

                case
                    DataRelativePathAggregateConsumerSpellingState
                        .UniqueConsumerSpelling:
                {
                    string? requestedPath =
                        consumer.AuthoritativeRequestedPath;

                    if (
                        string.IsNullOrWhiteSpace(
                            requestedPath) ||
                        !TryDeriveRootLogicalPath(
                            requestedPath,
                            out string rootLogical))
                    {
                        return Terminal(
                            canonicalComposition,
                            SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
                                .IndeterminateConsumerPathEvidence,
                            dataRoot,
                            leaves:
                                [],
                            error:
                                $"Unique consumer evidence for " +
                                $"'{consumer.WindowsLogicalPath}' does not " +
                                "contain a targeted-analyzer-compatible " +
                                "requested path."
                        );
                    }

                    rootLogicalByPath.Add(
                        consumer.WindowsLogicalPath,
                        rootLogical
                    );

                    break;
                }

                default:
                    return Terminal(
                        canonicalComposition,
                        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
                            .IndeterminateConsumerPathEvidence,
                        dataRoot,
                        leaves:
                            [],
                        error:
                            $"Consumer evidence for " +
                            $"'{consumer.WindowsLogicalPath}' has an " +
                            $"unrecognized state value: {consumer.State}."
                    );
            }
        }

        if (rootLogicalByPath.Count == 0)
        {
            return CompleteWithoutTargetedLookup(
                canonicalComposition,
                dataRoot!,
                consumerEvidence
            );
        }

        LinuxNoFollowPathOpenResult rootOpen =
            LinuxNoFollowPath.OpenRootReadOnly(
                dataRoot!
            );

        if (
            !rootOpen.Success ||
            rootOpen.OpenedPath is null)
        {
            string error =
                $"The consumer-authoritative Skyrim Data root could not be " +
                $"opened safely ({rootOpen.State}): " +
                (
                    rootOpen.Error ??
                    "no additional error"
                );

            return Terminal(
                canonicalComposition,
                SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
                    .IndeterminatePhysicalEvidence,
                dataRoot,
                BuildUnobservedLeaves(
                    consumerEvidence,
                    error
                ),
                error
            );
        }

        using LinuxNoFollowPathHandle dataRootHandle =
            rootOpen.OpenedPath;

        var leaves =
            new List<
                SkyrimWinningTargetedConsumerCaseRepairLeafProjection
            >(
                consumerEvidence.Count
            );

        var candidates =
            new List<
                DataRelativePathTargetedConsumerCaseRepairCandidate
            >();

        foreach (
            DataRelativePathAggregateConsumerSpellingEvidence consumer
            in consumerEvidence)
        {
            switch (consumer.State)
            {
                case
                    DataRelativePathAggregateConsumerSpellingState
                        .NoConsumerEvidence:
                    leaves.Add(
                        NonTargetedLeaf(
                            consumer,
                            SkyrimWinningTargetedConsumerCaseRepairLeafState
                                .NoConsumerEvidence
                        )
                    );

                    continue;

                case
                    DataRelativePathAggregateConsumerSpellingState
                        .ConflictingConsumerSpellings:
                    leaves.Add(
                        NonTargetedLeaf(
                            consumer,
                            SkyrimWinningTargetedConsumerCaseRepairLeafState
                                .ConflictingConsumerSpellings
                        )
                    );

                    continue;

                case
                    DataRelativePathAggregateConsumerSpellingState
                        .UniqueConsumerSpelling:
                    break;

                default:
                    return Terminal(
                        canonicalComposition,
                        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
                            .IndeterminateConsumerPathEvidence,
                        dataRoot,
                        leaves.ToArray(),
                        $"Consumer evidence for " +
                        $"'{consumer.WindowsLogicalPath}' changed to an " +
                        $"unrecognized state value: {consumer.State}."
                    );
            }

            string requestedPath =
                consumer.AuthoritativeRequestedPath!;

            string rootLogical =
                rootLogicalByPath[
                    consumer.WindowsLogicalPath
                ];

            DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis
                currentLeafAnalysis =
                    DataRelativePathRepairAggregateNamespaceCurrentLeafAnalyzer
                        .Analyze(
                            dataRootHandle,
                            rootLogical,
                            requestedPath,
                            aliasesDirectory
                        );

            DataRelativePathTargetedConsumerCaseRepairCandidateProjection
                targeted;

            try
            {
                targeted =
                    DataRelativePathTargetedConsumerCaseRepairCandidateProjector
                        .Project(
                            dataRoot!,
                            consumer,
                            currentLeafAnalysis
                        );
            }
            catch (ArgumentException ex)
            {
                return CandidateFailure(
                    canonicalComposition,
                    dataRoot!,
                    leaves,
                    consumer,
                    ex.Message
                );
            }
            catch (InvalidOperationException ex)
            {
                return CandidateFailure(
                    canonicalComposition,
                    dataRoot!,
                    leaves,
                    consumer,
                    ex.Message
                );
            }

            SkyrimWinningTargetedConsumerCaseRepairLeafState leafState =
                MapLeafState(
                    targeted.State
                );

            var leaf =
                new SkyrimWinningTargetedConsumerCaseRepairLeafProjection(
                    ConsumerSpelling:
                        consumer,
                    State:
                        leafState,
                    TargetedProjection:
                        targeted,
                    Error:
                        targeted.Error
                );

            leaves.Add(
                leaf
            );

            switch (targeted.State)
            {
                case
                    DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                        .Complete:

                case
                    DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                        .NoPhysicalRepresentation:
                    if (targeted.Candidate is not null)
                    {
                        candidates.Add(
                            targeted.Candidate
                        );
                    }

                    break;

                case
                    DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                        .IndeterminatePhysicalEvidence:
                    return Terminal(
                        canonicalComposition,
                        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
                            .IndeterminatePhysicalEvidence,
                        dataRoot,
                        leaves.ToArray(),
                        targeted.Error ??
                            "Targeted physical evidence is indeterminate."
                    );

                case
                    DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                        .IndeterminateEvidence:
                    return Terminal(
                        canonicalComposition,
                        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
                            .IndeterminateCandidateEvidence,
                        dataRoot,
                        leaves.ToArray(),
                        targeted.Error ??
                            "Targeted candidate evidence is indeterminate."
                    );

                default:
                    return Terminal(
                        canonicalComposition,
                        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
                            .IndeterminateCandidateEvidence,
                        dataRoot,
                        leaves.ToArray(),
                        $"Targeted candidate projection for " +
                        $"'{consumer.WindowsLogicalPath}' has an unrecognized " +
                        $"state value: {targeted.State}."
                    );
            }
        }

        return new(
            ConsumerSpellingComposition:
                canonicalComposition,
            State:
                SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
                    .Complete,
            DataRoot:
                dataRoot,
            Leaves:
                leaves.ToArray(),
            Candidates:
                candidates.ToArray(),
            Error:
                null
        );
    }

    private static bool TryRecomposeCompleteAuthority(
        SkyrimWinningConsumerSpellingEvidenceCompositionResult supplied,
        out SkyrimWinningConsumerSpellingEvidenceCompositionResult? canonical,
        out string? error)
    {
        canonical =
            null;

        error =
            null;

        try
        {
            if (
                supplied.ArmorAddonProjection is null ||
                supplied.HeadPartProjection is null ||
                supplied.Evidence is null ||
                supplied.Error is not null)
            {
                error =
                    "Supposedly complete consumer authority has an incomplete " +
                    "or contradictory result shape.";

                return false;
            }

            SkyrimWinningConsumerSpellingEvidenceCompositionResult recomposed =
                SkyrimWinningConsumerSpellingEvidenceComposer.Compose(
                    supplied.ArmorAddonProjection,
                    supplied.HeadPartProjection
                );

            if (
                recomposed.State !=
                    SkyrimWinningConsumerSpellingEvidenceCompositionState
                        .Complete ||
                recomposed.Evidence is null)
            {
                error =
                    "Supposedly complete consumer authority could not be " +
                    "re-established from its retained Bethesda source " +
                    "projections.";

                return false;
            }

            if (!EvidenceEquivalent(
                    supplied.Evidence,
                    recomposed.Evidence))
            {
                error =
                    "Supposedly complete consumer authority does not match " +
                    "canonical recomposition of its retained Bethesda source " +
                    "projections.";

                return false;
            }

            canonical =
                recomposed;

            return true;
        }
        catch (Exception ex) when (
            ex is ArgumentException or
            InvalidOperationException or
            NullReferenceException)
        {
            error =
                "Supposedly complete consumer authority was malformed: " +
                ex.Message;

            return false;
        }
    }

    private static bool TryBindCanonicalDataRoot(
        SkyrimWinningConsumerSpellingEvidenceCompositionResult composition,
        out string? dataRoot,
        out string? error)
    {
        dataRoot =
            null;

        error =
            null;

        try
        {
            string armorAddonDataRoot =
                composition
                    .ArmorAddonProjection
                    .Scan
                    .Inventory
                    .DataRoot;

            string headPartDataRoot =
                composition
                    .HeadPartProjection
                    .Inventory
                    .DataRoot;

            if (!TryCanonicalAbsolutePath(
                    armorAddonDataRoot,
                    out string? canonicalArmorAddonRoot) ||
                !TryCanonicalAbsolutePath(
                    headPartDataRoot,
                    out string? canonicalHeadPartRoot))
            {
                error =
                    "Complete Bethesda consumer authority does not retain " +
                    "canonical absolute Skyrim Data-root provenance.";

                return false;
            }

            if (!string.Equals(
                    canonicalArmorAddonRoot,
                    canonicalHeadPartRoot,
                    StringComparison.Ordinal))
            {
                error =
                    $"Complete Bethesda consumer authority refers to two " +
                    $"different Skyrim Data roots: " +
                    $"'{canonicalArmorAddonRoot}' and " +
                    $"'{canonicalHeadPartRoot}'.";

                return false;
            }

            dataRoot =
                canonicalArmorAddonRoot;

            return true;
        }
        catch (Exception ex) when (
            ex is ArgumentException or
            InvalidOperationException or
            NullReferenceException)
        {
            error =
                "Complete Bethesda consumer authority has malformed Data-root " +
                $"provenance: {ex.Message}";

            return false;
        }
    }

    private static bool TryCanonicalAbsolutePath(
        string? value,
        out string? canonical)
    {
        canonical =
            null;

        if (
            string.IsNullOrWhiteSpace(
                value) ||
            !Path.IsPathFullyQualified(
                value))
        {
            return false;
        }

        string full;

        try
        {
            full =
                Path.GetFullPath(
                    value
                );
        }
        catch
        {
            return false;
        }

        if (!string.Equals(
                full,
                value,
                StringComparison.Ordinal))
        {
            return false;
        }

        canonical =
            full;

        return true;
    }

    private static bool TryDeriveRootLogicalPath(
        string requestedPath,
        out string rootLogical)
    {
        rootLogical =
            string.Empty;

        int separatorIndex =
            requestedPath.IndexOf(
                '/'
            );

        if (
            separatorIndex <= 0 ||
            separatorIndex ==
                requestedPath.Length - 1)
        {
            return false;
        }

        string rootComponent =
            requestedPath[
                ..separatorIndex
            ];

        if (
            string.IsNullOrEmpty(
                rootComponent) ||
            rootComponent.Contains('\\') ||
            rootComponent.Contains('\0') ||
            rootComponent is "." or "..")
        {
            return false;
        }

        rootLogical =
            rootComponent.ToUpperInvariant();

        return true;
    }

    private static bool EvidenceEquivalent(
        IReadOnlyList<DataRelativePathAggregateConsumerSpellingEvidence> left,
        IReadOnlyList<DataRelativePathAggregateConsumerSpellingEvidence> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (
            int index = 0;
            index < left.Count;
            index++)
        {
            DataRelativePathAggregateConsumerSpellingEvidence? a =
                left[index];

            DataRelativePathAggregateConsumerSpellingEvidence? b =
                right[index];

            if (
                a is null ||
                b is null ||
                !string.Equals(
                    a.WindowsLogicalPath,
                    b.WindowsLogicalPath,
                    StringComparison.Ordinal) ||
                a.State != b.State ||
                a.DistinctRequestedPaths is null ||
                b.DistinctRequestedPaths is null ||
                !a.DistinctRequestedPaths.SequenceEqual(
                    b.DistinctRequestedPaths,
                    StringComparer.Ordinal
                ))
            {
                return false;
            }
        }

        return true;
    }

    private static
        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult
        CompleteWithoutTargetedLookup(
            SkyrimWinningConsumerSpellingEvidenceCompositionResult composition,
            string dataRoot,
            IReadOnlyList<DataRelativePathAggregateConsumerSpellingEvidence>
                evidence)
    {
        SkyrimWinningTargetedConsumerCaseRepairLeafProjection[] leaves =
            evidence
                .Select(
                    consumer =>
                        consumer.State switch
                        {
                            DataRelativePathAggregateConsumerSpellingState
                                .NoConsumerEvidence =>
                                    NonTargetedLeaf(
                                        consumer,
                                        SkyrimWinningTargetedConsumerCaseRepairLeafState
                                            .NoConsumerEvidence
                                    ),

                            DataRelativePathAggregateConsumerSpellingState
                                .ConflictingConsumerSpellings =>
                                    NonTargetedLeaf(
                                        consumer,
                                        SkyrimWinningTargetedConsumerCaseRepairLeafState
                                            .ConflictingConsumerSpellings
                                    ),

                            _ =>
                                throw new InvalidOperationException(
                                    "A supposedly non-targeted complete " +
                                    "consumer set contains a unique consumer " +
                                    "leaf."
                                )
                        }
                )
                .ToArray();

        return new(
            ConsumerSpellingComposition:
                composition,
            State:
                SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
                    .Complete,
            DataRoot:
                dataRoot,
            Leaves:
                leaves,
            Candidates:
                Array.Empty<
                    DataRelativePathTargetedConsumerCaseRepairCandidate
                >(),
            Error:
                null
        );
    }

    private static
        IReadOnlyList<
            SkyrimWinningTargetedConsumerCaseRepairLeafProjection
        >
        BuildUnobservedLeaves(
            IReadOnlyList<DataRelativePathAggregateConsumerSpellingEvidence>
                evidence,
            string error)
    {
        return evidence
            .Select(
                consumer =>
                    consumer.State switch
                    {
                        DataRelativePathAggregateConsumerSpellingState
                            .NoConsumerEvidence =>
                                NonTargetedLeaf(
                                    consumer,
                                    SkyrimWinningTargetedConsumerCaseRepairLeafState
                                        .NoConsumerEvidence
                                ),

                        DataRelativePathAggregateConsumerSpellingState
                            .ConflictingConsumerSpellings =>
                                NonTargetedLeaf(
                                    consumer,
                                    SkyrimWinningTargetedConsumerCaseRepairLeafState
                                        .ConflictingConsumerSpellings
                                ),

                        DataRelativePathAggregateConsumerSpellingState
                            .UniqueConsumerSpelling =>
                                new
                                    SkyrimWinningTargetedConsumerCaseRepairLeafProjection(
                                        ConsumerSpelling:
                                            consumer,
                                        State:
                                            SkyrimWinningTargetedConsumerCaseRepairLeafState
                                                .IndeterminatePhysicalEvidence,
                                        TargetedProjection:
                                            null,
                                        Error:
                                            error
                                    ),

                        _ =>
                            new
                                SkyrimWinningTargetedConsumerCaseRepairLeafProjection(
                                    ConsumerSpelling:
                                        consumer,
                                    State:
                                        SkyrimWinningTargetedConsumerCaseRepairLeafState
                                            .IndeterminateCandidateEvidence,
                                    TargetedProjection:
                                        null,
                                    Error:
                                        error
                                )
                    }
            )
            .ToArray();
    }

    private static
        SkyrimWinningTargetedConsumerCaseRepairLeafProjection
        NonTargetedLeaf(
            DataRelativePathAggregateConsumerSpellingEvidence consumer,
            SkyrimWinningTargetedConsumerCaseRepairLeafState state)
    {
        return new(
            ConsumerSpelling:
                consumer,
            State:
                state,
            TargetedProjection:
                null,
            Error:
                null
        );
    }

    private static SkyrimWinningTargetedConsumerCaseRepairLeafState
        MapLeafState(
            DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                state)
    {
        return state switch
        {
            DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                .Complete =>
                    SkyrimWinningTargetedConsumerCaseRepairLeafState
                        .Complete,

            DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                .NoPhysicalRepresentation =>
                    SkyrimWinningTargetedConsumerCaseRepairLeafState
                        .NoPhysicalRepresentation,

            DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                .IndeterminatePhysicalEvidence =>
                    SkyrimWinningTargetedConsumerCaseRepairLeafState
                        .IndeterminatePhysicalEvidence,

            _ =>
                SkyrimWinningTargetedConsumerCaseRepairLeafState
                    .IndeterminateCandidateEvidence
        };
    }

    private static
        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult
        CandidateFailure(
            SkyrimWinningConsumerSpellingEvidenceCompositionResult composition,
            string dataRoot,
            IReadOnlyList<
                SkyrimWinningTargetedConsumerCaseRepairLeafProjection
            > completedLeaves,
            DataRelativePathAggregateConsumerSpellingEvidence consumer,
            string error)
    {
        var leaves =
            completedLeaves.ToList();

        leaves.Add(
            new SkyrimWinningTargetedConsumerCaseRepairLeafProjection(
                ConsumerSpelling:
                    consumer,
                State:
                    SkyrimWinningTargetedConsumerCaseRepairLeafState
                        .IndeterminateCandidateEvidence,
                TargetedProjection:
                    null,
                Error:
                    error
            )
        );

        return Terminal(
            composition,
            SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
                .IndeterminateCandidateEvidence,
            dataRoot,
            leaves.ToArray(),
            error
        );
    }

    private static
        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult
        Terminal(
            SkyrimWinningConsumerSpellingEvidenceCompositionResult composition,
            SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
                state,
            string? dataRoot,
            IReadOnlyList<
                SkyrimWinningTargetedConsumerCaseRepairLeafProjection
            > leaves,
            string? error)
    {
        return new(
            ConsumerSpellingComposition:
                composition,
            State:
                state,
            DataRoot:
                dataRoot,
            Leaves:
                leaves,
            Candidates:
                Array.Empty<
                    DataRelativePathTargetedConsumerCaseRepairCandidate
                >(),
            Error:
                error
        );
    }
}
