using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairBatchManifestWriteState
{
    CreatedDurably,

    InvalidManifest,
    InvalidManifestName,
    SerializationFailed,
    ManifestTooLarge,

    TemporaryFileCreateFailed,
    WriteFailed,
    TemporaryFileSyncFailed,

    ManifestAlreadyExists,
    InitialPublishFailed,

    DirectorySyncFailed
}

public sealed record DataRelativePathRepairBatchManifestWriterResult(
    DataRelativePathRepairBatchManifestWriteState State,
    string ManifestChildName,
    LinuxOpenedFileIncarnationResult?
        WrittenManifestIncarnation,
    bool ManifestEntryChanged,
    string? Error
)
{
    public bool Success =>
        State ==
        DataRelativePathRepairBatchManifestWriteState
            .CreatedDurably;

    public LinuxFileIncarnationIdentity?
        WrittenManifestIncarnationIdentity =>
            WrittenManifestIncarnation?.Identity;
}

public static class DataRelativePathRepairBatchManifestWriter
{
    public static DataRelativePathRepairBatchManifestWriterResult
        CreateInitial(
            LinuxNoFollowPathHandle manifestDirectory,
            string manifestChildName,
            DataRelativePathRepairBatchManifestRecord manifest)
    {
        ArgumentNullException.ThrowIfNull(
            manifestDirectory
        );

        ArgumentNullException.ThrowIfNull(
            manifest
        );

        string? validationError =
            DataRelativePathRepairBatchManifest.Validate(
                manifest
            );

        if (validationError is not null)
        {
            return Result(
                DataRelativePathRepairBatchManifestWriteState
                    .InvalidManifest,
                manifestChildName,
                error:
                    validationError
            );
        }

        if (
            !IsValidChildName(
                manifestChildName))
        {
            return Result(
                DataRelativePathRepairBatchManifestWriteState
                    .InvalidManifestName,
                manifestChildName,
                error:
                    "The manifest name must identify exactly one " +
                    "direct child."
            );
        }

        byte[] bytes;

        try
        {
            bytes =
                DataRelativePathRepairBatchManifestJson
                    .Serialize(
                        manifest
                    );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathRepairBatchManifestWriteState
                    .SerializationFailed,
                manifestChildName,
                error:
                    ex.Message
            );
        }

        /*
         * A successful writer invocation must never durably publish a
         * representation that the batch-manifest reader will reject
         * solely because of its size.
         *
         * Enforce the reader's persisted-byte bound before creating even
         * an unnamed temporary inode, so ManifestTooLarge is unambiguously
         * a pre-publication result.
         */
        if (
            bytes.LongLength >
            DataRelativePathRepairBatchManifestReader
                .MaxManifestBytes)
        {
            return Result(
                DataRelativePathRepairBatchManifestWriteState
                    .ManifestTooLarge,
                manifestChildName,
                error:
                    $"Serialized batch-manifest length " +
                    $"{bytes.LongLength} exceeds the supported limit of " +
                    $"{DataRelativePathRepairBatchManifestReader.MaxManifestBytes} " +
                    "bytes."
            );
        }

        LinuxCreateUnnamedFileAtResult create =
            LinuxCreateUnnamedFileAt.Create(
                manifestDirectory
            );

        if (!create.Success)
        {
            return Result(
                DataRelativePathRepairBatchManifestWriteState
                    .TemporaryFileCreateFailed,
                manifestChildName,
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
                DataRelativePathRepairBatchManifestWriteState
                    .WriteFailed,
                manifestChildName,
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
                DataRelativePathRepairBatchManifestWriteState
                    .TemporaryFileSyncFailed,
                manifestChildName,
                error:
                    fileSync.Error ??
                    fileSync.State.ToString()
            );
        }

        /*
         * Publication is deliberately one-shot and no-overwrite.
         *
         * An existing batch manifest is never adopted or replaced.
         */
        LinuxPublishUnnamedFileAtResult publish =
            LinuxPublishUnnamedFileAt.Publish(
                temporary,
                manifestDirectory,
                manifestChildName
            );

        if (!publish.Success)
        {
            return Result(
                publish.State ==
                LinuxPublishUnnamedFileAtState
                    .DestinationExists
                    ? DataRelativePathRepairBatchManifestWriteState
                        .ManifestAlreadyExists
                    : DataRelativePathRepairBatchManifestWriteState
                        .InitialPublishFailed,
                manifestChildName,
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
         * Make the new batch-manifest directory entry durable before
         * reporting successful durable creation.
         */
        LinuxFsyncResult directorySync =
            LinuxFsync.Sync(
                manifestDirectory
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
                DataRelativePathRepairBatchManifestWriteState
                    .DirectorySyncFailed,
                manifestChildName,
                writtenManifestIncarnation:
                    writtenIncarnation.Success
                        ? writtenIncarnation
                        : null,
                manifestEntryChanged:
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
         * be reported as though durable batch-manifest creation failed.
         * A later descriptor-backed reader can reopen the immutable
         * batch manifest.
         */
        if (!writtenIncarnation.Success)
        {
            return new(
                State:
                    DataRelativePathRepairBatchManifestWriteState
                        .CreatedDurably,
                ManifestChildName:
                    manifestChildName,
                WrittenManifestIncarnation:
                    null,
                ManifestEntryChanged:
                    true,
                Error:
                    "The batch manifest was durably created, but its " +
                    "post-publication incarnation could not be captured."
            );
        }

        return Result(
            DataRelativePathRepairBatchManifestWriteState
                .CreatedDurably,
            manifestChildName,
            writtenManifestIncarnation:
                writtenIncarnation,
            manifestEntryChanged:
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
        DataRelativePathRepairBatchManifestWriterResult
        Result(
            DataRelativePathRepairBatchManifestWriteState state,
            string? manifestChildName,
            LinuxOpenedFileIncarnationResult?
                writtenManifestIncarnation = null,
            bool manifestEntryChanged = false,
            string? error = null)
    {
        return new(
            State:
                state,
            ManifestChildName:
                manifestChildName ??
                string.Empty,
            WrittenManifestIncarnation:
                writtenManifestIncarnation,
            ManifestEntryChanged:
                manifestEntryChanged,
            Error:
                error
        );
    }
}
