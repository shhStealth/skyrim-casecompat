using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathAggregateNamespaceManifestWriteState
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

public sealed record DataRelativePathAggregateNamespaceManifestWriterResult(
    DataRelativePathAggregateNamespaceManifestWriteState State,
    string ManifestChildName,
    LinuxOpenedFileIncarnationResult?
        WrittenManifestIncarnation,
    bool ManifestEntryChanged,
    string? Error
)
{
    public bool Success =>
        State ==
        DataRelativePathAggregateNamespaceManifestWriteState
            .CreatedDurably;

    public LinuxFileIncarnationIdentity?
        WrittenManifestIncarnationIdentity =>
            WrittenManifestIncarnation?.Identity;
}

public static class DataRelativePathAggregateNamespaceManifestWriter
{
    public static DataRelativePathAggregateNamespaceManifestWriterResult
        CreateInitial(
            LinuxNoFollowPathHandle manifestDirectory,
            string manifestChildName,
            DataRelativePathAggregateNamespaceManifestRecord manifest)
    {
        ArgumentNullException.ThrowIfNull(
            manifestDirectory
        );

        ArgumentNullException.ThrowIfNull(
            manifest
        );

        string? validationError =
            DataRelativePathAggregateNamespaceManifest.Validate(
                manifest
            );

        if (validationError is not null)
        {
            return Result(
                DataRelativePathAggregateNamespaceManifestWriteState
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
                DataRelativePathAggregateNamespaceManifestWriteState
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
                DataRelativePathAggregateNamespaceManifestJson
                    .Serialize(
                        manifest
                    );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathAggregateNamespaceManifestWriteState
                    .SerializationFailed,
                manifestChildName,
                error:
                    ex.Message
            );
        }

        /*
         * A successful writer invocation must never durably publish a
         * representation that the aggregate-namespace manifest reader will reject
         * solely because of its size.
         *
         * Enforce the reader's persisted-byte bound before creating even
         * an unnamed temporary inode, so ManifestTooLarge is unambiguously
         * a pre-publication result.
         */
        if (
            bytes.LongLength >
            DataRelativePathAggregateNamespaceManifestReader
                .MaxManifestBytes)
        {
            return Result(
                DataRelativePathAggregateNamespaceManifestWriteState
                    .ManifestTooLarge,
                manifestChildName,
                error:
                    $"Serialized aggregate-namespace manifest length " +
                    $"{bytes.LongLength} exceeds the supported limit of " +
                    $"{DataRelativePathAggregateNamespaceManifestReader.MaxManifestBytes} " +
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
                DataRelativePathAggregateNamespaceManifestWriteState
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
                DataRelativePathAggregateNamespaceManifestWriteState
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
                DataRelativePathAggregateNamespaceManifestWriteState
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
         * An existing aggregate namespace manifest is never adopted or replaced.
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
                    ? DataRelativePathAggregateNamespaceManifestWriteState
                        .ManifestAlreadyExists
                    : DataRelativePathAggregateNamespaceManifestWriteState
                        .InitialPublishFailed,
                manifestChildName,
                error:
                    publish.Error ??
                    publish.State.ToString()
            );
        }

        /*
         * The unnamed descriptor still refers to the exact inode that
         * has just been linked into the manifest-directory namespace.
         */
        LinuxOpenedFileIncarnationResult writtenIncarnation =
            LinuxOpenedFileIncarnation.Capture(
                temporary
            );

        /*
         * Make the new aggregate-namespace manifest directory entry durable before
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
                DataRelativePathAggregateNamespaceManifestWriteState
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
         * be reported as though durable aggregate-namespace manifest creation failed.
         * A later descriptor-backed reader can reopen the immutable
         * aggregate namespace manifest.
         */
        if (!writtenIncarnation.Success)
        {
            return new(
                State:
                    DataRelativePathAggregateNamespaceManifestWriteState
                        .CreatedDurably,
                ManifestChildName:
                    manifestChildName,
                WrittenManifestIncarnation:
                    null,
                ManifestEntryChanged:
                    true,
                Error:
                    "The aggregate namespace manifest was durably created, but its " +
                    "post-publication incarnation could not be captured."
            );
        }

        return Result(
            DataRelativePathAggregateNamespaceManifestWriteState
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
        DataRelativePathAggregateNamespaceManifestWriterResult
        Result(
            DataRelativePathAggregateNamespaceManifestWriteState state,
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
