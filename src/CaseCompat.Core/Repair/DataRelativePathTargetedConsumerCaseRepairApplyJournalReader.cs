using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
{
    Read,

    InvalidChildName,
    UnsupportedPlatform,
    InvalidSourceDirectoryHandle,
    SourceDirectoryNotDirectory,

    PhaseUnavailable,
    PhaseSymbolicLinkRejected,
    PhaseNotRegularFile,
    PhaseTooLarge,
    PhaseOpenFailed,

    PhaseIdentityFailed,

    PhaseLengthUnavailable,
    UnexpectedEndOfFile,
    ReadFailed,
    LengthChangedDuringRead,

    DeserializeFailed,
    PhaseInvalid
}

public sealed record
    DataRelativePathTargetedConsumerCaseRepairApplyJournalReaderResult(
        DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
            State,
        string ChildName,
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord?
            Phase,
        long? Length,
        string? PhaseSha256,
        string? Error
    )
{
    public bool Success =>
        State ==
        DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState.Read &&
        Phase is not null &&
        PhaseSha256 is not null;
}

public static class
    DataRelativePathTargetedConsumerCaseRepairApplyJournalReader
{
    public const long MaxPhaseSizeBytes =
        64L * 1024L;

    public static
        DataRelativePathTargetedConsumerCaseRepairApplyJournalReaderResult
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

    public static
        DataRelativePathTargetedConsumerCaseRepairApplyJournalReaderResult
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
                DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
                    .InvalidChildName,
                childName,
                error:
                    "The apply-journal phase child name must identify " +
                    "exactly one direct child and cannot be '.', '..', " +
                    "contain path separators, or contain NUL."
            );
        }

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
                    .UnsupportedPlatform,
                childName,
                error:
                    "Descriptor-backed targeted apply-journal reading is " +
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
                        DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
                            .InvalidSourceDirectoryHandle,

                    LinuxInspectChildAtState.ParentNotDirectory =>
                        DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
                            .SourceDirectoryNotDirectory,

                    LinuxInspectChildAtState.ChildUnavailable =>
                        DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
                            .PhaseUnavailable,

                    _ =>
                        DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
                            .PhaseOpenFailed
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
                inspect.Kind ==
                LinuxChildObjectKind.SymbolicLink
                    ? DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
                        .PhaseSymbolicLinkRejected
                    : DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
                        .PhaseNotRegularFile,
                childName,
                error:
                    "The targeted apply-journal phase child is not a " +
                    "regular file."
            );
        }

        if (
            inspect.Size is null ||
            inspect.Size < 0 ||
            inspect.Size > MaxPhaseSizeBytes)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
                    .PhaseTooLarge,
                childName,
                length:
                    inspect.Size,
                error:
                    $"Phase length {inspect.Size?.ToString() ?? "unknown"} " +
                    $"exceeds the supported limit of {MaxPhaseSizeBytes} " +
                    "bytes."
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
                            DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
                                .UnsupportedPlatform,

                    LinuxOpenChildRegularFileReadOnlyAtState
                        .InvalidParentHandle =>
                            DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
                                .InvalidSourceDirectoryHandle,

                    LinuxOpenChildRegularFileReadOnlyAtState
                        .ParentNotDirectory =>
                            DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
                                .SourceDirectoryNotDirectory,

                    LinuxOpenChildRegularFileReadOnlyAtState
                        .ChildUnavailable =>
                            DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
                                .PhaseUnavailable,

                    LinuxOpenChildRegularFileReadOnlyAtState
                        .ChildNotRegularFile =>
                            DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
                                .PhaseNotRegularFile,

                    _ =>
                        DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
                            .PhaseOpenFailed
                },
                childName,
                error:
                    opened.Error ??
                    opened.State.ToString()
            );
        }

        using LinuxOpenedChildHandle phase =
            opened.OpenedFile!;

        long length;

        try
        {
            length =
                RandomAccess.GetLength(
                    phase.Handle
                );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
                    .PhaseLengthUnavailable,
                childName,
                error:
                    ex.Message
            );
        }

        if (
            length < 0 ||
            length > MaxPhaseSizeBytes)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
                    .PhaseTooLarge,
                childName,
                length:
                    length,
                error:
                    $"Phase length {length} exceeds the supported limit " +
                    $"of {MaxPhaseSizeBytes} bytes."
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
                        phase.Handle,
                        bytes.AsSpan(
                            offset
                        ),
                        fileOffset:
                            offset
                    );

                if (read == 0)
                {
                    return Result(
                        DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
                            .UnexpectedEndOfFile,
                        childName,
                        length:
                            length,
                        error:
                            "The opened apply-journal phase reached EOF " +
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
                DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
                    .ReadFailed,
                childName,
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
                    phase.Handle
                );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
                    .PhaseLengthUnavailable,
                childName,
                length:
                    length,
                error:
                    ex.Message
            );
        }

        if (lengthAfterRead != length)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
                    .LengthChangedDuringRead,
                childName,
                length:
                    lengthAfterRead,
                error:
                    "The apply-journal phase length changed while its " +
                    "opened descriptor was being read."
            );
        }

        DataRelativePathTargetedConsumerCaseRepairApplyJournalJsonDeserializationResult
            deserialize =
                DataRelativePathTargetedConsumerCaseRepairApplyJournalJson
                    .DeserializeValidated(
                        bytes
                    );

        if (!deserialize.Success)
        {
            return Result(
                deserialize.State ==
                DataRelativePathTargetedConsumerCaseRepairApplyJournalJsonDeserializationState
                    .InvalidRecord
                    ? DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
                        .PhaseInvalid
                    : DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
                        .DeserializeFailed,
                childName,
                length:
                    length,
                error:
                    deserialize.Error ??
                    deserialize.State.ToString()
            );
        }

        string phaseSha256 =
            Convert.ToHexString(
                SHA256.HashData(
                    bytes
                )
            );

        return Result(
            DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
                .Read,
            childName,
            phase:
                deserialize.Record,
            length:
                length,
            phaseSha256:
                phaseSha256
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
        DataRelativePathTargetedConsumerCaseRepairApplyJournalReaderResult
        Result(
            DataRelativePathTargetedConsumerCaseRepairApplyJournalReadState
                state,
            string? childName,
            DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord?
                phase = null,
            long? length = null,
            string? phaseSha256 = null,
            string? error = null)
    {
        return new(
            State:
                state,
            ChildName:
                childName ??
                string.Empty,
            Phase:
                phase,
            Length:
                length,
            PhaseSha256:
                phaseSha256,
            Error:
                error
        );
    }
}
