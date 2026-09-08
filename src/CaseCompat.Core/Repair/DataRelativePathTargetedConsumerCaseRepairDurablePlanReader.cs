using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
{
    Read,

    InvalidChildName,
    UnsupportedPlatform,
    InvalidSourceDirectoryHandle,
    SourceDirectoryNotDirectory,

    PlanUnavailable,
    PlanSymbolicLinkRejected,
    PlanNotRegularFile,
    PlanTooLarge,
    PlanOpenFailed,

    PlanIdentityFailed,

    PlanLengthUnavailable,
    UnexpectedEndOfFile,
    ReadFailed,
    LengthChangedDuringRead,

    DeserializeFailed,
    PlanInvalid
}

public sealed record
    DataRelativePathTargetedConsumerCaseRepairDurablePlanReaderResult(
        DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState State,
        string ChildName,
        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord? Plan,
        LinuxOpenedFileIncarnationResult? PlanIncarnation,
        long? Length,
        string? PlanSha256,
        string? Error
    )
{
    public bool Success =>
        State ==
        DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState.Read &&
        Plan is not null &&
        PlanSha256 is not null;

    public LinuxFileIncarnationIdentity? PlanIncarnationIdentity =>
        PlanIncarnation?.Identity;
}

// Independent targeted-schema-v1 durable-plan reader.
//
// This is intentionally not a variant of the legacy repair-plan manifest
// reader. It reuses only low-level CaseCompat.Filesystem.Linux primitives
// and the same bounded-read-then-hash-the-exact-bytes pattern, inlined here
// rather than through a separate contents-capture primitive since this
// reader is its only consumer.
public static class DataRelativePathTargetedConsumerCaseRepairDurablePlanReader
{
    /*
     * A single targeted durable plan is expected to remain small: one
     * GUID, a few PATH_MAX-bounded path strings, SHA-256 hex fields, and
     * a directory-depth-bounded operation list. This ceiling gives
     * roughly 2-3x headroom over a pathological worst-case record while
     * still making an oversized file itself evidence of corruption.
     */
    public const long MaxPlanSizeBytes =
        64L * 1024L;

