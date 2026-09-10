using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairAliasReadState
{
    Read,

    InvalidChildName,
    UnsupportedPlatform,
    InvalidSourceDirectoryHandle,
    SourceDirectoryNotDirectory,

    AliasRecordUnavailable,
    AliasRecordSymbolicLinkRejected,
    AliasRecordNotRegularFile,
    AliasRecordTooLarge,
    AliasRecordOpenFailed,

    LengthUnavailable,
    UnexpectedEndOfFile,
    ReadFailed,
    LengthChangedDuringRead,

    DeserializeFailed,
    RecordInvalid
}

public sealed record DataRelativePathRepairAliasReaderResult(
    DataRelativePathRepairAliasReadState State,
    string ChildName,
    DataRelativePathRepairAliasRecord? Record,
    string? Error
)
{
    public bool Success =>
        State ==
            DataRelativePathRepairAliasReadState.Read &&
        Record is not null;
}

// Independent reader for one durable alias record.
//
// Like the targeted durable-plan reader, this rejects a symlinked,
// non-regular, or oversized record file before ever opening it, and
// re-validates the deserialized record with exactly the same rules a
// fresh in-memory record would have to pass. A record read back from
// here is exactly as trustworthy as one just built in memory - never
// more, since a caller relying on the alias itself must still re-read
// the actual filesystem symlink fresh regardless of what this record
// says.
public static class DataRelativePathRepairAliasReader
{
    /*
     * An alias record is a handful of scalar fields plus one file
     * identity - expected to remain well under 4 KiB. This ceiling is
     * generous headroom while still making an oversized file itself
     * evidence of corruption.
     */
    public const long MaxRecordSizeBytes =
        4L * 1024L;

    public static DataRelativePathRepairAliasReaderResult
        Read(
            LinuxNoFollowPathHandle sourceDirectory,
            string childName)
    {
        ArgumentNullException.ThrowIfNull(
            sourceDirectory
        );

        return Read(
            (ILinuxOpenedHandle)sourceDirectory,
            childName
        );
    }

