using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairBatchDirectoryOwnerInspectionState
{
    Authorized,

    InvalidContext,
    InvalidDestinationPath,

    EarlierChildOpenFailed,
    EarlierManifestReadFailed,
    EarlierManifestExpectationMismatch,

    OwnerJournalReadFailed,
    OwnerJournalBindingMismatch,
    OwnerJournalNotForwardApplied,

    AmbiguousOwnedDirectoryAuthority,
    NoOwnedDirectoryAuthority
}

public sealed record DataRelativePathRepairBatchDirectoryOwnerEvidence(
    Guid BatchId,
    int OwnerChildIndex,
    string OwnerChildName,
    Guid OwnerPlanId,
    string OwnerManifestSha256,
    int OwnerOperationIndex,
    string OwnerJournalChildName,
    Guid OwnerJournalId,
    LinuxDirectoryIncarnationIdentity
        OwnedDirectoryIncarnationIdentity
);

public sealed record DataRelativePathRepairBatchDirectoryOwnerInspection(
    DataRelativePathRepairBatchDirectoryOwnerInspectionState State,
    DataRelativePathRepairBatchDirectoryOwnerEvidence? Evidence,
    string? FailedChildName,
    string? Error
)
{
    public bool Success =>
        State ==
            DataRelativePathRepairBatchDirectoryOwnerInspectionState
                .Authorized &&
        Evidence is not null;
}

/*
 * Read-only proof of earlier same-batch directory ownership.
 *
 * This component grants no filesystem mutation authority.
 *
 * It proves only that:
 *
 *   1. an expected earlier durable batch member can still be reopened
 *      beneath the caller-retained batch directory descriptor;
 *
 *   2. its exact current manifest bytes still produce the expected
 *      PlanId and SHA-256;
 *
 *   3. the authenticated manifest contains a CreateDirectory operation
 *      for the exact requested destination path;
 *
 *   4. that operation's deterministic journal child still contains a
 *      valid schema-v2 Applied owned-directory journal; and
 *
 *   5. that journal contains the strong prepared-directory incarnation
 *      identity required by the existing owned-directory lifecycle.
 *
 * Schema-v3 BatchReused journals are explicitly NOT ownership authority.
 * They may be traversed while searching farther back for the true
 * schema-v2 owner, but they can never mint chained reuse authority.
 *
 * This component deliberately does NOT:
 *
 *   - open or inspect the current destination directory;
 *   - compare a current destination to the recorded owner incarnation;
 *   - create or replace any journal;
 *   - invoke CreateBatchReuseApplied;
 *   - alter standalone repair-apply behavior.
 */
