using CaseCompat.Filesystem.Linux;
using System.Text.Json;

namespace CaseCompat.Core.Repair;

public static class DataRelativePathRepairDirectoryJournalReader
{
    /*
     * A repair journal should be tiny. This upper bound prevents
     * an unexpected or substituted file from causing an
     * unbounded allocation during recovery.
     */
    public const long MaxJournalBytes =
        4L * 1024L * 1024L;

    public static
        DataRelativePathRepairDirectoryJournalReaderResult
        Read(
            LinuxNoFollowPathHandle journalDirectory,
            string journalChildName)
    {
        ArgumentNullException.ThrowIfNull(
            journalDirectory
        );

        return Read(
            (ILinuxOpenedHandle)journalDirectory,
            journalChildName
        );
    }

    public static
        DataRelativePathRepairDirectoryJournalReaderResult
        Read(
            ILinuxOpenedHandle journalDirectory,
            string journalChildName)
    {
        ArgumentNullException.ThrowIfNull(
            journalDirectory
        );

        if (!IsValidChildName(journalChildName))
        {
            return Result(
                DataRelativePathRepairDirectoryJournalReadState
                    .InvalidJournalName,
                journalChildName,
                error:
                    "The journal name must identify exactly " +
                    "one direct child."
            );
        }

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                DataRelativePathRepairDirectoryJournalReadState
                    .UnsupportedPlatform,
                journalChildName,
                error:
                    "Descriptor-backed repair journal reading " +
                    "is supported on Linux only."
            );
        }

        LinuxOpenChildReadOnlyAtResult opened =
            LinuxOpenChildReadOnlyAt.Open(
                journalDirectory,
                journalChildName
            );

        if (!opened.Success)
        {
            DataRelativePathRepairDirectoryJournalReadState state =
                opened.State switch
                {
                    LinuxOpenChildReadOnlyAtState
                        .UnsupportedPlatform =>
                            DataRelativePathRepairDirectoryJournalReadState
                                .UnsupportedPlatform,

                    LinuxOpenChildReadOnlyAtState
                        .InvalidParentHandle =>
                            DataRelativePathRepairDirectoryJournalReadState
                                .InvalidJournalDirectoryHandle,

                    LinuxOpenChildReadOnlyAtState
                        .ParentNotDirectory =>
                            DataRelativePathRepairDirectoryJournalReadState
                                .JournalDirectoryNotDirectory,

                    LinuxOpenChildReadOnlyAtState
                        .ChildUnavailable =>
                            DataRelativePathRepairDirectoryJournalReadState
                                .JournalUnavailable,

                    LinuxOpenChildReadOnlyAtState
                        .ChildSymbolicLinkRejected =>
                            DataRelativePathRepairDirectoryJournalReadState
                                .JournalSymbolicLinkRejected,

                    _ =>
                        DataRelativePathRepairDirectoryJournalReadState
                            .JournalOpenFailed
                };

            return Result(
                state,
                journalChildName,
                error:
                    opened.Error ??
                    opened.State.ToString()
            );
        }

        using LinuxOpenedChildHandle journal =
            opened.OpenedChild!;

        LinuxOpenedFileIncarnationResult incarnation =
            LinuxOpenedFileIncarnation.Capture(
                journal
            );

        if (!incarnation.Success)
        {
            return Result(
                incarnation.State ==
                LinuxOpenedFileIncarnationState.NotRegularFile
                    ? DataRelativePathRepairDirectoryJournalReadState
                        .JournalNotRegularFile
                    : DataRelativePathRepairDirectoryJournalReadState
                        .JournalIdentityFailed,
                journalChildName,
                journalIncarnation:
                    incarnation,
                error:
                    incarnation.Error ??
                    incarnation.State.ToString()
            );
        }

        long length;

        try
        {
            length =
                RandomAccess.GetLength(
                    journal.Handle
                );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalReadState
                    .JournalLengthUnavailable,
                journalChildName,
                journalIncarnation:
                    incarnation,
                error:
                    ex.Message
            );
        }

        if (
            length < 0 ||
            length > MaxJournalBytes)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalReadState
                    .JournalTooLarge,
                journalChildName,
                journalIncarnation:
                    incarnation,
                length:
                    length,
                error:
                    $"Journal length {length} exceeds the " +
                    $"supported recovery limit of " +
                    $"{MaxJournalBytes} bytes."
            );
        }

        byte[] bytes =
            new byte[
                checked(
                    (int)length
                )
            ];

        try
        {
            int offset =
                0;

            while (offset < bytes.Length)
            {
                int read =
                    RandomAccess.Read(
                        journal.Handle,
                        bytes.AsSpan(
                            offset
                        ),
                        fileOffset:
                            offset
                    );

                if (read == 0)
                {
                    return Result(
                        DataRelativePathRepairDirectoryJournalReadState
                            .UnexpectedEndOfFile,
                        journalChildName,
                        journalIncarnation:
                            incarnation,
                        length:
                            length,
                        error:
                            "The opened journal reached EOF " +
                            "before its captured length was read."
                    );
                }

                offset +=
                    read;
            }
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalReadState
                    .ReadFailed,
                journalChildName,
                journalIncarnation:
                    incarnation,
                length:
                    length,
                error:
                    ex.Message
            );
        }

        long lengthAfterRead;

        try
        {
            lengthAfterRead =
                RandomAccess.GetLength(
                    journal.Handle
                );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalReadState
                    .JournalLengthUnavailable,
                journalChildName,
                journalIncarnation:
                    incarnation,
                length:
                    length,
                error:
                    ex.Message
            );
        }

        if (lengthAfterRead != length)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalReadState
                    .LengthChangedDuringRead,
                journalChildName,
                journalIncarnation:
                    incarnation,
                length:
                    lengthAfterRead,
                error:
                    "The journal file length changed while its " +
                    "opened descriptor was being read."
            );
        }

        DataRelativePathRepairDirectoryJournalRecord? record;

        try
        {
            record =
                DataRelativePathRepairDirectoryJournalJson
                    .Deserialize(
                        bytes
                    );
        }
        catch (JsonException ex)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalReadState
                    .DeserializeFailed,
                journalChildName,
                journalIncarnation:
                    incarnation,
                length:
                    length,
                error:
                    ex.Message
            );
        }
        catch (NotSupportedException ex)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalReadState
                    .DeserializeFailed,
                journalChildName,
                journalIncarnation:
                    incarnation,
                length:
                    length,
                error:
                    ex.Message
            );
        }

        if (record is null)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalReadState
                    .DeserializeFailed,
                journalChildName,
                journalIncarnation:
                    incarnation,
                length:
                    length,
                error:
                    "The journal JSON did not contain a record."
            );
        }

        string? validationError =
            DataRelativePathRepairDirectoryJournal.Validate(
                record
            );

        if (validationError is not null)
        {
            return Result(
                DataRelativePathRepairDirectoryJournalReadState
                    .InvalidRecord,
                journalChildName,
                record:
                    record,
                journalIncarnation:
                    incarnation,
                length:
                    length,
                error:
                    validationError
            );
        }

        return Result(
            DataRelativePathRepairDirectoryJournalReadState.Loaded,
            journalChildName,
            record:
                record,
            journalIncarnation:
                incarnation,
            length:
                length
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
        DataRelativePathRepairDirectoryJournalReaderResult
        Result(
            DataRelativePathRepairDirectoryJournalReadState state,
            string? journalChildName,
            DataRelativePathRepairDirectoryJournalRecord? record = null,
            LinuxOpenedFileIncarnationResult? journalIncarnation = null,
            long? length = null,
            string? error = null)
    {
        return new
            DataRelativePathRepairDirectoryJournalReaderResult(
                State:
                    state,
                JournalChildName:
                    journalChildName ?? string.Empty,
                Record:
                    record,
                JournalIncarnation:
                    journalIncarnation,
                Length:
                    length,
                Error:
                    error
            );
    }
}
