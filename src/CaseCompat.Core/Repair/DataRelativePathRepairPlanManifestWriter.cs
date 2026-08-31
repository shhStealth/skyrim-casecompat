using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairPlanManifestWriteState
{
    CreatedDurably,

    InvalidManifest,
    InvalidManifestName,
    SerializationFailed,

    TemporaryFileCreateFailed,
    WriteFailed,
    TemporaryFileSyncFailed,

    ManifestAlreadyExists,
    InitialPublishFailed,

    DirectorySyncFailed
}

public sealed record DataRelativePathRepairPlanManifestWriterResult(
    DataRelativePathRepairPlanManifestWriteState State,
    string ManifestChildName,
    LinuxOpenedFileIncarnationResult?
        WrittenManifestIncarnation,
    bool ManifestEntryChanged,
    string? Error
)
{
    public bool Success =>
        State ==
        DataRelativePathRepairPlanManifestWriteState
            .CreatedDurably;

    public LinuxFileIncarnationIdentity?
        WrittenManifestIncarnationIdentity =>
            WrittenManifestIncarnation?.Identity;
}

public static class DataRelativePathRepairPlanManifestWriter
{
    public static DataRelativePathRepairPlanManifestWriterResult
        CreateInitial(
            LinuxNoFollowPathHandle manifestDirectory,
            string manifestChildName,
            DataRelativePathRepairPlanManifestRecord manifest)
    {
        ArgumentNullException.ThrowIfNull(
            manifestDirectory
        );

        ArgumentNullException.ThrowIfNull(
            manifest
        );

        string? validationError =
            DataRelativePathRepairPlanManifest.Validate(
                manifest
            );

        if (validationError is not null)
        {
            return Result(
                DataRelativePathRepairPlanManifestWriteState
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
                DataRelativePathRepairPlanManifestWriteState
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
                DataRelativePathRepairPlanManifestJson
                    .Serialize(
                        manifest
                    );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathRepairPlanManifestWriteState
                    .SerializationFailed,
                manifestChildName,
                error:
                    ex.Message
            );
        }

        LinuxCreateUnnamedFileAtResult create =
            LinuxCreateUnnamedFileAt.Create(
                manifestDirectory
            );

        if (!create.Success)
        {
            return Result(
                DataRelativePathRepairPlanManifestWriteState
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
                DataRelativePathRepairPlanManifestWriteState
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
                DataRelativePathRepairPlanManifestWriteState
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
         * An existing manifest is never adopted or replaced.
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
                    ? DataRelativePathRepairPlanManifestWriteState
                        .ManifestAlreadyExists
                    : DataRelativePathRepairPlanManifestWriteState
                        .InitialPublishFailed,
                manifestChildName,
                error:
                    publish.Error ??
                    publish.State.ToString()
            );
        }

        LinuxOpenedFileIncarnationResult writtenIncarnation =
            LinuxOpenedFileIncarnation.Capture(
                temporary
            );

        /*
         * Make the newly linked manifest-directory entry durable
         * before reporting success.
         */
        LinuxFsyncResult directorySync =
            LinuxFsync.Sync(
                manifestDirectory
            );

        if (!directorySync.Success)
        {
            return Result(
                DataRelativePathRepairPlanManifestWriteState
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
         * The entry itself is already durable at this point.
         *
         * As with the operation-journal writers, failure to
         * recapture its post-publication incarnation must not be
         * reported as if durable creation failed. A later reader
         * can reopen the immutable manifest.
         */
        if (!writtenIncarnation.Success)
        {
            return new(
                State:
                    DataRelativePathRepairPlanManifestWriteState
                        .CreatedDurably,
                ManifestChildName:
                    manifestChildName,
                WrittenManifestIncarnation:
                    null,
                ManifestEntryChanged:
                    true,
                Error:
                    "The plan manifest was durably created, but its " +
                    "post-publication incarnation could not be captured."
            );
        }

        return Result(
            DataRelativePathRepairPlanManifestWriteState
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
        DataRelativePathRepairPlanManifestWriterResult
        Result(
            DataRelativePathRepairPlanManifestWriteState state,
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
