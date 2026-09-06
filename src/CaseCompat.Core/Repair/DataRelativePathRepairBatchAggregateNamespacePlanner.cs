using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;

namespace CaseCompat.Core.Repair;

public sealed record
    DataRelativePathRepairBatchAggregateNamespacePlanningCandidate(
        string ChildName,
        DataRelativePathRepairPlanManifestRecord Manifest
    );

public sealed record
    DataRelativePathRepairBatchAggregateNamespacePlannedChild(
        int CandidateIndex,
        string ChildName,
        DataRelativePathRepairPlanManifestRecord Manifest,
        string ManifestSha256,
        uint SourceInodeGeneration
    );

public enum
    DataRelativePathRepairBatchAggregateNamespacePlanningDecisionState
{
    Authorized,

    InvalidCandidate,
    InvalidChildName,
    InvalidPlanManifest,
    ManifestSerializationFailed,
    SourceGenerationBindingFailed,
    InvalidNamespaceEvidence,
    CoverageRejected
}

public sealed record
    DataRelativePathRepairBatchAggregateNamespacePlanningDecision(
        int CandidateIndex,
        DataRelativePathRepairBatchAggregateNamespacePlanningDecisionState
            State,
        DataRelativePathRepairSourceGenerationBindingState?
            SourceGenerationBindingState,
        DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState?
            CoverageDecisionState,
        string? Error
    )
{
    public bool Authorized =>
        State ==
        DataRelativePathRepairBatchAggregateNamespacePlanningDecisionState
            .Authorized;
}

public enum DataRelativePathRepairBatchAggregateNamespacePlanState
{
    Planned,

    InvalidInput,
    InvalidNamespaceEvidence,
    CandidatePreparationFailed,
    CoverageRejected,
    BatchManifestCreationFailed
}

public sealed record
    DataRelativePathRepairBatchAggregateNamespacePlanResult(
        DataRelativePathRepairBatchAggregateNamespacePlanState State,
        DataRelativePathRepairBatchManifestRecord? BatchManifest,
        IReadOnlyList<
            DataRelativePathRepairBatchAggregateNamespacePlannedChild
        > PlannedChildren,
        IReadOnlyList<
            DataRelativePathRepairBatchAggregateNamespacePlanningDecision
        > Decisions,
        DataRelativePathRepairBatchAggregateNamespaceEvidenceReference?
            NamespaceEvidenceReference,
        DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorization?
            CoverageAuthorization,
        string? Error
    )
{
    public bool Success =>
        State ==
            DataRelativePathRepairBatchAggregateNamespacePlanState.Planned &&
        BatchManifest is not null &&
        Decisions.All(
            decision =>
                decision.Authorized
        );
}

/*
 * Pure planning integration for schema-v4 / coverage-policy-v3.
 *
 * Filesystem reads are limited to fresh source-generation/content binding.
 * Supplied aggregate namespace evidence is already descriptor-read and
 * SHA-bound. Nothing is persisted or published here.
 *
 * This class grants no apply, rollback, recovery, or execution authority.
 */
