namespace CaseCompat.Core.Repair;

// Completeness state for one manifest-independent targeted consumer-case
// candidate projection.
//
// NoPhysicalRepresentation is complete evidence: an authoritative consumer
// path was established, but no loose case-equivalent regular-file provider
// exists for that logical leaf.
//
// It must never be reinterpreted as NoConsumerEvidence.
public enum DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
{
    Complete,
    NoPhysicalRepresentation,

    IndeterminatePhysicalEvidence,
    IndeterminateEvidence
}

// Source-bound consumer-authoritative case-repair candidate derived from
// targeted current physical evidence rather than an aggregate namespace
// manifest.
//
// This descriptor is a candidate only. It grants no repair-plan, persistence,
// coverage, authorization, execution, rollback, or recovery authority.
public sealed record DataRelativePathTargetedConsumerCaseRepairCandidate(
    DataRelativePathAggregateConsumerPhysicalSpellingEvidence SpellingEvidence,
    DataRelativePathTargetedPhysicalLeafProjection PhysicalProjection,
    DataRelativePathTargetedPhysicalFileRepresentation SourceRepresentation
)
{
    public string WindowsLogicalPath =>
        SpellingEvidence.WindowsLogicalPath;

    public string AuthoritativeRequestedPath =>
        SpellingEvidence.AuthoritativeRequestedPath!;

    public DataRelativePathRepairSourceSnapshot SourceSnapshot =>
        SourceRepresentation.Snapshot;

    public uint SourceInodeGeneration =>
        SourceRepresentation.InodeGeneration;
}

// Read-only C4E-4B result.
//
// ConsumerSpelling remains the authoritative requested-spelling evidence.
// PhysicalProjection is the C4E-4A manifest-independent physical/content
// evidence.
//
// SpellingEvidence and PolicyState are absent when no loose physical
// representation exists or when the evidence is indeterminate.
public sealed record
    DataRelativePathTargetedConsumerCaseRepairCandidateProjection(
        DataRelativePathAggregateConsumerSpellingEvidence ConsumerSpelling,
        DataRelativePathTargetedPhysicalLeafProjection PhysicalProjection,
        DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
            State,
        DataRelativePathAggregateConsumerPhysicalSpellingEvidence?
            SpellingEvidence,
        DataRelativePathAggregateConsumerCaseRepairPolicyState? PolicyState,
        DataRelativePathTargetedConsumerCaseRepairCandidate? Candidate,
        string? Error
    )
{
    public bool CandidateEvidenceComplete =>
        State is
            DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                .Complete or
            DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                .NoPhysicalRepresentation;

    public bool HasCandidate =>
        Candidate is not null;
}

