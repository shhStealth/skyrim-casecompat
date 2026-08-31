using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public static class
    DataRelativePathRepairDirectoryRollbackRequestAction
{
    public static DataRelativePathRepairDirectoryRollbackRequest Request(
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
                DataRelativePathRepairDirectoryRollbackRequestState
                    .InvalidExpectedJournalIdentity,
                error:
                    "Directory rollback request requires a usable generation-aware " +
                    "identity when the caller binds rollback to an " +
                    "earlier journal read."
            );
        }

        /*
         * Lock before reading so the rollback decision and durable
         * journal transition refer to one serialized transaction
         * state among cooperating CaseCompat writers.
         */
        LinuxExclusiveDirectoryLockResult lockResult =
            LinuxExclusiveDirectoryLock.Acquire(
                journalDirectory
            );

        if (!lockResult.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryRollbackRequestState
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
                DataRelativePathRepairDirectoryRollbackRequestState
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
                DataRelativePathRepairDirectoryRollbackRequestState
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

        DataRelativePathRepairDirectoryJournalRecord journal =
            read.Record!;

        DataRelativePathRepairDirectoryRecoveryClassification
            classification =
                DataRelativePathRepairDirectoryRecoveryClassifier
                    .Classify(
                        journal,
                        trustedDataRoot
                    );

        /*
         * Request destructive rollback only while the currently
         * published final directory still proves ownership of the
         * inode described by the Applied journal.
         *
         * Missing, replaced, symbolic-link, staging-present, or
         * otherwise anomalous states remain untouched.
         */
        if (
            classification.State !=
            DataRelativePathRepairDirectoryRecoveryState
                .AppliedFinalMatches)
        {
            return Result(
                DataRelativePathRepairDirectoryRollbackRequestState
                    .RecoveryStateNotEligible,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                error:
                    $"Recovery state {classification.State} does " +
                    "not authorize a directory rollback request."
            );
        }

        DataRelativePathRepairDirectoryJournalTransitionResult
            transition =
                DataRelativePathRepairDirectoryJournal
                    .RequestRollback(
                        journal,
                        nowUtc
                    );

        if (!transition.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryRollbackRequestState
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
                DataRelativePathRepairDirectoryRollbackRequestState
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
            DataRelativePathRepairDirectoryRollbackRequestState
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

    private static DataRelativePathRepairDirectoryRollbackRequest Result(
        DataRelativePathRepairDirectoryRollbackRequestState state,
        LinuxExclusiveDirectoryLockState? lockState = null,
        DataRelativePathRepairDirectoryJournalReaderResult?
            journalRead = null,
        DataRelativePathRepairDirectoryRecoveryClassification?
            classification = null,
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
            JournalTransition:
                journalTransition,
            JournalWrite:
                journalWrite,
            Error:
                error
        );
    }
}
