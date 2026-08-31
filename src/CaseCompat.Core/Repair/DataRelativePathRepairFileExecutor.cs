using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public static class DataRelativePathRepairFileExecutor
{
    public static DataRelativePathRepairFileExecution Execute(
        LinuxNoFollowPathHandle journalDirectory,
        string journalChildName,
        DataRelativePathRepairFileJournalRecord intent,
        string trustedDataRoot,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(
            journalDirectory
        );

        ArgumentNullException.ThrowIfNull(
            intent
        );

        string? validationError =
            DataRelativePathRepairFileJournal.Validate(
                intent
            );

        if (
            validationError is not null ||
            intent.State !=
                DataRelativePathRepairFileJournalState
                    .IntentRecorded ||
            intent.Revision != 0)
        {
            return Result(
                DataRelativePathRepairFileExecutionState
                    .InvalidIntent,
                error:
                    validationError ??
                    "Forward execution requires a revision-zero " +
                    "IntentRecorded journal record."
            );
        }

        /*
         * The intent describes the requested repair; it does not
         * independently grant filesystem authority.
         *
         * Bind the recorded Data root to the caller-authorized
         * Data root before locking, inspecting, or mutating live
         * filesystem state.
         */
        if (
            !DataRelativePathRepairDataRootAuthority.Matches(
                trustedDataRoot,
                intent.DataRoot,
                out string? dataRootBindingError
            ))
        {
            return Result(
                DataRelativePathRepairFileExecutionState
                    .DataRootMismatch,
                error:
                    dataRootBindingError
            );
        }

        /*
         * Serialize all cooperating CaseCompat writers before
         * looking at mutable filesystem state.
         */
        LinuxExclusiveDirectoryLockResult lockResult =
            LinuxExclusiveDirectoryLock.Acquire(
                journalDirectory
            );

        if (!lockResult.Success)
        {
            return Result(
                DataRelativePathRepairFileExecutionState
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

        DataRelativePathRepairSourceLeaseAcquisition
            sourceAcquisition =
                DataRelativePathRepairSourceLeaseAcquirer
                    .Acquire(
                        trustedDataRoot,
                        intent.SourceSnapshot
                    );

        if (!sourceAcquisition.Success)
        {
            return Result(
                DataRelativePathRepairFileExecutionState
                    .SourceValidationFailed,
                lockState:
                    lockResult.State,
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
                        intent.DestinationParentSnapshot
                    );

        if (!parentAcquisition.Success)
        {
            return Result(
                DataRelativePathRepairFileExecutionState
                    .DestinationParentValidationFailed,
                lockState:
                    lockResult.State,
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
                intent.Operation.DestinationPath
            );

        /*
         * Avoid creating a durable Intent journal for a
         * destination which is already occupied at the time
         * execution begins.
         *
         * This is only a preflight. Publication still uses
         * linkat() without overwrite, so a later race remains
         * safely blocked.
         */
        LinuxOpenChildReadOnlyAtResult destinationInspection =
            LinuxOpenChildReadOnlyAt.Open(
                parent.OpenedPath,
                childName
            );

        if (destinationInspection.Success)
        {
            destinationInspection.OpenedChild!.Dispose();

            return Result(
                DataRelativePathRepairFileExecutionState
                    .DestinationExists,
                lockState:
                    lockResult.State,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    destinationInspection.State,
                error:
                    "The destination already exists. " +
                    "Forward repair never overwrites it."
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
                    DataRelativePathRepairFileExecutionState
                        .DestinationExists,
                    lockState:
                        lockResult.State,
                    sourceValidation:
                        sourceAcquisition.Validation,
                    parentValidation:
                        parentAcquisition.Validation,
                    destinationOpenState:
                        destinationInspection.State,
                    error:
                        "The destination name is occupied by " +
                        "a symbolic link. Forward repair will " +
                        "not replace it."
                );
            }

            return Result(
                DataRelativePathRepairFileExecutionState
                    .DestinationInspectionFailed,
                lockState:
                    lockResult.State,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    destinationInspection.State,
                error:
                    destinationInspection.Error ??
                    destinationInspection.State.ToString()
            );
        }

        /*
         * First durable transaction boundary.
         *
         * No repair asset has been created yet.
         */
        DataRelativePathRepairFileJournalWriterResult initialWrite =
            DataRelativePathRepairFileJournalWriter
                .CreateInitial(
                    journalDirectory,
                    journalChildName,
                    intent
                );

        if (!initialWrite.Success)
        {
            return Result(
                DataRelativePathRepairFileExecutionState
                    .InitialJournalWriteFailed,
                lockState:
                    lockResult.State,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    destinationInspection.State,
                initialJournalWrite:
                    initialWrite,
                error:
                    initialWrite.Error ??
                    initialWrite.State.ToString()
            );
        }

        LinuxFileIncarnationIdentity? initialJournalIncarnation =
                initialWrite.WrittenJournalIncarnationIdentity;

        if (
            initialJournalIncarnation is null ||
            !initialJournalIncarnation.Success)
        {
            return Result(
                DataRelativePathRepairFileExecutionState
                    .InitialJournalIdentityUnavailable,
                lockState:
                    lockResult.State,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    destinationInspection.State,
                initialJournalWrite:
                    initialWrite,
                error:
                    "The durable Intent journal did not return " +
                    "a usable written-journal identity."
            );
        }

        LinuxCreateUnnamedFileAtResult temporaryCreate =
            LinuxCreateUnnamedFileAt.Create(
                parent.OpenedPath
            );

        if (!temporaryCreate.Success)
        {
            return Result(
                DataRelativePathRepairFileExecutionState
                    .TemporaryFileCreateFailed,
                lockState:
                    lockResult.State,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    destinationInspection.State,
                initialJournalWrite:
                    initialWrite,
                temporaryFileCreate:
                    temporaryCreate,
                error:
                    temporaryCreate.Error ??
                    temporaryCreate.State.ToString()
            );
        }

        using LinuxUnnamedFileHandle temporary =
            temporaryCreate.OpenedFile!;

        /*
         * Copy directly from the validated source fd into the
         * anonymous destination fd.
         *
         * CopyAndVerify independently verifies the expected size
         * and SHA-256, so an in-place source mutation after lease
         * acquisition does not silently authorize different bytes.
         */
        LinuxCopyFileContentsResult copy =
            LinuxCopyFileContents.CopyAndVerify(
                source.OpenedPath,
                temporary,
                intent.SourceSnapshot.Size,
                intent.SourceSnapshot.Sha256
            );

        if (!copy.Success)
        {
            return Result(
                DataRelativePathRepairFileExecutionState
                    .CopyFailed,
                lockState:
                    lockResult.State,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    destinationInspection.State,
                initialJournalWrite:
                    initialWrite,
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
                DataRelativePathRepairFileExecutionState
                    .TemporaryFileSyncFailed,
                lockState:
                    lockResult.State,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    destinationInspection.State,
                initialJournalWrite:
                    initialWrite,
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
                DataRelativePathRepairFileExecutionState
                    .PreparedIdentityFailed,
                lockState:
                    lockResult.State,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    destinationInspection.State,
                initialJournalWrite:
                    initialWrite,
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
         * The Prepared journal stores the pre-publication
         * O_TMPFILE identity. MarkPrepared also validates that
         * this identity represents the still-unlinked file.
         */
        DataRelativePathRepairFileJournalTransitionResult
            preparedTransition =
                DataRelativePathRepairFileJournal
                    .MarkPrepared(
                        intent,
                        preparedIncarnation.Identity!,
                        nowUtc
                    );

        if (!preparedTransition.Success)
        {
            return Result(
                DataRelativePathRepairFileExecutionState
                    .PreparedTransitionFailed,
                lockState:
                    lockResult.State,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    destinationInspection.State,
                initialJournalWrite:
                    initialWrite,
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
                        initialJournalIncarnation,
                        preparedTransition.Record!
                    );

        if (!preparedWrite.Success)
        {
            return Result(
                DataRelativePathRepairFileExecutionState
                    .PreparedJournalWriteFailed,
                lockState:
                    lockResult.State,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    destinationInspection.State,
                initialJournalWrite:
                    initialWrite,
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
                DataRelativePathRepairFileExecutionState
                    .PreparedJournalIdentityUnavailable,
                lockState:
                    lockResult.State,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    destinationInspection.State,
                initialJournalWrite:
                    initialWrite,
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
                    "The durable Prepared journal did not return " +
                    "a usable written-journal identity."
            );
        }

        /*
         * Publication is one no-overwrite link operation from
         * the exact prepared anonymous fd into the exact validated
         * destination-parent fd.
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
                DataRelativePathRepairFileExecutionState
                    .PublicationFailed,
                lockState:
                    lockResult.State,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    destinationInspection.State,
                initialJournalWrite:
                    initialWrite,
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

        /*
         * The link is visible but must not be called durable
         * until the destination parent directory has synced.
         *
         * If this fails, Prepared remains the last durable
         * journal state and recovery can inspect the published
         * destination.
         */
        LinuxFsyncResult parentSync =
            LinuxFsync.Sync(
                parent.OpenedPath
            );

        if (!parentSync.Success)
        {
            return Result(
                DataRelativePathRepairFileExecutionState
                    .DestinationParentSyncFailed,
                lockState:
                    lockResult.State,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    destinationInspection.State,
                initialJournalWrite:
                    initialWrite,
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
                DataRelativePathRepairFileExecutionState
                    .AppliedTransitionFailed,
                lockState:
                    lockResult.State,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    destinationInspection.State,
                initialJournalWrite:
                    initialWrite,
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
            /*
             * The asset is already published and its parent
             * directory already synced.
             *
             * Prepared therefore remains a safe crash-recovery
             * checkpoint if this final journal replacement fails.
             */
            return Result(
                DataRelativePathRepairFileExecutionState
                    .AppliedJournalWriteFailed,
                lockState:
                    lockResult.State,
                sourceValidation:
                    sourceAcquisition.Validation,
                parentValidation:
                    parentAcquisition.Validation,
                destinationOpenState:
                    destinationInspection.State,
                initialJournalWrite:
                    initialWrite,
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
            DataRelativePathRepairFileExecutionState
                .AppliedDurably,
            lockState:
                lockResult.State,
            sourceValidation:
                sourceAcquisition.Validation,
            parentValidation:
                parentAcquisition.Validation,
            destinationOpenState:
                destinationInspection.State,
            initialJournalWrite:
                initialWrite,
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

    private static DataRelativePathRepairFileExecution Result(
        DataRelativePathRepairFileExecutionState state,
        LinuxExclusiveDirectoryLockState? lockState = null,
        DataRelativePathRepairSourceValidation?
            sourceValidation = null,
        DataRelativePathRepairDestinationParentValidation?
            parentValidation = null,
        LinuxOpenChildReadOnlyAtState?
            destinationOpenState = null,
        DataRelativePathRepairFileJournalWriterResult?
            initialJournalWrite = null,
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
        return new DataRelativePathRepairFileExecution(
            State:
                state,
            LockState:
                lockState,
            SourceValidation:
                sourceValidation,
            ParentValidation:
                parentValidation,
            DestinationOpenState:
                destinationOpenState,
            InitialJournalWrite:
                initialJournalWrite,
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
}
