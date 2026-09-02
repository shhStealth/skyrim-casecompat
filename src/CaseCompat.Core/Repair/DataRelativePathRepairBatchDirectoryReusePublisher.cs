using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairBatchDirectoryReusePublicationState
{
    PublishedDurably,

    InvalidOperation,
    DestinationParentBindingMismatch,
    DestinationParentAuthorityUnavailable,

    JournalLockUnavailable,

    DestinationOpenFailed,
    DestinationIncarnationUnavailable,
    DestinationIncarnationMismatch,

    RecordCreationFailed,
    JournalWriteFailed
}

public sealed record
    DataRelativePathRepairBatchDirectoryReusePublication(
        DataRelativePathRepairBatchDirectoryReusePublicationState
            State,
        LinuxExclusiveDirectoryLockState?
            JournalLockState,
        LinuxOpenChildDirectoryReadOnlyAtState?
            DestinationOpenState,
        LinuxOpenedDirectoryIncarnationResult?
            DestinationIncarnation,
        DataRelativePathRepairDirectoryJournalTransitionResult?
            RecordCreation,
        DataRelativePathRepairDirectoryJournalWriterResult?
            JournalWrite,
        string?
            Error
    )
{
    public bool Success =>
        State ==
            DataRelativePathRepairBatchDirectoryReusePublicationState
                .PublishedDurably;
}

/*
 * Durable publication of already-authorized same-batch directory reuse.
 *
 * This component deliberately does NOT decide whether reuse is authorized.
 * The caller must first obtain provenance from
 * DataRelativePathRepairBatchDirectoryReuseAuthorizer using the SAME
 * retained destination-parent lease supplied here.
 *
 * Before publishing the non-owning schema-v3 journal, this component:
 *
 *   - revalidates that the retained parent lease is bound to the exact
 *     current CreateDirectory operation;
 *   - reopens the exact final child descriptor-relative under that lease;
 *   - recaptures its generation-aware directory incarnation;
 *   - requires that incarnation to match the authorized provenance; and
 *   - retains the exact destination descriptor through durable journal
 *     publication.
 *
 * It never creates, replaces, removes, or otherwise mutates the reused
 * destination directory.
 */
