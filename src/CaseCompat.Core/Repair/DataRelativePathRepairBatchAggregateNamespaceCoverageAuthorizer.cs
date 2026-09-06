using CaseCompat.Core.Analysis;

namespace CaseCompat.Core.Repair;

public sealed record
    DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate(
        DataRelativePathRepairPlanManifestRecord Manifest,
        uint SourceInodeGeneration
    );

public sealed record
    DataRelativePathRepairBatchAggregateNamespaceCoverageEvidence(
        string RootWindowsLogicalPath,
        DataRelativePathAggregateNamespaceManifestReaderResult ReadResult
    );

public enum
    DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
{
    Authorized,

    InvalidBatchManifest,

    InvalidCandidate,
    InvalidCandidateShape,

    InvalidNamespaceEvidenceSet,
    MissingNamespaceReference,
    MissingNamespaceEvidence,
    AmbiguousNamespaceEvidence,

    NamespaceEvidenceReadFailed,
    NamespaceEvidenceSchemaMismatch,
    NamespaceEvidenceShaMismatch,
    NamespaceEvidenceRootMismatch,
    NamespaceEvidenceDataRootMismatch,
    InvalidNamespaceEvidence,

    MissingLogicalLeaf,

    EquivalentContentMultipleRepresentations,
    ConflictingContentMultipleRepresentations,
    UnsupportedLogicalLeafState,

    SourceRepresentationMismatch
}

public sealed record
    DataRelativePathRepairBatchAggregateNamespaceCoverageDecision(
        int CandidateIndex,
        DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
            State,
        string? Error
    )
{
    public bool Authorized =>
        State ==
        DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
            .Authorized;
}

public sealed record
    DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorization(
        IReadOnlyList<
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecision
        > Decisions
    )
{
    public int AuthorizedCount =>
        Decisions.Count(
            decision =>
                decision.Authorized
        );

    public int RejectedCount =>
        Decisions.Count -
        AuthorizedCount;

    public bool AllAuthorized =>
        RejectedCount == 0;
}

/*
 * Pure coverage-policy-v3 authorization against already-read,
 * exact-SHA-bound aggregate namespace evidence.
 *
 * IMPORTANT:
 *
 * - this class performs no filesystem opening, enumeration, hashing,
 *   persistence, planning, repair, execution, rollback, or recovery;
 * - coverage-policy-v2 remains a separate unchanged implementation;
 * - supplied routing keys are not authority by themselves;
 * - only a UniqueRepresentation logical leaf can authorize a candidate;
 * - multiple physical representations never trigger provider precedence;
 * - source correspondence includes the generation-aware evidence that is
 *   intentionally absent from DataRelativePathRepairSourceSnapshot.
 *
 * This grants planning/namespace-coverage authority only. It grants no
 * execution authority.
 */
