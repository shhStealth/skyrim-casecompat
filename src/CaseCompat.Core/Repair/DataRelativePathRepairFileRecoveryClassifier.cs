using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public static class DataRelativePathRepairFileRecoveryClassifier
{
    public static
        DataRelativePathRepairFileRecoveryClassification
        Classify(
            DataRelativePathRepairFileJournalRecord journal)
    {
        ArgumentNullException.ThrowIfNull(
            journal
        );

        string? validationError =
            DataRelativePathRepairFileJournal.Validate(
                journal
            );

        if (validationError is not null)
        {
            return Result(
                DataRelativePathRepairFileRecoveryState
                    .InvalidRecord,
                journal,
                error:
                    validationError
            );
        }

        /*
         * RecoveryConflict is already a durable terminal
         * journal conclusion. Do not reinterpret it against
         * the live filesystem here.
         */
        if (
            journal.State ==
            DataRelativePathRepairFileJournalState
                .RecoveryConflict)
        {
            return Result(
                DataRelativePathRepairFileRecoveryState
                    .RecoveryConflictTerminal,
                journal,
                error:
                    journal.RecoveryConflictReason
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
                DataRelativePathRepairFileRecoveryState
                    .DestinationParentValidationFailed,
                journal,
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

        string childName =
            Path.GetFileName(
                journal.Operation.DestinationPath
            );

        LinuxOpenChildReadOnlyAtResult opened =
            LinuxOpenChildReadOnlyAt.Open(
                parent.OpenedPath,
                childName
            );

        if (!opened.Success)
        {
            if (
                opened.State ==
                LinuxOpenChildReadOnlyAtState
                    .ChildUnavailable)
            {
                return Result(
                    ClassifyMissing(
                        journal.State
                    ),
                    journal,
                    parentValidation:
                        parentAcquisition.Validation,
                    destinationOpenState:
                        opened.State
                );
            }

            /*
             * O_NOFOLLOW rejected a symlink. We nevertheless
             * know that the destination name is occupied, so
             * this is a semantic conflict rather than a generic
             * inspection failure.
             */
            if (
                opened.State ==
                LinuxOpenChildReadOnlyAtState
                    .ChildSymbolicLinkRejected)
            {
                return Result(
                    ClassifyConflict(
                        journal.State
                    ),
                    journal,
                    parentValidation:
                        parentAcquisition.Validation,
                    destinationOpenState:
                        opened.State,
                    error:
                        "The destination name is occupied by " +
                        "a symbolic link."
                );
            }

            return Result(
                DataRelativePathRepairFileRecoveryState
                    .DestinationInspectionFailed,
                journal,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    opened.State,
                error:
                    opened.Error ??
                    opened.State.ToString()
            );
        }

        using LinuxOpenedChildHandle destination =
            opened.OpenedChild!;

        LinuxOpenedFileIdentityResult destinationIdentity =
            LinuxOpenedFileIdentity.Capture(
                destination
            );

        if (!destinationIdentity.Success)
        {
            /*
             * A directory or other non-regular object still
             * proves the destination name is occupied. Treat
             * that as conflict rather than losing the semantic
             * recovery classification.
             */
            if (
                destinationIdentity.State ==
                LinuxOpenedFileIdentityState
                    .NotRegularFile)
            {
                return Result(
                    ClassifyConflict(
                        journal.State
                    ),
                    journal,
                    parentValidation:
                        parentAcquisition.Validation,
                    destinationOpenState:
                        opened.State,
                    destinationIdentity:
                        destinationIdentity,
                    error:
                        "The destination name is occupied by " +
                        "a non-regular file."
                );
            }

            return Result(
                DataRelativePathRepairFileRecoveryState
                    .DestinationInspectionFailed,
                journal,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    opened.State,
                destinationIdentity:
                    destinationIdentity,
                error:
                    destinationIdentity.Error ??
                    destinationIdentity.State.ToString()
            );
        }

        if (
            journal.State ==
            DataRelativePathRepairFileJournalState
                .IntentRecorded)
        {
            return Result(
                DataRelativePathRepairFileRecoveryState
                    .IntentDestinationConflict,
                journal,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    opened.State,
                destinationIdentity:
                    destinationIdentity,
                error:
                    "A destination entry exists even though " +
                    "the durable journal has not reached " +
                    "Prepared state."
            );
        }

        if (
            journal.State ==
            DataRelativePathRepairFileJournalState
                .RolledBack)
        {
            return Result(
                DataRelativePathRepairFileRecoveryState
                    .RolledBackDestinationConflict,
                journal,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    opened.State,
                destinationIdentity:
                    destinationIdentity,
                error:
                    "A destination entry exists although the " +
                    "journal records a completed rollback."
            );
        }

        LinuxOpenedFileIdentityResult preparedIdentity =
            journal.PreparedFileIdentity!;

        LinuxOpenedFileSnapshotResult destinationSnapshot =
            LinuxOpenedFileSnapshot.Capture(
                destination,
                journal.Operation.DestinationPath
            );

        if (!destinationSnapshot.Success)
        {
            return Result(
                DataRelativePathRepairFileRecoveryState
                    .DestinationInspectionFailed,
                journal,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    opened.State,
                destinationIdentity:
                    destinationIdentity,
                destinationSnapshot:
                    destinationSnapshot,
                error:
                    destinationSnapshot.Error ??
                    destinationSnapshot.State.ToString()
            );
        }

        bool identityMatches =
            preparedIdentity.SameObjectAs(
                destinationIdentity
            );

        bool sizeMatches =
            destinationSnapshot.Size ==
            journal.SourceSnapshot.Size;

        bool hashMatches =
            string.Equals(
                destinationSnapshot.Sha256,
                journal.SourceSnapshot.Sha256,
                StringComparison.OrdinalIgnoreCase
            );

        bool matches =
            identityMatches &&
            sizeMatches &&
            hashMatches;

        string? mismatchReason =
            null;

        if (!identityMatches)
        {
            mismatchReason =
                "The destination inode does not match the " +
                "inode recorded while the repair file was " +
                "Prepared.";
        }
        else if (!sizeMatches)
        {
            mismatchReason =
                "The destination inode matches the Prepared " +
                "inode, but its size does not match the " +
                "journal source snapshot.";
        }
        else if (!hashMatches)
        {
            mismatchReason =
                "The destination inode and size match the " +
                "Prepared evidence, but its SHA-256 does not " +
                "match the journal source snapshot.";
        }

        return Result(
            matches
                ? ClassifyMatch(
                    journal.State
                )
                : ClassifyConflict(
                    journal.State
                ),
            journal,
            parentValidation:
                parentAcquisition.Validation,
            destinationOpenState:
                opened.State,
            destinationIdentity:
                destinationIdentity,
            destinationSnapshot:
                destinationSnapshot,
            error:
                mismatchReason
        );
    }

    private static DataRelativePathRepairFileRecoveryState
        ClassifyMissing(
            DataRelativePathRepairFileJournalState state)
    {
        return state switch
        {
            DataRelativePathRepairFileJournalState
                .IntentRecorded =>
                    DataRelativePathRepairFileRecoveryState
                        .IntentDestinationMissing,

            DataRelativePathRepairFileJournalState
                .Prepared =>
                    DataRelativePathRepairFileRecoveryState
                        .PreparedDestinationMissing,

            DataRelativePathRepairFileJournalState
                .Applied =>
                    DataRelativePathRepairFileRecoveryState
                        .AppliedDestinationMissing,

            DataRelativePathRepairFileJournalState
                .RollbackRequested =>
                    DataRelativePathRepairFileRecoveryState
                        .RollbackRequestedDestinationMissing,

            DataRelativePathRepairFileJournalState
                .RolledBack =>
                    DataRelativePathRepairFileRecoveryState
                        .RolledBackDestinationMissing,

            _ =>
                throw new InvalidOperationException(
                    $"Unsupported journal state {state}."
                )
        };
    }

    private static DataRelativePathRepairFileRecoveryState
        ClassifyMatch(
            DataRelativePathRepairFileJournalState state)
    {
        return state switch
        {
            DataRelativePathRepairFileJournalState
                .Prepared =>
                    DataRelativePathRepairFileRecoveryState
                        .PreparedDestinationMatches,

            DataRelativePathRepairFileJournalState
                .Applied =>
                    DataRelativePathRepairFileRecoveryState
                        .AppliedDestinationMatches,

            DataRelativePathRepairFileJournalState
                .RollbackRequested =>
                    DataRelativePathRepairFileRecoveryState
                        .RollbackRequestedDestinationMatches,

            _ =>
                throw new InvalidOperationException(
                    $"Journal state {state} cannot classify " +
                    "a matching prepared destination."
                )
        };
    }

    private static DataRelativePathRepairFileRecoveryState
        ClassifyConflict(
            DataRelativePathRepairFileJournalState state)
    {
        return state switch
        {
            DataRelativePathRepairFileJournalState
                .IntentRecorded =>
                    DataRelativePathRepairFileRecoveryState
                        .IntentDestinationConflict,

            DataRelativePathRepairFileJournalState
                .Prepared =>
                    DataRelativePathRepairFileRecoveryState
                        .PreparedDestinationConflict,

            DataRelativePathRepairFileJournalState
                .Applied =>
                    DataRelativePathRepairFileRecoveryState
                        .AppliedDestinationConflict,

            DataRelativePathRepairFileJournalState
                .RollbackRequested =>
                    DataRelativePathRepairFileRecoveryState
                        .RollbackRequestedDestinationConflict,

            DataRelativePathRepairFileJournalState
                .RolledBack =>
                    DataRelativePathRepairFileRecoveryState
                        .RolledBackDestinationConflict,

            _ =>
                throw new InvalidOperationException(
                    $"Unsupported journal state {state}."
                )
        };
    }

    private static
        DataRelativePathRepairFileRecoveryClassification
        Result(
            DataRelativePathRepairFileRecoveryState state,
            DataRelativePathRepairFileJournalRecord journal,
            DataRelativePathRepairDestinationParentValidation?
                parentValidation = null,
            LinuxOpenChildReadOnlyAtState?
                destinationOpenState = null,
            LinuxOpenedFileIdentityResult?
                destinationIdentity = null,
            LinuxOpenedFileSnapshotResult?
                destinationSnapshot = null,
            string? error = null)
    {
        return new
            DataRelativePathRepairFileRecoveryClassification(
                State:
                    state,
                Journal:
                    journal,
                ParentValidation:
                    parentValidation,
                DestinationOpenState:
                    destinationOpenState,
                DestinationIdentity:
                    destinationIdentity,
                DestinationSnapshot:
                    destinationSnapshot,
                Error:
                    error
            );
    }
}