    public static DataRelativePathRepairAliasReaderResult
        Read(
            ILinuxOpenedHandle sourceDirectory,
            string childName)
    {
        ArgumentNullException.ThrowIfNull(
            sourceDirectory
        );

        if (!IsValidChildName(childName))
        {
            return Result(
                DataRelativePathRepairAliasReadState.InvalidChildName,
                childName,
                error:
                    "The alias record child name must identify exactly " +
                    "one direct child and cannot be '.', '..', contain " +
                    "path separators, or contain NUL."
            );
        }

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                DataRelativePathRepairAliasReadState.UnsupportedPlatform,
                childName,
                error:
                    "Descriptor-backed alias-record reading is " +
                    "supported on Linux only."
            );
        }

        LinuxInspectChildAtResult inspect =
            LinuxInspectChildAt.Inspect(
                sourceDirectory,
                childName
            );

        if (!inspect.Success)
        {
            return Result(
                inspect.State switch
                {
                    LinuxInspectChildAtState.InvalidParentHandle =>
                        DataRelativePathRepairAliasReadState
                            .InvalidSourceDirectoryHandle,

                    LinuxInspectChildAtState.ParentNotDirectory =>
                        DataRelativePathRepairAliasReadState
                            .SourceDirectoryNotDirectory,

                    LinuxInspectChildAtState.ChildUnavailable =>
                        DataRelativePathRepairAliasReadState
                            .AliasRecordUnavailable,

                    _ =>
                        DataRelativePathRepairAliasReadState
                            .AliasRecordOpenFailed
                },
                childName,
                error:
                    inspect.Error ??
                    inspect.State.ToString()
            );
        }

        if (inspect.Kind != LinuxChildObjectKind.RegularFile)
        {
            return Result(
                inspect.Kind == LinuxChildObjectKind.SymbolicLink
                    ? DataRelativePathRepairAliasReadState
                        .AliasRecordSymbolicLinkRejected
                    : DataRelativePathRepairAliasReadState
                        .AliasRecordNotRegularFile,
                childName,
                error:
                    "The alias record child is not a regular file."
            );
        }

        if (
            inspect.Size is null ||
            inspect.Size < 0 ||
            inspect.Size > MaxRecordSizeBytes)
        {
            return Result(
                DataRelativePathRepairAliasReadState.AliasRecordTooLarge,
                childName,
                error:
                    $"Alias record length " +
                    $"{inspect.Size?.ToString() ?? "unknown"} exceeds " +
                    $"the supported limit of {MaxRecordSizeBytes} bytes."
            );
        }

        LinuxOpenChildRegularFileReadOnlyAtResult opened =
            LinuxOpenChildRegularFileReadOnlyAt.Open(
                sourceDirectory,
                childName
            );

        if (!opened.Success)
        {
            return Result(
                opened.State switch
                {
                    LinuxOpenChildRegularFileReadOnlyAtState
                        .UnsupportedPlatform =>
                            DataRelativePathRepairAliasReadState
                                .UnsupportedPlatform,

                    LinuxOpenChildRegularFileReadOnlyAtState
                        .InvalidParentHandle =>
                            DataRelativePathRepairAliasReadState
                                .InvalidSourceDirectoryHandle,

                    LinuxOpenChildRegularFileReadOnlyAtState
                        .ParentNotDirectory =>
                            DataRelativePathRepairAliasReadState
                                .SourceDirectoryNotDirectory,

                    LinuxOpenChildRegularFileReadOnlyAtState
                        .ChildUnavailable =>
                            DataRelativePathRepairAliasReadState
                                .AliasRecordUnavailable,

                    LinuxOpenChildRegularFileReadOnlyAtState
                        .ChildNotRegularFile =>
                            DataRelativePathRepairAliasReadState
                                .AliasRecordNotRegularFile,

                    _ =>
                        DataRelativePathRepairAliasReadState
                            .AliasRecordOpenFailed
                },
                childName,
                error:
                    opened.Error ??
                    opened.State.ToString()
            );
        }

        using LinuxOpenedChildHandle file =
            opened.OpenedFile!;

        long length;

        try
        {
            length =
                RandomAccess.GetLength(
                    file.Handle
                );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathRepairAliasReadState.LengthUnavailable,
                childName,
                error:
                    ex.Message
            );
        }

        if (
            length < 0 ||
            length > MaxRecordSizeBytes)
        {
            return Result(
                DataRelativePathRepairAliasReadState.AliasRecordTooLarge,
                childName,
                error:
                    $"Alias record length {length} exceeds the " +
                    $"supported limit of {MaxRecordSizeBytes} bytes."
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
                        file.Handle,
                        bytes.AsSpan(
                            offset
                        ),
                        fileOffset:
                            offset
                    );

                if (read == 0)
                {
                    return Result(
                        DataRelativePathRepairAliasReadState
                            .UnexpectedEndOfFile,
                        childName,
                        error:
                            "The opened alias record reached EOF " +
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
                DataRelativePathRepairAliasReadState.ReadFailed,
                childName,
                error:
                    ex.Message
            );
        }

        long lengthAfterRead;

        try
        {
            lengthAfterRead =
                RandomAccess.GetLength(
                    file.Handle
                );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathRepairAliasReadState.LengthUnavailable,
                childName,
                error:
                    ex.Message
            );
        }

        if (lengthAfterRead != length)
        {
            return Result(
                DataRelativePathRepairAliasReadState
                    .LengthChangedDuringRead,
                childName,
                error:
                    "The alias record length changed while its opened " +
                    "descriptor was being read."
            );
        }

        DataRelativePathRepairAliasJsonDeserializationResult deserialize =
            DataRelativePathRepairAliasJson.DeserializeValidated(
                bytes
            );

        if (!deserialize.Success)
        {
            return Result(
                deserialize.State ==
                DataRelativePathRepairAliasJsonDeserializationState
                    .InvalidRecord
                    ? DataRelativePathRepairAliasReadState.RecordInvalid
                    : DataRelativePathRepairAliasReadState
                        .DeserializeFailed,
                childName,
                error:
                    deserialize.Error ??
                    deserialize.State.ToString()
            );
        }

        return new(
            State:
                DataRelativePathRepairAliasReadState.Read,
            ChildName:
                childName,
            Record:
                deserialize.Record,
            Error:
                null
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

    private static DataRelativePathRepairAliasReaderResult Result(
        DataRelativePathRepairAliasReadState state,
        string? childName,
        string? error = null)
    {
        return new(
            State:
                state,
            ChildName:
                childName ??
                string.Empty,
            Record:
                null,
            Error:
                error
        );
    }
}
