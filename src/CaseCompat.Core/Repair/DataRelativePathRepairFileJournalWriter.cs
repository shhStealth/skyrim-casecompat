using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public static class DataRelativePathRepairFileJournalWriter
{
    public static
        DataRelativePathRepairFileJournalWriterResult
        CreateInitial(
            LinuxNoFollowPathHandle journalDirectory,
            string journalChildName,
            DataRelativePathRepairFileJournalRecord record)
    {
        ArgumentNullException.ThrowIfNull(
            journalDirectory
        );

        ArgumentNullException.ThrowIfNull(
            record
        );

        string? validationError =
            DataRelativePathRepairFileJournal.Validate(
                record
            );

        if (validationError is not null)
        {
            return Result(
                DataRelativePathRepairFileJournalWriteState
                    .InvalidRecord,
                journalChildName,
                error:
                    validationError
            );
        }

        if (!IsValidChildName(journalChildName))
        {
            return Result(
                DataRelativePathRepairFileJournalWriteState
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
            DataRelativePathRepairFileJournalState
                .IntentRecorded)
        {
            return Result(
                DataRelativePathRepairFileJournalWriteState
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
                DataRelativePathRepairFileJournalJson
                    .Serialize(
                        record
                    );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathRepairFileJournalWriteState
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
                DataRelativePathRepairFileJournalWriteState
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
                DataRelativePathRepairFileJournalWriteState
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
                DataRelativePathRepairFileJournalWriteState
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
                    ? DataRelativePathRepairFileJournalWriteState
                        .JournalAlreadyExists
                    : DataRelativePathRepairFileJournalWriteState
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
                DataRelativePathRepairFileJournalWriteState
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
                DataRelativePathRepairFileJournalWriterResult(
                    State:
                        DataRelativePathRepairFileJournalWriteState
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
            DataRelativePathRepairFileJournalWriteState
                .CreatedDurably,
            journalChildName,
            writtenJournalIncarnation:
                writtenIncarnation,
            journalEntryChanged:
                true
        );
    }

    public static
        DataRelativePathRepairFileJournalWriterResult
        ReplaceExisting(
            LinuxNoFollowPathHandle journalDirectory,
            string journalChildName,
            LinuxFileIncarnationIdentity
                expectedCurrentJournalIncarnation,
            DataRelativePathRepairFileJournalRecord record)
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
            DataRelativePathRepairFileJournal.Validate(
                record
            );

        if (validationError is not null)
        {
            return Result(
                DataRelativePathRepairFileJournalWriteState
                    .InvalidRecord,
                journalChildName,
                error:
                    validationError
            );
        }

        if (!IsValidChildName(journalChildName))
        {
            return Result(
                DataRelativePathRepairFileJournalWriteState
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
                DataRelativePathRepairFileJournalWriteState
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
                DataRelativePathRepairFileJournalWriteState
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
                DataRelativePathRepairFileJournalWriteState
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
                DataRelativePathRepairFileJournalWriteState
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
                DataRelativePathRepairFileJournalWriteState
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
                DataRelativePathRepairFileJournalJson
                    .Serialize(
                        record
                    );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathRepairFileJournalWriteState
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
                DataRelativePathRepairFileJournalWriteState
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
                DataRelativePathRepairFileJournalWriteState
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
                DataRelativePathRepairFileJournalWriteState
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
                DataRelativePathRepairFileJournalWriteState
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
                DataRelativePathRepairFileJournalWriteState
                    .StagingPublishFailed,
                journalChildName,
                stagingChildName:
                    stagingName,
                error:
                    stagePublish.Error ??
                    stagePublish.State.ToString()
            );
        }

        LinuxOpenedFileIdentityResult stagedIdentity =
            LinuxOpenedFileIdentity.Capture(
                temporary
            );

        if (!stagedIdentity.Success)
        {
            bool cleaned =
                CleanupStaging(
                    journalDirectory,
                    stagingName,
                    temporary
                );

            return Result(
                cleaned
                    ? DataRelativePathRepairFileJournalWriteState
                        .ReplacementFailed
                    : DataRelativePathRepairFileJournalWriteState
                        .StagingCleanupFailed,
                journalChildName,
                stagingChildName:
                    stagingName,
                stagingEntryMayRemain:
                    !cleaned,
                error:
                    "The staged journal identity could not be " +
                    "captured after publication."
            );
        }

        LinuxReplaceOwnedFileAtResult replace =
            LinuxReplaceOwnedFileAt.Replace(
                journalDirectory,
                stagingName,
                journalChildName,
                stagedIdentity,
                expectedCurrentJournalIncarnation
                    .PhysicalIdentity
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
                    ? DataRelativePathRepairFileJournalWriteState
                        .ReplacementFailed
                    : DataRelativePathRepairFileJournalWriteState
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
                DataRelativePathRepairFileJournalWriteState
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
                DataRelativePathRepairFileJournalWriterResult(
                    State:
                        DataRelativePathRepairFileJournalWriteState
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
            DataRelativePathRepairFileJournalWriteState
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
        DataRelativePathRepairFileJournalRecord record)
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
        DataRelativePathRepairFileJournalWriterResult
        Result(
            DataRelativePathRepairFileJournalWriteState state,
            string? journalChildName,
            string? stagingChildName = null,
            LinuxOpenedFileIncarnationResult?
                writtenJournalIncarnation = null,
            bool journalEntryChanged = false,
            bool stagingEntryMayRemain = false,
            string? error = null)
    {
        return new
            DataRelativePathRepairFileJournalWriterResult(
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
