using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public static class
    DataRelativePathRepairDirectoryRecoveryReconciler
{
    public static
        DataRelativePathRepairDirectoryRecoveryReconciliation
        Reconcile(
            LinuxNoFollowPathHandle journalDirectory,
            string journalChildName,
            LinuxOpenedFileIdentityResult
                expectedCurrentJournalIdentity,
            DataRelativePathRepairDirectoryJournalRecord journal,
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

        DataRelativePathRepairDirectoryRecoveryClassification
            classification =
                DataRelativePathRepairDirectoryRecoveryClassifier
                    .Classify(
                        journal
                    );

        if (!expectedCurrentJournalIdentity.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryRecoveryReconciliationState
                    .InvalidExpectedJournalIdentity,
                classification,
                error:
                    "Directory recovery reconciliation requires " +
                    "the identity of the exact journal inode that " +
                    "was read and classified."
            );
        }

        bool preparedPublished =
            classification.State ==
            DataRelativePathRepairDirectoryRecoveryState
                .PreparedFinalMatchesStagingMissing;

        bool rollbackMissing =
            classification.State ==
            DataRelativePathRepairDirectoryRecoveryState
                .RollbackRequestedFinalMissing;

        if (
            !preparedPublished &&
            !rollbackMissing)
        {
            return Result(
                DataRelativePathRepairDirectoryRecoveryReconciliationState
                    .NoAutomaticReconciliation,
                classification,
                error:
                    $"Recovery state {classification.State} is " +
                    "not a directory journal-only reconciliation " +
                    "case."
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
                DataRelativePathRepairDirectoryRecoveryReconciliationState
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

        /*
         * The shared destination-parent lease predates the stronger
         * mount-aware directory-journal identity rule.
         *
         * Recheck the complete physical identity before using this
         * descriptor for reconciliation.
         */
        LinuxFileIdentityResult expectedParentIdentity =
            journal.DestinationParentSnapshot.Identity;

        LinuxFileIdentityResult? actualParentIdentity =
            parent.ActualSnapshot.Identity;

        if (
            actualParentIdentity is null ||
            !SameDirectoryObject(
                expectedParentIdentity,
                actualParentIdentity
            ))
        {
            return Result(
                DataRelativePathRepairDirectoryRecoveryReconciliationState
                    .DestinationParentValidationFailed,
                classification,
                parentValidation:
                    acquisition.Validation,
                error:
                    "The destination parent no longer matches the " +
                    "complete mount-aware physical identity " +
                    "recorded by the directory journal."
            );
        }

        string stagingChildName =
            journal.PreparedStagingChildName!;

        string finalChildName =
            Path.GetFileName(
                journal.Operation.DestinationPath
            );

        /*
         * Classification happened before this second parent lease
         * was acquired. Revalidate every namespace assumption that
         * authorizes the journal transition.
         *
         * PreparedFinalMatchesStagingMissing:
         *   staging must still be absent;
         *   final must still be the recorded directory inode.
         *
         * RollbackRequestedFinalMissing:
         *   staging must still be absent;
         *   final must still be absent.
         */
        RevalidationResult stagingMissing =
            RevalidateMissingChild(
                parent.OpenedPath,
                stagingChildName
            );

        if (!stagingMissing.Success)
        {
            return Result(
                stagingMissing.Changed
                    ? DataRelativePathRepairDirectoryRecoveryReconciliationState
                        .NamespaceChangedBeforeReconciliation
                    : DataRelativePathRepairDirectoryRecoveryReconciliationState
                        .NamespaceRevalidationFailed,
                classification,
                parentValidation:
                    acquisition.Validation,
                error:
                    stagingMissing.Error
            );
        }

        RevalidationResult finalRevalidation =
            preparedPublished
                ? RevalidateMatchingFinalDirectory(
                    parent.OpenedPath,
                    finalChildName,
                    journal
                )
                : RevalidateMissingChild(
                    parent.OpenedPath,
                    finalChildName
                );

        if (!finalRevalidation.Success)
        {
            return Result(
                finalRevalidation.Changed
                    ? DataRelativePathRepairDirectoryRecoveryReconciliationState
                        .NamespaceChangedBeforeReconciliation
                    : DataRelativePathRepairDirectoryRecoveryReconciliationState
                        .NamespaceRevalidationFailed,
                classification,
                parentValidation:
                    acquisition.Validation,
                error:
                    finalRevalidation.Error
            );
        }

        /*
         * This fsync acknowledges a namespace mutation that may
         * already have completed before the previous process died.
         *
         * It does not create, rename, or remove a Skyrim-visible
         * directory.
         *
         * Do not advance the durable journal until the destination
         * parent has been synced successfully.
         */
        LinuxFsyncResult parentSync =
            LinuxFsync.Sync(
                parent.OpenedPath
            );

        if (!parentSync.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryRecoveryReconciliationState
                    .DestinationParentSyncFailed,
                classification,
                parentValidation:
                    acquisition.Validation,
                error:
                    parentSync.Error ??
                    parentSync.State.ToString()
            );
        }

        DataRelativePathRepairDirectoryJournalTransitionResult
            transition =
                preparedPublished
                    ? DataRelativePathRepairDirectoryJournal
                        .MarkApplied(
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
                DataRelativePathRepairDirectoryRecoveryReconciliationState
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

        DataRelativePathRepairDirectoryJournalRecord next =
            transition.Record!;

        /*
         * ReplaceExisting reopens the current journal entry and
         * proves that it still has exactly the inode identity the
         * caller originally read.
         *
         * If another recovery path replaced the journal after it
         * was classified, this write is refused.
         */
        DataRelativePathRepairDirectoryJournalWriterResult write =
            DataRelativePathRepairDirectoryJournalWriter
                .ReplaceExisting(
                    journalDirectory,
                    journalChildName,
                    expectedCurrentJournalIdentity,
                    next
                );

        if (!write.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryRecoveryReconciliationState
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
            preparedPublished
                ? DataRelativePathRepairDirectoryRecoveryReconciliationState
                    .AppliedDurably
                : DataRelativePathRepairDirectoryRecoveryReconciliationState
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
        RevalidateMatchingFinalDirectory(
            ILinuxOpenedHandle parent,
            string childName,
            DataRelativePathRepairDirectoryJournalRecord journal)
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
                            "The final destination changed after " +
                            "directory recovery classification."
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

        LinuxOpenedDirectorySnapshotResult snapshot =
            LinuxOpenedDirectorySnapshot.Capture(
                child,
                journal.Operation.DestinationPath
            );

        if (
            snapshot.State ==
            LinuxOpenedDirectorySnapshotState.NotDirectory)
        {
            return RevalidationResult.ChangedResult(
                "The final destination is no longer a directory."
            );
        }

        bool usableIdentity =
            snapshot.Identity is not null &&
            HasCompleteIdentity(
                snapshot.Identity
            ) &&
            (
                snapshot.State ==
                    LinuxOpenedDirectorySnapshotState.Captured ||
                snapshot.State ==
                    LinuxOpenedDirectorySnapshotState.FlagsUnavailable
            );

        if (!usableIdentity)
        {
            return RevalidationResult.Failed(
                snapshot.Error ??
                snapshot.State.ToString()
            );
        }

        if (
            journal.PreparedDirectoryIdentity is null ||
            !SameDirectoryObject(
                journal.PreparedDirectoryIdentity,
                snapshot.Identity!
            ))
        {
            return RevalidationResult.ChangedResult(
                "The final destination no longer matches the " +
                "mount-aware directory identity recorded while " +
                "the repair was Prepared."
            );
        }

        return RevalidationResult.Matched();
    }

    private static RevalidationResult
        RevalidateMissingChild(
            ILinuxOpenedHandle parent,
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
                $"The child '{childName}' appeared after directory " +
                "recovery classification."
            );
        }

        if (
            opened.State ==
            LinuxOpenChildReadOnlyAtState
                .ChildSymbolicLinkRejected)
        {
            return RevalidationResult.ChangedResult(
                $"A symbolic link appeared at child '{childName}' " +
                "after directory recovery classification."
            );
        }

        return RevalidationResult.Failed(
            opened.Error ??
            opened.State.ToString()
        );
    }

    private static bool SameDirectoryObject(
        LinuxFileIdentityResult left,
        LinuxFileIdentityResult right)
    {
        return
            HasCompleteIdentity(left) &&
            HasCompleteIdentity(right) &&
            left.DeviceMajor ==
                right.DeviceMajor &&
            left.DeviceMinor ==
                right.DeviceMinor &&
            left.Inode ==
                right.Inode &&
            left.MountId ==
                right.MountId;
    }

    private static bool HasCompleteIdentity(
        LinuxFileIdentityResult identity)
    {
        return
            identity.Success &&
            identity.DeviceMajor is not null &&
            identity.DeviceMinor is not null &&
            identity.Inode is not null &&
            identity.MountId is not null;
    }

    private static
        DataRelativePathRepairDirectoryRecoveryReconciliation
        Result(
            DataRelativePathRepairDirectoryRecoveryReconciliationState
                state,
            DataRelativePathRepairDirectoryRecoveryClassification
                classification,
            DataRelativePathRepairDestinationParentValidation?
                parentValidation = null,
            DataRelativePathRepairDirectoryJournalTransitionResult?
                journalTransition = null,
            DataRelativePathRepairDirectoryJournalWriterResult?
                journalWrite = null,
            string? error = null)
    {
        return new
            DataRelativePathRepairDirectoryRecoveryReconciliation(
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
