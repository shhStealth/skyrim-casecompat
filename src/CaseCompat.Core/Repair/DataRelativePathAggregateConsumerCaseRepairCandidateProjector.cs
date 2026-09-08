namespace CaseCompat.Core.Repair;

// Source-bound aggregate candidate derived only from:
//
// 1. validated durable aggregate namespace evidence;
// 2. authoritative consumer spelling evidence;
// 3. the C4E-1 consumer-case repair policy.
//
// SourceRepresentation is the sole physical representation already persisted
// for a UniqueRepresentation logical leaf. Retaining the complete manifest
// representation keeps RelativePath, SourceSnapshot, and InodeGeneration
// together.
//
// This is a candidate descriptor only. It grants no repair-plan,
// namespace-coverage, persistence, apply, execution, rollback, or recovery
// authority.
public sealed record DataRelativePathAggregateConsumerCaseRepairCandidate(
    DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingEvidence
        Evidence,
    DataRelativePathAggregateNamespaceManifestFileRepresentation
        SourceRepresentation
)
{
    public string WindowsLogicalPath =>
        Evidence.ManifestLeaf.WindowsLogicalPath;

    public string AuthoritativeRequestedPath =>
        Evidence.SpellingEvidence.AuthoritativeRequestedPath!;

    public DataRelativePathRepairSourceSnapshot SourceSnapshot =>
        SourceRepresentation.Snapshot;

    public uint SourceInodeGeneration =>
        SourceRepresentation.InodeGeneration;
}

// Pure C4E-2 projection.
//
// The validated aggregate manifest remains the evidence universe. C4D-6B is
// invoked first so manually supplied consumer rows cannot bypass aggregate
// manifest validation or the canonical consumer/physical spelling join.
//
// Only the exact C4E-1
// UniqueRepresentationConsumerCaseMismatchCandidate state is projected.
//
// Binding the only physical representation of a UniqueRepresentation leaf is
// deterministic and establishes no provider precedence. Multiple physical
// representations remain outside C4E-2.
//
// No filesystem access, hashing, source reacquisition, plan construction,
// authorization, persistence, or mutation occurs here.
public static class
    DataRelativePathAggregateConsumerCaseRepairCandidateProjector
{
    public static IReadOnlyList<
        DataRelativePathAggregateConsumerCaseRepairCandidate
    > Project(
        DataRelativePathAggregateNamespaceManifestRecord manifest,
        IReadOnlyList<DataRelativePathAggregateConsumerSpellingEvidence>
            consumerSpellings)
    {
        ArgumentNullException.ThrowIfNull(
            manifest
        );

        ArgumentNullException.ThrowIfNull(
            consumerSpellings
        );

        IReadOnlyList<
            DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingEvidence
        > joinedEvidence =
            DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingProjector
                .Project(
                    manifest,
                    consumerSpellings
                );

        var candidates =
            new List<
                DataRelativePathAggregateConsumerCaseRepairCandidate
            >();

        foreach (
            DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingEvidence
                evidence
            in joinedEvidence)
        {
            DataRelativePathAggregateConsumerCaseRepairPolicyState
                policyState =
                    DataRelativePathAggregateConsumerCaseRepairPolicyClassifier
                        .Classify(
                            evidence.ManifestLeaf.State,
                            evidence.SpellingEvidence.State
                        );

            switch (policyState)
            {
                case
                    DataRelativePathAggregateConsumerCaseRepairPolicyState
                        .UniqueRepresentationConsumerCaseMismatchCandidate:
                    candidates.Add(
                        BindUniqueRepresentationCandidate(
                            evidence
                        )
                    );
                    break;

                case
                    DataRelativePathAggregateConsumerCaseRepairPolicyState
                        .NoConsumerEvidence:

                case
                    DataRelativePathAggregateConsumerCaseRepairPolicyState
                        .ConflictingConsumerSpellings:

                case
                    DataRelativePathAggregateConsumerCaseRepairPolicyState
                        .ExactPhysicalSpellingPresent:

                case
                    DataRelativePathAggregateConsumerCaseRepairPolicyState
                        .EquivalentContentMultipleRepresentationsSourcePolicyRequired:

                case
                    DataRelativePathAggregateConsumerCaseRepairPolicyState
                        .ConflictingContentMultipleRepresentationsRejected:
                    break;

                case
                    DataRelativePathAggregateConsumerCaseRepairPolicyState
                        .IndeterminateEvidence:

                default:
                    throw new InvalidOperationException(
                        $"Validated aggregate spelling evidence for logical " +
                        $"leaf '{evidence.ManifestLeaf.WindowsLogicalPath}' " +
                        $"produced indeterminate consumer-case repair policy."
                    );
            }
        }

        return candidates.ToArray();
    }

    private static DataRelativePathAggregateConsumerCaseRepairCandidate
        BindUniqueRepresentationCandidate(
            DataRelativePathAggregateNamespaceManifestConsumerPhysicalSpellingEvidence
                evidence)
    {
        IReadOnlyList<
            DataRelativePathAggregateNamespaceManifestFileRepresentation
        >? representations =
            evidence.ManifestLeaf.PhysicalRepresentations;

        if (
            representations is null ||
            representations.Count != 1 ||
            representations[0] is null ||
            representations[0].Snapshot is null)
        {
            throw new InvalidOperationException(
                $"C4E-1 classified logical leaf " +
                $"'{evidence.ManifestLeaf.WindowsLogicalPath}' as a unique " +
                $"consumer-case repair candidate, but exactly one usable " +
                $"persisted physical representation was not present."
            );
        }

        DataRelativePathAggregateNamespaceManifestFileRepresentation source =
            representations[0];

        IReadOnlyList<string>? physicalRelativePaths =
            evidence.SpellingEvidence.PhysicalRelativePaths;

        string? requestedPath =
            evidence.SpellingEvidence.AuthoritativeRequestedPath;

        if (
            physicalRelativePaths is null ||
            physicalRelativePaths.Count != 1 ||
            string.IsNullOrWhiteSpace(
                requestedPath) ||
            !string.Equals(
                physicalRelativePaths[0],
                source.RelativePath,
                StringComparison.Ordinal) ||
            string.Equals(
                requestedPath,
                source.RelativePath,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"C4E-1 candidate evidence for logical leaf " +
                $"'{evidence.ManifestLeaf.WindowsLogicalPath}' is not " +
                $"consistent with one consumer-authoritative case mismatch " +
                $"against the sole persisted physical representation."
            );
        }

        return new(
            Evidence:
                evidence,
            SourceRepresentation:
                source
        );
    }
}