    public static
        DataRelativePathTargetedConsumerCaseRepairDurablePlanReaderResult
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
        DataRelativePathTargetedConsumerCaseRepairDurablePlanReaderResult
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
                DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                    .InvalidChildName,
                childName,
                error:
                    "The plan child name must identify exactly one " +
                    "direct child and cannot be '.', '..', contain " +
                    "path separators, or contain NUL."
            );
        }

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                    .UnsupportedPlatform,
                childName,
                error:
                    "Descriptor-backed targeted durable-plan reading is " +
                    "supported on Linux only."
            );
        }

        /*
         * Cheap stat-only pre-check: reject a missing, non-regular,
         * symlinked, or oversize child before ever opening it.
         */
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
                        DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                            .InvalidSourceDirectoryHandle,

                    LinuxInspectChildAtState.ParentNotDirectory =>
                        DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                            .SourceDirectoryNotDirectory,

                    LinuxInspectChildAtState.ChildUnavailable =>
                        DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                            .PlanUnavailable,

                    _ =>
                        DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                            .PlanOpenFailed
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
                    ? DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                        .PlanSymbolicLinkRejected
                    : DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                        .PlanNotRegularFile,
                childName,
                error:
                    "The targeted durable plan child is not a regular " +
                    "file."
            );
        }

        if (
            inspect.Size is null ||
            inspect.Size < 0 ||
            inspect.Size > MaxPlanSizeBytes)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                    .PlanTooLarge,
                childName,
                length:
                    inspect.Size,
                error:
                    $"Plan length {inspect.Size?.ToString() ?? "unknown"} " +
                    $"exceeds the supported limit of {MaxPlanSizeBytes} " +
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
                            DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                                .UnsupportedPlatform,

                    LinuxOpenChildRegularFileReadOnlyAtState
                        .InvalidParentHandle =>
                            DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                                .InvalidSourceDirectoryHandle,

                    LinuxOpenChildRegularFileReadOnlyAtState
                        .ParentNotDirectory =>
                            DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                                .SourceDirectoryNotDirectory,

                    LinuxOpenChildRegularFileReadOnlyAtState
                        .ChildUnavailable =>
                            DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                                .PlanUnavailable,

                    LinuxOpenChildRegularFileReadOnlyAtState
                        .ChildNotRegularFile =>
                            DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                                .PlanNotRegularFile,

                    _ =>
                        DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                            .PlanOpenFailed
                },
                childName,
                error:
                    opened.Error ??
                    opened.State.ToString()
            );
        }

        using LinuxOpenedChildHandle plan =
            opened.OpenedFile!;

        LinuxOpenedFileIncarnationResult incarnation =
            LinuxOpenedFileIncarnation.Capture(
                plan
            );

        if (!incarnation.Success)
        {
            return Result(
                incarnation.State ==
                LinuxOpenedFileIncarnationState.NotRegularFile
                    ? DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                        .PlanNotRegularFile
                    : DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                        .PlanIdentityFailed,
                childName,
                planIncarnation:
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
                    plan.Handle
                );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                    .PlanLengthUnavailable,
                childName,
                planIncarnation:
                    incarnation,
                error:
                    ex.Message
            );
        }

        if (
            length < 0 ||
            length > MaxPlanSizeBytes)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                    .PlanTooLarge,
                childName,
                planIncarnation:
                    incarnation,
                length:
                    length,
                error:
                    $"Plan length {length} exceeds the supported limit " +
                    $"of {MaxPlanSizeBytes} bytes."
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
                        plan.Handle,
                        bytes.AsSpan(
                            offset
                        ),
                        fileOffset:
                            offset
                    );

                if (read == 0)
                {
                    return Result(
                        DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                            .UnexpectedEndOfFile,
                        childName,
                        planIncarnation:
                            incarnation,
                        length:
                            length,
                        error:
                            "The opened plan file reached EOF before " +
                            "its captured length was read."
                    );
                }

                offset +=
                    read;
            }
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                    .ReadFailed,
                childName,
                planIncarnation:
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
                    plan.Handle
                );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                    .PlanLengthUnavailable,
                childName,
                planIncarnation:
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
                DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                    .LengthChangedDuringRead,
                childName,
                planIncarnation:
                    incarnation,
                length:
                    lengthAfterRead,
                error:
                    "The plan file length changed while its opened " +
                    "descriptor was being read."
            );
        }

        DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonDeserializationResult
            deserialize =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanJson
                    .DeserializeValidated(
                        bytes
                    );

        if (!deserialize.Success)
        {
            return Result(
                deserialize.State ==
                DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonDeserializationState
                    .InvalidPlan
                    ? DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                        .PlanInvalid
                    : DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                        .DeserializeFailed,
                childName,
                planIncarnation:
                    incarnation,
                length:
                    length,
                error:
                    deserialize.Error ??
                    deserialize.State.ToString()
            );
        }

        /*
         * Hash the exact in-memory byte sequence that was deserialized
         * and validated above.
         *
         * Do not perform a second descriptor read here: a separate hash
         * pass could observe different same-length contents after an
         * in-place modification and would no longer prove which bytes
         * produced the validated plan record.
         */
        string planSha256 =
            Convert.ToHexString(
                SHA256.HashData(
                    bytes
                )
            );

        return Result(
            DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                .Read,
            childName,
            plan:
                deserialize.Record,
            planIncarnation:
                incarnation,
            length:
                length,
            planSha256:
                planSha256
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
        DataRelativePathTargetedConsumerCaseRepairDurablePlanReaderResult
        Result(
            DataRelativePathTargetedConsumerCaseRepairDurablePlanReadState
                state,
            string? childName,
            DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord?
                plan = null,
            LinuxOpenedFileIncarnationResult? planIncarnation = null,
            long? length = null,
            string? planSha256 = null,
            string? error = null)
    {
        return new(
            State:
                state,
            ChildName:
                childName ??
                string.Empty,
            Plan:
                plan,
            PlanIncarnation:
                planIncarnation,
            Length:
                length,
            PlanSha256:
                planSha256,
            Error:
                error
        );
    }
}
