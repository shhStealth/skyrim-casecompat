using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairBatchApplyAuthorizationWriteState
{
    CreatedDurably,

    InvalidAuthorization,
    InvalidAuthorizationName,
    SerializationFailed,
    AuthorizationTooLarge,

    TemporaryFileCreateFailed,
    WriteFailed,
    TemporaryFileSyncFailed,

    AuthorizationAlreadyExists,
    InitialPublishFailed,

    DirectorySyncFailed
}

public sealed record DataRelativePathRepairBatchApplyAuthorizationWriterResult(
    DataRelativePathRepairBatchApplyAuthorizationWriteState State,
    string AuthorizationChildName,
    LinuxOpenedFileIncarnationResult?
        WrittenAuthorizationIncarnation,
    bool AuthorizationEntryChanged,
    string? Error
)
{
    public bool Success =>
        State ==
        DataRelativePathRepairBatchApplyAuthorizationWriteState
            .CreatedDurably;

    public LinuxFileIncarnationIdentity?
        WrittenAuthorizationIncarnationIdentity =>
            WrittenAuthorizationIncarnation?.Identity;
}

public static class DataRelativePathRepairBatchApplyAuthorizationWriter
{
    public static DataRelativePathRepairBatchApplyAuthorizationWriterResult
        CreateInitial(
            LinuxNoFollowPathHandle authorizationDirectory,
            string authorizationChildName,
            DataRelativePathRepairBatchApplyAuthorizationRecord authorization)
    {
        ArgumentNullException.ThrowIfNull(
            authorizationDirectory
        );

        ArgumentNullException.ThrowIfNull(
            authorization
        );

        string? validationError =
            DataRelativePathRepairBatchApplyAuthorization.Validate(
                authorization
            );

        if (validationError is not null)
        {
            return Result(
                DataRelativePathRepairBatchApplyAuthorizationWriteState
                    .InvalidAuthorization,
                authorizationChildName,
                error:
                    validationError
            );
        }

        if (
            !IsValidChildName(
                authorizationChildName))
        {
            return Result(
                DataRelativePathRepairBatchApplyAuthorizationWriteState
                    .InvalidAuthorizationName,
                authorizationChildName,
                error:
                    "The authorization name must identify exactly one " +
                    "direct child."
            );
        }

        byte[] bytes;

        try
        {
            bytes =
                DataRelativePathRepairBatchApplyAuthorizationJson
                    .Serialize(
                        authorization
                    );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathRepairBatchApplyAuthorizationWriteState
                    .SerializationFailed,
                authorizationChildName,
                error:
                    ex.Message
            );
        }

        /*
         * A successful writer invocation must never durably publish a
         * representation that the batch-authorization reader will reject
         * solely because of its size.
         *
         * Enforce the reader's persisted-byte bound before creating even
         * an unnamed temporary inode, so AuthorizationTooLarge is unambiguously
         * a pre-publication result.
         */
        if (
            bytes.LongLength >
            DataRelativePathRepairBatchApplyAuthorizationReader
                .MaxAuthorizationBytes)
        {
            return Result(
                DataRelativePathRepairBatchApplyAuthorizationWriteState
                    .AuthorizationTooLarge,
                authorizationChildName,
                error:
                    $"Serialized batch-authorization length " +
                    $"{bytes.LongLength} exceeds the supported limit of " +
                    $"{DataRelativePathRepairBatchApplyAuthorizationReader.MaxAuthorizationBytes} " +
                    "bytes."
            );
        }

        LinuxCreateUnnamedFileAtResult create =
            LinuxCreateUnnamedFileAt.Create(
                authorizationDirectory
            );

        if (!create.Success)
        {
            return Result(
                DataRelativePathRepairBatchApplyAuthorizationWriteState
                    .TemporaryFileCreateFailed,
                authorizationChildName,
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
                DataRelativePathRepairBatchApplyAuthorizationWriteState
                    .WriteFailed,
                authorizationChildName,
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
                DataRelativePathRepairBatchApplyAuthorizationWriteState
                    .TemporaryFileSyncFailed,
                authorizationChildName,
                error:
                    fileSync.Error ??
                    fileSync.State.ToString()
            );
        }

