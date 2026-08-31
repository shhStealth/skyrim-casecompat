using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public static class
    DataRelativePathRepairDirectoryForwardRecoveryAction
{
    public static
        DataRelativePathRepairDirectoryForwardRecovery
        Recover(
            LinuxNoFollowPathHandle journalDirectory,
            string journalChildName,
            DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(
            journalDirectory
        );

        /*
         * Lock before reading the journal so cooperating
         * CaseCompat writers cannot advance this transaction
         * beneath the recovery attempt.
         */
        LinuxExclusiveDirectoryLockResult lockResult =
            LinuxExclusiveDirectoryLock.Acquire(
                journalDirectory
            );

        if (!lockResult.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryForwardRecoveryState
                    .LockUnavailable,
                lockState:
                    lockResult.State,
                error:
                    lockResult.Error ??
                    lockResult.State.ToString()
            );
        }

        using LinuxExclusiveDirectoryLockLease lockLease =
            lockResult.Lease!;

        DataRelativePathRepairDirectoryJournalReaderResult read =
            DataRelativePathRepairDirectoryJournalReader.Read(
                journalDirectory,
                journalChildName
            );

        if (!read.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryForwardRecoveryState
                    .JournalReadFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                error:
                    read.Error ??
                    read.State.ToString()
            );
        }

        DataRelativePathRepairDirectoryJournalRecord journal =
            read.Record!;

        DataRelativePathRepairDirectoryRecoveryClassification
            classification =
                DataRelativePathRepairDirectoryRecoveryClassifier
                    .Classify(
                        journal
                    );

        /*
         * This first forward-recovery action intentionally handles
         * one state only:
         *
         *     Prepared
         *     staging name -> recorded inode X
         *     final name   -> missing
         *
         * Re-preparation of a missing staging directory is a
         * separate future recovery milestone.
         */
        if (
            classification.State !=
            DataRelativePathRepairDirectoryRecoveryState
                .PreparedStagingMatchesFinalMissing)
        {
            return Result(
                DataRelativePathRepairDirectoryForwardRecoveryState
                    .RecoveryStateNotEligible,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                error:
                    $"Recovery state {classification.State} does " +
                    "not authorize publication of an existing " +
                    "prepared directory."
            );
        }

        DataRelativePathRepairDestinationParentLeaseAcquisition
            parentAcquisition =
                DataRelativePathRepairDestinationParentLeaseAcquirer
                    .Acquire(
                        journal.DataRoot,
                        journal.DestinationParentSnapshot
                    );

        if (!parentAcquisition.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryForwardRecoveryState
                    .DestinationParentValidationFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    parentAcquisition.Validation,
                error:
                    parentAcquisition.Validation.Error ??
                    parentAcquisition.Validation.State.ToString()
            );
        }

        using DataRelativePathRepairValidatedDestinationParentLease
            parent =
                parentAcquisition.Lease!;

        /*
         * Forward recovery reacquires the destination parent after
         * classification. Revalidate the complete durable directory
         * incarnation again before any namespace-sensitive operation.
         *
         * Generation-unavailable is a hard failure. There is no
         * fallback to device/inode/mount-only ownership.
         */
        if (
            !parent.ActualIncarnation.Success ||
            parent.IncarnationIdentity is null)
        {
            return Result(
                DataRelativePathRepairDirectoryForwardRecoveryState
                    .DestinationParentValidationFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    parentAcquisition.Validation,
                error:
                    "The destination parent could not provide " +
                    "generation-aware incarnation identity from the " +
                    "retained directory descriptor: " +
                    (
                        parent.ActualIncarnation.Error ??
                        parent.ActualIncarnation.State.ToString()
                    )
            );
        }

        if (
            !journal.DestinationParentIncarnationIdentity
                .SameIncarnationAs(
                    parent.IncarnationIdentity
                ))
        {
            return Result(
                DataRelativePathRepairDirectoryForwardRecoveryState
                    .DestinationParentValidationFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    parentAcquisition.Validation,
                error:
                    "The destination parent no longer matches the " +
                    "generation-aware directory incarnation recorded " +
                    "by the durable journal."
            );
        }

        string stagingChildName =
            journal.PreparedStagingChildName!;

        string finalChildName =
            Path.GetFileName(
                journal.Operation.DestinationPath
            );

        /*
         * Reopen and retain the exact staging directory descriptor.
         *
         * This closes the classification-to-publication gap as far
         * as descriptor ownership is concerned. The publication
         * primitive will additionally revalidate the staging NAME
         * immediately before renameat2().
         */
        LinuxOpenChildReadOnlyAtResult stagingOpen =
            LinuxOpenChildReadOnlyAt.Open(
                parent.OpenedPath,
                stagingChildName
            );

        if (!stagingOpen.Success)
        {
            bool changed =
                stagingOpen.State is
                    LinuxOpenChildReadOnlyAtState.ChildUnavailable or
                    LinuxOpenChildReadOnlyAtState
                        .ChildSymbolicLinkRejected;

            return Result(
                changed
                    ? DataRelativePathRepairDirectoryForwardRecoveryState
                        .NamespaceChangedBeforePublication
                    : DataRelativePathRepairDirectoryForwardRecoveryState
                        .StagingRevalidationFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    parentAcquisition.Validation,
                stagingOpenState:
                    stagingOpen.State,
                error:
                    changed
                        ? "The recorded staging entry changed after " +
                            "recovery classification."
                        : stagingOpen.Error ??
                            stagingOpen.State.ToString()
            );
        }

        using LinuxOpenedChildHandle staging =
            stagingOpen.OpenedChild!;

        string stagingDisplayPath =
            Path.Combine(
                journal.DestinationParentSnapshot.PhysicalPath,
                stagingChildName
            );

        LinuxOpenedDirectoryIncarnationResult stagingIncarnation =
            LinuxOpenedDirectoryIncarnation.Capture(
                staging,
                stagingDisplayPath
            );

        if (
            stagingIncarnation.State ==
            LinuxOpenedDirectoryIncarnationState.NotDirectory)
        {
            return Result(
                DataRelativePathRepairDirectoryForwardRecoveryState
                    .NamespaceChangedBeforePublication,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    parentAcquisition.Validation,
                stagingOpenState:
                    stagingOpen.State,
                stagingSnapshot:
                    stagingIncarnation.Snapshot,
                error:
                    "The recorded staging entry is no longer a " +
                    "directory."
            );
        }

        /*
         * Forward publication authority requires the complete
         * incarnation captured from this exact reopened staging
         * descriptor.
         *
         * A usable physical snapshot without inode generation is not
         * sufficient to continue toward publication.
         */
        if (
            !stagingIncarnation.Success ||
            stagingIncarnation.Identity is null)
        {
            return Result(
                DataRelativePathRepairDirectoryForwardRecoveryState
                    .StagingRevalidationFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    parentAcquisition.Validation,
                stagingOpenState:
                    stagingOpen.State,
                stagingSnapshot:
                    stagingIncarnation.Snapshot,
                error:
                    stagingIncarnation.Error ??
                    stagingIncarnation.State.ToString()
            );
        }

        LinuxOpenedDirectorySnapshotResult stagingSnapshot =
            stagingIncarnation.Snapshot!;

        if (
            journal.PreparedDirectoryIncarnationIdentity is null ||
            !journal.PreparedDirectoryIncarnationIdentity
                .SameIncarnationAs(
                    stagingIncarnation.Identity
                ))
        {
            return Result(
                DataRelativePathRepairDirectoryForwardRecoveryState
                    .NamespaceChangedBeforePublication,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    parentAcquisition.Validation,
                stagingOpenState:
                    stagingOpen.State,
                stagingSnapshot:
                    stagingIncarnation.Snapshot,
                error:
                    "The staging directory no longer matches the " +
                    "generation-aware directory incarnation recorded " +
                    "while Prepared."
            );
        }

        /*
         * Revalidate final-name absence immediately before
         * publication.
         *
         * renameat2(RENAME_NOREPLACE) remains the final atomic
         * no-overwrite gate if a racer creates the destination
         * after this check.
         */
        LinuxOpenChildReadOnlyAtResult finalOpen =
            LinuxOpenChildReadOnlyAt.Open(
                parent.OpenedPath,
                finalChildName
            );

        if (
            finalOpen.State !=
            LinuxOpenChildReadOnlyAtState.ChildUnavailable)
        {
            if (finalOpen.Success)
            {
                finalOpen.OpenedChild!.Dispose();

                return Result(
                    DataRelativePathRepairDirectoryForwardRecoveryState
                        .NamespaceChangedBeforePublication,
                    lockState:
                        lockResult.State,
                    journalRead:
                        read,
                    classification:
                        classification,
                    parentValidation:
                        parentAcquisition.Validation,
                    stagingOpenState:
                        stagingOpen.State,
                    stagingSnapshot:
                        stagingSnapshot,
                    finalOpenState:
                        finalOpen.State,
                    error:
                        "The final destination appeared after " +
                        "recovery classification."
                );
            }

            if (
                finalOpen.State ==
                LinuxOpenChildReadOnlyAtState
                    .ChildSymbolicLinkRejected)
            {
                return Result(
                    DataRelativePathRepairDirectoryForwardRecoveryState
                        .NamespaceChangedBeforePublication,
                    lockState:
                        lockResult.State,
                    journalRead:
                        read,
                    classification:
                        classification,
                    parentValidation:
                        parentAcquisition.Validation,
                    stagingOpenState:
                        stagingOpen.State,
                    stagingSnapshot:
                        stagingSnapshot,
                    finalOpenState:
                        finalOpen.State,
                    error:
                        "A symbolic link appeared at the final " +
                        "destination after recovery classification."
                );
            }

            return Result(
                DataRelativePathRepairDirectoryForwardRecoveryState
                    .StagingRevalidationFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    parentAcquisition.Validation,
                stagingOpenState:
                    stagingOpen.State,
                stagingSnapshot:
                    stagingSnapshot,
                finalOpenState:
                    finalOpen.State,
                error:
                    finalOpen.Error ??
                    finalOpen.State.ToString()
            );
        }

        LinuxPublishOwnedDirectoryAtResult publication =
            LinuxPublishOwnedDirectoryAt.Publish(
                parent.OpenedPath,
                stagingChildName,
                finalChildName,
                staging,
                journal.PreparedDirectoryIncarnationIdentity!
            );

        if (!publication.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryForwardRecoveryState
                    .PublicationFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    parentAcquisition.Validation,
                stagingOpenState:
                    stagingOpen.State,
                stagingSnapshot:
                    stagingSnapshot,
                finalOpenState:
                    finalOpen.State,
                publication:
                    publication,
                error:
                    publication.Error ??
                    publication.State.ToString()
            );
        }

        /*
         * Publication succeeded, but Prepared remains the last
         * durable journal state until both the parent namespace and
         * the new Applied journal revision are durably committed.
         *
         * If recovery dies after rename but before those commits,
         * the read-only classifier will later observe:
         *
         *     PreparedFinalMatchesStagingMissing
         *
         * and the journal-only reconciler can finish the recovery.
         */
        LinuxFsyncResult parentSync =
            LinuxFsync.Sync(
                parent.OpenedPath
            );

        if (!parentSync.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryForwardRecoveryState
                    .DestinationParentSyncFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    parentAcquisition.Validation,
                stagingOpenState:
                    stagingOpen.State,
                stagingSnapshot:
                    stagingSnapshot,
                finalOpenState:
                    finalOpen.State,
                publication:
                    publication,
                destinationParentSync:
                    parentSync,
                error:
                    parentSync.Error ??
                    parentSync.State.ToString()
            );
        }

        DataRelativePathRepairDirectoryJournalTransitionResult
            appliedTransition =
                DataRelativePathRepairDirectoryJournal.MarkApplied(
                    journal,
                    nowUtc
                );

        if (!appliedTransition.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryForwardRecoveryState
                    .AppliedTransitionFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    parentAcquisition.Validation,
                stagingOpenState:
                    stagingOpen.State,
                stagingSnapshot:
                    stagingSnapshot,
                finalOpenState:
                    finalOpen.State,
                publication:
                    publication,
                destinationParentSync:
                    parentSync,
                appliedTransition:
                    appliedTransition,
                error:
                    appliedTransition.Error ??
                    appliedTransition.State.ToString()
            );
        }

        DataRelativePathRepairDirectoryJournalWriterResult
            appliedWrite =
                DataRelativePathRepairDirectoryJournalWriter
                    .ReplaceExisting(
                        journalDirectory,
                        journalChildName,
                        read.JournalIdentity!,
                        appliedTransition.Record!
                    );

        if (!appliedWrite.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryForwardRecoveryState
                    .AppliedJournalWriteFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    parentAcquisition.Validation,
                stagingOpenState:
                    stagingOpen.State,
                stagingSnapshot:
                    stagingSnapshot,
                finalOpenState:
                    finalOpen.State,
                publication:
                    publication,
                destinationParentSync:
                    parentSync,
                appliedTransition:
                    appliedTransition,
                appliedJournalWrite:
                    appliedWrite,
                error:
                    appliedWrite.Error ??
                    appliedWrite.State.ToString()
            );
        }

        return Result(
            DataRelativePathRepairDirectoryForwardRecoveryState
                .AppliedDurably,
            lockState:
                lockResult.State,
            journalRead:
                read,
            classification:
                classification,
            parentValidation:
                parentAcquisition.Validation,
            stagingOpenState:
                stagingOpen.State,
            stagingSnapshot:
                stagingSnapshot,
            finalOpenState:
                finalOpen.State,
            publication:
                publication,
            destinationParentSync:
                parentSync,
            appliedTransition:
                appliedTransition,
            appliedJournalWrite:
                appliedWrite
        );
    }

    private static
        DataRelativePathRepairDirectoryForwardRecovery
        Result(
            DataRelativePathRepairDirectoryForwardRecoveryState state,
            LinuxExclusiveDirectoryLockState? lockState = null,
            DataRelativePathRepairDirectoryJournalReaderResult?
                journalRead = null,
            DataRelativePathRepairDirectoryRecoveryClassification?
                classification = null,
            DataRelativePathRepairDestinationParentValidation?
                parentValidation = null,
            LinuxOpenChildReadOnlyAtState?
                stagingOpenState = null,
            LinuxOpenedDirectorySnapshotResult?
                stagingSnapshot = null,
            LinuxOpenChildReadOnlyAtState?
                finalOpenState = null,
            LinuxPublishOwnedDirectoryAtResult?
                publication = null,
            LinuxFsyncResult?
                destinationParentSync = null,
            DataRelativePathRepairDirectoryJournalTransitionResult?
                appliedTransition = null,
            DataRelativePathRepairDirectoryJournalWriterResult?
                appliedJournalWrite = null,
            string? error = null)
    {
        return new
            DataRelativePathRepairDirectoryForwardRecovery(
                State:
                    state,
                LockState:
                    lockState,
                JournalRead:
                    journalRead,
                Classification:
                    classification,
                ParentValidation:
                    parentValidation,
                StagingOpenState:
                    stagingOpenState,
                StagingSnapshot:
                    stagingSnapshot,
                FinalOpenState:
                    finalOpenState,
                Publication:
                    publication,
                DestinationParentSync:
                    destinationParentSync,
                AppliedTransition:
                    appliedTransition,
                AppliedJournalWrite:
                    appliedJournalWrite,
                Error:
                    error
            );
    }
}