// Manifest-independent C4E candidate projection for one already-targeted
// current logical leaf.
//
// Composition:
//   canonical consumer spelling
//       + C4E-4A targeted physical/content projection
//       + C4D-2 spelling relation
//       + C4E-1 policy
//       -> optional source-bound candidate
//
// No aggregate namespace manifest, sidecar projector, filesystem enumeration,
// hashing, source reacquisition, persistence, planning, authorization, or
// mutation occurs here.
//
// The supplied current-leaf analysis has already performed the filesystem
// observation. C4E-4A only adapts that completed evidence.
public static class
    DataRelativePathTargetedConsumerCaseRepairCandidateProjector
{
    public static
        DataRelativePathTargetedConsumerCaseRepairCandidateProjection
        Project(
            string dataRoot,
            DataRelativePathAggregateConsumerSpellingEvidence consumerSpelling,
            DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis
                currentLeafAnalysis)
    {
        if (string.IsNullOrWhiteSpace(
                dataRoot))
        {
            throw new ArgumentException(
                "A Data-root path is required.",
                nameof(dataRoot)
            );
        }

        ArgumentNullException.ThrowIfNull(
            consumerSpelling
        );

        ArgumentNullException.ThrowIfNull(
            currentLeafAnalysis
        );

        DataRelativePathAggregateConsumerSpellingEvidence validatedConsumer;

        try
        {
            validatedConsumer =
                ValidateConsumerSpelling(
                    consumerSpelling
                );
        }
        catch (ArgumentException ex)
        {
            return IndeterminateWithoutPhysicalProjection(
                consumerSpelling,
                currentLeafAnalysis,
                dataRoot,
                ex.Message
            );
        }

        DataRelativePathTargetedPhysicalLeafProjection physicalProjection;

        try
        {
            physicalProjection =
                DataRelativePathTargetedPhysicalLeafProjector.Project(
                    dataRoot,
                    currentLeafAnalysis
                );
        }
        catch (ArgumentException)
        {
            throw;
        }

        switch (physicalProjection.State)
        {
            case
                DataRelativePathTargetedPhysicalLeafProjectionState
                    .CurrentLeafAnalysisFailed:
                return Result(
                    validatedConsumer,
                    physicalProjection,
                    DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                        .IndeterminatePhysicalEvidence,
                    spellingEvidence:
                        null,
                    policyState:
                        null,
                    candidate:
                        null,
                    error:
                        physicalProjection.Error ??
                        physicalProjection.State.ToString()
                );

            case
                DataRelativePathTargetedPhysicalLeafProjectionState
                    .InvalidEvidence:
                return Result(
                    validatedConsumer,
                    physicalProjection,
                    DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                        .IndeterminateEvidence,
                    spellingEvidence:
                        null,
                    policyState:
                        null,
                    candidate:
                        null,
                    error:
                        physicalProjection.Error ??
                        physicalProjection.State.ToString()
                );

            case
                DataRelativePathTargetedPhysicalLeafProjectionState
                    .NoPhysicalRepresentation:

            case
                DataRelativePathTargetedPhysicalLeafProjectionState
                    .Projected:
                break;

            default:
                return Result(
                    validatedConsumer,
                    physicalProjection,
                    DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                        .IndeterminateEvidence,
                    spellingEvidence:
                        null,
                    policyState:
                        null,
                    candidate:
                        null,
                    error:
                        $"Targeted physical projection has an unrecognized " +
                        $"state value: {physicalProjection.State}."
                );
        }

        try
        {
            ValidateLogicalCorrespondence(
                validatedConsumer,
                physicalProjection
            );

            if (
                physicalProjection.State ==
                DataRelativePathTargetedPhysicalLeafProjectionState
                    .NoPhysicalRepresentation)
            {
                ValidateNoPhysicalRepresentationShape(
                    physicalProjection
                );

                return Result(
                    validatedConsumer,
                    physicalProjection,
                    DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                        .NoPhysicalRepresentation,
                    spellingEvidence:
                        null,
                    policyState:
                        null,
                    candidate:
                        null,
                    error:
                        null
                );
            }

            ValidateProjectedPhysicalShape(
                physicalProjection
            );

            DataRelativePathAggregateConsumerPhysicalSpellingEvidence
                spellingEvidence =
                    DataRelativePathAggregateConsumerPhysicalSpellingClassifier
                        .Classify(
                            validatedConsumer,
                            physicalProjection
                                .PhysicalRepresentations
                                .Select(
                                    representation =>
                                        representation.RelativePath
                                )
                                .ToArray()
                        );

            DataRelativePathAggregateConsumerCaseRepairPolicyState
                policyState =
                    DataRelativePathAggregateConsumerCaseRepairPolicyClassifier
                        .Classify(
                            physicalProjection.PhysicalState!.Value,
                            spellingEvidence.State
                        );

            if (
                policyState ==
                DataRelativePathAggregateConsumerCaseRepairPolicyState
                    .IndeterminateEvidence)
            {
                return Result(
                    validatedConsumer,
                    physicalProjection,
                    DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                        .IndeterminateEvidence,
                    spellingEvidence,
                    policyState,
                    candidate:
                        null,
                    error:
                        "Targeted consumer/physical evidence produced " +
                        "indeterminate consumer-case repair policy."
                );
            }

            DataRelativePathTargetedConsumerCaseRepairCandidate? candidate =
                policyState ==
                    DataRelativePathAggregateConsumerCaseRepairPolicyState
                        .UniqueRepresentationConsumerCaseMismatchCandidate
                    ? BindUniqueRepresentationCandidate(
                        physicalProjection,
                        spellingEvidence
                    )
                    : null;

            return Result(
                validatedConsumer,
                physicalProjection,
                DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                    .Complete,
                spellingEvidence,
                policyState,
                candidate,
                error:
                    null
            );
        }
        catch (ArgumentException ex)
        {
            return Result(
                validatedConsumer,
                physicalProjection,
                DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                    .IndeterminateEvidence,
                spellingEvidence:
                    null,
                policyState:
                    null,
                candidate:
                    null,
                error:
                    ex.Message
            );
        }
        catch (InvalidOperationException ex)
        {
            return Result(
                validatedConsumer,
                physicalProjection,
                DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                    .IndeterminateEvidence,
                spellingEvidence:
                    null,
                policyState:
                    null,
                candidate:
                    null,
                error:
                    ex.Message
            );
        }
    }

    private static DataRelativePathAggregateConsumerSpellingEvidence
        ValidateConsumerSpelling(
            DataRelativePathAggregateConsumerSpellingEvidence consumerSpelling)
    {
        if (consumerSpelling.DistinctRequestedPaths is null)
        {
            throw new ArgumentException(
                "Consumer spelling evidence has no requested-path collection.",
                nameof(consumerSpelling)
            );
        }

        DataRelativePathAggregateConsumerSpellingEvidence validated =
            DataRelativePathAggregateConsumerSpellingClassifier.Classify(
                consumerSpelling.WindowsLogicalPath,
                consumerSpelling.DistinctRequestedPaths
            );

        if (
            validated.State !=
                consumerSpelling.State ||
            !validated.DistinctRequestedPaths.SequenceEqual(
                consumerSpelling.DistinctRequestedPaths,
                StringComparer.Ordinal
            ))
        {
            throw new ArgumentException(
                "Consumer spelling evidence is not in canonical classifier " +
                "form.",
                nameof(consumerSpelling)
            );
        }

        return validated;
    }

    private static void ValidateLogicalCorrespondence(
        DataRelativePathAggregateConsumerSpellingEvidence consumerSpelling,
        DataRelativePathTargetedPhysicalLeafProjection physicalProjection)
    {
        if (physicalProjection.CurrentLeafAnalysis is null)
        {
            throw new InvalidOperationException(
                "Targeted physical projection has no current-leaf provenance."
            );
        }

        string observedLogicalPath =
            physicalProjection.CurrentLeafAnalysis.LogicalPath.Value;

        if (!string.Equals(
                consumerSpelling.WindowsLogicalPath,
                observedLogicalPath,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Consumer logical leaf '{consumerSpelling.WindowsLogicalPath}' " +
                $"does not match targeted physical logical leaf " +
                $"'{observedLogicalPath}'."
            );
        }
    }

    private static void ValidateNoPhysicalRepresentationShape(
        DataRelativePathTargetedPhysicalLeafProjection physicalProjection)
    {
        if (
            physicalProjection.PhysicalRepresentations is null ||
            physicalProjection.PhysicalRepresentations.Count != 0 ||
            physicalProjection.LogicalLeaf is not null ||
            physicalProjection.PhysicalState is not null)
        {
            throw new InvalidOperationException(
                "NoPhysicalRepresentation targeted evidence has an " +
                "inconsistent physical shape."
            );
        }
    }

    private static void ValidateProjectedPhysicalShape(
        DataRelativePathTargetedPhysicalLeafProjection physicalProjection)
    {
        if (
            physicalProjection.PhysicalRepresentations is null ||
            physicalProjection.PhysicalRepresentations.Count == 0 ||
            physicalProjection.LogicalLeaf is null ||
            physicalProjection.PhysicalState is null)
        {
            throw new InvalidOperationException(
                "Projected targeted physical evidence does not contain a " +
                "complete physical logical leaf."
            );
        }

        DataRelativePathAggregateLogicalLeaf reclassified =
            DataRelativePathAggregateLogicalLeafClassifier.Classify(
                physicalProjection.CurrentLeafAnalysis.LogicalPath.Value,
                physicalProjection
                    .PhysicalRepresentations
                    .Select(
                        representation =>
                            representation.Snapshot
                    )
                    .ToArray()
            );

        if (
            reclassified.State !=
            physicalProjection.LogicalLeaf.State)
        {
            throw new InvalidOperationException(
                "Projected targeted physical topology is not in canonical " +
                "classifier form."
            );
        }
    }

    private static DataRelativePathTargetedConsumerCaseRepairCandidate
        BindUniqueRepresentationCandidate(
            DataRelativePathTargetedPhysicalLeafProjection physicalProjection,
            DataRelativePathAggregateConsumerPhysicalSpellingEvidence
                spellingEvidence)
    {
        if (
            spellingEvidence.State !=
                DataRelativePathAggregateConsumerPhysicalSpellingState
                    .ConsumerCaseMismatch ||
            string.IsNullOrWhiteSpace(
                spellingEvidence.AuthoritativeRequestedPath) ||
            spellingEvidence.PhysicalRelativePaths is null ||
            spellingEvidence.PhysicalRelativePaths.Count != 1 ||
            physicalProjection.PhysicalRepresentations.Count != 1)
        {
            throw new InvalidOperationException(
                "Unique consumer-case candidate evidence is not bound to " +
                "exactly one physical representation."
            );
        }

        DataRelativePathTargetedPhysicalFileRepresentation source =
            physicalProjection.PhysicalRepresentations[0];

        if (
            !string.Equals(
                spellingEvidence.PhysicalRelativePaths[0],
                source.RelativePath,
                StringComparison.Ordinal) ||
            string.Equals(
                spellingEvidence.AuthoritativeRequestedPath,
                source.RelativePath,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Unique consumer-case candidate evidence does not describe " +
                "one exact consumer-to-physical casing mismatch."
            );
        }

        return new(
            SpellingEvidence:
                spellingEvidence,
            PhysicalProjection:
                physicalProjection,
            SourceRepresentation:
                source
        );
    }

    private static
        DataRelativePathTargetedConsumerCaseRepairCandidateProjection
        IndeterminateWithoutPhysicalProjection(
            DataRelativePathAggregateConsumerSpellingEvidence consumerSpelling,
            DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis
                currentLeafAnalysis,
            string dataRoot,
            string error)
    {
        DataRelativePathTargetedPhysicalLeafProjection physicalProjection;

        try
        {
            physicalProjection =
                DataRelativePathTargetedPhysicalLeafProjector.Project(
                    dataRoot,
                    currentLeafAnalysis
                );
        }
        catch
        {
            physicalProjection =
                new DataRelativePathTargetedPhysicalLeafProjection(
                    CurrentLeafAnalysis:
                        currentLeafAnalysis,
                    State:
                        DataRelativePathTargetedPhysicalLeafProjectionState
                            .InvalidEvidence,
                    PhysicalRepresentations:
                        Array.Empty<
                            DataRelativePathTargetedPhysicalFileRepresentation
                        >(),
                    LogicalLeaf:
                        null,
                    Error:
                        "Physical projection was not evaluated because " +
                        "consumer evidence was invalid."
                );
        }

        return Result(
            consumerSpelling,
            physicalProjection,
            DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                .IndeterminateEvidence,
            spellingEvidence:
                null,
            policyState:
                null,
            candidate:
                null,
            error
        );
    }

    private static
        DataRelativePathTargetedConsumerCaseRepairCandidateProjection
        Result(
            DataRelativePathAggregateConsumerSpellingEvidence consumerSpelling,
            DataRelativePathTargetedPhysicalLeafProjection physicalProjection,
            DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                state,
            DataRelativePathAggregateConsumerPhysicalSpellingEvidence?
                spellingEvidence,
            DataRelativePathAggregateConsumerCaseRepairPolicyState? policyState,
            DataRelativePathTargetedConsumerCaseRepairCandidate? candidate,
            string? error)
    {
        return new(
            ConsumerSpelling:
                consumerSpelling,
            PhysicalProjection:
                physicalProjection,
            State:
                state,
            SpellingEvidence:
                spellingEvidence,
            PolicyState:
                policyState,
            Candidate:
                candidate,
            Error:
                error
        );
    }
}
