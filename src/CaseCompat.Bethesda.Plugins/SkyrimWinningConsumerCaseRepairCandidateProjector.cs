using CaseCompat.Core.Repair;

namespace CaseCompat.Bethesda.Plugins;

// Completeness state for Bethesda consumer-authoritative repair candidates.
//
// These states deliberately preserve the global C4D-6C completeness boundary.
// A non-complete state publishes no candidates and must never be converted into
// per-leaf NoConsumerEvidence.
//
// Complete means only that globally complete consumer/physical evidence was
// safely allowed to reach Core C4E-2. A Complete result may still contain zero
// candidates because ordinary per-leaf repair policy can reject or suppress
// every leaf.
public enum SkyrimWinningConsumerCaseRepairCandidateProjectionState
{
    Complete,
    IncompleteWinnerSearch,
    IndeterminateConsumerPathEvidence,
    IndeterminateAggregateEvidence
}

// Read-only Bethesda C4E-3 result.
//
// ConsumerPhysicalSpellingProjection is retained by reference so the complete
// C4D provenance and its diagnostic state remain available.
//
// Candidates are the source-bound Core C4E-2 descriptors. Publishing them does
// not grant repair-plan, namespace-coverage, persistence, authorization, apply,
// execution, rollback, or recovery authority.
public sealed record
    SkyrimWinningConsumerCaseRepairCandidateProjectionResult(
        SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionResult
            ConsumerPhysicalSpellingProjection,
        SkyrimWinningConsumerCaseRepairCandidateProjectionState State,
        IReadOnlyList<DataRelativePathAggregateConsumerCaseRepairCandidate>
            Candidates,
        string? Error
    )
{
    public bool CandidateEvidenceComplete =>
        State ==
        SkyrimWinningConsumerCaseRepairCandidateProjectionState.Complete;

    public int CandidateCount =>
        Candidates.Count;
}

// Completeness-aware Bethesda orchestration over C4D-6C and Core C4E-2.
//
// C4D-6C is always evaluated first.
//
//   IncompleteWinnerSearch
//       -> publish zero candidates.
//
//   IndeterminateConsumerPathEvidence
//       -> publish zero candidates.
//
//   IndeterminateAggregateEvidence
//       -> publish zero candidates.
//
//   Complete
//       -> and only then allow the retained complete manifest / consumer
//          composition to reach Core C4E-2.
//
// Core C4E-2 intentionally performs its own canonical manifest/spelling join
// and policy projection. This means C4E-3 does not reinterpret C4D relation
// rows or duplicate per-leaf repair policy.
//
// No filesystem access, hashing, provider precedence, planning, persistence,
// authorization, mutation, execution, rollback, or recovery occurs here.
public static class SkyrimWinningConsumerCaseRepairCandidateProjector
{
    public static SkyrimWinningConsumerCaseRepairCandidateProjectionResult
        Project(
            DataRelativePathAggregateNamespaceManifestRecord manifest,
            SkyrimWinningConsumerSpellingEvidenceCompositionResult
                consumerSpellingComposition)
    {
        ArgumentNullException.ThrowIfNull(
            manifest
        );

        ArgumentNullException.ThrowIfNull(
            consumerSpellingComposition
        );

        SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionResult
            consumerPhysical =
                SkyrimWinningConsumerPhysicalSpellingEvidenceProjector
                    .Project(
                        manifest,
                        consumerSpellingComposition
                    );

        switch (consumerPhysical.State)
        {
            case
                SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionState
                    .IncompleteWinnerSearch:
                return Terminal(
                    consumerPhysical,
                    SkyrimWinningConsumerCaseRepairCandidateProjectionState
                        .IncompleteWinnerSearch,
                    consumerPhysical.Error
                );

            case
                SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionState
                    .IndeterminateConsumerPathEvidence:
                return Terminal(
                    consumerPhysical,
                    SkyrimWinningConsumerCaseRepairCandidateProjectionState
                        .IndeterminateConsumerPathEvidence,
                    consumerPhysical.Error
                );

            case
                SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionState
                    .IndeterminateAggregateEvidence:
                return Terminal(
                    consumerPhysical,
                    SkyrimWinningConsumerCaseRepairCandidateProjectionState
                        .IndeterminateAggregateEvidence,
                    consumerPhysical.Error
                );

            case
                SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionState
                    .Complete:
                break;

            default:
                return Terminal(
                    consumerPhysical,
                    SkyrimWinningConsumerCaseRepairCandidateProjectionState
                        .IndeterminateAggregateEvidence,
                    "The consumer/physical spelling projection has an " +
                    $"unrecognized state value: {consumerPhysical.State}."
                );
        }

        try
        {
            IReadOnlyList<
                DataRelativePathAggregateConsumerCaseRepairCandidate
            > candidates =
                DataRelativePathAggregateConsumerCaseRepairCandidateProjector
                    .Project(
                        consumerPhysical.Manifest,
                        consumerPhysical
                            .ConsumerSpellingComposition
                            .Evidence
                    );

            return new
                SkyrimWinningConsumerCaseRepairCandidateProjectionResult(
                    ConsumerPhysicalSpellingProjection:
                        consumerPhysical,
                    State:
                        SkyrimWinningConsumerCaseRepairCandidateProjectionState
                            .Complete,
                    Candidates:
                        candidates,
                    Error:
                        null
                );
        }
        catch (ArgumentException ex)
        {
            return CandidateIndeterminate(
                consumerPhysical,
                ex
            );
        }
        catch (InvalidOperationException ex)
        {
            return CandidateIndeterminate(
                consumerPhysical,
                ex
            );
        }
    }

    private static
        SkyrimWinningConsumerCaseRepairCandidateProjectionResult
        CandidateIndeterminate(
            SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionResult
                consumerPhysical,
            Exception exception)
    {
        return Terminal(
            consumerPhysical,
            SkyrimWinningConsumerCaseRepairCandidateProjectionState
                .IndeterminateAggregateEvidence,
            "Consumer-case repair candidate projection rejected the " +
            $"supposedly complete evidence: {exception.Message}"
        );
    }

    private static
        SkyrimWinningConsumerCaseRepairCandidateProjectionResult
        Terminal(
            SkyrimWinningConsumerPhysicalSpellingEvidenceProjectionResult
                consumerPhysical,
            SkyrimWinningConsumerCaseRepairCandidateProjectionState state,
            string? error)
    {
        return new
            SkyrimWinningConsumerCaseRepairCandidateProjectionResult(
                ConsumerPhysicalSpellingProjection:
                    consumerPhysical,
                State:
                    state,
                Candidates:
                    Array.Empty<
                        DataRelativePathAggregateConsumerCaseRepairCandidate
                    >(),
                Error:
                    error
            );
    }
}