public static class
    DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorizer
{
    public static
        DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorization
        Authorize(
            DataRelativePathRepairBatchManifestRecord batchManifest,
            IReadOnlyList<
                DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate
            > candidates,
            IReadOnlyList<
                DataRelativePathRepairBatchAggregateNamespaceCoverageEvidence
            > namespaceEvidence)
    {
        ArgumentNullException.ThrowIfNull(
            batchManifest
        );

        ArgumentNullException.ThrowIfNull(
            candidates
        );

        ArgumentNullException.ThrowIfNull(
            namespaceEvidence
        );

        string? batchValidationError;

        try
        {
            batchValidationError =
                DataRelativePathRepairBatchManifest.Validate(
                    batchManifest
                );
        }
        catch (Exception ex)
        {
            batchValidationError =
                ex.Message;
        }

        if (
            batchValidationError is not null ||
            batchManifest.SchemaVersion !=
                DataRelativePathRepairBatchManifestRecord
                    .SchemaVersion4 ||
            batchManifest.CoveragePolicyVersion !=
                DataRelativePathRepairBatchManifestRecord
                    .CoveragePolicyVersion3 ||
            batchManifest.AggregateNamespaceEvidence is null ||
            batchManifest.AggregateNamespaceEvidence.Count == 0)
        {
            return RejectAll(
                candidates.Count,
                DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                    .InvalidBatchManifest,
                batchValidationError ??
                "Coverage-policy-v3 requires a valid schema-v4 batch " +
                "manifest using coverage-policy version 3."
            );
        }

        var referencesByRoot =
            new Dictionary<
                string,
                DataRelativePathRepairBatchAggregateNamespaceEvidenceReference
            >(
                StringComparer.Ordinal
            );

        foreach (
            DataRelativePathRepairBatchAggregateNamespaceEvidenceReference?
                reference
            in batchManifest.AggregateNamespaceEvidence)
        {
            if (
                reference is null ||
                !referencesByRoot.TryAdd(
                    reference.RootWindowsLogicalPath,
                    reference
                ))
            {
                return RejectAll(
                    candidates.Count,
                    DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                        .InvalidBatchManifest,
                    "The schema-v4 batch namespace-evidence reference set " +
                    "is invalid or ambiguous."
                );
            }
        }

        var suppliedByRoot =
            new Dictionary<
                string,
                List<
                    DataRelativePathRepairBatchAggregateNamespaceCoverageEvidence
                >
            >(
                StringComparer.Ordinal
            );

        foreach (
            DataRelativePathRepairBatchAggregateNamespaceCoverageEvidence?
                supplied
            in namespaceEvidence)
        {
            if (
                supplied is null ||
                string.IsNullOrWhiteSpace(
                    supplied.RootWindowsLogicalPath
                ) ||
                !referencesByRoot.ContainsKey(
                    supplied.RootWindowsLogicalPath
                ))
            {
                return RejectAll(
                    candidates.Count,
                    DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                        .InvalidNamespaceEvidenceSet,
                    "The supplied aggregate namespace evidence set contains " +
                    "a null, invalid, or unreferenced logical root."
                );
            }

            if (
                !suppliedByRoot.TryGetValue(
                    supplied.RootWindowsLogicalPath,
                    out List<
                        DataRelativePathRepairBatchAggregateNamespaceCoverageEvidence
                    >? entries))
            {
                entries =
                    [];

                suppliedByRoot.Add(
                    supplied.RootWindowsLogicalPath,
                    entries
                );
            }

            entries.Add(
                supplied
            );
        }

        var decisions =
            new DataRelativePathRepairBatchAggregateNamespaceCoverageDecision?[
                candidates.Count
            ];

        for (
            int index = 0;
            index < candidates.Count;
            index++)
        {
            decisions[index] =
                AuthorizeCandidate(
                    index,
                    batchManifest,
                    candidates[index],
                    referencesByRoot,
                    suppliedByRoot
                );
        }

        return new(
            decisions
                .Select(
                    decision =>
                        decision!
                )
                .ToArray()
        );
    }

    private static
        DataRelativePathRepairBatchAggregateNamespaceCoverageDecision
        AuthorizeCandidate(
            int index,
            DataRelativePathRepairBatchManifestRecord batchManifest,
            DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate?
                candidate,
            IReadOnlyDictionary<
                string,
                DataRelativePathRepairBatchAggregateNamespaceEvidenceReference
            > referencesByRoot,
            IReadOnlyDictionary<
                string,
                List<
                    DataRelativePathRepairBatchAggregateNamespaceCoverageEvidence
                >
            > suppliedByRoot)
    {
        if (
            candidate is null ||
            candidate.Manifest is null)
        {
            return Decision(
                index,
                DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                    .InvalidCandidate,
                "Coverage-policy-v3 requires a non-null candidate and " +
                "plan manifest."
            );
        }

        DataRelativePathRepairPlanManifestRecord manifest =
            candidate.Manifest;

        string? candidateValidationError;

        try
        {
            candidateValidationError =
                DataRelativePathRepairPlanManifest.Validate(
                    manifest
                );
        }
        catch (Exception ex)
        {
            candidateValidationError =
                ex.Message;
        }

        if (candidateValidationError is not null)
        {
            return Decision(
                index,
                DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                    .InvalidCandidate,
                candidateValidationError
            );
        }

        if (
            manifest.SchemaVersion !=
                DataRelativePathRepairPlanManifestRecord
                    .SchemaVersion4 ||
            manifest.ResolvedPrefixSteps is null)
        {
            return Decision(
                index,
                DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                    .InvalidCandidateShape,
                "Coverage-policy-v3 requires a valid schema-v4 aggregate " +
                "alternate-branch child manifest."
            );
        }

        if (
            !string.Equals(
                manifest.DataRoot,
                batchManifest.DataRoot,
                StringComparison.Ordinal))
        {
            return Decision(
                index,
                DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                    .InvalidCandidateShape,
                "The candidate Data root does not match the schema-v4 " +
                "batch Data root."
            );
        }

        string logicalPath;

        try
        {
            logicalPath =
                WindowsLogicalPath
                    .FromRelativePath(
                        manifest.RequestedPath
                    )
                    .Value;
        }
        catch (Exception ex)
        {
            return Decision(
                index,
                DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                    .InvalidCandidateShape,
                $"The candidate requested path cannot be mapped to the " +
                $"Windows namespace: {ex.Message}"
            );
        }

        int rootSeparator =
            logicalPath.IndexOf(
                '/'
            );

        if (rootSeparator <= 0)
        {
            return Decision(
                index,
                DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                    .InvalidCandidateShape,
                "The candidate Windows-logical path does not identify a " +
                "file beneath one direct Data-child namespace root."
            );
        }

        string logicalRoot =
            logicalPath[..rootSeparator];

        if (
            !referencesByRoot.TryGetValue(
                logicalRoot,
                out DataRelativePathRepairBatchAggregateNamespaceEvidenceReference?
                    reference))
        {
            return Decision(
                index,
                DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                    .MissingNamespaceReference,
                $"The schema-v4 batch contains no aggregate namespace " +
                $"evidence reference for logical root '{logicalRoot}'."
            );
        }

        if (
            !suppliedByRoot.TryGetValue(
                logicalRoot,
                out List<
                    DataRelativePathRepairBatchAggregateNamespaceCoverageEvidence
                >? suppliedEntries) ||
            suppliedEntries.Count == 0)
        {
            return Decision(
                index,
                DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                    .MissingNamespaceEvidence,
                $"No already-read aggregate namespace evidence was supplied " +
                $"for logical root '{logicalRoot}'."
            );
        }

        if (suppliedEntries.Count != 1)
        {
            return Decision(
                index,
                DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                    .AmbiguousNamespaceEvidence,
                $"More than one aggregate namespace evidence result was " +
                $"supplied for logical root '{logicalRoot}'."
            );
        }

        DataRelativePathRepairBatchAggregateNamespaceCoverageEvidence supplied =
            suppliedEntries[0];

        DataRelativePathAggregateNamespaceManifestReaderResult? readResult =
            supplied.ReadResult;

        if (
            readResult is null ||
            !readResult.Success ||
            readResult.Manifest is null ||
            readResult.ManifestSha256 is null)
        {
            return Decision(
                index,
                DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                    .NamespaceEvidenceReadFailed,
                readResult?.Error ??
                $"Aggregate namespace evidence for logical root " +
                $"'{logicalRoot}' was not supplied as a successful reader " +
                "result."
            );
        }

        DataRelativePathAggregateNamespaceManifestRecord namespaceManifest =
            readResult.Manifest;

        if (
            namespaceManifest.SchemaVersion !=
                reference.ManifestSchemaVersion)
        {
            return Decision(
                index,
                DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                    .NamespaceEvidenceSchemaMismatch,
                $"Aggregate namespace evidence schema version " +
                $"{namespaceManifest.SchemaVersion} does not match the " +
                $"batch reference version {reference.ManifestSchemaVersion}."
            );
        }

        if (
            !string.Equals(
                readResult.ManifestSha256,
                reference.ManifestSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            return Decision(
                index,
                DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                    .NamespaceEvidenceShaMismatch,
                $"Aggregate namespace evidence SHA-256 for logical root " +
                $"'{logicalRoot}' does not match the schema-v4 batch " +
                "reference."
            );
        }

        if (
            !string.Equals(
                supplied.RootWindowsLogicalPath,
                reference.RootWindowsLogicalPath,
                StringComparison.Ordinal) ||
            !string.Equals(
                namespaceManifest.RootWindowsLogicalPath,
                reference.RootWindowsLogicalPath,
                StringComparison.Ordinal))
        {
            return Decision(
                index,
                DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                    .NamespaceEvidenceRootMismatch,
                $"Aggregate namespace evidence does not bind exactly to " +
                $"logical root '{reference.RootWindowsLogicalPath}'."
            );
        }

        if (
            !string.Equals(
                namespaceManifest.DataRoot,
                batchManifest.DataRoot,
                StringComparison.Ordinal))
        {
            return Decision(
                index,
                DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                    .NamespaceEvidenceDataRootMismatch,
                "Aggregate namespace evidence Data root does not match the " +
                "schema-v4 batch Data root."
            );
        }

        string? namespaceValidationError;

        try
        {
            namespaceValidationError =
                DataRelativePathAggregateNamespaceManifest.Validate(
                    namespaceManifest
                );
        }
        catch (Exception ex)
        {
            namespaceValidationError =
                ex.Message;
        }

        if (namespaceValidationError is not null)
        {
            return Decision(
                index,
                DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                    .InvalidNamespaceEvidence,
                namespaceValidationError
            );
        }

        DataRelativePathAggregateNamespaceManifestLogicalLeaf[]
            matchingLeaves =
                namespaceManifest.LogicalLeaves
                    .Where(
                        leaf =>
                            leaf is not null &&
                            string.Equals(
                                leaf.WindowsLogicalPath,
                                logicalPath,
                                StringComparison.Ordinal
                            )
                    )
                    .Take(
                        2
                    )
                    .ToArray();

        if (matchingLeaves.Length == 0)
        {
            return Decision(
                index,
                DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                    .MissingLogicalLeaf,
                $"Aggregate namespace evidence for logical root " +
                $"'{logicalRoot}' contains no logical leaf '{logicalPath}'."
            );
        }

        if (matchingLeaves.Length != 1)
        {
            return Decision(
                index,
                DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                    .InvalidNamespaceEvidence,
                $"Aggregate namespace evidence contains more than one " +
                $"logical leaf '{logicalPath}'."
            );
        }

        DataRelativePathAggregateNamespaceManifestLogicalLeaf leaf =
            matchingLeaves[0];

        switch (leaf.State)
        {
            case
                DataRelativePathAggregateLogicalLeafState
                    .EquivalentContentMultipleRepresentations:
                return Decision(
                    index,
                    DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                        .EquivalentContentMultipleRepresentations,
                    $"Logical leaf '{logicalPath}' has multiple " +
                    "byte-equivalent physical representations. " +
                    "Coverage-policy-v3 does not select a provider."
                );

            case
                DataRelativePathAggregateLogicalLeafState
                    .ConflictingContentMultipleRepresentations:
                return Decision(
                    index,
                    DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                        .ConflictingContentMultipleRepresentations,
                    $"Logical leaf '{logicalPath}' has conflicting physical " +
                    "content representations."
                );

            case
                DataRelativePathAggregateLogicalLeafState
                    .UniqueRepresentation:
                break;

            default:
                return Decision(
                    index,
                    DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                        .UnsupportedLogicalLeafState,
                    $"Logical leaf '{logicalPath}' has unsupported state " +
                    $"'{leaf.State}'."
                );
        }

        if (
            leaf.PhysicalRepresentations is null ||
            leaf.PhysicalRepresentations.Count != 1 ||
            leaf.PhysicalRepresentations[0] is null ||
            leaf.PhysicalRepresentations[0].Snapshot is null)
        {
            return Decision(
                index,
                DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                    .InvalidNamespaceEvidence,
                $"Unique logical leaf '{logicalPath}' does not contain " +
                "exactly one usable physical representation."
            );
        }

        DataRelativePathAggregateNamespaceManifestFileRepresentation
            representation =
                leaf.PhysicalRepresentations[0];

        DataRelativePathRepairSourceSnapshot source =
            manifest.SourceSnapshot;

        DataRelativePathRepairSourceSnapshot persisted =
            representation.Snapshot;

        string sourceRelative;

        try
        {
            sourceRelative =
                NormalizeRelativePath(
                    Path.GetRelativePath(
                        manifest.DataRoot,
                        source.PhysicalPath
                    )
                );
        }
        catch (
            Exception ex)
            when (
                ex is ArgumentException or
                NotSupportedException or
                PathTooLongException)
        {
            return Decision(
                index,
                DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                    .SourceRepresentationMismatch,
                $"The candidate source path cannot be expressed relative " +
                $"to the batch Data root: {ex.Message}"
            );
        }

        if (
            !string.Equals(
                sourceRelative,
                representation.RelativePath,
                StringComparison.Ordinal) ||
            !string.Equals(
                source.PhysicalPath,
                persisted.PhysicalPath,
                StringComparison.Ordinal) ||
            source.Identity is null ||
            persisted.Identity is null ||
            source.Identity.DeviceMajor !=
                persisted.Identity.DeviceMajor ||
            source.Identity.DeviceMinor !=
                persisted.Identity.DeviceMinor ||
            source.Identity.Inode !=
                persisted.Identity.Inode ||
            source.Identity.MountId !=
                persisted.Identity.MountId ||
            candidate.SourceInodeGeneration !=
                representation.InodeGeneration ||
            source.Size !=
                persisted.Size ||
            !string.Equals(
                source.Sha256,
                persisted.Sha256,
                StringComparison.OrdinalIgnoreCase))
        {
            return Decision(
                index,
                DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                    .SourceRepresentationMismatch,
                $"Candidate source evidence does not exactly match the sole " +
                $"persisted physical representation for logical leaf " +
                $"'{logicalPath}'."
            );
        }

        return Decision(
            index,
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .Authorized,
            error:
                null
        );
    }

    private static string NormalizeRelativePath(
        string relativePath)
    {
        string normalized =
            relativePath.Replace(
                Path.DirectorySeparatorChar,
                '/'
            );

        if (
            Path.AltDirectorySeparatorChar !=
                Path.DirectorySeparatorChar)
        {
            normalized =
                normalized.Replace(
                    Path.AltDirectorySeparatorChar,
                    '/'
                );
        }

        return normalized;
    }

    private static
        DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorization
        RejectAll(
            int candidateCount,
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                state,
            string? error)
    {
        var decisions =
            new DataRelativePathRepairBatchAggregateNamespaceCoverageDecision[
                candidateCount
            ];

        for (
            int index = 0;
            index < candidateCount;
            index++)
        {
            decisions[index] =
                Decision(
                    index,
                    state,
                    error
                );
        }

        return new(
            decisions
        );
    }

    private static
        DataRelativePathRepairBatchAggregateNamespaceCoverageDecision
        Decision(
            int candidateIndex,
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                state,
            string? error)
    {
        return new(
            CandidateIndex:
                candidateIndex,
            State:
                state,
            Error:
                error
        );
    }
}