        /*
         * Publication is deliberately one-shot and no-overwrite.
         *
         * An existing batch authorization is never adopted or replaced.
         */
        LinuxPublishUnnamedFileAtResult publish =
            LinuxPublishUnnamedFileAt.Publish(
                temporary,
                authorizationDirectory,
                authorizationChildName
            );

        if (!publish.Success)
        {
            return Result(
                publish.State ==
                LinuxPublishUnnamedFileAtState
                    .DestinationExists
                    ? DataRelativePathRepairBatchApplyAuthorizationWriteState
                        .AuthorizationAlreadyExists
                    : DataRelativePathRepairBatchApplyAuthorizationWriteState
                        .InitialPublishFailed,
                authorizationChildName,
                error:
                    publish.Error ??
                    publish.State.ToString()
            );
        }

        /*
         * The unnamed descriptor still refers to the exact inode that
         * has just been linked into the batch-directory namespace.
         */
        LinuxOpenedFileIncarnationResult writtenIncarnation =
            LinuxOpenedFileIncarnation.Capture(
                temporary
            );

        /*
         * Make the new batch-authorization directory entry durable before
         * reporting successful durable creation.
         */
        LinuxFsyncResult directorySync =
            LinuxFsync.Sync(
                authorizationDirectory
            );

        if (!directorySync.Success)
        {
            /*
             * Publication has already succeeded. The named entry may now
             * exist even though its directory-entry durability could not
             * be established, so this must remain visibly distinct from
             * every pre-publication failure.
             */
            return Result(
                DataRelativePathRepairBatchApplyAuthorizationWriteState
                    .DirectorySyncFailed,
                authorizationChildName,
                writtenAuthorizationIncarnation:
                    writtenIncarnation.Success
                        ? writtenIncarnation
                        : null,
                authorizationEntryChanged:
                    true,
                error:
                    directorySync.Error ??
                    directorySync.State.ToString()
            );
        }

        /*
         * The directory entry is durable at this point.
         *
         * Failure to capture the post-publication incarnation must not
         * be reported as though durable batch-authorization creation failed.
         * A later descriptor-backed reader can reopen the immutable
         * batch authorization.
         */
        if (!writtenIncarnation.Success)
        {
            return new(
                State:
                    DataRelativePathRepairBatchApplyAuthorizationWriteState
                        .CreatedDurably,
                AuthorizationChildName:
                    authorizationChildName,
                WrittenAuthorizationIncarnation:
                    null,
                AuthorizationEntryChanged:
                    true,
                Error:
                    "The batch authorization was durably created, but its " +
                    "post-publication incarnation could not be captured."
            );
        }

        return Result(
            DataRelativePathRepairBatchApplyAuthorizationWriteState
                .CreatedDurably,
            authorizationChildName,
            writtenAuthorizationIncarnation:
                writtenIncarnation,
            authorizationEntryChanged:
                true
        );
    }

    private static string? WriteExactBytes(
        LinuxUnnamedFileHandle file,
        byte[] bytes)
    {
        try
        {
            /*
             * RandomAccess.Write either writes the supplied span or
             * throws. The anonymous inode has length zero and has not
             * been exposed in the filesystem namespace yet.
             */
            RandomAccess.Write(
                file.Handle,
                bytes,
                fileOffset:
                    0
            );

            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
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
        DataRelativePathRepairBatchApplyAuthorizationWriterResult
        Result(
            DataRelativePathRepairBatchApplyAuthorizationWriteState state,
            string? authorizationChildName,
            LinuxOpenedFileIncarnationResult?
                writtenAuthorizationIncarnation = null,
            bool authorizationEntryChanged = false,
            string? error = null)
    {
        return new(
            State:
                state,
            AuthorizationChildName:
                authorizationChildName ??
                string.Empty,
            WrittenAuthorizationIncarnation:
                writtenAuthorizationIncarnation,
            AuthorizationEntryChanged:
                authorizationEntryChanged,
            Error:
                error
        );
    }
}
