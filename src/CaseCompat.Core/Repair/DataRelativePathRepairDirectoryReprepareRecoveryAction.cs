using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public static class
    DataRelativePathRepairDirectoryReprepareRecoveryAction
{
    public static DataRelativePathRepairDirectoryReprepareRecovery Recover(
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
                DataRelativePathRepairDirectoryReprepareRecoveryState
                    .InvalidExpectedJournalIdentity,
                error:
                    "Directory re-preparation requires a usable generation-aware " +
                    "identity when the caller binds recovery to an " +
                    "earlier journal read."
            );
        }

        LinuxExclusiveDirectoryLockResult lockResult =
            LinuxExclusiveDirectoryLock.Acquire(
                journalDirectory
            );

        if (!lockResult.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryReprepareRecoveryState
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
                DataRelativePathRepairDirectoryReprepareRecoveryState
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
                DataRelativePathRepairDirectoryReprepareRecoveryState
                    .JournalIncarnationChanged,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                error:
                    "The recovery journal changed after the caller " +
                    "read and bound it. Recovery is refused before " +
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

        if (
            classification.State !=
            DataRelativePathRepairDirectoryRecoveryState
                .PreparedBothMissing)
        {
            return Result(
                DataRelativePathRepairDirectoryReprepareRecoveryState
                    .RecoveryStateNotEligible,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                error:
                    $"Recovery state {classification.State} does " +
                    "not authorize directory re-preparation."
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
                DataRelativePathRepairDirectoryReprepareRecoveryState
                    .DestinationParentValidationFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
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
         * Re-preparation is about to create a fresh directory beneath
         * this retained parent descriptor. Authorize that namespace
         * mutation only from the complete durable parent incarnation.
         *
         * Generation-unavailable is a hard failure. There is no
         * device/inode/mount-only fallback for directory ownership.
         */
        if (
            !parent.ActualIncarnation.Success ||
            parent.IncarnationIdentity is null)
        {
            return Result(
                DataRelativePathRepairDirectoryReprepareRecoveryState
                    .DestinationParentValidationFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    parentAcquisition.Validation,
                error:
                    "The destination parent could not provide " +
                    "generation-aware incarnation identity from the " +
                    "retained directory descriptor: " +
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
                DataRelativePathRepairDirectoryReprepareRecoveryState
                    .DestinationParentValidationFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    parentAcquisition.Validation,
                error:
                    "The destination parent no longer matches the " +
                    "generation-aware directory incarnation recorded " +
                    "by the durable journal."
            );
        }

        string oldStagingChildName =
            journal.PreparedStagingChildName!;

        string finalChildName =
            Path.GetFileName(
                journal.Operation.DestinationPath
            );

        MissingRevalidationResult oldStagingMissing =
            RevalidateMissingChild(
                parent.OpenedPath,
                oldStagingChildName
            );

        if (!oldStagingMissing.Success)
        {
            return Result(
                oldStagingMissing.Changed
                    ? DataRelativePathRepairDirectoryReprepareRecoveryState
                        .NamespaceChangedBeforePreparation
                    : DataRelativePathRepairDirectoryReprepareRecoveryState
                        .NamespaceRevalidationFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    parentAcquisition.Validation,
                error:
                    oldStagingMissing.Error
            );
        }

        MissingRevalidationResult finalMissing =
            RevalidateMissingChild(
                parent.OpenedPath,
                finalChildName
            );

        if (!finalMissing.Success)
        {
            return Result(
                finalMissing.Changed
                    ? DataRelativePathRepairDirectoryReprepareRecoveryState
                        .NamespaceChangedBeforePreparation
                    : DataRelativePathRepairDirectoryReprepareRecoveryState
                        .NamespaceRevalidationFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    parentAcquisition.Validation,
                error:
                    finalMissing.Error
            );
        }

        string freshStagingChildName =
            CreateFreshStagingName(
                journal,
                oldStagingChildName,
                finalChildName
            );

        string freshDisplayPath =
            Path.Combine(
                journal.DestinationParentSnapshot.PhysicalPath,
                freshStagingChildName
            );

        /*
         * LinuxPrepareOwnedDirectoryAt performs:
         *
         *   mkdirat(fresh name)
         *   openat(O_NOFOLLOW)
         *   descriptor identity capture
         *   fsync(parent)
         *
         * It deliberately performs no automatic cleanup after a
         * partial failure.
         */
        LinuxPrepareOwnedDirectoryAtResult preparation =
            LinuxPrepareOwnedDirectoryAt.Prepare(
                parent.OpenedPath,
                freshStagingChildName,
                freshDisplayPath
            );

        if (!preparation.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryReprepareRecoveryState
                    .PreparationFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    parentAcquisition.Validation,
                freshStagingChildName:
                    freshStagingChildName,
                preparation:
                    preparation,
                unjournaledStagingEntryMayRemain:
                    preparation.StagingEntryMayRemain,
                error:
                    preparation.Error ??
                    preparation.State.ToString()
            );
        }

        using LinuxPreparedOwnedDirectoryLease prepared =
            preparation.Lease!;

        DataRelativePathRepairDirectoryJournalTransitionResult
            reprepareTransition =
                DataRelativePathRepairDirectoryJournal.Reprepare(
                    journal,
                    freshStagingChildName,
                    prepared.IncarnationIdentity,
                    nowUtc
                );

        if (!reprepareTransition.Success)
        {
            /*
             * A fresh staging directory now exists, but the durable
             * journal still describes the old missing staging
             * object.
             *
             * Do not guess at deletion here. The exact new identity
             * is returned to the caller, but a crash at this boundary
             * can inherently leave an unjournaled staging directory.
             */
            return Result(
                DataRelativePathRepairDirectoryReprepareRecoveryState
                    .ReprepareTransitionFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    parentAcquisition.Validation,
                freshStagingChildName:
                    freshStagingChildName,
                preparation:
                    preparation,
                reprepareTransition:
                    reprepareTransition,
                unjournaledStagingEntryMayRemain:
                    true,
                error:
                    reprepareTransition.Error ??
                    reprepareTransition.State.ToString()
            );
        }

        DataRelativePathRepairDirectoryJournalWriterResult
            reprepareWrite =
                DataRelativePathRepairDirectoryJournalWriter
                    .ReplaceExisting(
                        journalDirectory,
                        journalChildName,
                        read.JournalIncarnationIdentity!,
                        reprepareTransition.Record!
                    );

        if (!reprepareWrite.Success)
        {
            /*
             * The new staging directory is not yet represented by a
             * durable journal revision. Report the potential orphan
             * explicitly rather than deleting anything based only on
             * its CaseCompat-looking name.
             */
            return Result(
                DataRelativePathRepairDirectoryReprepareRecoveryState
                    .RepreparedJournalWriteFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                parentValidation:
                    parentAcquisition.Validation,
                freshStagingChildName:
                    freshStagingChildName,
                preparation:
                    preparation,
                reprepareTransition:
                    reprepareTransition,
                repreparedJournalWrite:
                    reprepareWrite,
                unjournaledStagingEntryMayRemain:
                    true,
                error:
                    reprepareWrite.Error ??
                    reprepareWrite.State.ToString()
            );
        }

        return Result(
            DataRelativePathRepairDirectoryReprepareRecoveryState
                .RepreparedDurably,
            lockState:
                lockResult.State,
            journalRead:
                read,
            classification:
                classification,
            parentValidation:
                parentAcquisition.Validation,
            freshStagingChildName:
                freshStagingChildName,
            preparation:
                preparation,
            reprepareTransition:
                reprepareTransition,
            repreparedJournalWrite:
                reprepareWrite,
            unjournaledStagingEntryMayRemain:
                false
        );
    }

    private static MissingRevalidationResult RevalidateMissingChild(
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
            return MissingRevalidationResult.Missing();
        }

        if (opened.Success)
        {
            opened.OpenedChild!.Dispose();

            return MissingRevalidationResult.ChangedResult(
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
                $"A symbolic link appeared at child '{childName}' " +
                "after recovery classification."
            );
        }

        return MissingRevalidationResult.Failed(
            opened.Error ??
            opened.State.ToString()
        );
    }

    private static string CreateFreshStagingName(
        DataRelativePathRepairDirectoryJournalRecord journal,
        string oldStagingChildName,
        string finalChildName)
    {
        while (true)
        {
            string candidate =
                $".casecompat-dir-" +
                $"{journal.JournalId:N}-" +
                $"r{journal.Revision}-" +
                $"{Guid.NewGuid():N}.stage";

            if (
                !string.Equals(
                    candidate,
                    oldStagingChildName,
                    StringComparison.Ordinal
                ) &&
                !string.Equals(
                    candidate,
                    finalChildName,
                    StringComparison.Ordinal
                ))
            {
                return candidate;
            }
        }
    }

    private static
        DataRelativePathRepairDirectoryReprepareRecovery
        Result(
            DataRelativePathRepairDirectoryReprepareRecoveryState state,
            LinuxExclusiveDirectoryLockState? lockState = null,
            DataRelativePathRepairDirectoryJournalReaderResult?
                journalRead = null,
            DataRelativePathRepairDirectoryRecoveryClassification?
                classification = null,
            DataRelativePathRepairDestinationParentValidation?
                parentValidation = null,
            string? freshStagingChildName = null,
            LinuxPrepareOwnedDirectoryAtResult?
                preparation = null,
            DataRelativePathRepairDirectoryJournalTransitionResult?
                reprepareTransition = null,
            DataRelativePathRepairDirectoryJournalWriterResult?
                repreparedJournalWrite = null,
            bool unjournaledStagingEntryMayRemain = false,
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
            FreshStagingChildName:
                freshStagingChildName,
            Preparation:
                preparation,
            ReprepareTransition:
                reprepareTransition,
            RepreparedJournalWrite:
                repreparedJournalWrite,
            UnjournaledStagingEntryMayRemain:
                unjournaledStagingEntryMayRemain,
            Error:
                error
        );
    }

    private sealed record MissingRevalidationResult(
        bool Success,
        bool Changed,
        string? Error)
    {
        public static MissingRevalidationResult Missing()
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

        public static MissingRevalidationResult ChangedResult(
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

        public static MissingRevalidationResult Failed(
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
