using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathTargetedConsumerCaseRepairDurablePlanWriteState
{
    CreatedDurably,

    InvalidChildName,
    InvalidPlan,
    SerializationFailed,

    TemporaryFileCreateFailed,
    WriteFailed,
    TemporaryFileSyncFailed,

    PlanAlreadyExists,
    InitialPublishFailed,

    DirectorySyncFailed
}

public sealed record
    DataRelativePathTargetedConsumerCaseRepairDurablePlanWriterResult(
        DataRelativePathTargetedConsumerCaseRepairDurablePlanWriteState State,
        string ChildName,
        byte[]? WrittenBytes,
        LinuxOpenedFileIncarnationResult? WrittenIncarnation,
        bool PlanEntryChanged,
        string? Error
    )
{
    public bool Success =>
        State ==
        DataRelativePathTargetedConsumerCaseRepairDurablePlanWriteState
            .CreatedDurably;

    public LinuxFileIncarnationIdentity? WrittenIncarnationIdentity =>
        WrittenIncarnation?.Identity;
}

// Independent targeted-schema-v1 durable-plan writer.
//
// This is intentionally not a variant of the legacy repair-plan manifest
// writer. It reuses only low-level CaseCompat.Filesystem.Linux primitives:
// an unnamed-inode write followed by a one-shot, no-overwrite publish.
//
// An existing plan file at the requested child name is never adopted or
// replaced.
public static class DataRelativePathTargetedConsumerCaseRepairDurablePlanWriter
{
    public static
        DataRelativePathTargetedConsumerCaseRepairDurablePlanWriterResult
        CreateInitial(
            LinuxNoFollowPathHandle destinationDirectory,
            string childName,
            DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord plan)
    {
        ArgumentNullException.ThrowIfNull(
            destinationDirectory
        );

        ArgumentNullException.ThrowIfNull(
            plan
        );

        if (!IsValidChildName(childName))
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairDurablePlanWriteState
                    .InvalidChildName,
                childName,
                error:
                    "The plan child name must identify exactly one " +
                    "direct child and cannot be '.', '..', contain " +
                    "path separators, or contain NUL."
            );
        }

        DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonSerializationResult
            serialize =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanJson
                    .SerializeValidated(
                        plan
                    );

        if (!serialize.Success)
        {
            return Result(
                serialize.State ==
                DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonSerializationState
                    .InvalidPlan
                    ? DataRelativePathTargetedConsumerCaseRepairDurablePlanWriteState
                        .InvalidPlan
                    : DataRelativePathTargetedConsumerCaseRepairDurablePlanWriteState
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
                DataRelativePathTargetedConsumerCaseRepairDurablePlanWriteState
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
                DataRelativePathTargetedConsumerCaseRepairDurablePlanWriteState
                    .WriteFailed,
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
                DataRelativePathTargetedConsumerCaseRepairDurablePlanWriteState
                    .TemporaryFileSyncFailed,
                childName,
                error:
                    fileSync.Error ??
                    fileSync.State.ToString()
            );
        }

        /*
         * Publication is deliberately one-shot and no-overwrite.
         *
         * An existing plan file is never adopted or replaced.
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
                LinuxPublishUnnamedFileAtState
                    .DestinationExists
                    ? DataRelativePathTargetedConsumerCaseRepairDurablePlanWriteState
                        .PlanAlreadyExists
                    : DataRelativePathTargetedConsumerCaseRepairDurablePlanWriteState
                        .InitialPublishFailed,
                childName,
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
         * Make the newly linked directory entry durable before
         * reporting success.
         */
        LinuxFsyncResult directorySync =
            LinuxFsync.Sync(
                destinationDirectory
            );

        if (!directorySync.Success)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairDurablePlanWriteState
                    .DirectorySyncFailed,
                childName,
                writtenBytes:
                    bytes,
                writtenIncarnation:
                    writtenIncarnation.Success
                        ? writtenIncarnation
                        : null,
                planEntryChanged:
                    true,
                error:
                    directorySync.Error ??
                    directorySync.State.ToString()
            );
        }

        /*
         * The entry itself is already durable at this point.
         *
         * Failure to recapture its post-publication incarnation must
         * not be reported as if durable creation failed. A later
         * reader can reopen the immutable plan file.
         */
        if (!writtenIncarnation.Success)
        {
            return new(
                State:
                    DataRelativePathTargetedConsumerCaseRepairDurablePlanWriteState
                        .CreatedDurably,
                ChildName:
                    childName,
                WrittenBytes:
                    bytes,
                WrittenIncarnation:
                    null,
                PlanEntryChanged:
                    true,
                Error:
                    "The targeted durable plan was durably created, " +
                    "but its post-publication incarnation could not " +
                    "be captured."
            );
        }

        return Result(
            DataRelativePathTargetedConsumerCaseRepairDurablePlanWriteState
                .CreatedDurably,
            childName,
            writtenBytes:
                bytes,
            writtenIncarnation:
                writtenIncarnation,
            planEntryChanged:
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
        DataRelativePathTargetedConsumerCaseRepairDurablePlanWriterResult
        Result(
            DataRelativePathTargetedConsumerCaseRepairDurablePlanWriteState
                state,
            string? childName,
            byte[]? writtenBytes = null,
            LinuxOpenedFileIncarnationResult? writtenIncarnation = null,
            bool planEntryChanged = false,
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
            WrittenIncarnation:
                writtenIncarnation,
            PlanEntryChanged:
                planEntryChanged,
            Error:
                error
        );
    }
}
