using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public static class DataRelativePathRepairFileRecoveryClassifier
{
    public static
        DataRelativePathRepairFileRecoveryClassification
        Classify(
            DataRelativePathRepairFileJournalRecord journal,
            string trustedDataRoot)
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
         * The durable journal describes recovery state; it does not
         * grant filesystem authority.
         *
         * Bind its recorded Data root to the independently trusted
         * root supplied by the recovery caller before inspecting or
         * mutating anything beneath that root.
         */
        if (
            !DataRelativePathRepairDataRootAuthority.Matches(
                trustedDataRoot,
                journal.DataRoot,
                out string? dataRootBindingError
            ))
        {
            return Result(
                DataRelativePathRepairFileRecoveryState
                    .DataRootMismatch,
                journal,
                error:
                    dataRootBindingError
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
                        trustedDataRoot,
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

        LinuxOpenedFileIncarnationResult destinationIncarnation =
            LinuxOpenedFileIncarnation.Capture(
                destination
            );

        /*
         * A directory or other non-regular object still proves
         * the destination name is occupied. Treat that as
         * conflict rather than losing the semantic recovery
         * classification.
         */
        if (
            destinationIncarnation.State ==
            LinuxOpenedFileIncarnationState
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
                    destinationIncarnation.PhysicalIdentity,
                destinationIncarnation:
                    destinationIncarnation,
                error:
                    "The destination name is occupied by " +
                    "a non-regular file."
            );
        }

        /*
         * Once a durable journal contains generation-aware file
         * authority, recovery must not fall back to a weaker
         * device/inode/mount comparison when generation capture
         * is unavailable.
         */
        if (!destinationIncarnation.Success)
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
                    destinationIncarnation.PhysicalIdentity,
                destinationIncarnation:
                    destinationIncarnation,
                error:
                    destinationIncarnation.Error ??
                    destinationIncarnation.State.ToString()
            );
        }

        LinuxOpenedFileIdentityResult destinationIdentity =
            destinationIncarnation.PhysicalIdentity!;

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
                destinationIncarnation:
                    destinationIncarnation,
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
                destinationIncarnation:
                    destinationIncarnation,
                error:
                    "A destination entry exists although the " +
                    "journal records a completed rollback."
            );
        }

        LinuxFileIncarnationIdentity preparedIncarnation =
            journal.PreparedFileIncarnationIdentity!;

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
                destinationIncarnation:
                    destinationIncarnation,
                destinationSnapshot:
                    destinationSnapshot,
                error:
                    destinationSnapshot.Error ??
                    destinationSnapshot.State.ToString()
            );
        }

        bool incarnationMatches =
            preparedIncarnation.SameIncarnationAs(
                destinationIncarnation.Identity!
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
            incarnationMatches &&
            sizeMatches &&
            hashMatches;

        string? mismatchReason =
            null;

        if (!incarnationMatches)
        {
            mismatchReason =
                "The destination file incarnation does not match " +
                "the incarnation recorded while the repair file " +
                "was Prepared.";
        }
        else if (!sizeMatches)
        {
            mismatchReason =
                "The destination file incarnation matches the " +
                "Prepared authority, but its size does not match " +
                "the journal source snapshot.";
        }
        else if (!hashMatches)
        {
            mismatchReason =
                "The destination file incarnation and size match " +
                "the Prepared evidence, but its SHA-256 does not " +
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
            destinationIncarnation:
                destinationIncarnation,
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
            LinuxOpenedFileIncarnationResult?
                destinationIncarnation = null,
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
            )
            {
                DestinationIncarnation =
                    destinationIncarnation
            };
    }
}
