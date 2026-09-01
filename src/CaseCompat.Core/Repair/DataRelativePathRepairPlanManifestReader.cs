using CaseCompat.Filesystem.Linux;
using System.Text.Json;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairPlanManifestReadState
{
    Read,

    InvalidManifestName,
    UnsupportedPlatform,
    InvalidManifestDirectoryHandle,
    ManifestDirectoryNotDirectory,

    ManifestUnavailable,
    ManifestSymbolicLinkRejected,
    ManifestOpenFailed,

    ManifestIdentityFailed,
    ManifestNotRegularFile,

    ManifestLengthUnavailable,
    ManifestTooLarge,
    UnexpectedEndOfFile,
    ReadFailed,
    LengthChangedDuringRead,

    DeserializeFailed,
    ManifestInvalid
}

public sealed record DataRelativePathRepairPlanManifestReaderResult(
    DataRelativePathRepairPlanManifestReadState State,
    string ManifestChildName,
    DataRelativePathRepairPlanManifestRecord? Manifest,
    LinuxOpenedFileIncarnationResult?
        ManifestIncarnation,
    long? Length,
    string? Error
)
{
    public bool Success =>
        State ==
        DataRelativePathRepairPlanManifestReadState.Read &&
        Manifest is not null;

    public LinuxFileIncarnationIdentity?
        ManifestIncarnationIdentity =>
            ManifestIncarnation?.Identity;
}

public static class DataRelativePathRepairPlanManifestReader
{
    /*
     * A single path-repair manifest is expected to remain small.
     * Keep the same defensive upper bound used by operation
     * journals.
     */
    public const long MaxManifestBytes =
        4L * 1024L * 1024L;

    public static DataRelativePathRepairPlanManifestReaderResult
        Read(
            LinuxNoFollowPathHandle manifestDirectory,
            string manifestChildName)
    {
        ArgumentNullException.ThrowIfNull(
            manifestDirectory
        );

        return Read(
            (ILinuxOpenedHandle)manifestDirectory,
            manifestChildName
        );
    }