public static class
    DataRelativePathRepairBatchAggregateNamespacePlanner
{
    public static DataRelativePathRepairBatchAggregateNamespacePlanResult
        Plan(
            LinuxNoFollowPathHandle dataRoot,
            Guid batchId,
            DateTimeOffset createdUtc,
            string childManifestName,
            IReadOnlyList<
                DataRelativePathRepairBatchAggregateNamespacePlanningCandidate
            > candidates,
            DataRelativePathAggregateNamespaceManifestReaderResult
                namespaceEvidence)
    {
        ArgumentNullException.ThrowIfNull(
            dataRoot
        );

        ArgumentNullException.ThrowIfNull(
            candidates
        );

        ArgumentNullException.ThrowIfNull(
            namespaceEvidence
        );

        if (
            candidates.Count == 0 ||
            !IsValidChildName(
                childManifestName))
        {
            return Result(
                DataRelativePathRepairBatchAggregateNamespacePlanState
                    .InvalidInput,
                [],
                [],
                error:
                    "Aggregate namespace planning requires at least one " +
                    "candidate and one valid direct child manifest name."
            );
        }

        if (
            !namespaceEvidence.Success ||
            namespaceEvidence.Manifest is null ||
            string.IsNullOrWhiteSpace(
                namespaceEvidence.ManifestSha256))
        {
            return Result(
                DataRelativePathRepairBatchAggregateNamespacePlanState
                    .InvalidNamespaceEvidence,
                [],
                Enumerable.Range(
                        0,
                        candidates.Count
                    )
                    .Select(
                        index =>
                            Decision(
                                index,
                                DataRelativePathRepairBatchAggregateNamespacePlanningDecisionState
                                    .InvalidNamespaceEvidence,
                                error:
                                    namespaceEvidence.Error ??
                                    "Aggregate namespace evidence was not " +
                                    "successfully descriptor-read."
                            )
                    )
                    .ToArray(),
                error:
                    namespaceEvidence.Error ??
                    "Aggregate namespace evidence is incomplete."
            );
        }

        DataRelativePathAggregateNamespaceManifestRecord namespaceManifest =
            namespaceEvidence.Manifest;

        string? namespaceValidationError =
            DataRelativePathAggregateNamespaceManifest.Validate(
                namespaceManifest
            );

        if (
            namespaceManifest.SchemaVersion !=
                DataRelativePathAggregateNamespaceManifestRecord
                    .SchemaVersion1 ||
            namespaceValidationError is not null ||
            !string.Equals(
                namespaceManifest.DataRoot,
                dataRoot.FullPath,
                StringComparison.Ordinal))
        {
            string error =
                namespaceValidationError ??
                (
                    namespaceManifest.SchemaVersion !=
                        DataRelativePathAggregateNamespaceManifestRecord
                            .SchemaVersion1
                        ? "Aggregate namespace evidence has an unsupported " +
                          "schema version."
                        : "Aggregate namespace evidence Data root does not " +
                          "match the trusted retained Data root."
                );

            return Result(
                DataRelativePathRepairBatchAggregateNamespacePlanState
                    .InvalidNamespaceEvidence,
                [],
                Enumerable.Range(
                        0,
                        candidates.Count
                    )
                    .Select(
                        index =>
                            Decision(
                                index,
                                DataRelativePathRepairBatchAggregateNamespacePlanningDecisionState
                                    .InvalidNamespaceEvidence,
                                error:
                                    error
                            )
                    )
                    .ToArray(),
                error:
                    error
            );
        }

        var namespaceReference =
            new DataRelativePathRepairBatchAggregateNamespaceEvidenceReference(
                ManifestSchemaVersion:
                    namespaceManifest.SchemaVersion,
                RootWindowsLogicalPath:
                    namespaceManifest.RootWindowsLogicalPath,
                ManifestSha256:
                    namespaceEvidence.ManifestSha256!
            );

        var planned =
            new List<
                DataRelativePathRepairBatchAggregateNamespacePlannedChild>(
                    candidates.Count
                );

        var decisions =
            new DataRelativePathRepairBatchAggregateNamespacePlanningDecision?[
                candidates.Count
            ];

        for (
            int index = 0;
            index < candidates.Count;
            index++)
        {
            DataRelativePathRepairBatchAggregateNamespacePlanningCandidate?
                candidate =
                    candidates[index];

            if (
                candidate is null ||
                candidate.Manifest is null)
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchAggregateNamespacePlanningDecisionState
                            .InvalidCandidate,
                        error:
                            "Aggregate namespace planning candidate is null " +
                            "or has no plan manifest."
                    );

                continue;
            }

            if (!IsValidChildName(
                    candidate.ChildName))
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchAggregateNamespacePlanningDecisionState
                            .InvalidChildName,
                        error:
                            "Candidate child name must identify exactly one " +
                            "direct child."
                    );

                continue;
            }

            string? planValidationError;

            try
            {
                planValidationError =
                    DataRelativePathRepairPlanManifest.Validate(
                        candidate.Manifest
                    );
            }
            catch (Exception ex)
            {
                planValidationError =
                    ex.Message;
            }

            if (
                planValidationError is not null ||
                candidate.Manifest.SchemaVersion !=
                    DataRelativePathRepairPlanManifestRecord
                        .SchemaVersion4 ||
                !string.Equals(
                    candidate.Manifest.DataRoot,
                    dataRoot.FullPath,
                    StringComparison.Ordinal))
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchAggregateNamespacePlanningDecisionState
                            .InvalidPlanManifest,
                        error:
                            planValidationError ??
                            "Candidate must be an exact schema-v4 plan " +
                            "manifest bound to the trusted Data root."
                    );

                continue;
            }

            byte[] manifestBytes;

            try
            {
                manifestBytes =
                    DataRelativePathRepairPlanManifestJson.Serialize(
                        candidate.Manifest
                    );
            }
            catch (Exception ex)
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchAggregateNamespacePlanningDecisionState
                            .ManifestSerializationFailed,
                        error:
                            ex.Message
                    );

                continue;
            }

            string manifestSha256 =
                Convert.ToHexString(
                    SHA256.HashData(
                        manifestBytes
                    )
                );

            DataRelativePathRepairSourceGenerationBinding binding =
                DataRelativePathRepairSourceGenerationBinder.Bind(
                    dataRoot,
                    candidate.Manifest.SourceSnapshot
                );

            if (!binding.Success)
            {
                decisions[index] =
                    Decision(
                        index,
                        DataRelativePathRepairBatchAggregateNamespacePlanningDecisionState
                            .SourceGenerationBindingFailed,
                        sourceGenerationBindingState:
                            binding.State,
                        error:
                            binding.Error ??
                            binding.State.ToString()
                    );

                continue;
            }

            planned.Add(
                new(
                    CandidateIndex:
                        index,
                    ChildName:
                        candidate.ChildName,
                    Manifest:
                        candidate.Manifest,
                    ManifestSha256:
                        manifestSha256,
                    SourceInodeGeneration:
                        binding.SourceInodeGeneration!.Value
                )
            );
        }

        if (
            decisions.Any(
                decision =>
                    decision is not null))
        {
            for (
                int index = 0;
                index < decisions.Length;
                index++)
            {
                decisions[index] ??=
                    Decision(
                        index,
                        DataRelativePathRepairBatchAggregateNamespacePlanningDecisionState
                            .InvalidCandidate,
                        error:
                            "Another candidate failed preparation; no " +
                            "partial candidate subset is authorized."
                    );
            }

            return Result(
                DataRelativePathRepairBatchAggregateNamespacePlanState
                    .CandidatePreparationFailed,
                planned,
                decisions.Select(
                        decision =>
                            decision!
                    )
                    .ToArray(),
                namespaceReference:
                    namespaceReference,
                error:
                    "One or more candidates failed preparation."
            );
        }

        if (
            planned
                .Select(
                    child =>
                        child.ChildName
                )
                .Distinct(
                    StringComparer.Ordinal
                )
                .Count() !=
                planned.Count ||
            planned
                .Select(
                    child =>
                        child.Manifest.PlanId
                )
                .Distinct()
                .Count() !=
                planned.Count)
        {
            return Result(
                DataRelativePathRepairBatchAggregateNamespacePlanState
                    .InvalidInput,
                planned,
                planned
                    .Select(
                        child =>
                            Decision(
                                child.CandidateIndex,
                                DataRelativePathRepairBatchAggregateNamespacePlanningDecisionState
                                    .InvalidCandidate,
                                error:
                                    "Candidate child names and PlanId values " +
                                    "must each be unique."
                            )
                    )
                    .OrderBy(
                        decision =>
                            decision.CandidateIndex
                    )
                    .ToArray(),
                namespaceReference:
                    namespaceReference,
                error:
                    "Duplicate candidate child name or PlanId."
            );
        }

        DataRelativePathRepairBatchManifestChild[] children =
            planned
                .Select(
                    child =>
                        new DataRelativePathRepairBatchManifestChild(
                            ChildName:
                                child.ChildName,
                            PlanId:
                                child.Manifest.PlanId,
                            ManifestSha256:
                                child.ManifestSha256
                        )
                )
                .ToArray();

        DataRelativePathRepairBatchManifestRecord ephemeralBatch =
            new(
                SchemaVersion:
                    DataRelativePathRepairBatchManifestRecord.SchemaVersion4,
                BatchId:
                    batchId,
                CreatedUtc:
                    createdUtc,
                DataRoot:
                    dataRoot.FullPath,
                ChildManifestName:
                    childManifestName,
                InputPathCount:
                    candidates.Count,
                SafeRejectionCount:
                    0,
                Children:
                    children
            )
            {
                CoveragePolicyVersion =
                    DataRelativePathRepairBatchManifestRecord
                        .CoveragePolicyVersion3,
                AggregateNamespaceEvidence =
                    [
                        namespaceReference
                    ]
            };

        string? batchValidationError =
            DataRelativePathRepairBatchManifest.Validate(
                ephemeralBatch
            );

        if (batchValidationError is not null)
        {
            return Result(
                DataRelativePathRepairBatchAggregateNamespacePlanState
                    .InvalidInput,
                planned,
                planned
                    .Select(
                        child =>
                            Decision(
                                child.CandidateIndex,
                                DataRelativePathRepairBatchAggregateNamespacePlanningDecisionState
                                    .InvalidCandidate,
                                error:
                                    batchValidationError
                            )
                    )
                    .OrderBy(
                        decision =>
                            decision.CandidateIndex
                    )
                    .ToArray(),
                namespaceReference:
                    namespaceReference,
                error:
                    batchValidationError
            );
        }

        DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate[]
            coverageCandidates =
                planned
                    .OrderBy(
                        child =>
                            child.CandidateIndex
                    )
                    .Select(
                        child =>
                            new DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate(
                                Manifest:
                                    child.Manifest,
                                SourceInodeGeneration:
                                    child.SourceInodeGeneration
                            )
                    )
                    .ToArray();

        var suppliedEvidence =
            new[]
            {
                new DataRelativePathRepairBatchAggregateNamespaceCoverageEvidence(
                    RootWindowsLogicalPath:
                        namespaceReference.RootWindowsLogicalPath,
                    ReadResult:
                        namespaceEvidence
                )
            };

        DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorization
            authorization =
                DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorizer
                    .Authorize(
                        ephemeralBatch,
                        coverageCandidates,
                        suppliedEvidence
                    );

        var mappedDecisions =
            new DataRelativePathRepairBatchAggregateNamespacePlanningDecision[
                candidates.Count
            ];

        foreach (
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecision
                coverageDecision
            in authorization.Decisions)
        {
            mappedDecisions[
                coverageDecision.CandidateIndex
            ] =
                coverageDecision.Authorized
                    ? Decision(
                        coverageDecision.CandidateIndex,
                        DataRelativePathRepairBatchAggregateNamespacePlanningDecisionState
                            .Authorized,
                        coverageDecisionState:
                            coverageDecision.State
                    )
                    : Decision(
                        coverageDecision.CandidateIndex,
                        DataRelativePathRepairBatchAggregateNamespacePlanningDecisionState
                            .CoverageRejected,
                        coverageDecisionState:
                            coverageDecision.State,
                        error:
                            coverageDecision.Error ??
                            coverageDecision.State.ToString()
                    );
        }

        if (!authorization.AllAuthorized)
        {
            return Result(
                DataRelativePathRepairBatchAggregateNamespacePlanState
                    .CoverageRejected,
                planned,
                mappedDecisions,
                namespaceReference,
                authorization,
                error:
                    "Aggregate namespace coverage policy v3 rejected one " +
                    "or more candidates."
            );
        }

        DataRelativePathRepairBatchManifestCreation creation =
            DataRelativePathRepairBatchManifest
                .CreateAggregateNamespaceCoverageAuthorized(
                    batchId,
                    createdUtc,
                    dataRoot.FullPath,
                    childManifestName,
                    candidates.Count,
                    safeRejectionCount:
                        0,
                    children,
                    [
                        namespaceReference
                    ]
                );

        if (
            !creation.Success ||
            creation.Manifest is null)
        {
            return Result(
                DataRelativePathRepairBatchAggregateNamespacePlanState
                    .BatchManifestCreationFailed,
                planned,
                mappedDecisions,
                namespaceReference,
                authorization,
                error:
                    creation.Error ??
                    creation.State.ToString()
            );
        }

        return Result(
            DataRelativePathRepairBatchAggregateNamespacePlanState.Planned,
            planned,
            mappedDecisions,
            namespaceReference,
            authorization,
            batchManifest:
                creation.Manifest
        );
    }

    private static bool IsValidChildName(
        string? childName)
    {
        return
            !string.IsNullOrWhiteSpace(
                childName) &&
            childName is not "." and not ".." &&
            !childName.Contains('/') &&
            !childName.Contains('\\') &&
            !childName.Contains('\0');
    }

    private static
        DataRelativePathRepairBatchAggregateNamespacePlanningDecision
        Decision(
            int candidateIndex,
            DataRelativePathRepairBatchAggregateNamespacePlanningDecisionState
                state,
            DataRelativePathRepairSourceGenerationBindingState?
                sourceGenerationBindingState = null,
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState?
                coverageDecisionState = null,
            string? error = null)
    {
        return new(
            CandidateIndex:
                candidateIndex,
            State:
                state,
            SourceGenerationBindingState:
                sourceGenerationBindingState,
            CoverageDecisionState:
                coverageDecisionState,
            Error:
                error
        );
    }

    private static DataRelativePathRepairBatchAggregateNamespacePlanResult
        Result(
            DataRelativePathRepairBatchAggregateNamespacePlanState state,
            IReadOnlyList<
                DataRelativePathRepairBatchAggregateNamespacePlannedChild
            > plannedChildren,
            IReadOnlyList<
                DataRelativePathRepairBatchAggregateNamespacePlanningDecision
            > decisions,
            DataRelativePathRepairBatchAggregateNamespaceEvidenceReference?
                namespaceReference = null,
            DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorization?
                authorization = null,
            string? error = null,
            DataRelativePathRepairBatchManifestRecord? batchManifest = null)
    {
        return new(
            State:
                state,
            BatchManifest:
                batchManifest,
            PlannedChildren:
                plannedChildren,
            Decisions:
                decisions,
            NamespaceEvidenceReference:
                namespaceReference,
            CoverageAuthorization:
                authorization,
            Error:
                error
        );
    }
}
