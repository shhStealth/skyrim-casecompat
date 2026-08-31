using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public static class DataRelativePathRepairDirectoryExecutor
{
    public static DataRelativePathRepairDirectoryExecution Execute(
        LinuxNoFollowPathHandle journalDirectory,
        string journalChildName,
        DataRelativePathRepairPlanOperation operation,
        DataRelativePathRepairDestinationParentSnapshot
            destinationParentSnapshot,
        string trustedDataRoot,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(
            journalDirectory
        );

        ArgumentNullException.ThrowIfNull(
            operation
        );

        ArgumentNullException.ThrowIfNull(
            destinationParentSnapshot
        );

        DataRelativePathRepairDirectoryJournalTransitionResult?
            intentTransition =
                null;

        DataRelativePathRepairDirectoryJournalWriterResult?
            initialWrite =
                null;

        LinuxExclusiveDirectoryLockState?
            initialLockState =
                null;

        DataRelativePathRepairDestinationParentValidation?
            parentValidation =
                null;

        LinuxOpenChildReadOnlyAtState?
            destinationOpenState =
                null;

        /*
         * Initial execution owns exactly one responsibility before
         * handing the transaction to the recovery state machine:
         *
         *     establish a durable revision-zero IntentRecorded
         *     journal while the projected destination parent is
         *     still proven and the final name is still absent.
         *
         * Preparation and publication deliberately reuse the
         * independently recoverable actions.
         */
        {
            LinuxExclusiveDirectoryLockResult lockResult =
                LinuxExclusiveDirectoryLock.Acquire(
                    journalDirectory
                );

            initialLockState =
                lockResult.State;

            if (!lockResult.Success)
            {
                return Result(
                    DataRelativePathRepairDirectoryExecutionState
                        .LockUnavailable,
                    initialLockState:
                        lockResult.State,
                    error:
                        lockResult.Error ??
                        lockResult.State.ToString()
                );
            }

            using LinuxExclusiveDirectoryLockLease lockLease =
                lockResult.Lease!;

            DataRelativePathRepairDestinationParentLeaseAcquisition
                parentAcquisition =
                    DataRelativePathRepairDestinationParentLeaseAcquirer
                        .Acquire(
                            trustedDataRoot,
                            destinationParentSnapshot
                        );

            parentValidation =
                parentAcquisition.Validation;

            if (!parentAcquisition.Success)
            {
                return Result(
                    DataRelativePathRepairDirectoryExecutionState
                        .DestinationParentValidationFailed,
                    initialLockState:
                        lockResult.State,
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
             * Directory mutation and durable directory ownership
             * require the complete generation-aware incarnation of
             * the exact retained parent descriptor.
             *
             * There is no device/inode/mount-only fallback.
             */
            if (
                !parent.ActualIncarnation.Success ||
                parent.IncarnationIdentity is null)
            {
                return Result(
                    DataRelativePathRepairDirectoryExecutionState
                        .DestinationParentIncarnationUnavailable,
                    initialLockState:
                        lockResult.State,
                    parentValidation:
                        parentAcquisition.Validation,
                    error:
                        "The destination parent could not provide " +
                        "generation-aware incarnation identity from " +
                        "the retained directory descriptor: " +
                        (
                            parent.ActualIncarnation.Error ??
                            parent.ActualIncarnation.State.ToString()
                        )
                );
            }

            /*
             * Construct the journal intent only after obtaining
             * filesystem authority from the retained parent.
             *
             * CreateIntent validates that the operation, Data root,
             * destination-parent snapshot, and journal state are
             * internally consistent.
             *
             * The independently trusted root is what is persisted.
             */
            intentTransition =
                DataRelativePathRepairDirectoryJournal.CreateIntent(
                    Guid.NewGuid(),
                    nowUtc,
                    trustedDataRoot,
                    operation,
                    destinationParentSnapshot,
                    parent.IncarnationIdentity
                );

            if (!intentTransition.Success)
            {
                return Result(
                    DataRelativePathRepairDirectoryExecutionState
                        .IntentTransitionFailed,
                    initialLockState:
                        lockResult.State,
                    parentValidation:
                        parentAcquisition.Validation,
                    intentTransition:
                        intentTransition,
                    error:
                        intentTransition.Error ??
                        intentTransition.State.ToString()
                );
            }

            DataRelativePathRepairDirectoryJournalRecord intent =
                intentTransition.Record!;

            string childName =
                Path.GetFileName(
                    intent.Operation.DestinationPath
                );

            /*
             * Avoid creating a durable transaction for a final
             * destination that is already occupied at the start of
             * execution.
             *
             * This remains only a preflight. Later directory
             * publication still uses RENAME_NOREPLACE, so a race
             * after this inspection remains fail-closed.
             */
            LinuxOpenChildReadOnlyAtResult destinationInspection =
                LinuxOpenChildReadOnlyAt.Open(
                    parent.OpenedPath,
                    childName
                );

            destinationOpenState =
                destinationInspection.State;

            if (destinationInspection.Success)
            {
                destinationInspection.OpenedChild!.Dispose();

                return Result(
                    DataRelativePathRepairDirectoryExecutionState
                        .DestinationExists,
                    initialLockState:
                        lockResult.State,
                    parentValidation:
                        parentAcquisition.Validation,
                    destinationOpenState:
                        destinationInspection.State,
                    intentTransition:
                        intentTransition,
                    error:
                        "The destination already exists. " +
                        "Directory repair never overwrites it."
                );
            }

            if (
                destinationInspection.State !=
                LinuxOpenChildReadOnlyAtState
                    .ChildUnavailable)
            {
                if (
                    destinationInspection.State ==
                    LinuxOpenChildReadOnlyAtState
                        .ChildSymbolicLinkRejected)
                {
                    return Result(
                        DataRelativePathRepairDirectoryExecutionState
                            .DestinationExists,
                        initialLockState:
                            lockResult.State,
                        parentValidation:
                            parentAcquisition.Validation,
                        destinationOpenState:
                            destinationInspection.State,
                        intentTransition:
                            intentTransition,
                        error:
                            "The destination name is occupied by " +
                            "a symbolic link. Directory repair will " +
                            "not replace it."
                    );
                }

                return Result(
                    DataRelativePathRepairDirectoryExecutionState
                        .DestinationInspectionFailed,
                    initialLockState:
                        lockResult.State,
                    parentValidation:
                        parentAcquisition.Validation,
                    destinationOpenState:
                        destinationInspection.State,
                    intentTransition:
                        intentTransition,
                    error:
                        destinationInspection.Error ??
                        destinationInspection.State.ToString()
                );
            }

            /*
             * First durable transaction boundary.
             *
             * No repair directory has been created yet.
             */
            initialWrite =
                DataRelativePathRepairDirectoryJournalWriter
                    .CreateInitial(
                        journalDirectory,
                        journalChildName,
                        intent
                    );

            if (!initialWrite.Success)
            {
                /*
                 * In particular, if the journal-directory fsync
                 * fails, do not proceed with asset mutation merely
                 * because a namespace entry may now be visible.
                 *
                 * Preparation begins only after CreatedDurably.
                 */
                return Result(
                    DataRelativePathRepairDirectoryExecutionState
                        .InitialJournalWriteFailed,
                    initialLockState:
                        lockResult.State,
                    parentValidation:
                        parentAcquisition.Validation,
                    destinationOpenState:
                        destinationInspection.State,
                    intentTransition:
                        intentTransition,
                    initialJournalWrite:
                        initialWrite,
                    error:
                        initialWrite.Error ??
                        initialWrite.State.ToString()
                );
            }

            /*
             * End the initial lock/parent-descriptor scope here.
             *
             * The next phase deliberately reopens the durable journal,
             * reacquires the lock, reacquires the parent beneath the
             * trusted root, and revalidates the live namespace.
             *
             * This makes the normal path exercise exactly the same
             * code that would resume after a process crash.
             */
        }

        DataRelativePathRepairDirectoryIntentRecovery
            intentRecovery =
                DataRelativePathRepairDirectoryIntentRecoveryAction
                    .Recover(
                        journalDirectory,
                        journalChildName,
                        trustedDataRoot,
                        nowUtc
                    );

        if (!intentRecovery.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryExecutionState
                    .IntentRecoveryFailed,
                initialLockState:
                    initialLockState,
                parentValidation:
                    parentValidation,
                destinationOpenState:
                    destinationOpenState,
                intentTransition:
                    intentTransition,
                initialJournalWrite:
                    initialWrite,
                intentRecovery:
                    intentRecovery,
                error:
                    intentRecovery.Error ??
                    intentRecovery.State.ToString()
            );
        }

        /*
         * Intent recovery stops at durable Prepared.
         *
         * Forward recovery now consumes that exact recorded
         * incarnation and publishes it with no-replace semantics.
         */
        DataRelativePathRepairDirectoryForwardRecovery
            forwardRecovery =
                DataRelativePathRepairDirectoryForwardRecoveryAction
                    .Recover(
                        journalDirectory,
                        journalChildName,
                        trustedDataRoot,
                        nowUtc
                    );

        if (!forwardRecovery.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryExecutionState
                    .ForwardRecoveryFailed,
                initialLockState:
                    initialLockState,
                parentValidation:
                    parentValidation,
                destinationOpenState:
                    destinationOpenState,
                intentTransition:
                    intentTransition,
                initialJournalWrite:
                    initialWrite,
                intentRecovery:
                    intentRecovery,
                forwardRecovery:
                    forwardRecovery,
                error:
                    forwardRecovery.Error ??
                    forwardRecovery.State.ToString()
            );
        }

        return Result(
            DataRelativePathRepairDirectoryExecutionState
                .AppliedDurably,
            initialLockState:
                initialLockState,
            parentValidation:
                parentValidation,
            destinationOpenState:
                destinationOpenState,
            intentTransition:
                intentTransition,
            initialJournalWrite:
                initialWrite,
            intentRecovery:
                intentRecovery,
            forwardRecovery:
                forwardRecovery
        );
    }

    private static DataRelativePathRepairDirectoryExecution Result(
        DataRelativePathRepairDirectoryExecutionState state,
        LinuxExclusiveDirectoryLockState?
            initialLockState = null,
        DataRelativePathRepairDestinationParentValidation?
            parentValidation = null,
        LinuxOpenChildReadOnlyAtState?
            destinationOpenState = null,
        DataRelativePathRepairDirectoryJournalTransitionResult?
            intentTransition = null,
        DataRelativePathRepairDirectoryJournalWriterResult?
            initialJournalWrite = null,
        DataRelativePathRepairDirectoryIntentRecovery?
            intentRecovery = null,
        DataRelativePathRepairDirectoryForwardRecovery?
            forwardRecovery = null,
        string? error = null)
    {
        return new(
            State:
                state,
            InitialLockState:
                initialLockState,
            ParentValidation:
                parentValidation,
            DestinationOpenState:
                destinationOpenState,
            IntentTransition:
                intentTransition,
            InitialJournalWrite:
                initialJournalWrite,
            IntentRecovery:
                intentRecovery,
            ForwardRecovery:
                forwardRecovery,
            Error:
                error
        );
    }
}
