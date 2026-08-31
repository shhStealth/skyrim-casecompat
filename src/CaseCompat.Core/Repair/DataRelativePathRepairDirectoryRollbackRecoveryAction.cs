using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public static class
    DataRelativePathRepairDirectoryRollbackRecoveryAction
{
    public static
        DataRelativePathRepairDirectoryRollbackRecovery
        Recover(
            LinuxNoFollowPathHandle journalDirectory,
            string journalChildName,
            DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(
            journalDirectory
        );

        /*
         * Lock before reading the journal.
         *
         * Cooperating CaseCompat writers therefore see one
         * serialized read -> classify -> remove -> persist
         * transaction.
         */
        LinuxExclusiveDirectoryLockResult lockResult =
            LinuxExclusiveDirectoryLock.Acquire(
                journalDirectory
            );

        if (!lockResult.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryRollbackRecoveryState
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
                DataRelativePathRepairDirectoryRollbackRecoveryState
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
         * This action is intentionally destructive and narrow.
         *
         * It handles exactly:
         *
         *   durable RollbackRequested
         *   staging name absent
         *   final name -> recorded directory inode X
         *
         * A missing final directory is handled by the
         * non-destructive reconciler. Conflicts are untouched.
         */
        if (
            classification.State !=
            DataRelativePathRepairDirectoryRecoveryState
                .RollbackRequestedFinalMatches)
        {
            return Result(
                DataRelativePathRepairDirectoryRollbackRecoveryState
                    .RecoveryStateNotEligible,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                error:
                    $"Recovery state {classification.State} does " +
                    "not authorize destructive directory rollback."
            );
        }

        /*
         * Validate the future journal transition BEFORE deleting
         * anything.
         *
         * This transitioned record is not persisted until after
         * AT_REMOVEDIR and destination-parent fsync succeed.
         */
        DataRelativePathRepairDirectoryJournalTransitionResult
            transition =
                DataRelativePathRepairDirectoryJournal.MarkRolledBack(
                    journal,
                    nowUtc
                );

        if (!transition.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryRollbackRecoveryState
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
                DataRelativePathRepairDirectoryRollbackRecoveryState
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

        /*
         * Directory-journal ownership is mount-aware.
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
                DataRelativePathRepairDirectoryRollbackRecoveryState
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
         * Revalidate the staging side first.
         *
         * RollbackRequestedFinalMatches requires the historical
         * staging name to remain absent. If it reappeared after
         * classification, do not delete anything.
         */
        MissingRevalidationResult stagingMissing =
            RevalidateMissingChild(
                parent.OpenedPath,
                stagingChildName
            );

        if (!stagingMissing.Success)
        {
            return Result(
                stagingMissing.Changed
                    ? DataRelativePathRepairDirectoryRollbackRecoveryState
                        .NamespaceChangedBeforeRemove
                    : DataRelativePathRepairDirectoryRollbackRecoveryState
                        .NamespaceRevalidationFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    acquisition.Validation,
                stagingOpenState:
                    stagingMissing.OpenState,
                journalTransition:
                    transition,
                error:
                    stagingMissing.Error
            );
        }

        MatchingDirectoryRevalidationResult final =
            RevalidateOwnedFinalDirectory(
                parent.OpenedPath,
                finalChildName,
                journal
            );

        if (!final.Success)
        {
            return Result(
                final.Changed
                    ? DataRelativePathRepairDirectoryRollbackRecoveryState
                        .NamespaceChangedBeforeRemove
                    : DataRelativePathRepairDirectoryRollbackRecoveryState
                        .NamespaceRevalidationFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    acquisition.Validation,
                stagingOpenState:
                    stagingMissing.OpenState,
                finalOpenState:
                    final.OpenState,
                finalSnapshot:
                    final.Snapshot,
                journalTransition:
                    transition,
                error:
                    final.Error
            );
        }

        /*
         * LinuxRemoveOwnedDirectoryAt performs another immediate
         * exact-child O_NOFOLLOW open and complete generation-aware
         * incarnation comparison before exactly one:
         *
         *     unlinkat(parentFd, childName, AT_REMOVEDIR)
         *
         * There is deliberately no recursive deletion and no retry.
         *
         * The kernel itself is the final emptiness gate.
         */
        LinuxRemoveOwnedDirectoryAtResult remove =
            LinuxRemoveOwnedDirectoryAt.Remove(
                parent.OpenedPath,
                finalChildName,
                journal.PreparedDirectoryIncarnationIdentity!
            );

        if (!remove.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryRollbackRecoveryState
                    .RemoveFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    acquisition.Validation,
                stagingOpenState:
                    stagingMissing.OpenState,
                finalOpenState:
                    final.OpenState,
                finalSnapshot:
                    final.Snapshot,
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
         * Keep RollbackRequested as the durable journal state until
         * the removal itself is durable.
         *
         * If fsync fails, a future classifier can safely determine
         * whether Final still exists or is already missing.
         */
        LinuxFsyncResult parentSync =
            LinuxFsync.Sync(
                parent.OpenedPath
            );

        if (!parentSync.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryRollbackRecoveryState
                    .DestinationParentSyncFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    acquisition.Validation,
                stagingOpenState:
                    stagingMissing.OpenState,
                finalOpenState:
                    final.OpenState,
                finalSnapshot:
                    final.Snapshot,
                removeResult:
                    remove,
                destinationParentSync:
                    parentSync,
                journalTransition:
                    transition,
                error:
                    parentSync.Error ??
                    parentSync.State.ToString()
            );
        }

        DataRelativePathRepairDirectoryJournalWriterResult write =
            DataRelativePathRepairDirectoryJournalWriter
                .ReplaceExisting(
                    journalDirectory,
                    journalChildName,
                    read.JournalIdentity!,
                    transition.Record!
                );

        if (!write.Success)
        {
            /*
             * Removal is already durable.
             *
             * Leaving the journal at RollbackRequested is
             * recoverable: the journal-only reconciler will observe
             * staging missing + final missing and persist RolledBack.
             */
            return Result(
                DataRelativePathRepairDirectoryRollbackRecoveryState
                    .JournalWriteFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    acquisition.Validation,
                stagingOpenState:
                    stagingMissing.OpenState,
                finalOpenState:
                    final.OpenState,
                finalSnapshot:
                    final.Snapshot,
                removeResult:
                    remove,
                destinationParentSync:
                    parentSync,
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
            DataRelativePathRepairDirectoryRollbackRecoveryState
                .RolledBackDurably,
            lockState:
                lockResult.State,
            journalRead:
                read,
            classification:
                classification,
            parentValidation:
                acquisition.Validation,
            stagingOpenState:
                stagingMissing.OpenState,
            finalOpenState:
                final.OpenState,
            finalSnapshot:
                final.Snapshot,
            removeResult:
                remove,
            destinationParentSync:
                parentSync,
            journalTransition:
                transition,
            journalWrite:
                write
        );
    }

    private static MissingRevalidationResult
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
            return MissingRevalidationResult.Missing(
                opened.State
            );
        }

        if (opened.Success)
        {
            opened.OpenedChild!.Dispose();

            return MissingRevalidationResult.ChangedResult(
                opened.State,
                $"The child '{childName}' appeared after recovery " +
                "classification."
            );
        }

        if (
            opened.State ==
            LinuxOpenChildReadOnlyAtState
                .ChildSymbolicLinkRejected)
        {
            return MissingRevalidationResult.ChangedResult(
                opened.State,
                $"A symbolic link appeared at child '{childName}' " +
                "after recovery classification."
            );
        }

        return MissingRevalidationResult.Failed(
            opened.State,
            opened.Error ??
            opened.State.ToString()
        );
    }

    private static MatchingDirectoryRevalidationResult
        RevalidateOwnedFinalDirectory(
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
            if (
                opened.State is
                    LinuxOpenChildReadOnlyAtState.ChildUnavailable or
                    LinuxOpenChildReadOnlyAtState
                        .ChildSymbolicLinkRejected)
            {
                return
                    MatchingDirectoryRevalidationResult.ChangedResult(
                        opened.State,
                        null,
                        "The final destination changed after " +
                        "recovery classification."
                    );
            }

            return MatchingDirectoryRevalidationResult.Failed(
                opened.State,
                null,
                opened.Error ??
                opened.State.ToString()
            );
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
            return MatchingDirectoryRevalidationResult.ChangedResult(
                opened.State,
                snapshot,
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
            return MatchingDirectoryRevalidationResult.Failed(
                opened.State,
                snapshot,
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
            return MatchingDirectoryRevalidationResult.ChangedResult(
                opened.State,
                snapshot,
                "The final destination no longer matches the " +
                "mount-aware directory identity recorded while " +
                "Prepared."
            );
        }

        return MatchingDirectoryRevalidationResult.Matched(
            opened.State,
            snapshot
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
        DataRelativePathRepairDirectoryRollbackRecovery
        Result(
            DataRelativePathRepairDirectoryRollbackRecoveryState state,
            LinuxExclusiveDirectoryLockState? lockState = null,
            DataRelativePathRepairDirectoryJournalReaderResult?
                journalRead = null,
            DataRelativePathRepairDirectoryRecoveryClassification?
                classification = null,
            DataRelativePathRepairDestinationParentValidation?
                parentValidation = null,
            LinuxOpenChildReadOnlyAtState?
                stagingOpenState = null,
            LinuxOpenChildReadOnlyAtState?
                finalOpenState = null,
            LinuxOpenedDirectorySnapshotResult?
                finalSnapshot = null,
            LinuxRemoveOwnedDirectoryAtResult?
                removeResult = null,
            LinuxFsyncResult?
                destinationParentSync = null,
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
            StagingOpenState:
                stagingOpenState,
            FinalOpenState:
                finalOpenState,
            FinalSnapshot:
                finalSnapshot,
            RemoveResult:
                removeResult,
            DestinationParentSync:
                destinationParentSync,
            JournalTransition:
                journalTransition,
            JournalWrite:
                journalWrite,
            Error:
                error
        );
    }

    private sealed record MissingRevalidationResult(
        bool Success,
        bool Changed,
        LinuxOpenChildReadOnlyAtState? OpenState,
        string? Error)
    {
        public static MissingRevalidationResult Missing(
            LinuxOpenChildReadOnlyAtState state)
        {
            return new(
                Success:
                    true,
                Changed:
                    false,
                OpenState:
                    state,
                Error:
                    null
            );
        }

        public static MissingRevalidationResult ChangedResult(
            LinuxOpenChildReadOnlyAtState state,
            string error)
        {
            return new(
                Success:
                    false,
                Changed:
                    true,
                OpenState:
                    state,
                Error:
                    error
            );
        }

        public static MissingRevalidationResult Failed(
            LinuxOpenChildReadOnlyAtState state,
            string error)
        {
            return new(
                Success:
                    false,
                Changed:
                    false,
                OpenState:
                    state,
                Error:
                    error
            );
        }
    }

    private sealed record MatchingDirectoryRevalidationResult(
        bool Success,
        bool Changed,
        LinuxOpenChildReadOnlyAtState? OpenState,
        LinuxOpenedDirectorySnapshotResult? Snapshot,
        string? Error)
    {
        public static MatchingDirectoryRevalidationResult Matched(
            LinuxOpenChildReadOnlyAtState state,
            LinuxOpenedDirectorySnapshotResult snapshot)
        {
            return new(
                Success:
                    true,
                Changed:
                    false,
                OpenState:
                    state,
                Snapshot:
                    snapshot,
                Error:
                    null
            );
        }

        public static MatchingDirectoryRevalidationResult ChangedResult(
            LinuxOpenChildReadOnlyAtState state,
            LinuxOpenedDirectorySnapshotResult? snapshot,
            string error)
        {
            return new(
                Success:
                    false,
                Changed:
                    true,
                OpenState:
                    state,
                Snapshot:
                    snapshot,
                Error:
                    error
            );
        }

        public static MatchingDirectoryRevalidationResult Failed(
            LinuxOpenChildReadOnlyAtState state,
            LinuxOpenedDirectorySnapshotResult? snapshot,
            string error)
        {
            return new(
                Success:
                    false,
                Changed:
                    false,
                OpenState:
                    state,
                Snapshot:
                    snapshot,
                Error:
                    error
            );
        }
    }
}
