using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public static class DataRelativePathRepairDirectoryJournalWriter
{
    public static
        DataRelativePathRepairDirectoryJournalWriterResult
        CreateInitial(
            LinuxNoFollowPathHandle journalDirectory,
            string journalChildName,
            DataRelativePathRepairDirectoryJournalRecord record)
    {
        ArgumentNullException.ThrowIfNull(
            journalDirectory
        );

        ArgumentNullException.ThrowIfNull(
            record
        );

        string? validationError =
            DataRelativePathRepairDirectoryJournal.Validate(
                record
            );

        if (validationError is not null)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalWriteState
                    .InvalidRecord,
                journalChildName,
                error:
                    validationError
            );
        }

        if (!IsValidChildName(journalChildName))
        {
            return Result(
                DataRelativePathRepairDirectoryJournalWriteState
                    .InvalidJournalName,
                journalChildName,
                error:
                    "The journal name must identify exactly " +
                    "one direct child."
            );
        }

        if (
            record.Revision != 0 ||
            record.State !=
            DataRelativePathRepairDirectoryJournalState
                .IntentRecorded)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalWriteState
                    .InvalidInitialRevision,
                journalChildName,
                error:
                    "Initial journal creation requires revision " +
                    "zero in IntentRecorded state."
            );
        }

        byte[] bytes;

        try
        {
            bytes =
                DataRelativePathRepairDirectoryJournalJson
                    .Serialize(
                        record
                    );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalWriteState
                    .SerializationFailed,
                journalChildName,
                error:
                    ex.Message
            );
        }

        LinuxCreateUnnamedFileAtResult create =
            LinuxCreateUnnamedFileAt.Create(
                journalDirectory
            );

        if (!create.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalWriteState
                    .TemporaryFileCreateFailed,
                journalChildName,
                error:
                    create.Error ??
                    create.State.ToString()
            );
        }

        using LinuxUnnamedFileHandle temporary =
            create.OpenedFile!;

        string? writeError =
            WriteExactBytes(
                temporary,
                bytes
            );

        if (writeError is not null)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalWriteState
                    .WriteFailed,
                journalChildName,
                error:
                    writeError
            );
        }

        LinuxFsyncResult fileSync =
            LinuxFsync.Sync(
                temporary
            );

        if (!fileSync.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalWriteState
                    .TemporaryFileSyncFailed,
                journalChildName,
                error:
                    fileSync.Error ??
                    fileSync.State.ToString()
            );
        }

        LinuxPublishUnnamedFileAtResult publish =
            LinuxPublishUnnamedFileAt.Publish(
                temporary,
                journalDirectory,
                journalChildName
            );

        if (!publish.Success)
        {
            return Result(
                publish.State ==
                LinuxPublishUnnamedFileAtState
                    .DestinationExists
                    ? DataRelativePathRepairDirectoryJournalWriteState
                        .JournalAlreadyExists
                    : DataRelativePathRepairDirectoryJournalWriteState
                        .InitialPublishFailed,
                journalChildName,
                error:
                    publish.Error ??
                    publish.State.ToString()
            );
        }

        LinuxOpenedFileIncarnationResult writtenIncarnation =
            LinuxOpenedFileIncarnation.Capture(
                temporary
            );

        LinuxFsyncResult directorySync =
            LinuxFsync.Sync(
                journalDirectory
            );

        if (!directorySync.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalWriteState
                    .DirectorySyncFailed,
                journalChildName,
                writtenJournalIncarnation:
                    writtenIncarnation.Success
                        ? writtenIncarnation
                        : null,
                journalEntryChanged:
                    true,
                error:
                    directorySync.Error ??
                    directorySync.State.ToString()
            );
        }

        if (!writtenIncarnation.Success)
        {
            /*
             * The directory entry is already durable here.
             *
             * Failure to re-capture the identity must not be
             * reported as if the journal creation failed.
             * The caller can reopen the journal entry and
             * recover its identity before the next update.
             */
            return new
                DataRelativePathRepairDirectoryJournalWriterResult(
                    State:
                        DataRelativePathRepairDirectoryJournalWriteState
                            .CreatedDurably,
                    JournalChildName:
                        journalChildName,
                    StagingChildName:
                        null,
                    WrittenJournalIncarnation:
                        null,
                    JournalEntryChanged:
                        true,
                    StagingEntryMayRemain:
                        false,
                    Error:
                        "The journal was durably created, but " +
                        "its post-publication incarnation could not " +
                        "be captured."
                );
        }

        return Result(
            DataRelativePathRepairDirectoryJournalWriteState
                .CreatedDurably,
            journalChildName,
            writtenJournalIncarnation:
                writtenIncarnation,
            journalEntryChanged:
                true
        );
    }

    public static
        DataRelativePathRepairDirectoryJournalWriterResult
        ReplaceExisting(
            LinuxNoFollowPathHandle journalDirectory,
            string journalChildName,
            LinuxFileIncarnationIdentity
                expectedCurrentJournalIncarnation,
            DataRelativePathRepairDirectoryJournalRecord record)
    {
        ArgumentNullException.ThrowIfNull(
            journalDirectory
        );

        ArgumentNullException.ThrowIfNull(
            expectedCurrentJournalIncarnation
        );

        ArgumentNullException.ThrowIfNull(
            record
        );

        string? validationError =
            DataRelativePathRepairDirectoryJournal.Validate(
                record
            );

        if (validationError is not null)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalWriteState
                    .InvalidRecord,
                journalChildName,
                error:
                    validationError
            );
        }

        if (!IsValidChildName(journalChildName))
        {
            return Result(
                DataRelativePathRepairDirectoryJournalWriteState
                    .InvalidJournalName,
                journalChildName,
                error:
                    "The journal name must identify exactly " +
                    "one direct child."
            );
        }

        if (record.Revision <= 0)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalWriteState
                    .InvalidReplacementRevision,
                journalChildName,
                error:
                    "Journal replacement requires a revision " +
                    "greater than zero."
            );
        }

        if (!expectedCurrentJournalIncarnation.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalWriteState
                    .InvalidExpectedCurrentIdentity,
                journalChildName,
                error:
                    "Journal replacement requires the captured " +
                    "identity of the current journal file."
            );
        }

        LinuxOpenChildReadOnlyAtResult currentOpen =
            LinuxOpenChildReadOnlyAt.Open(
                journalDirectory,
                journalChildName
            );

        if (!currentOpen.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalWriteState
                    .CurrentJournalOpenFailed,
                journalChildName,
                error:
                    currentOpen.Error ??
                    currentOpen.State.ToString()
            );
        }

        using LinuxOpenedChildHandle current =
            currentOpen.OpenedChild!;

        LinuxOpenedFileIncarnationResult actualCurrentIncarnation =
            LinuxOpenedFileIncarnation.Capture(
                current
            );

        if (!actualCurrentIncarnation.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalWriteState
                    .CurrentJournalIdentityFailed,
                journalChildName,
                error:
                    actualCurrentIncarnation.Error ??
                    actualCurrentIncarnation.State.ToString()
            );
        }

        if (
            !expectedCurrentJournalIncarnation.SameIncarnationAs(
                actualCurrentIncarnation.Identity!
            ))
        {
            return Result(
                DataRelativePathRepairDirectoryJournalWriteState
                    .CurrentJournalIdentityMismatch,
                journalChildName,
                error:
                    "The current journal entry is no longer " +
                    "the inode the caller previously verified."
            );
        }

        byte[] bytes;

        try
        {
            bytes =
                DataRelativePathRepairDirectoryJournalJson
                    .Serialize(
                        record
                    );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalWriteState
                    .SerializationFailed,
                journalChildName,
                error:
                    ex.Message
            );
        }

        LinuxCreateUnnamedFileAtResult create =
            LinuxCreateUnnamedFileAt.Create(
                journalDirectory
            );

        if (!create.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalWriteState
                    .TemporaryFileCreateFailed,
                journalChildName,
                error:
                    create.Error ??
                    create.State.ToString()
            );
        }

        using LinuxUnnamedFileHandle temporary =
            create.OpenedFile!;

        string? writeError =
            WriteExactBytes(
                temporary,
                bytes
            );

        if (writeError is not null)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalWriteState
                    .WriteFailed,
                journalChildName,
                error:
                    writeError
            );
        }

        LinuxFsyncResult fileSync =
            LinuxFsync.Sync(
                temporary
            );

        if (!fileSync.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalWriteState
                    .TemporaryFileSyncFailed,
                journalChildName,
                error:
                    fileSync.Error ??
                    fileSync.State.ToString()
            );
        }

        LinuxOpenedFileIdentityResult temporaryIdentity =
            LinuxOpenedFileIdentity.Capture(
                temporary
            );

        if (!temporaryIdentity.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalWriteState
                    .CurrentJournalIdentityFailed,
                journalChildName,
                error:
                    "The prepared journal revision identity " +
                    "could not be captured."
            );
        }

        string stagingName =
            CreateStagingName(
                record
            );

        LinuxPublishUnnamedFileAtResult stagePublish =
            LinuxPublishUnnamedFileAt.Publish(
                temporary,
                journalDirectory,
                stagingName
            );

        if (!stagePublish.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalWriteState
                    .StagingPublishFailed,
                journalChildName,
                stagingChildName:
                    stagingName,
                error:
                    stagePublish.Error ??
                    stagePublish.State.ToString()
            );
        }

        LinuxOpenedFileIncarnationResult stagedIncarnation =
            LinuxOpenedFileIncarnation.Capture(
                temporary
            );

        if (!stagedIncarnation.Success)
        {
            bool cleaned =
                CleanupStaging(
                    journalDirectory,
                    stagingName,
                    temporary
                );

            return Result(
                cleaned
                    ? DataRelativePathRepairDirectoryJournalWriteState
                        .ReplacementFailed
                    : DataRelativePathRepairDirectoryJournalWriteState
                        .StagingCleanupFailed,
                journalChildName,
                stagingChildName:
                    stagingName,
                stagingEntryMayRemain:
                    !cleaned,
                error:
                    "The staged journal incarnation could not be " +
                    "captured after publication."
            );
        }

        LinuxReplaceOwnedFileAtResult replace =
            LinuxReplaceOwnedFileAt.Replace(
                journalDirectory,
                stagingName,
                journalChildName,
                stagedIncarnation.Identity!,
                expectedCurrentJournalIncarnation
            );

        if (!replace.Success)
        {
            bool cleaned =
                CleanupStaging(
                    journalDirectory,
                    stagingName,
                    temporary
                );

            return Result(
                cleaned
                    ? DataRelativePathRepairDirectoryJournalWriteState
                        .ReplacementFailed
                    : DataRelativePathRepairDirectoryJournalWriteState
                        .StagingCleanupFailed,
                journalChildName,
                stagingChildName:
                    stagingName,
                stagingEntryMayRemain:
                    !cleaned,
                error:
                    replace.Error ??
                    replace.State.ToString()
            );
        }

        LinuxOpenedFileIncarnationResult writtenIncarnation =
            LinuxOpenedFileIncarnation.Capture(
                temporary
            );

        LinuxFsyncResult directorySync =
            LinuxFsync.Sync(
                journalDirectory
            );

        if (!directorySync.Success)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalWriteState
                    .DirectorySyncFailed,
                journalChildName,
                writtenJournalIncarnation:
                    writtenIncarnation.Success
                        ? writtenIncarnation
                        : null,
                journalEntryChanged:
                    true,
                error:
                    directorySync.Error ??
                    directorySync.State.ToString()
            );
        }

        if (!writtenIncarnation.Success)
        {
            return new
                DataRelativePathRepairDirectoryJournalWriterResult(
                    State:
                        DataRelativePathRepairDirectoryJournalWriteState
                            .ReplacedDurably,
                    JournalChildName:
                        journalChildName,
                    StagingChildName:
                        null,
                    WrittenJournalIncarnation:
                        null,
                    JournalEntryChanged:
                        true,
                    StagingEntryMayRemain:
                        false,
                    Error:
                        "The journal was durably replaced, but " +
                        "its post-replacement incarnation could not " +
                        "be captured."
                );
        }

        return Result(
            DataRelativePathRepairDirectoryJournalWriteState
                .ReplacedDurably,
            journalChildName,
            writtenJournalIncarnation:
                writtenIncarnation,
            journalEntryChanged:
                true
        );
    }

    private static string? WriteExactBytes(
        ILinuxOpenedHandle destination,
        byte[] bytes)
    {
        try
        {
            if (
                RandomAccess.GetLength(
                    destination.Handle
                ) != 0)
            {
                return
                    "The prepared journal file was not empty.";
            }

            RandomAccess.Write(
                destination.Handle,
                bytes,
                fileOffset:
                    0
            );

            long actualLength =
                RandomAccess.GetLength(
                    destination.Handle
                );

            if (actualLength != bytes.LongLength)
            {
                return
                    $"Journal write length mismatch. Expected " +
                    $"{bytes.LongLength}, actual {actualLength}.";
            }

            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static bool CleanupStaging(
        LinuxNoFollowPathHandle journalDirectory,
        string stagingName,
        ILinuxOpenedHandle stagingFile)
    {
        LinuxOpenedFileIncarnationResult incarnation =
            LinuxOpenedFileIncarnation.Capture(
                stagingFile
            );

        /*
         * Cleanup is destructive. If generation-aware authority
         * cannot be captured from the retained staging descriptor,
         * leave the namespace entry in place rather than fall back
         * to weak identity.
         */
        if (!incarnation.Success)
        {
            return false;
        }

        LinuxRemoveOwnedFileAtResult remove =
            LinuxRemoveOwnedFileAt.Remove(
                journalDirectory,
                stagingName,
                incarnation.Identity!
            );

        if (
            !remove.Success &&
            remove.State !=
            LinuxRemoveOwnedFileAtState
                .ChildUnavailable)
        {
            return false;
        }

        if (remove.Success)
        {
            LinuxFsyncResult sync =
                LinuxFsync.Sync(
                    journalDirectory
                );

            if (!sync.Success)
            {
                return false;
            }
        }

        return true;
    }

    private static string CreateStagingName(
        DataRelativePathRepairDirectoryJournalRecord record)
    {
        return
            $".casecompat-journal-" +
            $"{record.JournalId:N}-" +
            $"r{record.Revision}-" +
            $"{Guid.NewGuid():N}.tmp";
    }

    private static bool IsValidChildName(
        string? childName)
    {
        if (
            string.IsNullOrEmpty(
                childName
            ) ||
            childName is "." or "..")
        {
            return false;
        }

        return
            !childName.Contains('/') &&
            !childName.Contains('\\') &&
            !childName.Contains('\0');
    }

    private static
        DataRelativePathRepairDirectoryJournalWriterResult
        Result(
            DataRelativePathRepairDirectoryJournalWriteState state,
            string? journalChildName,
            string? stagingChildName = null,
            LinuxOpenedFileIncarnationResult?
                writtenJournalIncarnation = null,
            bool journalEntryChanged = false,
            bool stagingEntryMayRemain = false,
            string? error = null)
    {
        return new
            DataRelativePathRepairDirectoryJournalWriterResult(
                State:
                    state,
                JournalChildName:
                    journalChildName ?? string.Empty,
                StagingChildName:
                    stagingChildName,
                WrittenJournalIncarnation:
                    writtenJournalIncarnation,
                JournalEntryChanged:
                    journalEntryChanged,
                StagingEntryMayRemain:
                    stagingEntryMayRemain,
                Error:
                    error
            );
    }
}
