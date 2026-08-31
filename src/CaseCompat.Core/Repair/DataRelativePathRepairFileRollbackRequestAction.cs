using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public static class
    DataRelativePathRepairFileRollbackRequestAction
{
    public static DataRelativePathRepairFileRollbackRequest Request(
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
                DataRelativePathRepairFileRollbackRequestState
                    .InvalidExpectedJournalIdentity,
                error:
                    "File rollback request requires a usable generation-aware " +
                    "identity when the caller binds rollback to an " +
                    "earlier journal read."
            );
        }

        /*
         * Lock before reading the journal so the decision and
         * durable transition refer to one serialized transaction
         * state among cooperating CaseCompat writers.
         */
        LinuxExclusiveDirectoryLockResult lockResult =
            LinuxExclusiveDirectoryLock.Acquire(
                journalDirectory
            );

        if (!lockResult.Success)
        {
            return Result(
                DataRelativePathRepairFileRollbackRequestState
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
                DataRelativePathRepairFileRollbackRequestState
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
                DataRelativePathRepairFileRollbackRequestState
                    .JournalIncarnationChanged,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                error:
                    "The rollback journal changed after the caller " +
                    "read and bound it. Rollback is refused before " +
                    "classification or filesystem mutation."
            );
        }

        DataRelativePathRepairFileJournalRecord journal =
            read.Record!;

        DataRelativePathRepairFileRecoveryClassification
            classification =
                DataRelativePathRepairFileRecoveryClassifier
                    .Classify(
                        journal,
                        trustedDataRoot
                    );

        /*
         * A rollback request is authorized only while the
         * currently published destination still proves that it
         * is the file described by the Applied journal.
         *
         * Missing, replaced, mutated, symbolic-link, and other
         * anomalous states remain untouched.
         */
        if (
            classification.State !=
            DataRelativePathRepairFileRecoveryState
                .AppliedDestinationMatches)
        {
            return Result(
                DataRelativePathRepairFileRollbackRequestState
                    .RecoveryStateNotEligible,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                error:
                    $"Recovery state {classification.State} " +
                    "does not authorize a rollback request."
            );
        }

        DataRelativePathRepairFileJournalTransitionResult
            transition =
                DataRelativePathRepairFileJournal
                    .RequestRollback(
                        journal,
                        nowUtc
                    );

        if (!transition.Success)
        {
            return Result(
                DataRelativePathRepairFileRollbackRequestState
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

        DataRelativePathRepairFileJournalWriterResult write =
            DataRelativePathRepairFileJournalWriter
                .ReplaceExisting(
                    journalDirectory,
                    journalChildName,
                    read.JournalIncarnationIdentity!,
                    transition.Record!
                );

        if (!write.Success)
        {
            return Result(
                DataRelativePathRepairFileRollbackRequestState
                    .JournalWriteFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
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
            DataRelativePathRepairFileRollbackRequestState
                .RequestedDurably,
            lockState:
                lockResult.State,
            journalRead:
                read,
            classification:
                classification,
            journalTransition:
                transition,
            journalWrite:
                write
        );
    }

    private static DataRelativePathRepairFileRollbackRequest Result(
        DataRelativePathRepairFileRollbackRequestState state,
        LinuxExclusiveDirectoryLockState? lockState = null,
        DataRelativePathRepairFileJournalReaderResult?
            journalRead = null,
        DataRelativePathRepairFileRecoveryClassification?
            classification = null,
        DataRelativePathRepairFileJournalTransitionResult?
            journalTransition = null,
        DataRelativePathRepairFileJournalWriterResult?
            journalWrite = null,
        string? error = null)
    {
        return new DataRelativePathRepairFileRollbackRequest(
            State:
                state,
            LockState:
                lockState,
            JournalRead:
                journalRead,
            Classification:
                classification,
            JournalTransition:
                journalTransition,
            JournalWrite:
                journalWrite,
            Error:
                error
        );
    }
}