public static class
    DataRelativePathRepairBatchDirectoryReusePublisher
{
    public static DataRelativePathRepairBatchDirectoryReusePublication
        PublishAuthorized(
            LinuxNoFollowPathHandle journalDirectory,
            string journalChildName,
            DataRelativePathRepairValidatedDestinationParentLease
                destinationParent,
            DataRelativePathRepairPlanManifestOperation currentEntry,
            string trustedDataRoot,
            DateTimeOffset nowUtc,
            DataRelativePathRepairDirectoryBatchReuseProvenance
                provenance)
    {
        ArgumentNullException.ThrowIfNull(
            journalDirectory
        );

        ArgumentNullException.ThrowIfNull(
            destinationParent
        );

        ArgumentNullException.ThrowIfNull(
            currentEntry
        );

        ArgumentNullException.ThrowIfNull(
            provenance
        );

        DataRelativePathRepairPlanOperation operation =
            currentEntry.Operation;

        if (
            operation.Kind !=
                DataRelativePathRepairPlanOperationKind.CreateDirectory ||
            operation.SourcePath is not null ||
            string.IsNullOrWhiteSpace(
                operation.DestinationPath
            ) ||
            !Path.IsPathFullyQualified(
                operation.DestinationPath
            ))
        {
            return Result(
                DataRelativePathRepairBatchDirectoryReusePublicationState
                    .InvalidOperation,
                error:
                    "Batch reuse publication requires one absolute " +
                    "CreateDirectory operation with no source."
            );
        }

        string destinationPath;
        string destinationParentPath;
        string expectedParentPath;
        string openedParentPath;
        string openedRootPath;
        string fullTrustedDataRoot;

        try
        {
            destinationPath =
                Path.GetFullPath(
                    operation.DestinationPath
                );

            string? lexicalParent =
                Path.GetDirectoryName(
                    destinationPath
                );

            if (string.IsNullOrWhiteSpace(lexicalParent))
            {
                return Result(
                    DataRelativePathRepairBatchDirectoryReusePublicationState
                        .InvalidOperation,
                    error:
                        "The reused directory destination has no lexical " +
                        "parent."
                );
            }

            destinationParentPath =
                Path.GetFullPath(
                    lexicalParent
                );

            expectedParentPath =
                Path.GetFullPath(
                    destinationParent.ExpectedSnapshot.PhysicalPath
                );

            openedParentPath =
                Path.GetFullPath(
                    destinationParent.OpenedPath.FullPath
                );

            openedRootPath =
                Path.GetFullPath(
                    destinationParent.OpenedPath.RootPath
                );

            fullTrustedDataRoot =
                Path.GetFullPath(
                    trustedDataRoot
                );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathRepairBatchDirectoryReusePublicationState
                    .DestinationParentBindingMismatch,
                error:
                    "The reuse-publication path metadata could not be " +
                    "normalized: " +
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
                fullTrustedDataRoot,
                openedRootPath,
                StringComparison.Ordinal
            ))
        {
            return Result(
                DataRelativePathRepairBatchDirectoryReusePublicationState
                    .DestinationParentBindingMismatch,
                error:
                    "The retained destination-parent lease is not bound " +
                    "to the exact current CreateDirectory destination " +
                    "and trusted Data root."
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
                DataRelativePathRepairBatchDirectoryReusePublicationState
                    .DestinationParentAuthorityUnavailable,
                error:
                    "The retained destination-parent lease does not " +
                    "provide complete strict generation-aware authority " +
                    "for batch reuse publication."
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
                DataRelativePathRepairBatchDirectoryReusePublicationState
                    .DestinationParentAuthorityUnavailable,
                error:
                    "The retained destination-parent lease no longer " +
                    "provides the physical identity required for batch " +
                    "reuse publication."
            );
        }

        LinuxExclusiveDirectoryLockResult lockResult =
            LinuxExclusiveDirectoryLock.Acquire(
                journalDirectory
            );

        if (
            !lockResult.Success ||
            lockResult.Lease is null)
        {
            return Result(
                DataRelativePathRepairBatchDirectoryReusePublicationState
                    .JournalLockUnavailable,
                journalLockState:
                    lockResult.State,
                error:
                    lockResult.Error ??
                    lockResult.State.ToString()
            );
        }

        using LinuxExclusiveDirectoryLockLease journalLock =
            lockResult.Lease;

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
                DataRelativePathRepairBatchDirectoryReusePublicationState
                    .DestinationOpenFailed,
                journalLockState:
                    lockResult.State,
                destinationOpenState:
                    destinationOpen.State,
                error:
                    destinationOpen.Error ??
                    destinationOpen.State.ToString()
            );
        }

        /*
         * Keep this descriptor alive through journal publication.
         *
         * Provenance remains tied to this exact strong incarnation even if
         * later namespace activity causes the next recovery classification
         * to report a missing or conflicting final name.
         */
        using LinuxNoFollowPathHandle openedDestination =
            destinationOpen.OpenedDirectory;

        LinuxOpenedDirectoryIncarnationResult destinationIncarnation =
            LinuxOpenedDirectoryIncarnation.Capture(
                openedDestination
            );

        if (
            !destinationIncarnation.Success ||
            destinationIncarnation.Identity is null)
        {
            return Result(
                DataRelativePathRepairBatchDirectoryReusePublicationState
                    .DestinationIncarnationUnavailable,
                journalLockState:
                    lockResult.State,
                destinationOpenState:
                    destinationOpen.State,
                destinationIncarnation:
                    destinationIncarnation,
                error:
                    destinationIncarnation.Error ??
                    destinationIncarnation.State.ToString()
            );
        }

        if (
            !provenance.ReusedDirectoryIncarnationIdentity
                .SameIncarnationAs(
                    destinationIncarnation.Identity
                ))
        {
            return Result(
                DataRelativePathRepairBatchDirectoryReusePublicationState
                    .DestinationIncarnationMismatch,
                journalLockState:
                    lockResult.State,
                destinationOpenState:
                    destinationOpen.State,
                destinationIncarnation:
                    destinationIncarnation,
                error:
                    "The live destination directory changed after reuse " +
                    "authorization. No BatchReused journal was published."
            );
        }

        DataRelativePathRepairDirectoryJournalTransitionResult
            recordCreation =
                DataRelativePathRepairDirectoryJournal
                    .CreateBatchReuseApplied(
                        Guid.NewGuid(),
                        nowUtc,
                        fullTrustedDataRoot,
                        operation,
                        destinationParent.ExpectedSnapshot,
                        parentIncarnation,
                        provenance
                    );

        if (
            !recordCreation.Success ||
            recordCreation.Record is null)
        {
            return Result(
                DataRelativePathRepairBatchDirectoryReusePublicationState
                    .RecordCreationFailed,
                journalLockState:
                    lockResult.State,
                destinationOpenState:
                    destinationOpen.State,
                destinationIncarnation:
                    destinationIncarnation,
                recordCreation:
                    recordCreation,
                error:
                    recordCreation.Error ??
                    recordCreation.State.ToString()
            );
        }

        DataRelativePathRepairDirectoryJournalWriterResult journalWrite =
            DataRelativePathRepairDirectoryJournalWriter
                .CreateBatchReuseApplied(
                    journalDirectory,
                    journalChildName,
                    recordCreation.Record
                );

        if (!journalWrite.Success)
        {
            return Result(
                DataRelativePathRepairBatchDirectoryReusePublicationState
                    .JournalWriteFailed,
                journalLockState:
                    lockResult.State,
                destinationOpenState:
                    destinationOpen.State,
                destinationIncarnation:
                    destinationIncarnation,
                recordCreation:
                    recordCreation,
                journalWrite:
                    journalWrite,
                error:
                    journalWrite.Error ??
                    journalWrite.State.ToString()
            );
        }

        return Result(
            DataRelativePathRepairBatchDirectoryReusePublicationState
                .PublishedDurably,
            journalLockState:
                lockResult.State,
            destinationOpenState:
                destinationOpen.State,
            destinationIncarnation:
                destinationIncarnation,
            recordCreation:
                recordCreation,
            journalWrite:
                journalWrite
        );
    }

    private static
        DataRelativePathRepairBatchDirectoryReusePublication
        Result(
            DataRelativePathRepairBatchDirectoryReusePublicationState state,
            LinuxExclusiveDirectoryLockState?
                journalLockState = null,
            LinuxOpenChildDirectoryReadOnlyAtState?
                destinationOpenState = null,
            LinuxOpenedDirectoryIncarnationResult?
                destinationIncarnation = null,
            DataRelativePathRepairDirectoryJournalTransitionResult?
                recordCreation = null,
            DataRelativePathRepairDirectoryJournalWriterResult?
                journalWrite = null,
            string?
                error = null)
    {
        return new(
            State:
                state,
            JournalLockState:
                journalLockState,
            DestinationOpenState:
                destinationOpenState,
            DestinationIncarnation:
                destinationIncarnation,
            RecordCreation:
                recordCreation,
            JournalWrite:
                journalWrite,
            Error:
                error
        );
    }
}
