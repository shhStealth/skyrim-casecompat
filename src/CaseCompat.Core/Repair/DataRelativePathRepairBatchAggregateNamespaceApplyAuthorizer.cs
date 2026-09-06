using CaseCompat.Core.Analysis;
using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;

namespace CaseCompat.Core.Repair;

public enum
    DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
{
    Authorized,

    InvalidBatchManifest,
    InvalidNamespaceEvidence,

    InvalidChildManifestSet,
    InvalidChildManifest,
    ChildManifestBindingMismatch,

    SourceGenerationBindingFailed,
    HistoricalCoverageRejected,

    CurrentLeafAnalysisFailed,
    CurrentRootSetMismatch,
    MissingCurrentLogicalLeaf,

    CurrentSourceMismatch,

    EquivalentContentMultipleRepresentations,
    ConflictingContentMultipleRepresentations
}

public sealed record
    DataRelativePathRepairBatchAggregateNamespaceApplyDecision(
        int CandidateIndex,
        string ChildName,
        DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
            State,
        DataRelativePathRepairSourceGenerationBindingState?
            SourceGenerationBindingState,
        DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState?
            HistoricalCoverageDecisionState,
        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis?
            CurrentLeafAnalysis,
        string? Error
    )
{
    public bool Authorized =>
        State ==
        DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
            .Authorized;
}

public enum
    DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizationState
{
    Authorized,

    InvalidInput,
    InvalidBatchManifest,
    InvalidNamespaceEvidence,
    InvalidChildManifestSet,
    AuthorizationRejected
}

public sealed record
    DataRelativePathRepairBatchAggregateNamespaceApplyAuthorization(
        DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizationState
            State,
        IReadOnlyList<
            DataRelativePathRepairBatchAggregateNamespaceApplyDecision
        > Decisions,
        DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorization?
            HistoricalCoverageAuthorization,
        string? Error
    )
{
    public bool AllAuthorized =>
        State ==
            DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizationState
                .Authorized &&
        Decisions.Count > 0 &&
        Decisions.All(
            decision =>
                decision.Authorized
        );
}

/*
 * Point-in-time execution-preflight evidence for schema-v4 /
 * coverage-policy-v3 aggregate namespace batches.
 *
 * This component performs no mutation and writes no durable authorization.
 * C4B/C4C must rerun it at the actual mutation boundary.
 */
