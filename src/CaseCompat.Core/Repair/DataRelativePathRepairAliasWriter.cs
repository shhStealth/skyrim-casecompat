using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairAliasWriteState
{
    CreatedDurably,

    InvalidChildName,
    InvalidRecord,
    SerializationFailed,

    TemporaryFileCreateFailed,
    WriteFailed,
    TemporaryFileSyncFailed,

    AliasAlreadyExists,
    InitialPublishFailed,

    DirectorySyncFailed
}

public sealed record DataRelativePathRepairAliasWriterResult(
    DataRelativePathRepairAliasWriteState State,
    string ChildName,
    string? Error
)
{
    public bool Success =>
        State ==
            DataRelativePathRepairAliasWriteState.CreatedDurably;
}

// Independent durable writer for one alias record.
//
// Reuses only the same low-level, already-proven unnamed-inode-write
// then one-shot no-overwrite publish primitives the targeted
// durable-plan writer uses. An existing alias record at the requested
// child name is never adopted or replaced - each alias gets exactly
// one durable record, written once, at creation time.
public static class DataRelativePathRepairAliasWriter
{
    public static DataRelativePathRepairAliasWriterResult
        CreateInitial(
            LinuxNoFollowPathHandle destinationDirectory,
            string childName,
            DataRelativePathRepairAliasRecord record)
    {
        ArgumentNullException.ThrowIfNull(
            destinationDirectory
        );

        ArgumentNullException.ThrowIfNull(
            record
        );

        if (!IsValidChildName(childName))
        {
            return Result(
                DataRelativePathRepairAliasWriteState.InvalidChildName,
                childName,
                error:
                    "The alias record child name must identify exactly " +
                    "one direct child and cannot be '.', '..', contain " +
                    "path separators, or contain NUL."
            );
        }

        DataRelativePathRepairAliasJsonSerializationResult serialize =
            DataRelativePathRepairAliasJson.SerializeValidated(
                record
            );

        if (!serialize.Success)
        {
            return Result(
                serialize.State ==
                DataRelativePathRepairAliasJsonSerializationState
                    .InvalidRecord
                    ? DataRelativePathRepairAliasWriteState.InvalidRecord
                    : DataRelativePathRepairAliasWriteState
                        .SerializationFailed,
                childName,
                error:
                    serialize.Error ??
                    serialize.State.ToString()
            );
        }

        byte[] bytes =
            serialize.Bytes!;

        LinuxCreateUnnamedFileAtResult create =
            LinuxCreateUnnamedFileAt.Create(
                destinationDirectory
            );

        if (!create.Success)
        {
            return Result(
                DataRelativePathRepairAliasWriteState
                    .TemporaryFileCreateFailed,
                childName,
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
                DataRelativePathRepairAliasWriteState.WriteFailed,
                childName,
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
                DataRelativePathRepairAliasWriteState
                    .TemporaryFileSyncFailed,
                childName,
                error:
                    fileSync.Error ??
                    fileSync.State.ToString()
            );
        }

        /*
         * Publication is deliberately one-shot and no-overwrite. An
         * existing alias record is never adopted or replaced.
         */
        LinuxPublishUnnamedFileAtResult publish =
            LinuxPublishUnnamedFileAt.Publish(
                temporary,
                destinationDirectory,
                childName
            );

        if (!publish.Success)
        {
            return Result(
                publish.State ==
                LinuxPublishUnnamedFileAtState.DestinationExists
                    ? DataRelativePathRepairAliasWriteState
                        .AliasAlreadyExists
                    : DataRelativePathRepairAliasWriteState
                        .InitialPublishFailed,
                childName,
                error:
                    publish.Error ??
                    publish.State.ToString()
            );
        }

        LinuxFsyncResult directorySync =
            LinuxFsync.Sync(
                destinationDirectory
            );

        if (!directorySync.Success)
        {
            return Result(
                DataRelativePathRepairAliasWriteState
                    .DirectorySyncFailed,
                childName,
                error:
                    directorySync.Error ??
                    directorySync.State.ToString()
            );
        }

        return Result(
            DataRelativePathRepairAliasWriteState.CreatedDurably,
            childName
        );
    }

    private static string? WriteExactBytes(
        LinuxUnnamedFileHandle file,
        byte[] bytes)
    {
        try
        {
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

    private static DataRelativePathRepairAliasWriterResult Result(
        DataRelativePathRepairAliasWriteState state,
        string? childName,
        string? error = null)
    {
        return new(
            State:
                state,
            ChildName:
                childName ??
                string.Empty,
            Error:
                error
        );
    }
}
