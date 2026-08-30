using CaseCompat.Filesystem.Linux;
using System.Text.Json;

namespace CaseCompat.Core.Repair;

public static class DataRelativePathRepairFileJournalReader
{
    /*
     * A repair journal should be tiny. This upper bound prevents
     * an unexpected or substituted file from causing an
     * unbounded allocation during recovery.
     */
    public const long MaxJournalBytes =
        4L * 1024L * 1024L;

    public static
        DataRelativePathRepairFileJournalReaderResult
        Read(
            LinuxNoFollowPathHandle journalDirectory,
            string journalChildName)
    {
        ArgumentNullException.ThrowIfNull(
            journalDirectory
        );

        if (!IsValidChildName(journalChildName))
        {
            return Result(
                DataRelativePathRepairFileJournalReadState
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
                DataRelativePathRepairFileJournalReadState
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
            DataRelativePathRepairFileJournalReadState state =
                opened.State switch
                {
                    LinuxOpenChildReadOnlyAtState
                        .UnsupportedPlatform =>
                            DataRelativePathRepairFileJournalReadState
                                .UnsupportedPlatform,

                    LinuxOpenChildReadOnlyAtState
                        .InvalidParentHandle =>
                            DataRelativePathRepairFileJournalReadState
                                .InvalidJournalDirectoryHandle,

                    LinuxOpenChildReadOnlyAtState
                        .ParentNotDirectory =>
                            DataRelativePathRepairFileJournalReadState
                                .JournalDirectoryNotDirectory,

                    LinuxOpenChildReadOnlyAtState
                        .ChildUnavailable =>
                            DataRelativePathRepairFileJournalReadState
                                .JournalUnavailable,

                    LinuxOpenChildReadOnlyAtState
                        .ChildSymbolicLinkRejected =>
                            DataRelativePathRepairFileJournalReadState
                                .JournalSymbolicLinkRejected,

                    _ =>
                        DataRelativePathRepairFileJournalReadState
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

        LinuxOpenedFileIdentityResult identity =
            LinuxOpenedFileIdentity.Capture(
                journal
            );

        if (!identity.Success)
        {
            return Result(
                identity.State ==
                LinuxOpenedFileIdentityState.NotRegularFile
                    ? DataRelativePathRepairFileJournalReadState
                        .JournalNotRegularFile
                    : DataRelativePathRepairFileJournalReadState
                        .JournalIdentityFailed,
                journalChildName,
                journalIdentity:
                    identity,
                error:
                    identity.Error ??
                    identity.State.ToString()
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
                DataRelativePathRepairFileJournalReadState
                    .JournalLengthUnavailable,
                journalChildName,
                journalIdentity:
                    identity,
                error:
                    ex.Message
            );
        }

        if (
            length < 0 ||
            length > MaxJournalBytes)
        {
            return Result(
                DataRelativePathRepairFileJournalReadState
                    .JournalTooLarge,
                journalChildName,
                journalIdentity:
                    identity,
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
                        DataRelativePathRepairFileJournalReadState
                            .UnexpectedEndOfFile,
                        journalChildName,
                        journalIdentity:
                            identity,
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
                DataRelativePathRepairFileJournalReadState
                    .ReadFailed,
                journalChildName,
                journalIdentity:
                    identity,
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
                DataRelativePathRepairFileJournalReadState
                    .JournalLengthUnavailable,
                journalChildName,
                journalIdentity:
                    identity,
                length:
                    length,
                error:
                    ex.Message
            );
        }

        if (lengthAfterRead != length)
        {
            return Result(
                DataRelativePathRepairFileJournalReadState
                    .LengthChangedDuringRead,
                journalChildName,
                journalIdentity:
                    identity,
                length:
                    lengthAfterRead,
                error:
                    "The journal file length changed while its " +
                    "opened descriptor was being read."
            );
        }

        DataRelativePathRepairFileJournalRecord? record;

        try
        {
            record =
                DataRelativePathRepairFileJournalJson
                    .Deserialize(
                        bytes
                    );
        }
        catch (JsonException ex)
        {
            return Result(
                DataRelativePathRepairFileJournalReadState
                    .DeserializeFailed,
                journalChildName,
                journalIdentity:
                    identity,
                length:
                    length,
                error:
                    ex.Message
            );
        }
        catch (NotSupportedException ex)
        {
            return Result(
                DataRelativePathRepairFileJournalReadState
                    .DeserializeFailed,
                journalChildName,
                journalIdentity:
                    identity,
                length:
                    length,
                error:
                    ex.Message
            );
        }

        if (record is null)
        {
            return Result(
                DataRelativePathRepairFileJournalReadState
                    .DeserializeFailed,
                journalChildName,
                journalIdentity:
                    identity,
                length:
                    length,
                error:
                    "The journal JSON did not contain a record."
            );
        }

        string? validationError =
            DataRelativePathRepairFileJournal.Validate(
                record
            );

        if (validationError is not null)
        {
            return Result(
                DataRelativePathRepairFileJournalReadState
                    .InvalidRecord,
                journalChildName,
                record:
                    record,
                journalIdentity:
                    identity,
                length:
                    length,
                error:
                    validationError
            );
        }

        return Result(
            DataRelativePathRepairFileJournalReadState.Loaded,
            journalChildName,
            record:
                record,
            journalIdentity:
                identity,
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
        DataRelativePathRepairFileJournalReaderResult
        Result(
            DataRelativePathRepairFileJournalReadState state,
            string? journalChildName,
            DataRelativePathRepairFileJournalRecord? record = null,
            LinuxOpenedFileIdentityResult? journalIdentity = null,
            long? length = null,
            string? error = null)
    {
        return new
            DataRelativePathRepairFileJournalReaderResult(
                State:
                    state,
                JournalChildName:
                    journalChildName ?? string.Empty,
                Record:
                    record,
                JournalIdentity:
                    journalIdentity,
                Length:
                    length,
                Error:
                    error
            );
    }
}
