using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public static class
    DataRelativePathRepairFileForwardRecoveryAction
{
    public static DataRelativePathRepairFileForwardRecovery Recover(
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
                DataRelativePathRepairFileForwardRecoveryState
                    .InvalidExpectedJournalIdentity,
                error:
                    "File forward recovery requires a usable generation-aware " +
                    "identity when the caller binds recovery to an " +
                    "earlier journal read."
            );
        }

        /*
         * Lock before reading the journal so cooperating
         * CaseCompat writers cannot advance the transaction
         * beneath this recovery attempt.
         */
        LinuxExclusiveDirectoryLockResult lockResult =
            LinuxExclusiveDirectoryLock.Acquire(
                journalDirectory
            );

        if (!lockResult.Success)
        {
            return Result(
                DataRelativePathRepairFileForwardRecoveryState
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
                DataRelativePathRepairFileForwardRecoveryState
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
                DataRelativePathRepairFileForwardRecoveryState
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

        DataRelativePathRepairFileJournalRecord journal =
            read.Record!;

        DataRelativePathRepairFileRecoveryClassification
            classification =
                DataRelativePathRepairFileRecoveryClassifier
                    .Classify(
                        journal,
                        trustedDataRoot
                    );

        bool recoverIntent =
            classification.State ==
            DataRelativePathRepairFileRecoveryState
                .IntentDestinationMissing;

        bool reprepare =
            classification.State ==
            DataRelativePathRepairFileRecoveryState
                .PreparedDestinationMissing;

        if (
            !recoverIntent &&
            !reprepare)
        {
            return Result(
                DataRelativePathRepairFileForwardRecoveryState
                    .RecoveryStateNotEligible,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                error:
                    $"Recovery state {classification.State} " +
                    "does not require forward re-preparation."
            );
        }

        DataRelativePathRepairSourceLeaseAcquisition
            sourceAcquisition =
                DataRelativePathRepairSourceLeaseAcquirer
                    .Acquire(
                        trustedDataRoot,
                        journal.SourceSnapshot
                    );

        if (!sourceAcquisition.Success)
        {
            return Result(
                DataRelativePathRepairFileForwardRecoveryState
                    .SourceValidationFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                sourceValidation:
                    sourceAcquisition.Validation,
                error:
                    sourceAcquisition.Validation.Error ??
                    sourceAcquisition.Validation.State.ToString()
            );
        }

        using DataRelativePathRepairValidatedSourceLease source =
            sourceAcquisition.Lease!;

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
                DataRelativePathRepairFileForwardRecoveryState
                    .DestinationParentValidationFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                sourceValidation:
                    sourceAcquisition.Validation,
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

        MissingRevalidationResult missing =
            RevalidateMissingDestination(
                parent.OpenedPath,
                childName
            );

        if (!missing.Success)
        {
            return Result(
                missing.Changed
                    ? DataRelativePathRepairFileForwardRecoveryState
                        .DestinationChangedBeforePreparation
                    : DataRelativePathRepairFileForwardRecoveryState
                        .DestinationRevalidationFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    missing.OpenState,
                error:
                    missing.Error
            );
        }

        LinuxCreateUnnamedFileAtResult temporaryCreate =
            LinuxCreateUnnamedFileAt.Create(
                parent.OpenedPath
            );

        if (!temporaryCreate.Success)
        {
            return Result(
                DataRelativePathRepairFileForwardRecoveryState
                    .TemporaryFileCreateFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    missing.OpenState,
                temporaryFileCreate:
                    temporaryCreate,
                error:
                    temporaryCreate.Error ??
                    temporaryCreate.State.ToString()
            );
        }

        using LinuxUnnamedFileHandle temporary =
            temporaryCreate.OpenedFile!;

        LinuxCopyFileContentsResult copy =
            LinuxCopyFileContents.CopyAndVerify(
                source.OpenedPath,
                temporary,
                journal.SourceSnapshot.Size,
                journal.SourceSnapshot.Sha256
            );

        if (!copy.Success)
        {
            return Result(
                DataRelativePathRepairFileForwardRecoveryState
                    .CopyFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    missing.OpenState,
                temporaryFileCreate:
                    temporaryCreate,
                copyResult:
                    copy,
                error:
                    copy.Error ??
                    copy.State.ToString()
            );
        }

        LinuxFsyncResult temporarySync =
            LinuxFsync.Sync(
                temporary
            );

        if (!temporarySync.Success)
        {
            return Result(
                DataRelativePathRepairFileForwardRecoveryState
                    .TemporaryFileSyncFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    missing.OpenState,
                temporaryFileCreate:
                    temporaryCreate,
                copyResult:
                    copy,
                temporaryFileSync:
                    temporarySync,
                error:
                    temporarySync.Error ??
                    temporarySync.State.ToString()
            );
        }

        LinuxOpenedFileIncarnationResult preparedIncarnation =
            LinuxOpenedFileIncarnation.Capture(
                temporary
            );

        LinuxOpenedFileIdentityResult? preparedIdentity =
            preparedIncarnation.PhysicalIdentity;

        if (!preparedIncarnation.Success)
        {
            return Result(
                DataRelativePathRepairFileForwardRecoveryState
                    .PreparedIdentityFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    missing.OpenState,
                temporaryFileCreate:
                    temporaryCreate,
                copyResult:
                    copy,
                temporaryFileSync:
                    temporarySync,
                preparedIdentity:
                    preparedIdentity,
                error:
                    preparedIncarnation.Error ??
                    preparedIncarnation.State.ToString()
            );
        }

        /*
         * Intent recovery performs the original
         * Intent -> Prepared transition.
         *
         * Prepared recovery replaces the dead historical
         * anonymous inode with this newly prepared inode while
         * remaining durably in Prepared.
         */
        DataRelativePathRepairFileJournalTransitionResult
            preparedTransition =
                recoverIntent
                    ? DataRelativePathRepairFileJournal
                        .MarkPrepared(
                            journal,
                            preparedIncarnation.Identity!,
                            nowUtc
                        )
                    : DataRelativePathRepairFileJournal
                        .Reprepare(
                            journal,
                            preparedIncarnation.Identity!,
                            nowUtc
                        );

        if (!preparedTransition.Success)
        {
            return Result(
                DataRelativePathRepairFileForwardRecoveryState
                    .PreparedTransitionFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    missing.OpenState,
                temporaryFileCreate:
                    temporaryCreate,
                copyResult:
                    copy,
                temporaryFileSync:
                    temporarySync,
                preparedIdentity:
                    preparedIdentity,
                preparedTransition:
                    preparedTransition,
                error:
                    preparedTransition.Error ??
                    preparedTransition.State.ToString()
            );
        }

        DataRelativePathRepairFileJournalWriterResult
            preparedWrite =
                DataRelativePathRepairFileJournalWriter
                    .ReplaceExisting(
                        journalDirectory,
                        journalChildName,
                        read.JournalIncarnationIdentity!,
                        preparedTransition.Record!
                    );

        if (!preparedWrite.Success)
        {
            return Result(
                DataRelativePathRepairFileForwardRecoveryState
                    .PreparedJournalWriteFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    missing.OpenState,
                temporaryFileCreate:
                    temporaryCreate,
                copyResult:
                    copy,
                temporaryFileSync:
                    temporarySync,
                preparedIdentity:
                    preparedIdentity,
                preparedTransition:
                    preparedTransition,
                preparedJournalWrite:
                    preparedWrite,
                error:
                    preparedWrite.Error ??
                    preparedWrite.State.ToString()
            );
        }

        LinuxFileIncarnationIdentity? preparedJournalIncarnation =
                preparedWrite.WrittenJournalIncarnationIdentity;

        if (
            preparedJournalIncarnation is null ||
            !preparedJournalIncarnation.Success)
        {
            return Result(
                DataRelativePathRepairFileForwardRecoveryState
                    .PreparedJournalIdentityUnavailable,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    missing.OpenState,
                temporaryFileCreate:
                    temporaryCreate,
                copyResult:
                    copy,
                temporaryFileSync:
                    temporarySync,
                preparedIdentity:
                    preparedIdentity,
                preparedTransition:
                    preparedTransition,
                preparedJournalWrite:
                    preparedWrite,
                error:
                    "The durable re-preparation journal write " +
                    "did not return a usable journal identity."
            );
        }

        /*
         * Publication remains no-overwrite.
         *
         * If an unrelated process creates the destination after
         * the absence revalidation, linkat() fails rather than
         * replacing that entry.
         */
        LinuxPublishUnnamedFileAtResult publication =
            LinuxPublishUnnamedFileAt.Publish(
                temporary,
                parent.OpenedPath,
                childName
            );

        if (!publication.Success)
        {
            return Result(
                DataRelativePathRepairFileForwardRecoveryState
                    .PublicationFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    missing.OpenState,
                temporaryFileCreate:
                    temporaryCreate,
                copyResult:
                    copy,
                temporaryFileSync:
                    temporarySync,
                preparedIdentity:
                    preparedIdentity,
                preparedTransition:
                    preparedTransition,
                preparedJournalWrite:
                    preparedWrite,
                publication:
                    publication,
                error:
                    publication.Error ??
                    publication.State.ToString()
            );
        }

        LinuxFsyncResult parentSync =
            LinuxFsync.Sync(
                parent.OpenedPath
            );

        if (!parentSync.Success)
        {
            /*
             * Prepared remains the last durable journal
             * checkpoint. Recovery can classify the now-visible
             * destination against the prepared identity/hash.
             */
            return Result(
                DataRelativePathRepairFileForwardRecoveryState
                    .DestinationParentSyncFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    missing.OpenState,
                temporaryFileCreate:
                    temporaryCreate,
                copyResult:
                    copy,
                temporaryFileSync:
                    temporarySync,
                preparedIdentity:
                    preparedIdentity,
                preparedTransition:
                    preparedTransition,
                preparedJournalWrite:
                    preparedWrite,
                publication:
                    publication,
                destinationParentSync:
                    parentSync,
                error:
                    parentSync.Error ??
                    parentSync.State.ToString()
            );
        }

        DataRelativePathRepairFileJournalTransitionResult
            appliedTransition =
                DataRelativePathRepairFileJournal
                    .MarkApplied(
                        preparedTransition.Record!,
                        nowUtc
                    );

        if (!appliedTransition.Success)
        {
            return Result(
                DataRelativePathRepairFileForwardRecoveryState
                    .AppliedTransitionFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    missing.OpenState,
                temporaryFileCreate:
                    temporaryCreate,
                copyResult:
                    copy,
                temporaryFileSync:
                    temporarySync,
                preparedIdentity:
                    preparedIdentity,
                preparedTransition:
                    preparedTransition,
                preparedJournalWrite:
                    preparedWrite,
                publication:
                    publication,
                destinationParentSync:
                    parentSync,
                appliedTransition:
                    appliedTransition,
                error:
                    appliedTransition.Error ??
                    appliedTransition.State.ToString()
            );
        }

        DataRelativePathRepairFileJournalWriterResult appliedWrite =
            DataRelativePathRepairFileJournalWriter
                .ReplaceExisting(
                    journalDirectory,
                    journalChildName,
                    preparedJournalIncarnation,
                    appliedTransition.Record!
                );

        if (!appliedWrite.Success)
        {
            return Result(
                DataRelativePathRepairFileForwardRecoveryState
                    .AppliedJournalWriteFailed,
                lockState:
                    lockResult.State,
                journalRead:
                    read,
                classification:
                    classification,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    missing.OpenState,
                temporaryFileCreate:
                    temporaryCreate,
                copyResult:
                    copy,
                temporaryFileSync:
                    temporarySync,
                preparedIdentity:
                    preparedIdentity,
                preparedTransition:
                    preparedTransition,
                preparedJournalWrite:
                    preparedWrite,
                publication:
                    publication,
                destinationParentSync:
                    parentSync,
                appliedTransition:
                    appliedTransition,
                appliedJournalWrite:
                    appliedWrite,
                error:
                    appliedWrite.Error ??
                    appliedWrite.State.ToString()
            );
        }

        return Result(
            DataRelativePathRepairFileForwardRecoveryState
                .AppliedDurably,
            lockState:
                lockResult.State,
            journalRead:
                read,
            classification:
                classification,
            sourceValidation:
                sourceAcquisition.Validation,
            parentValidation:
                parentAcquisition.Validation,
            destinationOpenState:
                missing.OpenState,
            temporaryFileCreate:
                temporaryCreate,
            copyResult:
                copy,
            temporaryFileSync:
                temporarySync,
            preparedIdentity:
                preparedIdentity,
            preparedTransition:
                preparedTransition,
            preparedJournalWrite:
                preparedWrite,
            publication:
                publication,
            destinationParentSync:
                parentSync,
            appliedTransition:
                appliedTransition,
            appliedJournalWrite:
                appliedWrite
        );
    }

    private static MissingRevalidationResult
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
            return MissingRevalidationResult.Missing(
                opened.State
            );
        }

        if (opened.Success)
        {
            opened.OpenedChild!.Dispose();

            return MissingRevalidationResult.ChangedResult(
                opened.State,
                "The destination appeared after recovery " +
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
                "A symbolic link appeared at the destination " +
                "after recovery classification."
            );
        }

        return MissingRevalidationResult.Failed(
            opened.State,
            opened.Error ??
            opened.State.ToString()
        );
    }

    private static DataRelativePathRepairFileForwardRecovery Result(
        DataRelativePathRepairFileForwardRecoveryState state,
        LinuxExclusiveDirectoryLockState? lockState = null,
        DataRelativePathRepairFileJournalReaderResult?
            journalRead = null,
        DataRelativePathRepairFileRecoveryClassification?
            classification = null,
        DataRelativePathRepairSourceValidation?
            sourceValidation = null,
        DataRelativePathRepairDestinationParentValidation?
            parentValidation = null,
        LinuxOpenChildReadOnlyAtState?
            destinationOpenState = null,
        LinuxCreateUnnamedFileAtResult?
            temporaryFileCreate = null,
        LinuxCopyFileContentsResult?
            copyResult = null,
        LinuxFsyncResult?
            temporaryFileSync = null,
        LinuxOpenedFileIdentityResult?
            preparedIdentity = null,
        DataRelativePathRepairFileJournalTransitionResult?
            preparedTransition = null,
        DataRelativePathRepairFileJournalWriterResult?
            preparedJournalWrite = null,
        LinuxPublishUnnamedFileAtResult?
            publication = null,
        LinuxFsyncResult?
            destinationParentSync = null,
        DataRelativePathRepairFileJournalTransitionResult?
            appliedTransition = null,
        DataRelativePathRepairFileJournalWriterResult?
            appliedJournalWrite = null,
        string? error = null)
    {
        return new DataRelativePathRepairFileForwardRecovery(
            State:
                state,
            LockState:
                lockState,
            JournalRead:
                journalRead,
            Classification:
                classification,
            SourceValidation:
                sourceValidation,
            ParentValidation:
                parentValidation,
            DestinationOpenState:
                destinationOpenState,
            TemporaryFileCreate:
                temporaryFileCreate,
            CopyResult:
                copyResult,
            TemporaryFileSync:
                temporaryFileSync,
            PreparedIdentity:
                preparedIdentity,
            PreparedTransition:
                preparedTransition,
            PreparedJournalWrite:
                preparedJournalWrite,
            Publication:
                publication,
            DestinationParentSync:
                destinationParentSync,
            AppliedTransition:
                appliedTransition,
            AppliedJournalWrite:
                appliedJournalWrite,
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
}