public static class
    DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizer
{
    public static
        DataRelativePathRepairBatchAggregateNamespaceApplyAuthorization
        Authorize(
            LinuxNoFollowPathHandle dataRoot,
            DataRelativePathRepairBatchManifestRecord batchManifest,
            IReadOnlyList<
                DataRelativePathRepairPlanManifestRecord
            > childManifests,
            DataRelativePathAggregateNamespaceManifestReaderResult
                namespaceEvidence)
    {
        return AuthorizeCore(
            dataRoot,
            batchManifest,
            childManifests,
            namespaceEvidence,
            afterHistoricalCoverageAuthorization:
                null
        );
    }

    /*
     * Fresh point-in-time authorization for exactly one not-yet-started
     * schema-v4 / coverage-policy-v3 batch child.
     *
     * Immutable batch/evidence/child membership remains exact-batch scoped.
     * Filesystem freshness is deliberately scoped to candidateIndex so
     * already-started siblings are not required to resemble their pre-apply
     * source namespace again.
     *
     * This performs no mutation and writes no durable authorization.
     */
    public static
        DataRelativePathRepairBatchAggregateNamespaceApplyAuthorization
        AuthorizeCurrentChild(
            LinuxNoFollowPathHandle dataRoot,
            DataRelativePathRepairBatchManifestRecord batchManifest,
            IReadOnlyList<
                DataRelativePathRepairPlanManifestRecord
            > childManifests,
            DataRelativePathAggregateNamespaceManifestReaderResult
                namespaceEvidence,
            int candidateIndex)
    {
        ArgumentNullException.ThrowIfNull(
            dataRoot
        );

        ArgumentNullException.ThrowIfNull(
            batchManifest
        );

        ArgumentNullException.ThrowIfNull(
            childManifests
        );

        ArgumentNullException.ThrowIfNull(
            namespaceEvidence
        );

        if (
            batchManifest.SchemaVersion !=
                DataRelativePathRepairBatchManifestRecord
                    .SchemaVersion4 ||
            batchManifest.CoveragePolicyVersion !=
                DataRelativePathRepairBatchManifestRecord
                    .CoveragePolicyVersion3 ||
            batchManifest.SafeRejectionCount != 0 ||
            batchManifest.Children is null ||
            batchManifest.Children.Count <= 0 ||
            batchManifest.InputPathCount !=
                batchManifest.Children.Count ||
            batchManifest.AggregateNamespaceEvidence is null ||
            batchManifest.AggregateNamespaceEvidence.Count != 1 ||
            !string.Equals(
                batchManifest.DataRoot,
                dataRoot.FullPath,
                StringComparison.Ordinal))
        {
            return Result(
                DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizationState
                    .InvalidBatchManifest,
                [],
                error:
                    "The batch is not an exact schema-v4 / " +
                    "coverage-policy-v3 all-input batch bound to the " +
                    "trusted Data root."
            );
        }

        string? batchValidation =
            DataRelativePathRepairBatchManifest.Validate(
                batchManifest
            );

        if (batchValidation is not null)
        {
            return Result(
                DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizationState
                    .InvalidBatchManifest,
                [],
                error:
                    batchValidation
            );
        }

        DataRelativePathRepairBatchAggregateNamespaceEvidenceReference
            namespaceReference =
                batchManifest.AggregateNamespaceEvidence[0];

        if (
            !namespaceEvidence.Success ||
            namespaceEvidence.Manifest is null ||
            string.IsNullOrWhiteSpace(
                namespaceEvidence.ManifestSha256))
        {
            return Result(
                DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizationState
                    .InvalidNamespaceEvidence,
                [],
                error:
                    namespaceEvidence.Error ??
                    "The supplied aggregate namespace evidence was not " +
                    "successfully descriptor-read."
            );
        }

        DataRelativePathAggregateNamespaceManifestRecord sidecar =
            namespaceEvidence.Manifest;

        string? sidecarValidation =
            DataRelativePathAggregateNamespaceManifest.Validate(
                sidecar
            );

        if (
            sidecarValidation is not null ||
            namespaceReference.ManifestSchemaVersion !=
                sidecar.SchemaVersion ||
            !string.Equals(
                namespaceReference.RootWindowsLogicalPath,
                sidecar.RootWindowsLogicalPath,
                StringComparison.Ordinal) ||
            !string.Equals(
                namespaceReference.ManifestSha256,
                namespaceEvidence.ManifestSha256,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                sidecar.DataRoot,
                dataRoot.FullPath,
                StringComparison.Ordinal))
        {
            return Result(
                DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizationState
                    .InvalidNamespaceEvidence,
                [],
                error:
                    sidecarValidation ??
                    "The supplied aggregate namespace evidence does not " +
                    "match the batch's exact schema/root/SHA/Data-root " +
                    "reference."
            );
        }

        if (
            childManifests.Count !=
                batchManifest.Children.Count)
        {
            return Result(
                DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizationState
                    .InvalidChildManifestSet,
                [],
                error:
                    "The supplied child manifest set does not exactly " +
                    "match batch membership."
            );
        }

        if (
            candidateIndex < 0 ||
            candidateIndex >= childManifests.Count)
        {
            return Result(
                DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizationState
                    .InvalidInput,
                [],
                error:
                    "The selected candidate index is outside the exact batch."
            );
        }

        /*
         * Every persisted child remains part of immutable batch authority.
         * Validate the complete set structurally and canonically, but do not
         * reacquire sibling SourceSnapshots here.
         */
        for (
            int index = 0;
            index < childManifests.Count;
            index++)
        {
            DataRelativePathRepairPlanManifestRecord child =
                childManifests[index];

            DataRelativePathRepairBatchManifestChild batchChild =
                batchManifest.Children[index];

            if (
                child.SchemaVersion !=
                    DataRelativePathRepairPlanManifestRecord
                        .SchemaVersion4)
            {
                return Reject(
                    [
                        Decision(
                            index,
                            batchChild.ChildName,
                            DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                                .InvalidChildManifest,
                            error:
                                "Aggregate namespace execution requires " +
                                "child plan schema version 4."
                        )
                    ],
                    "One or more child manifests are not schema-v4."
                );
            }

            string? childValidation =
                DataRelativePathRepairPlanManifest.Validate(
                    child
                );

            if (childValidation is not null)
            {
                return Reject(
                    [
                        Decision(
                            index,
                            batchChild.ChildName,
                            DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                                .InvalidChildManifest,
                            error:
                                childValidation
                        )
                    ],
                    "One or more child manifests are invalid."
                );
            }

            if (
                !string.Equals(
                    child.DataRoot,
                    batchManifest.DataRoot,
                    StringComparison.Ordinal) ||
                child.PlanId !=
                    batchChild.PlanId)
            {
                return Reject(
                    [
                        Decision(
                            index,
                            batchChild.ChildName,
                            DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                                .ChildManifestBindingMismatch,
                            error:
                                "The child Data root or PlanId does not match " +
                                "the exact batch child descriptor."
                        )
                    ],
                    "A child manifest does not bind to the batch."
                );
            }

            byte[] childCanonical =
                DataRelativePathRepairPlanManifestJson.Serialize(
                    child
                );

            string childSha =
                Convert.ToHexString(
                    SHA256.HashData(
                        childCanonical
                    )
                );

            if (
                !string.Equals(
                    childSha,
                    batchChild.ManifestSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Reject(
                    [
                        Decision(
                            index,
                            batchChild.ChildName,
                            DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                                .ChildManifestBindingMismatch,
                            error:
                                "The canonical child manifest SHA-256 does not " +
                                "match the batch child descriptor."
                        )
                    ],
                    "A child manifest SHA-256 does not bind to the batch."
                );
            }
        }

        DataRelativePathRepairPlanManifestRecord selectedChild =
            childManifests[candidateIndex];

        DataRelativePathRepairBatchManifestChild selectedBatchChild =
            batchManifest.Children[candidateIndex];

        DataRelativePathRepairSourceGenerationBinding binding =
            DataRelativePathRepairSourceGenerationBinder.Bind(
                dataRoot,
                selectedChild.SourceSnapshot
            );

        if (
            !binding.Success ||
            binding.SourceInodeGeneration is null)
        {
            return Reject(
                [
                    Decision(
                        candidateIndex,
                        selectedBatchChild.ChildName,
                        DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                            .SourceGenerationBindingFailed,
                        sourceGenerationBindingState:
                            binding.State,
                        error:
                            binding.Error ??
                            binding.State.ToString()
                    )
                ],
                "Fresh source-generation binding failed."
            );
        }

        DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorization
            rawHistorical =
                DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorizer
                    .Authorize(
                        batchManifest,
                        [
                            new(
                                Manifest:
                                    selectedChild,
                                SourceInodeGeneration:
                                    binding.SourceInodeGeneration.Value
                            )
                        ],
                        [
                            new(
                                RootWindowsLogicalPath:
                                    namespaceReference
                                        .RootWindowsLogicalPath,
                                ReadResult:
                                    namespaceEvidence
                            )
                        ]
                    );

        DataRelativePathRepairBatchAggregateNamespaceCoverageDecision
            rawHistoricalDecision =
                rawHistorical.Decisions.Single();

        DataRelativePathRepairBatchAggregateNamespaceCoverageDecision
            historicalDecision =
                rawHistoricalDecision with
                {
                    CandidateIndex =
                        candidateIndex
                };

        DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorization
            historical =
                new(
                    [
                        historicalDecision
                    ]
                );

        if (!historical.AllAuthorized)
        {
            return Reject(
                [
                    Decision(
                        candidateIndex,
                        selectedBatchChild.ChildName,
                        DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                            .HistoricalCoverageRejected,
                        sourceGenerationBindingState:
                            binding.State,
                        historicalCoverageDecisionState:
                            historicalDecision.State,
                        error:
                            historicalDecision.Error ??
                            historicalDecision.State.ToString()
                    )
                ],
                "Historical aggregate namespace coverage was rejected.",
                historical
            );
        }

        string[] expectedPhysicalRoots =
            sidecar.DataRootChildNames
                .Where(
                    name =>
                        IsEquivalentRootName(
                            name,
                            namespaceReference
                                .RootWindowsLogicalPath
                        )
                )
                .OrderBy(
                    name =>
                        name,
                    StringComparer.Ordinal
                )
                .ToArray();

        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis current =
            DataRelativePathRepairAggregateNamespaceCurrentLeafAnalyzer
                .Analyze(
                    dataRoot,
                    namespaceReference
                        .RootWindowsLogicalPath,
                    selectedChild.RequestedPath
                );

        if (!current.Success)
        {
            return Reject(
                [
                    Decision(
                        candidateIndex,
                        selectedBatchChild.ChildName,
                        DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                            .CurrentLeafAnalysisFailed,
                        binding.State,
                        historicalDecision.State,
                        current,
                        current.Error ??
                            current.State.ToString()
                    )
                ],
                "Current aggregate namespace leaf analysis failed.",
                historical
            );
        }

        if (
            !expectedPhysicalRoots.SequenceEqual(
                current.PhysicalRootNames,
                StringComparer.Ordinal))
        {
            return Reject(
                [
                    Decision(
                        candidateIndex,
                        selectedBatchChild.ChildName,
                        DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                            .CurrentRootSetMismatch,
                        binding.State,
                        historicalDecision.State,
                        current,
                        "The current Windows-equivalent namespace-root set " +
                        "does not exactly match the sidecar root set."
                    )
                ],
                "The current namespace root set changed.",
                historical
            );
        }

        if (current.Representations.Count == 0)
        {
            return Reject(
                [
                    Decision(
                        candidateIndex,
                        selectedBatchChild.ChildName,
                        DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                            .MissingCurrentLogicalLeaf,
                        binding.State,
                        historicalDecision.State,
                        current,
                        "The authorized Windows-logical leaf is currently " +
                        "missing."
                    )
                ],
                "The current logical leaf is missing.",
                historical
            );
        }

        if (current.Representations.Count > 1)
        {
            bool equivalentContent =
                current.Representations
                    .Skip(
                        1
                    )
                    .All(
                        representation =>
                            representation.Size ==
                                current.Representations[0].Size &&
                            string.Equals(
                                representation.Sha256,
                                current.Representations[0].Sha256,
                                StringComparison.OrdinalIgnoreCase)
                    );

            DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                state =
                    equivalentContent
                        ? DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                            .EquivalentContentMultipleRepresentations
                        : DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                            .ConflictingContentMultipleRepresentations;

            return Reject(
                [
                    Decision(
                        candidateIndex,
                        selectedBatchChild.ChildName,
                        state,
                        binding.State,
                        historicalDecision.State,
                        current,
                        "The current logical leaf has multiple physical " +
                        "representations; C4A does not select a provider."
                    )
                ],
                "The current logical leaf is not source-unambiguous.",
                historical
            );
        }

        DataRelativePathRepairAggregateNamespaceCurrentFileRepresentation
            representation =
                current.Representations[0];

        DataRelativePathAggregateNamespaceManifestFileRepresentation?
            historicalRepresentation =
                FindHistoricalRepresentation(
                    sidecar,
                    selectedChild,
                    dataRoot
                );

        if (
            historicalRepresentation is null ||
            !CurrentRepresentationMatches(
                dataRoot,
                selectedChild.SourceSnapshot,
                representation,
                historicalRepresentation
            ))
        {
            return Reject(
                [
                    Decision(
                        candidateIndex,
                        selectedBatchChild.ChildName,
                        DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                            .CurrentSourceMismatch,
                        binding.State,
                        historicalDecision.State,
                        current,
                        "The sole current physical representation does not " +
                        "exactly match the authorized source identity, " +
                        "generation, size, SHA-256, and physical path."
                    )
                ],
                "Current source authority no longer matches planning evidence.",
                historical
            );
        }

        return Result(
            DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizationState
                .Authorized,
            [
                Decision(
                    candidateIndex,
                    selectedBatchChild.ChildName,
                    DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                        .Authorized,
                    binding.State,
                    historicalDecision.State,
                    current,
                    error:
                        null
                )
            ],
            historical,
            error:
                null
        );
    }

    /*
     * Internal deterministic race seam used only by tests.
     * Production callers use Authorize().
     */
    internal static
        DataRelativePathRepairBatchAggregateNamespaceApplyAuthorization
        AuthorizeCore(
            LinuxNoFollowPathHandle dataRoot,
            DataRelativePathRepairBatchManifestRecord batchManifest,
            IReadOnlyList<
                DataRelativePathRepairPlanManifestRecord
            > childManifests,
            DataRelativePathAggregateNamespaceManifestReaderResult
                namespaceEvidence,
            Action? afterHistoricalCoverageAuthorization)
    {
        ArgumentNullException.ThrowIfNull(
            dataRoot
        );

        ArgumentNullException.ThrowIfNull(
            batchManifest
        );

        ArgumentNullException.ThrowIfNull(
            childManifests
        );

        ArgumentNullException.ThrowIfNull(
            namespaceEvidence
        );

        if (
            batchManifest.SchemaVersion !=
                DataRelativePathRepairBatchManifestRecord
                    .SchemaVersion4 ||
            batchManifest.CoveragePolicyVersion !=
                DataRelativePathRepairBatchManifestRecord
                    .CoveragePolicyVersion3 ||
            batchManifest.SafeRejectionCount != 0 ||
            batchManifest.Children is null ||
            batchManifest.Children.Count <= 0 ||
            batchManifest.InputPathCount !=
                batchManifest.Children.Count ||
            batchManifest.AggregateNamespaceEvidence is null ||
            batchManifest.AggregateNamespaceEvidence.Count != 1 ||
            !string.Equals(
                batchManifest.DataRoot,
                dataRoot.FullPath,
                StringComparison.Ordinal))
        {
            return Result(
                DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizationState
                    .InvalidBatchManifest,
                [],
                error:
                    "The batch is not an exact schema-v4 / " +
                    "coverage-policy-v3 all-input batch bound to the " +
                    "trusted Data root."
            );
        }

        string? batchValidation =
            DataRelativePathRepairBatchManifest.Validate(
                batchManifest
            );

        if (batchValidation is not null)
        {
            return Result(
                DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizationState
                    .InvalidBatchManifest,
                [],
                error:
                    batchValidation
            );
        }

        DataRelativePathRepairBatchAggregateNamespaceEvidenceReference
            namespaceReference =
                batchManifest.AggregateNamespaceEvidence[0];

        if (
            !namespaceEvidence.Success ||
            namespaceEvidence.Manifest is null ||
            string.IsNullOrWhiteSpace(
                namespaceEvidence.ManifestSha256))
        {
            return Result(
                DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizationState
                    .InvalidNamespaceEvidence,
                [],
                error:
                    namespaceEvidence.Error ??
                    "The supplied aggregate namespace evidence was not " +
                    "successfully descriptor-read."
            );
        }

        DataRelativePathAggregateNamespaceManifestRecord sidecar =
            namespaceEvidence.Manifest;

        string? sidecarValidation =
            DataRelativePathAggregateNamespaceManifest.Validate(
                sidecar
            );

        if (
            sidecarValidation is not null ||
            namespaceReference.ManifestSchemaVersion !=
                sidecar.SchemaVersion ||
            !string.Equals(
                namespaceReference.RootWindowsLogicalPath,
                sidecar.RootWindowsLogicalPath,
                StringComparison.Ordinal) ||
            !string.Equals(
                namespaceReference.ManifestSha256,
                namespaceEvidence.ManifestSha256,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                sidecar.DataRoot,
                dataRoot.FullPath,
                StringComparison.Ordinal))
        {
            return Result(
                DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizationState
                    .InvalidNamespaceEvidence,
                [],
                error:
                    sidecarValidation ??
                    "The supplied aggregate namespace evidence does not " +
                    "match the batch's exact schema/root/SHA/Data-root " +
                    "reference."
            );
        }

        if (
            childManifests.Count !=
                batchManifest.Children.Count)
        {
            return Result(
                DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizationState
                    .InvalidChildManifestSet,
                [],
                error:
                    "The supplied child manifest set does not exactly " +
                    "match batch membership."
            );
        }

        var decisions =
            new DataRelativePathRepairBatchAggregateNamespaceApplyDecision[
                childManifests.Count
            ];

        var bindings =
            new DataRelativePathRepairSourceGenerationBinding[
                childManifests.Count
            ];

        var coverageCandidates =
            new DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate[
                childManifests.Count
            ];

        for (
            int index = 0;
            index < childManifests.Count;
            index++)
        {
            DataRelativePathRepairPlanManifestRecord child =
                childManifests[index];

            DataRelativePathRepairBatchManifestChild batchChild =
                batchManifest.Children[index];

            if (
                child.SchemaVersion !=
                    DataRelativePathRepairPlanManifestRecord
                        .SchemaVersion4)
            {
                decisions[index] =
                    Decision(
                        index,
                        batchChild.ChildName,
                        DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                            .InvalidChildManifest,
                        error:
                            "Aggregate namespace execution requires " +
                            "child plan schema version 4."
                    );

                return Reject(
                    decisions,
                    "One or more child manifests are not schema-v4."
                );
            }

            string? childValidation =
                DataRelativePathRepairPlanManifest.Validate(
                    child
                );

            if (childValidation is not null)
            {
                decisions[index] =
                    Decision(
                        index,
                        batchChild.ChildName,
                        DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                            .InvalidChildManifest,
                        error:
                            childValidation
                    );

                return Reject(
                    decisions,
                    "One or more child manifests are invalid."
                );
            }

            if (
                !string.Equals(
                    child.DataRoot,
                    batchManifest.DataRoot,
                    StringComparison.Ordinal) ||
                child.PlanId !=
                    batchChild.PlanId)
            {
                decisions[index] =
                    Decision(
                        index,
                        batchChild.ChildName,
                        DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                            .ChildManifestBindingMismatch,
                        error:
                            "The child Data root or PlanId does not match " +
                            "the exact batch child descriptor."
                    );

                return Reject(
                    decisions,
                    "A child manifest does not bind to the batch."
                );
            }

            byte[] childCanonical =
                DataRelativePathRepairPlanManifestJson.Serialize(
                    child
                );

            string childSha =
                Convert.ToHexString(
                    SHA256.HashData(
                        childCanonical
                    )
                );

            if (
                !string.Equals(
                    childSha,
                    batchChild.ManifestSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                decisions[index] =
                    Decision(
                        index,
                        batchChild.ChildName,
                        DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                            .ChildManifestBindingMismatch,
                        error:
                            "The canonical child manifest SHA-256 does not " +
                            "match the batch child descriptor."
                    );

                return Reject(
                    decisions,
                    "A child manifest SHA-256 does not bind to the batch."
                );
            }

            DataRelativePathRepairSourceGenerationBinding binding =
                DataRelativePathRepairSourceGenerationBinder.Bind(
                    dataRoot,
                    child.SourceSnapshot
                );

            bindings[index] =
                binding;

            if (
                !binding.Success ||
                binding.SourceInodeGeneration is null)
            {
                decisions[index] =
                    Decision(
                        index,
                        batchChild.ChildName,
                        DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                            .SourceGenerationBindingFailed,
                        sourceGenerationBindingState:
                            binding.State,
                        error:
                            binding.Error ??
                            binding.State.ToString()
                    );

                return Reject(
                    decisions,
                    "Fresh source-generation binding failed."
                );
            }

            coverageCandidates[index] =
                new(
                    Manifest:
                        child,
                    SourceInodeGeneration:
                        binding.SourceInodeGeneration.Value
                );
        }

        DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorization
            historical =
                DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorizer
                    .Authorize(
                        batchManifest,
                        coverageCandidates,
                        [
                            new(
                                RootWindowsLogicalPath:
                                    namespaceReference
                                        .RootWindowsLogicalPath,
                                ReadResult:
                                    namespaceEvidence
                            )
                        ]
                    );

        if (!historical.AllAuthorized)
        {
            for (
                int index = 0;
                index < decisions.Length;
                index++)
            {
                DataRelativePathRepairBatchAggregateNamespaceCoverageDecision?
                    historicalDecision =
                        historical.Decisions
                            .FirstOrDefault(
                                decision =>
                                    decision.CandidateIndex ==
                                    index
                            );

                decisions[index] =
                    Decision(
                        index,
                        batchManifest.Children[index].ChildName,
                        DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                            .HistoricalCoverageRejected,
                        sourceGenerationBindingState:
                            bindings[index].State,
                        historicalCoverageDecisionState:
                            historicalDecision?.State,
                        error:
                            historicalDecision?.Error ??
                            historicalDecision?.State.ToString() ??
                            "Historical policy-v3 coverage was rejected."
                    );
            }

            return Result(
                DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizationState
                    .AuthorizationRejected,
                decisions,
                historical,
                "Historical aggregate namespace coverage was rejected."
            );
        }

        afterHistoricalCoverageAuthorization
            ?.Invoke();

        string[] expectedPhysicalRoots =
            sidecar.DataRootChildNames
                .Where(
                    name =>
                        IsEquivalentRootName(
                            name,
                            namespaceReference
                                .RootWindowsLogicalPath
                        )
                )
                .OrderBy(
                    name =>
                        name,
                    StringComparer.Ordinal
                )
                .ToArray();

        for (
            int index = 0;
            index < childManifests.Count;
            index++)
        {
            DataRelativePathRepairPlanManifestRecord child =
                childManifests[index];

            string childName =
                batchManifest.Children[
                    index
                ].ChildName;

            DataRelativePathRepairBatchAggregateNamespaceCoverageDecision
                historicalDecision =
                    historical.Decisions
                        .First(
                            decision =>
                                decision.CandidateIndex ==
                                index
                        );

            DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis
                current =
                    DataRelativePathRepairAggregateNamespaceCurrentLeafAnalyzer
                        .Analyze(
                            dataRoot,
                            namespaceReference
                                .RootWindowsLogicalPath,
                            child.RequestedPath
                        );

            if (!current.Success)
            {
                decisions[index] =
                    Decision(
                        index,
                        childName,
                        DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                            .CurrentLeafAnalysisFailed,
                        bindings[index].State,
                        historicalDecision.State,
                        current,
                        current.Error ??
                            current.State.ToString()
                    );

                return Reject(
                    decisions,
                    "Current aggregate namespace leaf analysis failed.",
                    historical
                );
            }

            if (
                !expectedPhysicalRoots.SequenceEqual(
                    current.PhysicalRootNames,
                    StringComparer.Ordinal))
            {
                decisions[index] =
                    Decision(
                        index,
                        childName,
                        DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                            .CurrentRootSetMismatch,
                        bindings[index].State,
                        historicalDecision.State,
                        current,
                        "The current Windows-equivalent namespace-root set " +
                        "does not exactly match the sidecar root set."
                    );

                return Reject(
                    decisions,
                    "The current namespace root set changed.",
                    historical
                );
            }

            if (current.Representations.Count == 0)
            {
                decisions[index] =
                    Decision(
                        index,
                        childName,
                        DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                            .MissingCurrentLogicalLeaf,
                        bindings[index].State,
                        historicalDecision.State,
                        current,
                        "The authorized Windows-logical leaf is currently " +
                        "missing."
                    );

                return Reject(
                    decisions,
                    "The current logical leaf is missing.",
                    historical
                );
            }

            if (current.Representations.Count > 1)
            {
                bool equivalentContent =
                    current.Representations
                        .Skip(
                            1
                        )
                        .All(
                            representation =>
                                representation.Size ==
                                    current.Representations[0].Size &&
                                string.Equals(
                                    representation.Sha256,
                                    current.Representations[0].Sha256,
                                    StringComparison.OrdinalIgnoreCase)
                        );

                DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                    state =
                        equivalentContent
                            ? DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                                .EquivalentContentMultipleRepresentations
                            : DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                                .ConflictingContentMultipleRepresentations;

                decisions[index] =
                    Decision(
                        index,
                        childName,
                        state,
                        bindings[index].State,
                        historicalDecision.State,
                        current,
                        "The current logical leaf has multiple physical " +
                        "representations; C4A does not select a provider."
                    );

                return Reject(
                    decisions,
                    "The current logical leaf is not source-unambiguous.",
                    historical
                );
            }

            DataRelativePathRepairAggregateNamespaceCurrentFileRepresentation
                representation =
                    current.Representations[0];

            DataRelativePathAggregateNamespaceManifestFileRepresentation?
                historicalRepresentation =
                    FindHistoricalRepresentation(
                        sidecar,
                        child,
                        dataRoot
                    );

            if (
                historicalRepresentation is null ||
                !CurrentRepresentationMatches(
                    dataRoot,
                    child.SourceSnapshot,
                    representation,
                    historicalRepresentation
                ))
            {
                decisions[index] =
                    Decision(
                        index,
                        childName,
                        DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                            .CurrentSourceMismatch,
                        bindings[index].State,
                        historicalDecision.State,
                        current,
                        "The sole current physical representation does not " +
                        "exactly match the authorized source identity, " +
                        "generation, size, SHA-256, and physical path."
                    );

                return Reject(
                    decisions,
                    "Current source authority no longer matches planning " +
                    "evidence.",
                    historical
                );
            }

            decisions[index] =
                Decision(
                    index,
                    childName,
                    DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                        .Authorized,
                    bindings[index].State,
                    historicalDecision.State,
                    current,
                    error:
                        null
                );
        }

        return Result(
            DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizationState
                .Authorized,
            decisions,
            historical,
            error:
                null
        );
    }

    private static
        DataRelativePathAggregateNamespaceManifestFileRepresentation?
        FindHistoricalRepresentation(
            DataRelativePathAggregateNamespaceManifestRecord sidecar,
            DataRelativePathRepairPlanManifestRecord child,
            LinuxNoFollowPathHandle dataRoot)
    {
        string logical;

        try
        {
            logical =
                WindowsLogicalPath.FromRelativePath(
                    child.RequestedPath
                ).Value;
        }
        catch (ArgumentException)
        {
            return null;
        }

        DataRelativePathAggregateNamespaceManifestLogicalLeaf? leaf =
            sidecar.LogicalLeaves
                .SingleOrDefault(
                    item =>
                        string.Equals(
                            item.WindowsLogicalPath,
                            logical,
                            StringComparison.Ordinal
                        )
                );

        if (leaf is null)
        {
            return null;
        }

        string? sourceRelative =
            TryGetCanonicalRelativePath(
                dataRoot.FullPath,
                child.SourceSnapshot.PhysicalPath
            );

        if (sourceRelative is null)
        {
            return null;
        }

        return leaf.PhysicalRepresentations
            .SingleOrDefault(
                item =>
                    string.Equals(
                        item.RelativePath,
                        sourceRelative,
                        StringComparison.Ordinal
                    ) &&
                    SnapshotMatches(
                        child.SourceSnapshot,
                        item.Snapshot
                    )
            );
    }

    private static bool CurrentRepresentationMatches(
        LinuxNoFollowPathHandle dataRoot,
        DataRelativePathRepairSourceSnapshot expectedSource,
        DataRelativePathRepairAggregateNamespaceCurrentFileRepresentation
            current,
        DataRelativePathAggregateNamespaceManifestFileRepresentation
            historical)
    {
        string? expectedRelative =
            TryGetCanonicalRelativePath(
                dataRoot.FullPath,
                expectedSource.PhysicalPath
            );

        if (
            expectedRelative is null ||
            !string.Equals(
                current.RelativePath,
                expectedRelative,
                StringComparison.Ordinal) ||
            !string.Equals(
                historical.RelativePath,
                expectedRelative,
                StringComparison.Ordinal) ||
            current.Size !=
                expectedSource.Size ||
            current.Size !=
                historical.Snapshot.Size ||
            !string.Equals(
                current.Sha256,
                expectedSource.Sha256,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                current.Sha256,
                historical.Snapshot.Sha256,
                StringComparison.OrdinalIgnoreCase) ||
            current.IncarnationIdentity.InodeGeneration !=
                historical.InodeGeneration)
        {
            return false;
        }

        LinuxOpenedFileIdentityResult currentIdentity =
            current.IncarnationIdentity.PhysicalIdentity;

        LinuxFileIdentityResult sourceIdentity =
            expectedSource.Identity;

        LinuxFileIdentityResult historicalIdentity =
            historical.Snapshot.Identity;

        return
            currentIdentity.DeviceMajor ==
                sourceIdentity.DeviceMajor &&
            currentIdentity.DeviceMinor ==
                sourceIdentity.DeviceMinor &&
            currentIdentity.Inode ==
                sourceIdentity.Inode &&
            currentIdentity.MountId ==
                sourceIdentity.MountId &&
            currentIdentity.DeviceMajor ==
                historicalIdentity.DeviceMajor &&
            currentIdentity.DeviceMinor ==
                historicalIdentity.DeviceMinor &&
            currentIdentity.Inode ==
                historicalIdentity.Inode &&
            currentIdentity.MountId ==
                historicalIdentity.MountId;
    }

    /*
     * LinkCount is deliberately not part of execution authority.
     */
    private static bool SnapshotMatches(
        DataRelativePathRepairSourceSnapshot left,
        DataRelativePathRepairSourceSnapshot right)
    {
        return
            string.Equals(
                left.PhysicalPath,
                right.PhysicalPath,
                StringComparison.Ordinal) &&
            left.Size ==
                right.Size &&
            string.Equals(
                left.Sha256,
                right.Sha256,
                StringComparison.OrdinalIgnoreCase) &&
            left.Identity.DeviceMajor ==
                right.Identity.DeviceMajor &&
            left.Identity.DeviceMinor ==
                right.Identity.DeviceMinor &&
            left.Identity.Inode ==
                right.Identity.Inode &&
            left.Identity.MountId ==
                right.Identity.MountId;
    }

    private static string? TryGetCanonicalRelativePath(
        string dataRoot,
        string physicalPath)
    {
        string root =
            Path.GetFullPath(
                dataRoot
            );

        string full =
            Path.GetFullPath(
                physicalPath
            );

        string relative =
            Path.GetRelativePath(
                root,
                full
            )
            .Replace(
                Path.DirectorySeparatorChar,
                '/'
            );

        if (
            string.IsNullOrWhiteSpace(
                relative) ||
            relative == ".." ||
            relative.StartsWith(
                "../",
                StringComparison.Ordinal) ||
            Path.IsPathRooted(
                relative))
        {
            return null;
        }

        return relative;
    }

    private static bool IsEquivalentRootName(
        string physicalName,
        string rootWindowsLogicalPath)
    {
        if (
            string.IsNullOrEmpty(
                physicalName) ||
            physicalName is "." or ".." ||
            physicalName.Contains('/') ||
            physicalName.Contains('\\') ||
            physicalName.Contains('\0'))
        {
            return false;
        }

        return string.Equals(
            physicalName.ToUpperInvariant(),
            rootWindowsLogicalPath,
            StringComparison.Ordinal
        );
    }

    private static
        DataRelativePathRepairBatchAggregateNamespaceApplyDecision
        Decision(
            int candidateIndex,
            string childName,
            DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                state,
            DataRelativePathRepairSourceGenerationBindingState?
                sourceGenerationBindingState = null,
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState?
                historicalCoverageDecisionState = null,
            DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis?
                currentLeafAnalysis = null,
            string? error = null)
    {
        return new(
            CandidateIndex:
                candidateIndex,
            ChildName:
                childName,
            State:
                state,
            SourceGenerationBindingState:
                sourceGenerationBindingState,
            HistoricalCoverageDecisionState:
                historicalCoverageDecisionState,
            CurrentLeafAnalysis:
                currentLeafAnalysis,
            Error:
                error
        );
    }

    private static
        DataRelativePathRepairBatchAggregateNamespaceApplyAuthorization
        Reject(
            IReadOnlyList<
                DataRelativePathRepairBatchAggregateNamespaceApplyDecision
            > decisions,
            string error,
            DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorization?
                historical = null)
    {
        return Result(
            DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizationState
                .AuthorizationRejected,
            decisions,
            historical,
            error
        );
    }

    private static
        DataRelativePathRepairBatchAggregateNamespaceApplyAuthorization
        Result(
            DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizationState
                state,
            IReadOnlyList<
                DataRelativePathRepairBatchAggregateNamespaceApplyDecision
            > decisions,
            DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorization?
                historical = null,
            string? error = null)
    {
        return new(
            State:
                state,
            Decisions:
                decisions,
            HistoricalCoverageAuthorization:
                historical,
            Error:
                error
        );
    }
}
