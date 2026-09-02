using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public static class
    DataRelativePathRepairBatchReusedDirectoryRollbackAction
{
    public static
        DataRelativePathRepairBatchReusedDirectoryRollback
        Advance(
            LinuxNoFollowPathHandle journalDirectory,
            string journalChildName,
            string trustedDataRoot,
            DateTimeOffset nowUtc,
            LinuxFileIncarnationIdentity?
                expectedCurrentJournalIncarnation = null)
    {
        ArgumentNullException.ThrowIfNull(
            journalDirectory
        );

        if (
            expectedCurrentJournalIncarnation is not null &&
            !expectedCurrentJournalIncarnation.Success)
        {
            return Result(
                DataRelativePathRepairBatchReusedDirectoryRollbackState
                    .InvalidExpectedJournalIdentity,
                error:
                    "BatchReused directory rollback requires a usable " +
                    "generation-aware journal identity when the caller " +
                    "binds rollback to an earlier journal read."
            );
        }

        /*
         * Serialize the journal read, namespace proof, and durable
         * transition among cooperating CaseCompat writers.
         *
         * This action never owns or removes the borrowed directory.
         */
        LinuxExclusiveDirectoryLockResult lockResult =
            LinuxExclusiveDirectoryLock.Acquire(
                journalDirectory
            );

        if (!lockResult.Success)
        {
            return Result(
                DataRelativePathRepairBatchReusedDirectoryRollbackState
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
                DataRelativePathRepairBatchReusedDirectoryRollbackState
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

        if (
            expectedCurrentJournalIncarnation is not null &&
            (
                read.JournalIncarnationIdentity is null ||
                !expectedCurrentJournalIncarnation
                    .SameIncarnationAs(
                        read.JournalIncarnationIdentity
                    )
            ))
        {
            return Result(
                DataRelativePathRepairBatchReusedDirectoryRollbackState
                    .JournalIncarnationChanged,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                error:
                    "The BatchReused rollback journal changed after the " +
                    "caller read and bound it. The journal transition is " +
                    "refused."
            );
        }

        DataRelativePathRepairDirectoryJournalRecord journal =
            read.Record!;

        DataRelativePathRepairDirectoryRecoveryClassification
            classification =
                DataRelativePathRepairDirectoryRecoveryClassifier
                    .Classify(
                        journal,
                        trustedDataRoot
                    );

        bool requesting =
            classification.State ==
            DataRelativePathRepairDirectoryRecoveryState
                .ReusedAppliedFinalMatches;

        bool completing =
            classification.State ==
            DataRelativePathRepairDirectoryRecoveryState
                .ReusedRollbackRequestedFinalMatches;

        if (!requesting && !completing)
        {
            return Result(
                DataRelativePathRepairBatchReusedDirectoryRollbackState
                    .RecoveryStateNotEligible,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                error:
                    $"Recovery state {classification.State} does not " +
                    "authorize a BatchReused journal-only rollback " +
                    "transition."
            );
        }

        DataRelativePathRepairDirectoryJournalTransitionResult
            transition =
                requesting
                    ? DataRelativePathRepairDirectoryJournal
                        .RequestRollback(
                            journal,
                            nowUtc
                        )
                    : DataRelativePathRepairDirectoryJournal
                        .MarkRolledBack(
                            journal,
                            nowUtc
                        );

        if (!transition.Success)
        {
            return Result(
                DataRelativePathRepairBatchReusedDirectoryRollbackState
                    .JournalTransitionFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                journalTransition:
                    transition,
                error:
                    transition.Error ??
                    transition.State.ToString()
            );
        }

        /*
         * Classification occurred before this retained parent lease was
         * acquired. Revalidate the complete durable parent incarnation
         * before trusting the final child namespace.
         */
        DataRelativePathRepairDestinationParentLeaseAcquisition
            acquisition =
                DataRelativePathRepairDestinationParentLeaseAcquirer
                    .Acquire(
                        trustedDataRoot,
                        journal.DestinationParentSnapshot
                    );

        if (!acquisition.Success)
        {
            return Result(
                DataRelativePathRepairBatchReusedDirectoryRollbackState
                    .DestinationParentValidationFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    acquisition.Validation,
                journalTransition:
                    transition,
                error:
                    acquisition.Validation.Error ??
                    acquisition.Validation.State.ToString()
            );
        }

        using DataRelativePathRepairValidatedDestinationParentLease
            parent =
                acquisition.Lease!;

        if (
            !parent.ActualIncarnation.Success ||
            parent.IncarnationIdentity is null)
        {
            return Result(
                DataRelativePathRepairBatchReusedDirectoryRollbackState
                    .DestinationParentValidationFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    acquisition.Validation,
                journalTransition:
                    transition,
                error:
                    "The destination parent could not provide complete " +
                    "generation-aware incarnation identity from the " +
                    "retained descriptor: " +
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
                DataRelativePathRepairBatchReusedDirectoryRollbackState
                    .DestinationParentValidationFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    acquisition.Validation,
                journalTransition:
                    transition,
                error:
                    "The destination parent no longer matches the " +
                    "generation-aware incarnation recorded by the " +
                    "BatchReused journal."
            );
        }

        string finalChildName =
            Path.GetFileName(
                journal.Operation.DestinationPath
            );

        LinuxOpenChildReadOnlyAtResult finalOpen =
            LinuxOpenChildReadOnlyAt.Open(
                parent.OpenedPath,
                finalChildName
            );

        if (!finalOpen.Success)
        {
            return Result(
                DataRelativePathRepairBatchReusedDirectoryRollbackState
                    .FinalOpenFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    acquisition.Validation,
                finalOpenState:
                    finalOpen.State,
                journalTransition:
                    transition,
                error:
                    finalOpen.Error ??
                    $"The borrowed final directory changed before the " +
                    $"journal-only rollback transition: {finalOpen.State}."
            );
        }

        /*
         * Keep the exact borrowed directory descriptor alive through
         * journal publication. The borrower never unlinks, renames,
         * stages, or otherwise mutates this Skyrim-visible directory.
         */
        using LinuxOpenedChildHandle final =
            finalOpen.OpenedChild!;

        LinuxOpenedDirectoryIncarnationResult finalIncarnation =
            LinuxOpenedDirectoryIncarnation.Capture(
                final,
                journal.Operation.DestinationPath
            );

        if (
            !finalIncarnation.Success ||
            finalIncarnation.Identity is null)
        {
            return Result(
                DataRelativePathRepairBatchReusedDirectoryRollbackState
                    .FinalIncarnationUnavailable,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    acquisition.Validation,
                finalOpenState:
                    finalOpen.State,
                finalIncarnation:
                    finalIncarnation,
                journalTransition:
                    transition,
                error:
                    finalIncarnation.Error ??
                    finalIncarnation.State.ToString()
            );
        }

        LinuxDirectoryIncarnationIdentity expectedBorrowedIdentity =
            journal.BatchReuseProvenance!
                .ReusedDirectoryIncarnationIdentity;

        if (
            !expectedBorrowedIdentity
                .SameIncarnationAs(
                    finalIncarnation.Identity
                ))
        {
            return Result(
                DataRelativePathRepairBatchReusedDirectoryRollbackState
                    .FinalIncarnationMismatch,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    acquisition.Validation,
                finalOpenState:
                    finalOpen.State,
                finalIncarnation:
                    finalIncarnation,
                journalTransition:
                    transition,
                error:
                    "The final destination changed after BatchReused " +
                    "rollback classification and no longer matches the " +
                    "generation-aware borrowed-directory incarnation."
            );
        }

        DataRelativePathRepairDirectoryJournalWriterResult write =
            DataRelativePathRepairDirectoryJournalWriter
                .ReplaceExisting(
                    journalDirectory,
                    journalChildName,
                    read.JournalIncarnationIdentity!,
                    transition.Record!
                );

        if (!write.Success)
        {
            return Result(
                DataRelativePathRepairBatchReusedDirectoryRollbackState
                    .JournalWriteFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    acquisition.Validation,
                finalOpenState:
                    finalOpen.State,
                finalIncarnation:
                    finalIncarnation,
                journalTransition:
                    transition,
                journalWrite:
                    write,
                error:
                    write.Error ??
                    write.State.ToString()
            );
        }

        return Result(
            requesting
                ? DataRelativePathRepairBatchReusedDirectoryRollbackState
                    .RequestedDurably
                : DataRelativePathRepairBatchReusedDirectoryRollbackState
                    .RolledBackDurably,
            lockState:
                lockResult.State,
            journalRead:
                read,
            classification:
                classification,
            parentValidation:
                acquisition.Validation,
            finalOpenState:
                finalOpen.State,
            finalIncarnation:
                finalIncarnation,
            journalTransition:
                transition,
            journalWrite:
                write
        );
    }

    private static
        DataRelativePathRepairBatchReusedDirectoryRollback
        Result(
            DataRelativePathRepairBatchReusedDirectoryRollbackState state,
            LinuxExclusiveDirectoryLockState? lockState = null,
            DataRelativePathRepairDirectoryJournalReaderResult?
                journalRead = null,
            DataRelativePathRepairDirectoryRecoveryClassification?
                classification = null,
            DataRelativePathRepairDestinationParentValidation?
                parentValidation = null,
            LinuxOpenChildReadOnlyAtState?
                finalOpenState = null,
            LinuxOpenedDirectoryIncarnationResult?
                finalIncarnation = null,
            DataRelativePathRepairDirectoryJournalTransitionResult?
                journalTransition = null,
            DataRelativePathRepairDirectoryJournalWriterResult?
                journalWrite = null,
            string? error = null)
    {
        return new(
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
            FinalOpenState:
                finalOpenState,
            FinalIncarnation:
                finalIncarnation,
            JournalTransition:
                journalTransition,
            JournalWrite:
                journalWrite,
            Error:
                error
        );
    }
}
