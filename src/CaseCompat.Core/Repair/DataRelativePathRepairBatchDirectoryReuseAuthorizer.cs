using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairBatchDirectoryReuseAuthorizationState
{
    Authorized,

    InvalidOperation,
    DestinationParentBindingMismatch,
    DestinationParentAuthorityUnavailable,

    CurrentChildOpenFailed,
    CurrentManifestReadFailed,
    CurrentManifestExpectationMismatch,
    CurrentOperationBindingMismatch,

    OwnerInspectionFailed,

    CurrentDestinationOpenFailed,
    CurrentDestinationIncarnationUnavailable,
    CurrentDestinationIncarnationMismatch
}

public sealed record DataRelativePathRepairBatchDirectoryReuseAuthorization(
    DataRelativePathRepairBatchDirectoryReuseAuthorizationState State,
    DataRelativePathRepairBatchDirectoryOwnerInspection? OwnerInspection,
    DataRelativePathRepairBatchDirectoryOwnerEvidence? OwnerEvidence,
    DataRelativePathRepairPlanManifestReaderResult? CurrentManifestRead,
    LinuxOpenChildDirectoryReadOnlyAtState? CurrentDestinationOpenState,
    LinuxOpenedDirectoryIncarnationResult? CurrentDestinationIncarnation,
    DataRelativePathRepairDirectoryBatchReuseProvenance? Provenance,
    string? Error
)
{
    public bool Success =>
        State ==
            DataRelativePathRepairBatchDirectoryReuseAuthorizationState
                .Authorized &&
        OwnerEvidence is not null &&
        CurrentDestinationIncarnation?.Success == true &&
        Provenance is not null;
}

/*
 * Read-only same-batch directory-reuse authorization.
 *
 * This component combines:
 *
 *   - exact current-child durable batch membership;
 *   - exact authenticated current manifest operation;
 *   - the caller's already-validated destination-parent lease;
 *   - an earlier true schema-v2 owned Applied directory journal; and
 *   - the current descriptor-derived final-directory incarnation.
 *
 * Authorization succeeds only when the exact current directory is the
 * same strong incarnation previously created by the authenticated owner.
 *
 * This component deliberately does NOT:
 *
 *   - create or replace a journal;
 *   - invoke CreateBatchReuseApplied;
 *   - change standalone repair-apply behavior;
 *   - treat a schema-v3 BatchReused journal as ownership authority.
 *
 * The result is point-in-time evidence only. Future durable publication
 * must still be performed inside the forward transaction and must
 * revalidate the live final name before allowing later operations to
 * consume it.
 */