public static class
    DataRelativePathRepairBatchDirectoryOwnerInspector
{
    public static DataRelativePathRepairBatchDirectoryOwnerInspection
        Inspect(
            LinuxNoFollowPathHandle batchDirectory,
            DataRelativePathRepairBatchExecutionContext context,
            string destinationPath)
    {
        ArgumentNullException.ThrowIfNull(
            batchDirectory
        );

        ArgumentNullException.ThrowIfNull(
            context
        );

        string? contextError =
            ValidateContext(
                context
            );

        if (contextError is not null)
        {
            return Result(
                DataRelativePathRepairBatchDirectoryOwnerInspectionState
                    .InvalidContext,
                error:
                    contextError
            );
        }

        if (
            string.IsNullOrWhiteSpace(
                destinationPath
            ) ||
            !Path.IsPathFullyQualified(
                destinationPath
            ))
        {
            return Result(
                DataRelativePathRepairBatchDirectoryOwnerInspectionState
                    .InvalidDestinationPath,
                error:
                    "Directory-owner inspection requires an absolute " +
                    "destination path."
            );
        }

        DataRelativePathRepairBatchDirectoryOwnerEvidence?
            authorizedOwner =
                null;

        foreach (
            DataRelativePathRepairBatchExecutionChildExpectation
                expectedChild
            in context.EarlierChildren)
        {
            LinuxOpenChildDirectoryReadOnlyAtResult childOpen =
                LinuxOpenChildDirectoryReadOnlyAt.Open(
                    batchDirectory,
                    expectedChild.ChildName
                );

            if (
                !childOpen.Success ||
                childOpen.OpenedDirectory is null)
            {
                return Result(
                    DataRelativePathRepairBatchDirectoryOwnerInspectionState
                        .EarlierChildOpenFailed,
                    failedChildName:
                        expectedChild.ChildName,
                    error:
                        childOpen.Error ??
                        childOpen.State.ToString()
                );
            }

            using LinuxNoFollowPathHandle childDirectory =
                childOpen.OpenedDirectory;

            DataRelativePathRepairPlanManifestReaderResult manifestRead =
                DataRelativePathRepairPlanManifestReader.Read(
                    childDirectory,
                    context.ChildManifestName
                );

            if (!manifestRead.Success)
            {
                return Result(
                    DataRelativePathRepairBatchDirectoryOwnerInspectionState
                        .EarlierManifestReadFailed,
                    failedChildName:
                        expectedChild.ChildName,
                    error:
                        manifestRead.Error ??
                        manifestRead.State.ToString()
                );
            }

            DataRelativePathRepairPlanManifestRecord ownerManifest =
                manifestRead.Manifest!;

            if (
                ownerManifest.PlanId !=
                    expectedChild.PlanId ||
                manifestRead.ManifestSha256 is null ||
                !string.Equals(
                    manifestRead.ManifestSha256,
                    expectedChild.ManifestSha256,
                    StringComparison.OrdinalIgnoreCase
                ) ||
                !string.Equals(
                    ownerManifest.DataRoot,
                    context.DataRoot,
                    StringComparison.Ordinal
                ))
            {
                return Result(
                    DataRelativePathRepairBatchDirectoryOwnerInspectionState
                        .EarlierManifestExpectationMismatch,
                    failedChildName:
                        expectedChild.ChildName,
                    error:
                        "An earlier child no longer matches its exact " +
                        "durable batch PlanId, manifest SHA-256, or Data " +
                        "root expectation."
                );
            }

            foreach (
                DataRelativePathRepairPlanManifestOperation entry
                in ownerManifest.Operations)
            {
                if (
                    entry.Operation.Kind !=
                        DataRelativePathRepairPlanOperationKind
                            .CreateDirectory ||
                    !string.Equals(
                        entry.Operation.DestinationPath,
                        destinationPath,
                        StringComparison.Ordinal
                    ))
                {
                    continue;
                }

                DataRelativePathRepairDirectoryJournalReaderResult
                    journalRead =
                        DataRelativePathRepairDirectoryJournalReader.Read(
                            childDirectory,
                            entry.JournalChildName
                        );

                if (!journalRead.Success)
                {
                    return Result(
                        DataRelativePathRepairBatchDirectoryOwnerInspectionState
                            .OwnerJournalReadFailed,
                        failedChildName:
                            expectedChild.ChildName,
                        error:
                            journalRead.Error ??
                            journalRead.State.ToString()
                    );
                }

                DataRelativePathRepairDirectoryJournalRecord journal =
                    journalRead.Record!;

                if (
                    !string.Equals(
                        journal.DataRoot,
                        ownerManifest.DataRoot,
                        StringComparison.Ordinal
                    ) ||
                    !SameOperation(
                        journal.Operation,
                        entry.Operation
                    ))
                {
                    return Result(
                        DataRelativePathRepairBatchDirectoryOwnerInspectionState
                            .OwnerJournalBindingMismatch,
                        failedChildName:
                            expectedChild.ChildName,
                        error:
                            "The durable directory journal does not bind " +
                            "to the exact authenticated manifest operation."
                    );
                }

                /*
                 * A previous borrower may itself have the same
                 * CreateDirectory operation in its immutable plan.
                 *
                 * Its v3 journal proves reuse only. It does not own the
                 * directory and therefore cannot authorize another
                 * borrower. Continue searching farther back for the
                 * actual schema-v2 owner.
                 */
                if (
                    journal.SchemaVersion ==
                        DataRelativePathRepairDirectoryJournalRecord
                            .SchemaVersion3 &&
                    journal.OwnershipDisposition ==
                        DataRelativePathRepairDirectoryOwnershipDisposition
                            .BatchReused)
                {
                    if (
                        journal.State !=
                            DataRelativePathRepairDirectoryJournalState
                                .Applied)
                    {
                        return Result(
                            DataRelativePathRepairBatchDirectoryOwnerInspectionState
                                .OwnerJournalNotForwardApplied,
                            failedChildName:
                                expectedChild.ChildName,
                            error:
                                "A matching earlier BatchReused journal " +
                                "is not currently in Applied state."
                        );
                    }

                    continue;
                }

                /*
                 * Ordinary owned-directory journals intentionally remain
                 * schema v2. Schema v2 predates explicit disposition, so
                 * null ownership metadata is the only accepted owner form.
                 *
                 * Do not silently extend this predicate when a future
                 * schema introduces another disposition.
                 */
                bool isOwnedSchemaV2 =
                    journal.SchemaVersion ==
                        DataRelativePathRepairDirectoryJournalRecord
                            .SchemaVersion2 &&
                    journal.OwnershipDisposition is null &&
                    journal.BatchReuseProvenance is null;

                if (
                    !isOwnedSchemaV2 ||
                    journal.State !=
                        DataRelativePathRepairDirectoryJournalState
                            .Applied ||
                    journal.PreparedDirectoryIncarnationIdentity is null)
                {
                    return Result(
                        DataRelativePathRepairBatchDirectoryOwnerInspectionState
                            .OwnerJournalNotForwardApplied,
                        failedChildName:
                            expectedChild.ChildName,
                        error:
                            "A matching earlier directory journal does not " +
                            "prove schema-v2 owned Applied authority with " +
                            "strong prepared-directory incarnation evidence."
                    );
                }

                DataRelativePathRepairBatchDirectoryOwnerEvidence evidence =
                    new(
                        BatchId:
                            context.BatchId,
                        OwnerChildIndex:
                            expectedChild.Index,
                        OwnerChildName:
                            expectedChild.ChildName,
                        OwnerPlanId:
                            expectedChild.PlanId,
                        OwnerManifestSha256:
                            expectedChild.ManifestSha256,
                        OwnerOperationIndex:
                            entry.Index,
                        OwnerJournalChildName:
                            entry.JournalChildName,
                        OwnerJournalId:
                            journal.JournalId,
                        OwnedDirectoryIncarnationIdentity:
                            journal.PreparedDirectoryIncarnationIdentity
                    );

                if (authorizedOwner is not null)
                {
                    return Result(
                        DataRelativePathRepairBatchDirectoryOwnerInspectionState
                            .AmbiguousOwnedDirectoryAuthority,
                        failedChildName:
                            expectedChild.ChildName,
                        error:
                            "More than one earlier child claims schema-v2 " +
                            "owned Applied authority for the exact same " +
                            "directory destination."
                    );
                }

                authorizedOwner =
                    evidence;
            }
        }

        if (authorizedOwner is null)
        {
            return Result(
                DataRelativePathRepairBatchDirectoryOwnerInspectionState
                    .NoOwnedDirectoryAuthority,
                error:
                    "No earlier durable batch child proves owned Applied " +
                    "authority for the exact directory destination."
            );
        }

        return Result(
            DataRelativePathRepairBatchDirectoryOwnerInspectionState
                .Authorized,
            evidence:
                authorizedOwner
        );
    }

    private static bool SameOperation(
        DataRelativePathRepairPlanOperation left,
        DataRelativePathRepairPlanOperation right)
    {
        return
            left.Kind ==
                right.Kind &&
            string.Equals(
                left.DestinationPath,
                right.DestinationPath,
                StringComparison.Ordinal
            ) &&
            string.Equals(
                left.SourcePath,
                right.SourcePath,
                StringComparison.Ordinal
            );
    }

    private static string? ValidateContext(
        DataRelativePathRepairBatchExecutionContext context)
    {
        if (context.BatchId == Guid.Empty)
        {
            return
                "Batch execution context requires a non-empty BatchId.";
        }

        if (
            string.IsNullOrWhiteSpace(
                context.DataRoot
            ) ||
            !Path.IsPathFullyQualified(
                context.DataRoot
            ))
        {
            return
                "Batch execution context requires an absolute Data root.";
        }

        if (
            !IsDirectChildName(
                context.ChildManifestName
            ))
        {
            return
                "Batch execution context child manifest name is invalid.";
        }

        if (
            context.CurrentChildIndex < 0 ||
            context.CurrentChild.Index !=
                context.CurrentChildIndex)
        {
            return
                "Batch execution context current-child index is invalid.";
        }

        if (
            context.EarlierChildren.Count !=
                context.CurrentChildIndex)
        {
            return
                "Batch execution context earlier-child prefix length does " +
                "not match the current durable child index.";
        }

        string? currentError =
            ValidateExpectation(
                context.CurrentChild,
                context.CurrentChildIndex
            );

        if (currentError is not null)
        {
            return
                $"Current child expectation is invalid: {currentError}";
        }

        for (
            int index = 0;
            index < context.EarlierChildren.Count;
            index++)
        {
            string? error =
                ValidateExpectation(
                    context.EarlierChildren[index],
                    index
                );

            if (error is not null)
            {
                return
                    $"Earlier child expectation {index} is invalid: " +
                    error;
            }
        }

        return null;
    }

    private static string? ValidateExpectation(
        DataRelativePathRepairBatchExecutionChildExpectation expectation,
        int expectedIndex)
    {
        if (expectation.Index != expectedIndex)
        {
            return
                $"index {expectation.Index} does not equal " +
                $"{expectedIndex}.";
        }

        if (!IsDirectChildName(expectation.ChildName))
        {
            return
                "child name is not a valid direct child.";
        }

        if (expectation.PlanId == Guid.Empty)
        {
            return
                "PlanId is empty.";
        }

        if (!IsSha256(expectation.ManifestSha256))
        {
            return
                "manifest SHA-256 is invalid.";
        }

        return null;
    }

    private static bool IsDirectChildName(
        string? value)
    {
        if (
            string.IsNullOrEmpty(
                value
            ) ||
            value is "." or "..")
        {
            return false;
        }

        return
            value.IndexOf(
                Path.DirectorySeparatorChar
            ) < 0 &&
            value.IndexOf(
                Path.AltDirectorySeparatorChar
            ) < 0;
    }

    private static bool IsSha256(
        string? value)
    {
        if (
            value is null ||
            value.Length != 64)
        {
            return false;
        }

        foreach (char c in value)
        {
            if (
                !(
                    c is >= '0' and <= '9' ||
                    c is >= 'A' and <= 'F' ||
                    c is >= 'a' and <= 'f'
                ))
            {
                return false;
            }
        }

        return true;
    }

    private static DataRelativePathRepairBatchDirectoryOwnerInspection
        Result(
            DataRelativePathRepairBatchDirectoryOwnerInspectionState state,
            DataRelativePathRepairBatchDirectoryOwnerEvidence? evidence = null,
            string? failedChildName = null,
            string? error = null)
    {
        return new(
            State:
                state,
            Evidence:
                evidence,
            FailedChildName:
                failedChildName,
            Error:
                error
        );
    }
}