    public static DataRelativePathRepairPlanManifestReaderResult
        Read(
            ILinuxOpenedHandle manifestDirectory,
            string manifestChildName)
    {
        ArgumentNullException.ThrowIfNull(
            manifestDirectory
        );

        if (
            !IsValidChildName(
                manifestChildName))
        {
            return Result(
                DataRelativePathRepairPlanManifestReadState
                    .InvalidManifestName,
                manifestChildName,
                error:
                    "The manifest name must identify exactly one " +
                    "direct child."
            );
        }

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                DataRelativePathRepairPlanManifestReadState
                    .UnsupportedPlatform,
                manifestChildName,
                error:
                    "Descriptor-backed plan-manifest reading is " +
                    "supported on Linux only."
            );
        }

        LinuxOpenChildReadOnlyAtResult opened =
            LinuxOpenChildReadOnlyAt.Open(
                manifestDirectory,
                manifestChildName
            );

        if (!opened.Success)
        {
            DataRelativePathRepairPlanManifestReadState state =
                opened.State switch
                {
                    LinuxOpenChildReadOnlyAtState
                        .UnsupportedPlatform =>
                            DataRelativePathRepairPlanManifestReadState
                                .UnsupportedPlatform,

                    LinuxOpenChildReadOnlyAtState
                        .InvalidParentHandle =>
                            DataRelativePathRepairPlanManifestReadState
                                .InvalidManifestDirectoryHandle,

                    LinuxOpenChildReadOnlyAtState
                        .ParentNotDirectory =>
                            DataRelativePathRepairPlanManifestReadState
                                .ManifestDirectoryNotDirectory,

                    LinuxOpenChildReadOnlyAtState
                        .ChildUnavailable =>
                            DataRelativePathRepairPlanManifestReadState
                                .ManifestUnavailable,

                    LinuxOpenChildReadOnlyAtState
                        .ChildSymbolicLinkRejected =>
                            DataRelativePathRepairPlanManifestReadState
                                .ManifestSymbolicLinkRejected,

                    _ =>
                        DataRelativePathRepairPlanManifestReadState
                            .ManifestOpenFailed
                };

            return Result(
                state,
                manifestChildName,
                error:
                    opened.Error ??
                    opened.State.ToString()
            );
        }

        using LinuxOpenedChildHandle manifest =
            opened.OpenedChild!;

        LinuxOpenedFileIncarnationResult incarnation =
            LinuxOpenedFileIncarnation.Capture(
                manifest
            );

        if (!incarnation.Success)
        {
            return Result(
                incarnation.State ==
                LinuxOpenedFileIncarnationState.NotRegularFile
                    ? DataRelativePathRepairPlanManifestReadState
                        .ManifestNotRegularFile
                    : DataRelativePathRepairPlanManifestReadState
                        .ManifestIdentityFailed,
                manifestChildName,
                manifestIncarnation:
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
                    manifest.Handle
                );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathRepairPlanManifestReadState
                    .ManifestLengthUnavailable,
                manifestChildName,
                manifestIncarnation:
                    incarnation,
                error:
                    ex.Message
            );
        }

        if (
            length < 0 ||
            length > MaxManifestBytes)
        {
            return Result(
                DataRelativePathRepairPlanManifestReadState
                    .ManifestTooLarge,
                manifestChildName,
                manifestIncarnation:
                    incarnation,
                length:
                    length,
                error:
                    $"Manifest length {length} exceeds the supported " +
                    $"limit of {MaxManifestBytes} bytes."
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
                        manifest.Handle,
                        bytes.AsSpan(
                            offset
                        ),
                        fileOffset:
                            offset
                    );

                if (read == 0)
                {
                    return Result(
                        DataRelativePathRepairPlanManifestReadState
                            .UnexpectedEndOfFile,
                        manifestChildName,
                        manifestIncarnation:
                            incarnation,
                        length:
                            length,
                        error:
                            "The opened manifest reached EOF before " +
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
                DataRelativePathRepairPlanManifestReadState
                    .ReadFailed,
                manifestChildName,
                manifestIncarnation:
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
                    manifest.Handle
                );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathRepairPlanManifestReadState
                    .ManifestLengthUnavailable,
                manifestChildName,
                manifestIncarnation:
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
                DataRelativePathRepairPlanManifestReadState
                    .LengthChangedDuringRead,
                manifestChildName,
                manifestIncarnation:
                    incarnation,
                length:
                    lengthAfterRead,
                error:
                    "The manifest file length changed while its " +
                    "opened descriptor was being read."
            );
        }

        DataRelativePathRepairPlanManifestRecord? record;

        try
        {
            record =
                DataRelativePathRepairPlanManifestJson
                    .Deserialize(
                        bytes
                    );
        }
        catch (JsonException ex)
        {
            return Result(
                DataRelativePathRepairPlanManifestReadState
                    .DeserializeFailed,
                manifestChildName,
                manifestIncarnation:
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
                DataRelativePathRepairPlanManifestReadState
                    .DeserializeFailed,
                manifestChildName,
                manifestIncarnation:
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
                DataRelativePathRepairPlanManifestReadState
                    .DeserializeFailed,
                manifestChildName,
                manifestIncarnation:
                    incarnation,
                length:
                    length,
                error:
                    "The manifest JSON produced no record."
            );
        }

        string? validationError =
            DataRelativePathRepairPlanManifest.Validate(
                record
            );

        if (validationError is not null)
        {
            return Result(
                DataRelativePathRepairPlanManifestReadState
                    .ManifestInvalid,
                manifestChildName,
                manifestIncarnation:
                    incarnation,
                length:
                    length,
                error:
                    validationError
            );
        }

        return Result(
            DataRelativePathRepairPlanManifestReadState.Read,
            manifestChildName,
            manifest:
                record,
            manifestIncarnation:
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
        DataRelativePathRepairPlanManifestReaderResult
        Result(
            DataRelativePathRepairPlanManifestReadState state,
            string? manifestChildName,
            DataRelativePathRepairPlanManifestRecord?
                manifest = null,
            LinuxOpenedFileIncarnationResult?
                manifestIncarnation = null,
            long? length = null,
            string? error = null)
    {
        return new(
            State:
                state,
            ManifestChildName:
                manifestChildName ??
                string.Empty,
            Manifest:
                manifest,
            ManifestIncarnation:
                manifestIncarnation,
            Length:
                length,
            Error:
                error
        );
    }
}