public static class
    DataRelativePathRepairBatchDirectoryReuseAuthorizer
{
    public static DataRelativePathRepairBatchDirectoryReuseAuthorization
        Authorize(
            LinuxNoFollowPathHandle batchDirectory,
            DataRelativePathRepairBatchExecutionContext context,
            DataRelativePathRepairValidatedDestinationParentLease
                destinationParent,
            DataRelativePathRepairPlanManifestOperation currentEntry)
    {
        ArgumentNullException.ThrowIfNull(
            batchDirectory
        );

        ArgumentNullException.ThrowIfNull(
            context
        );

        ArgumentNullException.ThrowIfNull(
            destinationParent
        );

        ArgumentNullException.ThrowIfNull(
            currentEntry
        );

        DataRelativePathRepairPlanOperation operation =
            currentEntry.Operation;

        if (
            operation.Kind !=
                DataRelativePathRepairPlanOperationKind.CreateDirectory ||
            operation.SourcePath is not null ||
            currentEntry.Index < 0 ||
            string.IsNullOrWhiteSpace(
                operation.DestinationPath
            ) ||
            !Path.IsPathFullyQualified(
                operation.DestinationPath
            ))
        {
            return Result(
                DataRelativePathRepairBatchDirectoryReuseAuthorizationState
                    .InvalidOperation,
                error:
                    "Batch directory reuse requires one authenticated " +
                    "absolute CreateDirectory operation with no source."
            );
        }

        string destinationPath;
        string destinationParentPath;
        string expectedParentPath;
        string openedParentPath;
        string contextDataRoot;
        string openedRootPath;

        try
        {
            destinationPath =
                Path.GetFullPath(
                    operation.DestinationPath
                );

            string? parent =
                Path.GetDirectoryName(
                    destinationPath
                );

            if (string.IsNullOrWhiteSpace(parent))
            {
                return Result(
                    DataRelativePathRepairBatchDirectoryReuseAuthorizationState
                        .InvalidOperation,
                    error:
                        "The CreateDirectory destination has no lexical " +
                        "parent."
                );
            }

            destinationParentPath =
                Path.GetFullPath(
                    parent
                );

            expectedParentPath =
                Path.GetFullPath(
                    destinationParent.ExpectedSnapshot.PhysicalPath
                );

            openedParentPath =
                Path.GetFullPath(
                    destinationParent.OpenedPath.FullPath
                );

            contextDataRoot =
                Path.GetFullPath(
                    context.DataRoot
                );

            openedRootPath =
                Path.GetFullPath(
                    destinationParent.OpenedPath.RootPath
                );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathRepairBatchDirectoryReuseAuthorizationState
                    .DestinationParentBindingMismatch,
                error:
                    "The operation or validated-parent path metadata " +
                    "could not be normalized: " +
                    ex.Message
            );
        }

        if (
            !string.Equals(
                destinationParentPath,
                expectedParentPath,
                StringComparison.Ordinal
            ) ||
            !string.Equals(
                destinationParentPath,
                openedParentPath,
                StringComparison.Ordinal
            ) ||
            !string.Equals(
                contextDataRoot,
                openedRootPath,
                StringComparison.Ordinal
            ))
        {
            return Result(
                DataRelativePathRepairBatchDirectoryReuseAuthorizationState
                    .DestinationParentBindingMismatch,
                error:
                    "The validated destination-parent lease is not bound " +
                    "to the exact parent of the current CreateDirectory " +
                    "operation and current batch Data root."
            );
        }

        LinuxOpenedDirectorySnapshotResult actualParentSnapshot =
            destinationParent.ActualSnapshot;

        LinuxDirectoryIncarnationIdentity?
            parentIncarnation =
                destinationParent.IncarnationIdentity;

        if (
            !actualParentSnapshot.Success ||
            actualParentSnapshot.Identity is null ||
            actualParentSnapshot.CasefoldEnabled is not false ||
            destinationParent.ExpectedSnapshot.CasefoldEnabled ||
            !destinationParent.ActualIncarnation.Success ||
            parentIncarnation is null)
        {
            return Result(
                DataRelativePathRepairBatchDirectoryReuseAuthorizationState
                    .DestinationParentAuthorityUnavailable,
                error:
                    "The supplied destination-parent lease does not " +
                    "currently provide the complete strict, descriptor-" +
                    "derived authority required for directory reuse."
            );
        }

        LinuxFileIdentityResult actualParentIdentity =
            actualParentSnapshot.Identity;

        if (
            !destinationParent.ExpectedSnapshot.Identity
                .SameObjectAs(
                    actualParentIdentity
                ) ||
            !actualParentIdentity
                .SameObjectAs(
                    parentIncarnation.PhysicalIdentity
                ))
        {
            return Result(
                DataRelativePathRepairBatchDirectoryReuseAuthorizationState
                    .DestinationParentAuthorityUnavailable,
                error:
                    "The supplied destination-parent lease does not " +
                    "currently provide the complete strict, descriptor-" +
                    "derived authority required for directory reuse."
            );
        }

        LinuxOpenChildDirectoryReadOnlyAtResult currentChildOpen =
            LinuxOpenChildDirectoryReadOnlyAt.Open(
                batchDirectory,
                context.CurrentChild.ChildName
            );

        if (
            !currentChildOpen.Success ||
            currentChildOpen.OpenedDirectory is null)
        {
            return Result(
                DataRelativePathRepairBatchDirectoryReuseAuthorizationState
                    .CurrentChildOpenFailed,
                error:
                    currentChildOpen.Error ??
                    currentChildOpen.State.ToString()
            );
        }

        DataRelativePathRepairPlanManifestReaderResult currentManifestRead;

        using (
            LinuxNoFollowPathHandle currentChildDirectory =
                currentChildOpen.OpenedDirectory)
        {
            currentManifestRead =
                DataRelativePathRepairPlanManifestReader.Read(
                    currentChildDirectory,
                    context.ChildManifestName
                );
        }

        if (!currentManifestRead.Success)
        {
            return Result(
                DataRelativePathRepairBatchDirectoryReuseAuthorizationState
                    .CurrentManifestReadFailed,
                currentManifestRead:
                    currentManifestRead,
                error:
                    currentManifestRead.Error ??
                    currentManifestRead.State.ToString()
            );
        }

        DataRelativePathRepairPlanManifestRecord currentManifest =
            currentManifestRead.Manifest!;

        if (
            currentManifest.PlanId !=
                context.CurrentChild.PlanId ||
            currentManifestRead.ManifestSha256 is null ||
            !string.Equals(
                currentManifestRead.ManifestSha256,
                context.CurrentChild.ManifestSha256,
                StringComparison.OrdinalIgnoreCase
            ) ||
            !string.Equals(
                currentManifest.DataRoot,
                context.DataRoot,
                StringComparison.Ordinal
            ))
        {
            return Result(
                DataRelativePathRepairBatchDirectoryReuseAuthorizationState
                    .CurrentManifestExpectationMismatch,
                currentManifestRead:
                    currentManifestRead,
                error:
                    "The current child no longer matches its exact " +
                    "durable batch PlanId, manifest SHA-256, or Data-root " +
                    "expectation."
            );
        }

        if (
            currentEntry.Index >=
                currentManifest.Operations.Count ||
            !SameEntry(
                currentEntry,
                currentManifest.Operations[
                    currentEntry.Index
                ]
            ))
        {
            return Result(
                DataRelativePathRepairBatchDirectoryReuseAuthorizationState
                    .CurrentOperationBindingMismatch,
                currentManifestRead:
                    currentManifestRead,
                error:
                    "The supplied CreateDirectory entry is not the exact " +
                    "authenticated operation at that index in the current " +
                    "durable child manifest."
            );
        }

        DataRelativePathRepairBatchDirectoryOwnerInspection
            ownerInspection =
                DataRelativePathRepairBatchDirectoryOwnerInspector.Inspect(
                    batchDirectory,
                    context,
                    destinationPath
                );

        if (!ownerInspection.Success)
        {
            return Result(
                DataRelativePathRepairBatchDirectoryReuseAuthorizationState
                    .OwnerInspectionFailed,
                ownerInspection:
                    ownerInspection,
                currentManifestRead:
                    currentManifestRead,
                error:
                    ownerInspection.Error ??
                    ownerInspection.State.ToString()
            );
        }

        DataRelativePathRepairBatchDirectoryOwnerEvidence owner =
            ownerInspection.Evidence!;

        string finalChildName =
            Path.GetFileName(
                destinationPath
            );

        LinuxOpenChildDirectoryReadOnlyAtResult destinationOpen =
            LinuxOpenChildDirectoryReadOnlyAt.Open(
                destinationParent.OpenedPath,
                finalChildName
            );

        if (
            !destinationOpen.Success ||
            destinationOpen.OpenedDirectory is null)
        {
            return Result(
                DataRelativePathRepairBatchDirectoryReuseAuthorizationState
                    .CurrentDestinationOpenFailed,
                ownerInspection:
                    ownerInspection,
                ownerEvidence:
                    owner,
                currentManifestRead:
                    currentManifestRead,
                currentDestinationOpenState:
                    destinationOpen.State,
                error:
                    destinationOpen.Error ??
                    destinationOpen.State.ToString()
            );
        }

        LinuxOpenedDirectoryIncarnationResult currentIncarnation;

        using (
            LinuxNoFollowPathHandle openedDestination =
                destinationOpen.OpenedDirectory)
        {
            currentIncarnation =
                LinuxOpenedDirectoryIncarnation.Capture(
                    openedDestination
                );
        }

        if (
            !currentIncarnation.Success ||
            currentIncarnation.Identity is null)
        {
            return Result(
                DataRelativePathRepairBatchDirectoryReuseAuthorizationState
                    .CurrentDestinationIncarnationUnavailable,
                ownerInspection:
                    ownerInspection,
                ownerEvidence:
                    owner,
                currentManifestRead:
                    currentManifestRead,
                currentDestinationOpenState:
                    destinationOpen.State,
                currentDestinationIncarnation:
                    currentIncarnation,
                error:
                    currentIncarnation.Error ??
                    currentIncarnation.State.ToString()
            );
        }

        LinuxDirectoryIncarnationIdentity currentIdentity =
            currentIncarnation.Identity;

        if (
            !owner.OwnedDirectoryIncarnationIdentity
                .SameIncarnationAs(
                    currentIdentity
                ))
        {
            return Result(
                DataRelativePathRepairBatchDirectoryReuseAuthorizationState
                    .CurrentDestinationIncarnationMismatch,
                ownerInspection:
                    ownerInspection,
                ownerEvidence:
                    owner,
                currentManifestRead:
                    currentManifestRead,
                currentDestinationOpenState:
                    destinationOpen.State,
                currentDestinationIncarnation:
                    currentIncarnation,
                error:
                    "The current destination directory is not the same " +
                    "strong filesystem incarnation created by the " +
                    "authenticated earlier batch owner."
            );
        }

        var provenance =
            new DataRelativePathRepairDirectoryBatchReuseProvenance(
                BatchId:
                    context.BatchId,
                OwnerChildName:
                    owner.OwnerChildName,
                OwnerPlanId:
                    owner.OwnerPlanId,
                OwnerManifestSha256:
                    owner.OwnerManifestSha256,
                OwnerOperationIndex:
                    owner.OwnerOperationIndex,
                OwnerJournalChildName:
                    owner.OwnerJournalChildName,
                ReusedDirectoryIncarnationIdentity:
                    currentIdentity
            );

        return Result(
            DataRelativePathRepairBatchDirectoryReuseAuthorizationState
                .Authorized,
            ownerInspection:
                ownerInspection,
            ownerEvidence:
                owner,
            currentManifestRead:
                currentManifestRead,
            currentDestinationOpenState:
                destinationOpen.State,
            currentDestinationIncarnation:
                currentIncarnation,
            provenance:
                provenance
        );
    }

    private static bool SameEntry(
        DataRelativePathRepairPlanManifestOperation left,
        DataRelativePathRepairPlanManifestOperation right)
    {
        return
            left.Index ==
                right.Index &&
            string.Equals(
                left.JournalChildName,
                right.JournalChildName,
                StringComparison.Ordinal
            ) &&
            SameOperation(
                left.Operation,
                right.Operation
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

    private static DataRelativePathRepairBatchDirectoryReuseAuthorization
        Result(
            DataRelativePathRepairBatchDirectoryReuseAuthorizationState state,
            DataRelativePathRepairBatchDirectoryOwnerInspection?
                ownerInspection = null,
            DataRelativePathRepairBatchDirectoryOwnerEvidence?
                ownerEvidence = null,
            DataRelativePathRepairPlanManifestReaderResult?
                currentManifestRead = null,
            LinuxOpenChildDirectoryReadOnlyAtState?
                currentDestinationOpenState = null,
            LinuxOpenedDirectoryIncarnationResult?
                currentDestinationIncarnation = null,
            DataRelativePathRepairDirectoryBatchReuseProvenance?
                provenance = null,
            string? error = null)
    {
        return new(
            State:
                state,
            OwnerInspection:
                ownerInspection,
            OwnerEvidence:
                ownerEvidence,
            CurrentManifestRead:
                currentManifestRead,
            CurrentDestinationOpenState:
                currentDestinationOpenState,
            CurrentDestinationIncarnation:
                currentDestinationIncarnation,
            Provenance:
                provenance,
            Error:
                error
        );
    }
}
