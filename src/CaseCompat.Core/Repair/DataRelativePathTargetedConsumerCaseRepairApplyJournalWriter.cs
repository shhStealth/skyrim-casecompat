using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathTargetedConsumerCaseRepairApplyJournalWriteState
{
    CreatedDurably,

    InvalidChildName,
    InvalidRecord,
    SerializationFailed,

    TemporaryFileCreateFailed,
    WriteFailed,
    TemporaryFileSyncFailed,

    JournalPhaseAlreadyExists,
    InitialPublishFailed,

    DirectorySyncFailed
}

public sealed record
    DataRelativePathTargetedConsumerCaseRepairApplyJournalWriterResult(
        DataRelativePathTargetedConsumerCaseRepairApplyJournalWriteState
            State,
        string ChildName,
        byte[]? WrittenBytes,
        string? Error
    )
{
    public bool Success =>
        State ==
        DataRelativePathTargetedConsumerCaseRepairApplyJournalWriteState
            .CreatedDurably;
}

// Each apply-journal phase is published exactly once as a new
// immutable file. There is deliberately no "replace existing" writer:
// the phase progression (IntentRecorded -> Prepared -> Applied ->
// RolledBack) is expressed entirely by which distinct phase child
// names exist, reusing only the already-proven no-overwrite
// unnamed-file-publish primitives.
public static class
    DataRelativePathTargetedConsumerCaseRepairApplyJournalWriter
{
    public static
        DataRelativePathTargetedConsumerCaseRepairApplyJournalWriterResult
        CreateInitial(
            LinuxNoFollowPathHandle destinationDirectory,
            string childName,
            DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord
                phase)
    {
        ArgumentNullException.ThrowIfNull(
            destinationDirectory
        );

        ArgumentNullException.ThrowIfNull(
            phase
        );

        if (!IsValidChildName(childName))
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyJournalWriteState
                    .InvalidChildName,
                childName,
                error:
                    "The apply-journal phase child name must identify " +
                    "exactly one direct child and cannot be '.', '..', " +
                    "contain path separators, or contain NUL."
            );
        }

        DataRelativePathTargetedConsumerCaseRepairApplyJournalJsonSerializationResult
            serialize =
                DataRelativePathTargetedConsumerCaseRepairApplyJournalJson
                    .SerializeValidated(
                        phase
                    );

        if (!serialize.Success)
        {
            return Result(
                serialize.State ==
                DataRelativePathTargetedConsumerCaseRepairApplyJournalJsonSerializationState
                    .InvalidRecord
                    ? DataRelativePathTargetedConsumerCaseRepairApplyJournalWriteState
                        .InvalidRecord
                    : DataRelativePathTargetedConsumerCaseRepairApplyJournalWriteState
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
                DataRelativePathTargetedConsumerCaseRepairApplyJournalWriteState
                    .TemporaryFileCreateFailed,
                childName,
                error:
                    create.Error ??
                    create.State.ToString()
            );
        }

        using LinuxUnnamedFileHandle temporary =
            create.OpenedFile!;

        try
        {
            RandomAccess.Write(
                temporary.Handle,
                bytes,
                fileOffset:
                    0
            );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyJournalWriteState
                    .WriteFailed,
                childName,
                error:
                    ex.Message
            );
        }

        LinuxFsyncResult fileSync =
            LinuxFsync.Sync(
                temporary
            );

        if (!fileSync.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyJournalWriteState
                    .TemporaryFileSyncFailed,
                childName,
                error:
                    fileSync.Error ??
                    fileSync.State.ToString()
            );
        }

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
                LinuxPublishUnnamedFileAtState
                    .DestinationExists
                    ? DataRelativePathTargetedConsumerCaseRepairApplyJournalWriteState
                        .JournalPhaseAlreadyExists
                    : DataRelativePathTargetedConsumerCaseRepairApplyJournalWriteState
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
                DataRelativePathTargetedConsumerCaseRepairApplyJournalWriteState
                    .DirectorySyncFailed,
                childName,
                writtenBytes:
                    bytes,
                error:
                    directorySync.Error ??
                    directorySync.State.ToString()
            );
        }

        return Result(
            DataRelativePathTargetedConsumerCaseRepairApplyJournalWriteState
                .CreatedDurably,
            childName,
            writtenBytes:
                bytes
        );
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
        DataRelativePathTargetedConsumerCaseRepairApplyJournalWriterResult
        Result(
            DataRelativePathTargetedConsumerCaseRepairApplyJournalWriteState
                state,
            string? childName,
            byte[]? writtenBytes = null,
            string? error = null)
    {
        return new(
            State:
                state,
            ChildName:
                childName ??
                string.Empty,
            WrittenBytes:
                writtenBytes,
            Error:
                error
        );
    }
}
