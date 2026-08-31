using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public static class
    DataRelativePathRepairFileRollbackRecoveryAction
{
    public static
        DataRelativePathRepairFileRollbackRecovery
        Recover(
            LinuxNoFollowPathHandle journalDirectory,
            string journalChildName,
            DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(
            journalDirectory
        );

        /*
         * The lock is acquired before reading the journal.
         *
         * Therefore every cooperating CaseCompat writer sees
         * one serialized read -> classify -> mutate -> persist
         * transaction.
         */
        LinuxExclusiveDirectoryLockResult lockResult =
            LinuxExclusiveDirectoryLock.Acquire(
                journalDirectory
            );

        if (!lockResult.Success)
        {
            return Result(
                DataRelativePathRepairFileRollbackRecoveryState
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

        DataRelativePathRepairFileJournalReaderResult read =
            DataRelativePathRepairFileJournalReader.Read(
                journalDirectory,
                journalChildName
            );

        if (!read.Success)
        {
            return Result(
                DataRelativePathRepairFileRollbackRecoveryState
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

        DataRelativePathRepairFileJournalRecord journal =
            read.Record!;

        DataRelativePathRepairFileRecoveryClassification
            classification =
                DataRelativePathRepairFileRecoveryClassifier
                    .Classify(
                        journal
                    );

        /*
         * This action is intentionally destructive and narrow.
         *
         * It handles exactly one state:
         *
         *   durable RollbackRequested
         *   + current destination proves ownership
         *
         * Missing destinations are handled by the non-destructive
         * journal reconciler. Every other state is left untouched.
         */
        if (
            classification.State !=
            DataRelativePathRepairFileRecoveryState
                .RollbackRequestedDestinationMatches)
        {
            return Result(
                DataRelativePathRepairFileRollbackRecoveryState
                    .RecoveryStateNotEligible,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                error:
                    $"Recovery state {classification.State} " +
                    "does not authorize destructive rollback."
            );
        }

        /*
         * Validate the journal transition before deleting
         * anything. The transitioned record is not persisted
         * until after unlink + parent fsync.
         */
        DataRelativePathRepairFileJournalTransitionResult
            transition =
                DataRelativePathRepairFileJournal
                    .MarkRolledBack(
                        journal,
                        nowUtc
                    );

        if (!transition.Success)
        {
            return Result(
                DataRelativePathRepairFileRollbackRecoveryState
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

        DataRelativePathRepairDestinationParentLeaseAcquisition
            acquisition =
                DataRelativePathRepairDestinationParentLeaseAcquirer
                    .Acquire(
                        journal.DataRoot,
                        journal.DestinationParentSnapshot
                    );

        if (!acquisition.Success)
        {
            return Result(
                DataRelativePathRepairFileRollbackRecoveryState
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

        string childName =
            Path.GetFileName(
                journal.Operation.DestinationPath
            );

        RevalidationResult revalidation =
            RevalidateOwnedDestination(
                parent.OpenedPath,
                childName,
                journal
            );

        if (!revalidation.Success)
        {
            return Result(
                revalidation.Changed
                    ? DataRelativePathRepairFileRollbackRecoveryState
                        .DestinationChangedBeforeRemove
                    : DataRelativePathRepairFileRollbackRecoveryState
                        .DestinationRevalidationFailed,
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
                    revalidation.Error
            );
        }

        /*
         * LinuxRemoveOwnedFileAt performs its own immediate
         * O_NOFOLLOW direct-child open and identity check before
         * exactly one unlinkat().
         *
         * There remains the documented narrow name race between
         * that final identity check and unlinkat(). Do not retry.
         */
        LinuxRemoveOwnedFileAtResult remove =
            LinuxRemoveOwnedFileAt.Remove(
                parent.OpenedPath,
                childName,
                journal.PreparedFileIdentity!
            );

        if (!remove.Success)
        {
            return Result(
                DataRelativePathRepairFileRollbackRecoveryState
                    .RemoveFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    acquisition.Validation,
                removeResult:
                    remove,
                journalTransition:
                    transition,
                error:
                    remove.Error ??
                    remove.State.ToString()
            );
        }

        /*
         * The journal must remain RollbackRequested until the
         * deletion is durable in the destination directory.
         *
         * If this fsync fails, the next recovery pass will still
         * see RollbackRequested and can classify whether the file
         * is present or missing.
         */
        LinuxFsyncResult parentSync =
            LinuxFsync.Sync(
                parent.OpenedPath
            );

        if (!parentSync.Success)
        {
            return Result(
                DataRelativePathRepairFileRollbackRecoveryState
                    .DestinationParentSyncFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    acquisition.Validation,
                removeResult:
                    remove,
                journalTransition:
                    transition,
                error:
                    parentSync.Error ??
                    parentSync.State.ToString()
            );
        }

        DataRelativePathRepairFileJournalWriterResult write =
            DataRelativePathRepairFileJournalWriter
                .ReplaceExisting(
                    journalDirectory,
                    journalChildName,
                    read.JournalIdentity!,
                    transition.Record!
                );

        if (!write.Success)
        {
            /*
             * The asset deletion is already durable here.
             * Leaving the previous durable journal at
             * RollbackRequested is recoverable: the next pass
             * classifies the destination as missing and the
             * non-destructive reconciler can finish RolledBack.
             */
            return Result(
                DataRelativePathRepairFileRollbackRecoveryState
                    .JournalWriteFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    acquisition.Validation,
                removeResult:
                    remove,
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
            DataRelativePathRepairFileRollbackRecoveryState
                .RolledBackDurably,
            lockState:
                lockResult.State,
            journalRead:
                read,
            classification:
                classification,
            parentValidation:
                acquisition.Validation,
            removeResult:
                remove,
            journalTransition:
                transition,
            journalWrite:
                write
        );
    }

    private static RevalidationResult
        RevalidateOwnedDestination(
            LinuxNoFollowPathHandle parent,
            string childName,
            DataRelativePathRepairFileJournalRecord journal)
    {
        LinuxOpenChildReadOnlyAtResult opened =
            LinuxOpenChildReadOnlyAt.Open(
                parent,
                childName
            );

        if (!opened.Success)
        {
            if (
                opened.State is
                    LinuxOpenChildReadOnlyAtState
                        .ChildUnavailable or
                    LinuxOpenChildReadOnlyAtState
                        .ChildSymbolicLinkRejected)
            {
                return RevalidationResult.ChangedResult(
                    "The destination changed after recovery " +
                    "classification."
                );
            }

            return RevalidationResult.Failed(
                opened.Error ??
                opened.State.ToString()
            );
        }

        using LinuxOpenedChildHandle child =
            opened.OpenedChild!;

        LinuxOpenedFileIdentityResult identity =
            LinuxOpenedFileIdentity.Capture(
                child
            );

        if (!identity.Success)
        {
            if (
                identity.State ==
                LinuxOpenedFileIdentityState.NotRegularFile)
            {
                return RevalidationResult.ChangedResult(
                    "The destination is no longer a regular file."
                );
            }

            return RevalidationResult.Failed(
                identity.Error ??
                identity.State.ToString()
            );
        }

        LinuxOpenedFileSnapshotResult snapshot =
            LinuxOpenedFileSnapshot.Capture(
                child,
                journal.Operation.DestinationPath
            );

        if (!snapshot.Success)
        {
            return RevalidationResult.Failed(
                snapshot.Error ??
                snapshot.State.ToString()
            );
        }

        bool identityMatches =
            journal.PreparedFileIdentity is not null &&
            journal.PreparedFileIdentity.SameObjectAs(
                identity
            );

        bool sizeMatches =
            snapshot.Size ==
            journal.SourceSnapshot.Size;

        bool hashMatches =
            string.Equals(
                snapshot.Sha256,
                journal.SourceSnapshot.Sha256,
                StringComparison.OrdinalIgnoreCase
            );

        if (
            !identityMatches ||
            !sizeMatches ||
            !hashMatches)
        {
            return RevalidationResult.ChangedResult(
                "The destination no longer matches the " +
                "Prepared identity, size, and SHA-256 evidence."
            );
        }

        return RevalidationResult.Matched();
    }

    private static DataRelativePathRepairFileRollbackRecovery
        Result(
            DataRelativePathRepairFileRollbackRecoveryState state,
            LinuxExclusiveDirectoryLockState? lockState = null,
            DataRelativePathRepairFileJournalReaderResult?
                journalRead = null,
            DataRelativePathRepairFileRecoveryClassification?
                classification = null,
            DataRelativePathRepairDestinationParentValidation?
                parentValidation = null,
            LinuxRemoveOwnedFileAtResult?
                removeResult = null,
            DataRelativePathRepairFileJournalTransitionResult?
                journalTransition = null,
            DataRelativePathRepairFileJournalWriterResult?
                journalWrite = null,
            string? error = null)
    {
        return new DataRelativePathRepairFileRollbackRecovery(
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
            RemoveResult:
                removeResult,
            JournalTransition:
                journalTransition,
            JournalWrite:
                journalWrite,
            Error:
                error
        );
    }

    private sealed record RevalidationResult(
        bool Success,
        bool Changed,
        string? Error)
    {
        public static RevalidationResult Matched()
        {
            return new(
                Success:
                    true,
                Changed:
                    false,
                Error:
                    null
            );
        }

        public static RevalidationResult ChangedResult(
            string error)
        {
            return new(
                Success:
                    false,
                Changed:
                    true,
                Error:
                    error
            );
        }

        public static RevalidationResult Failed(
            string error)
        {
            return new(
                Success:
                    false,
                Changed:
                    false,
                Error:
                    error
            );
        }
    }
}
