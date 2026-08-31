using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public static class
    DataRelativePathRepairDirectoryIntentRecoveryAction
{
    public static DataRelativePathRepairDirectoryIntentRecovery Recover(
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
                DataRelativePathRepairDirectoryIntentRecoveryState
                    .InvalidExpectedJournalIdentity,
                error:
                    "Directory intent recovery requires a usable generation-aware " +
                    "identity when the caller binds recovery to an " +
                    "earlier journal read."
            );
        }

        /*
         * Serialize cooperating CaseCompat writers before reading
         * or acting on the durable transaction.
         */
        LinuxExclusiveDirectoryLockResult lockResult =
            LinuxExclusiveDirectoryLock.Acquire(
                journalDirectory
            );

        if (!lockResult.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryIntentRecoveryState
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
                DataRelativePathRepairDirectoryIntentRecoveryState
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
                DataRelativePathRepairDirectoryIntentRecoveryState
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

        /*
         * This action is intentionally narrow.
         *
         * It handles exactly:
         *
         *   durable IntentRecorded
         *   + final destination still missing
         *
         * It creates and durably records a prepared staging
         * directory, then stops. Publication remains the separate
         * responsibility of directory forward recovery.
         */
        if (
            classification.State !=
            DataRelativePathRepairDirectoryRecoveryState
                .IntentFinalMissing)
        {
            return Result(
                DataRelativePathRepairDirectoryIntentRecoveryState
                    .RecoveryStateNotEligible,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                error:
                    $"Recovery state {classification.State} does " +
                    "not authorize preparation from IntentRecorded."
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
                DataRelativePathRepairDirectoryIntentRecoveryState
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
         * Directory ownership authority must be generation-aware.
         *
         * The shared parent lease may be usable by file repair
         * without inode generation, but directory mutation cannot
         * fall back to device/inode/mount-only identity.
         */
        if (
            !parent.ActualIncarnation.Success ||
            parent.IncarnationIdentity is null)
        {
            return Result(
                DataRelativePathRepairDirectoryIntentRecoveryState
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
                DataRelativePathRepairDirectoryIntentRecoveryState
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
                    "by the durable Intent journal."
            );
        }

        string finalChildName =
            Path.GetFileName(
                journal.Operation.DestinationPath
            );

        /*
         * Classification occurred using an earlier parent lease.
         * Revalidate the final namespace entry beneath this exact
         * retained parent descriptor before creating anything.
         */
        MissingRevalidationResult finalMissing =
            RevalidateMissingChild(
                parent.OpenedPath,
                finalChildName
            );

        if (!finalMissing.Success)
        {
            return Result(
                finalMissing.Changed
                    ? DataRelativePathRepairDirectoryIntentRecoveryState
                        .NamespaceChangedBeforePreparation
                    : DataRelativePathRepairDirectoryIntentRecoveryState
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
                finalChildName
            );

        string freshDisplayPath =
            Path.Combine(
                journal.DestinationParentSnapshot.PhysicalPath,
                freshStagingChildName
            );

        /*
         * LinuxPrepareOwnedDirectoryAt performs the actual namespace
         * preparation beneath the retained parent descriptor:
         *
         *   mkdirat(no overwrite)
         *   openat(O_NOFOLLOW)
         *   strong directory-incarnation capture
         *   fsync(parent)
         *
         * Partial failure deliberately prefers a possible residue
         * over uncertain deletion.
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
                DataRelativePathRepairDirectoryIntentRecoveryState
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

        /*
         * The directory now exists durably, but the durable journal
         * still says IntentRecorded. Advance exactly one state:
         *
         *     IntentRecorded -> Prepared
         *
         * Do not publish the final name in this action.
         */
        DataRelativePathRepairDirectoryJournalTransitionResult
            preparedTransition =
                DataRelativePathRepairDirectoryJournal.MarkPrepared(
                    journal,
                    freshStagingChildName,
                    prepared.IncarnationIdentity,
                    nowUtc
                );

        if (!preparedTransition.Success)
        {
            /*
             * The newly prepared directory is not represented by the
             * durable journal. Do not infer cleanup authority from its
             * CaseCompat-looking name.
             */
            return Result(
                DataRelativePathRepairDirectoryIntentRecoveryState
                    .PreparedTransitionFailed,
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
                preparedTransition:
                    preparedTransition,
                unjournaledStagingEntryMayRemain:
                    true,
                error:
                    preparedTransition.Error ??
                    preparedTransition.State.ToString()
            );
        }

        DataRelativePathRepairDirectoryJournalWriterResult
            preparedWrite =
                DataRelativePathRepairDirectoryJournalWriter
                    .ReplaceExisting(
                        journalDirectory,
                        journalChildName,
                        read.JournalIncarnationIdentity!,
                        preparedTransition.Record!
                    );

        if (!preparedWrite.Success)
        {
            /*
             * The filesystem preparation is already durable while the
             * journal remains at IntentRecorded.
             *
             * Report the possible orphan explicitly. Never delete it
             * merely because its name has the CaseCompat prefix.
             */
            return Result(
                DataRelativePathRepairDirectoryIntentRecoveryState
                    .PreparedJournalWriteFailed,
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
                preparedTransition:
                    preparedTransition,
                preparedJournalWrite:
                    preparedWrite,
                unjournaledStagingEntryMayRemain:
                    true,
                error:
                    preparedWrite.Error ??
                    preparedWrite.State.ToString()
            );
        }

        return Result(
            DataRelativePathRepairDirectoryIntentRecoveryState
                .PreparedDurably,
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
            preparedTransition:
                preparedTransition,
            preparedJournalWrite:
                preparedWrite,
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
                    finalChildName,
                    StringComparison.Ordinal
                ))
            {
                return candidate;
            }
        }
    }

    private static
        DataRelativePathRepairDirectoryIntentRecovery
        Result(
            DataRelativePathRepairDirectoryIntentRecoveryState state,
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
                preparedTransition = null,
            DataRelativePathRepairDirectoryJournalWriterResult?
                preparedJournalWrite = null,
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
            PreparedTransition:
                preparedTransition,
            PreparedJournalWrite:
                preparedJournalWrite,
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
