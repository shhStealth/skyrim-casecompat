using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public static class DataRelativePathRepairFileRecoveryReconciler
{
    public static
        DataRelativePathRepairFileRecoveryReconciliation
        Reconcile(
            LinuxNoFollowPathHandle journalDirectory,
            string journalChildName,
            LinuxOpenedFileIdentityResult
                expectedCurrentJournalIdentity,
            DataRelativePathRepairFileJournalRecord journal,
            DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(
            journalDirectory
        );

        ArgumentNullException.ThrowIfNull(
            expectedCurrentJournalIdentity
        );

        ArgumentNullException.ThrowIfNull(
            journal
        );

        DataRelativePathRepairFileRecoveryClassification
            classification =
                DataRelativePathRepairFileRecoveryClassifier
                    .Classify(
                        journal
                    );

        if (!expectedCurrentJournalIdentity.Success)
        {
            return Result(
                DataRelativePathRepairFileRecoveryReconciliationState
                    .InvalidExpectedJournalIdentity,
                classification,
                error:
                    "Recovery reconciliation requires the " +
                    "identity of the exact journal inode that " +
                    "was read and classified."
            );
        }

        bool preparedMatch =
            classification.State ==
            DataRelativePathRepairFileRecoveryState
                .PreparedDestinationMatches;

        bool rollbackMissing =
            classification.State ==
            DataRelativePathRepairFileRecoveryState
                .RollbackRequestedDestinationMissing;

        if (
            !preparedMatch &&
            !rollbackMissing)
        {
            return Result(
                DataRelativePathRepairFileRecoveryReconciliationState
                    .NoAutomaticReconciliation,
                classification,
                error:
                    $"Recovery state {classification.State} " +
                    "is not a journal-only reconciliation case."
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
                DataRelativePathRepairFileRecoveryReconciliationState
                    .DestinationParentValidationFailed,
                classification,
                parentValidation:
                    acquisition.Validation,
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

        if (preparedMatch)
        {
            RevalidationResult revalidation =
                RevalidateMatchingDestination(
                    parent.OpenedPath,
                    childName,
                    journal
                );

            if (!revalidation.Success)
            {
                return Result(
                    revalidation.Changed
                        ? DataRelativePathRepairFileRecoveryReconciliationState
                            .DestinationChangedBeforeReconciliation
                        : DataRelativePathRepairFileRecoveryReconciliationState
                            .DestinationRevalidationFailed,
                    classification,
                    parentValidation:
                        acquisition.Validation,
                    error:
                        revalidation.Error
                );
            }
        }
        else
        {
            RevalidationResult revalidation =
                RevalidateMissingDestination(
                    parent.OpenedPath,
                    childName
                );

            if (!revalidation.Success)
            {
                return Result(
                    revalidation.Changed
                        ? DataRelativePathRepairFileRecoveryReconciliationState
                            .DestinationChangedBeforeReconciliation
                        : DataRelativePathRepairFileRecoveryReconciliationState
                            .DestinationRevalidationFailed,
                    classification,
                    parentValidation:
                        acquisition.Validation,
                    error:
                        revalidation.Error
                );
            }
        }

        /*
         * This fsync is part of crash recovery, not a new asset
         * mutation.
         *
         * PreparedDestinationMatches may mean the previous
         * process crashed after linkat() but before the parent
         * directory fsync completed.
         *
         * RollbackRequestedDestinationMissing may mean unlinkat()
         * happened before the previous process died but the
         * directory was not yet synced.
         *
         * Do not advance the durable journal state until this
         * parent descriptor has been synced successfully.
         */
        LinuxFsyncResult parentSync =
            LinuxFsync.Sync(
                parent.OpenedPath
            );

        if (!parentSync.Success)
        {
            return Result(
                DataRelativePathRepairFileRecoveryReconciliationState
                    .DestinationParentSyncFailed,
                classification,
                parentValidation:
                    acquisition.Validation,
                error:
                    parentSync.Error ??
                    parentSync.State.ToString()
            );
        }

        DataRelativePathRepairFileJournalTransitionResult
            transition =
                preparedMatch
                    ? DataRelativePathRepairFileJournal
                        .MarkApplied(
                            journal,
                            nowUtc
                        )
                    : DataRelativePathRepairFileJournal
                        .MarkRolledBack(
                            journal,
                            nowUtc
                        );

        if (!transition.Success)
        {
            return Result(
                DataRelativePathRepairFileRecoveryReconciliationState
                    .JournalTransitionFailed,
                classification,
                parentValidation:
                    acquisition.Validation,
                journalTransition:
                    transition,
                error:
                    transition.Error ??
                    transition.State.ToString()
            );
        }

        DataRelativePathRepairFileJournalRecord next =
            transition.Record!;

        DataRelativePathRepairFileJournalWriterResult write =
            DataRelativePathRepairFileJournalWriter
                .ReplaceExisting(
                    journalDirectory,
                    journalChildName,
                    expectedCurrentJournalIdentity,
                    next
                );

        if (!write.Success)
        {
            return Result(
                DataRelativePathRepairFileRecoveryReconciliationState
                    .JournalWriteFailed,
                classification,
                parentValidation:
                    acquisition.Validation,
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
            preparedMatch
                ? DataRelativePathRepairFileRecoveryReconciliationState
                    .AppliedDurably
                : DataRelativePathRepairFileRecoveryReconciliationState
                    .RolledBackDurably,
            classification,
            parentValidation:
                acquisition.Validation,
            journalTransition:
                transition,
            journalWrite:
                write
        );
    }

    private static RevalidationResult
        RevalidateMatchingDestination(
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
            return opened.State switch
            {
                LinuxOpenChildReadOnlyAtState
                    .ChildUnavailable or
                LinuxOpenChildReadOnlyAtState
                    .ChildSymbolicLinkRejected =>
                        RevalidationResult.ChangedResult(
                            "The destination changed after " +
                            "recovery classification."
                        ),

                _ =>
                    RevalidationResult.Failed(
                        opened.Error ??
                        opened.State.ToString()
                    )
            };
        }

        using LinuxOpenedChildHandle child =
            opened.OpenedChild!;

        LinuxOpenedFileIdentityResult identity =
            LinuxOpenedFileIdentity.Capture(
                child
            );

        if (!identity.Success)
        {
            return identity.State ==
                LinuxOpenedFileIdentityState.NotRegularFile
                    ? RevalidationResult.ChangedResult(
                        "The destination is no longer a " +
                        "regular file."
                    )
                    : RevalidationResult.Failed(
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

    private static RevalidationResult
        RevalidateMissingDestination(
            LinuxNoFollowPathHandle parent,
            string childName)
    {
        LinuxOpenChildReadOnlyAtResult opened =
            LinuxOpenChildReadOnlyAt.Open(
                parent,
                childName
            );

        if (
            opened.State ==
            LinuxOpenChildReadOnlyAtState.ChildUnavailable)
        {
            return RevalidationResult.Matched();
        }

        if (opened.Success)
        {
            opened.OpenedChild!.Dispose();

            return RevalidationResult.ChangedResult(
                "The destination appeared after recovery " +
                "classification."
            );
        }

        if (
            opened.State ==
            LinuxOpenChildReadOnlyAtState
                .ChildSymbolicLinkRejected)
        {
            return RevalidationResult.ChangedResult(
                "A symbolic link appeared at the destination " +
                "after recovery classification."
            );
        }

        return RevalidationResult.Failed(
            opened.Error ??
            opened.State.ToString()
        );
    }

    private static
        DataRelativePathRepairFileRecoveryReconciliation
        Result(
            DataRelativePathRepairFileRecoveryReconciliationState state,
            DataRelativePathRepairFileRecoveryClassification
                classification,
            DataRelativePathRepairDestinationParentValidation?
                parentValidation = null,
            DataRelativePathRepairFileJournalTransitionResult?
                journalTransition = null,
            DataRelativePathRepairFileJournalWriterResult?
                journalWrite = null,
            string? error = null)
    {
        return new
            DataRelativePathRepairFileRecoveryReconciliation(
                State:
                    state,
                Classification:
                    classification,
                ParentValidation:
                    parentValidation,
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
            return new RevalidationResult(
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
            return new RevalidationResult(
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
            return new RevalidationResult(
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
