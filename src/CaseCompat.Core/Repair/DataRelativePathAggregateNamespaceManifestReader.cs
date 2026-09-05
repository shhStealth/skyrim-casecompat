using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;
using System.Text.Json;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathAggregateNamespaceManifestReadState
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

public sealed record DataRelativePathAggregateNamespaceManifestReaderResult(
    DataRelativePathAggregateNamespaceManifestReadState State,
    string ManifestChildName,
    DataRelativePathAggregateNamespaceManifestRecord? Manifest,
    LinuxOpenedFileIncarnationResult?
        ManifestIncarnation,
    long? Length,
    string? ManifestSha256,
    string? Error
)
{
    public bool Success =>
        State ==
        DataRelativePathAggregateNamespaceManifestReadState.Read &&
        Manifest is not null &&
        ManifestSha256 is not null;

    public LinuxFileIncarnationIdentity?
        ManifestIncarnationIdentity =>
            ManifestIncarnation?.Identity;
}

public static class DataRelativePathAggregateNamespaceManifestReader
{
    /*
     * Aggregate namespace evidence can represent a very large physical
     * namespace, so the compact batch-manifest 4 MiB ceiling is not
     * appropriate here.
     *
     * Reading remains explicitly bounded because the validated exact-byte
     * representation is held in memory and SHA-bound. This limit is
     * intentionally conservative until real aggregate sidecar sizes are
     * measured during later planning-only integration.
     */
    public const long MaxManifestBytes =
        512L * 1024L * 1024L;

    public static DataRelativePathAggregateNamespaceManifestReaderResult
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

    public static DataRelativePathAggregateNamespaceManifestReaderResult
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
                DataRelativePathAggregateNamespaceManifestReadState
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
                DataRelativePathAggregateNamespaceManifestReadState
                    .UnsupportedPlatform,
                manifestChildName,
                error:
                    "Descriptor-backed aggregate-namespace manifest reading is " +
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
            DataRelativePathAggregateNamespaceManifestReadState state =
                opened.State switch
                {
                    LinuxOpenChildReadOnlyAtState
                        .UnsupportedPlatform =>
                            DataRelativePathAggregateNamespaceManifestReadState
                                .UnsupportedPlatform,

                    LinuxOpenChildReadOnlyAtState
                        .InvalidParentHandle =>
                            DataRelativePathAggregateNamespaceManifestReadState
                                .InvalidManifestDirectoryHandle,

                    LinuxOpenChildReadOnlyAtState
                        .ParentNotDirectory =>
                            DataRelativePathAggregateNamespaceManifestReadState
                                .ManifestDirectoryNotDirectory,

                    LinuxOpenChildReadOnlyAtState
                        .ChildUnavailable =>
                            DataRelativePathAggregateNamespaceManifestReadState
                                .ManifestUnavailable,

                    LinuxOpenChildReadOnlyAtState
                        .ChildSymbolicLinkRejected =>
                            DataRelativePathAggregateNamespaceManifestReadState
                                .ManifestSymbolicLinkRejected,

                    _ =>
                        DataRelativePathAggregateNamespaceManifestReadState
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
                    ? DataRelativePathAggregateNamespaceManifestReadState
                        .ManifestNotRegularFile
                    : DataRelativePathAggregateNamespaceManifestReadState
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
                DataRelativePathAggregateNamespaceManifestReadState
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
                DataRelativePathAggregateNamespaceManifestReadState
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
                        DataRelativePathAggregateNamespaceManifestReadState
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
                DataRelativePathAggregateNamespaceManifestReadState
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
                DataRelativePathAggregateNamespaceManifestReadState
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
                DataRelativePathAggregateNamespaceManifestReadState
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

        DataRelativePathAggregateNamespaceManifestRecord? record;

        try
        {
            record =
                DataRelativePathAggregateNamespaceManifestJson
                    .Deserialize(
                        bytes
                    );
        }
        catch (JsonException ex)
        {
            return Result(
                DataRelativePathAggregateNamespaceManifestReadState
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
                DataRelativePathAggregateNamespaceManifestReadState
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
                DataRelativePathAggregateNamespaceManifestReadState
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
            DataRelativePathAggregateNamespaceManifest.Validate(
                record
            );

        if (validationError is not null)
        {
            return Result(
                DataRelativePathAggregateNamespaceManifestReadState
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

        /*
         * Hash the exact in-memory byte sequence that was deserialized
         * and validated above.
         *
         * Do not perform a second descriptor read here: a separate hash
         * pass could observe different same-length contents after an
         * in-place modification and would no longer prove which bytes
         * produced the validated manifest record.
         */
        string manifestSha256 =
            Convert.ToHexString(
                SHA256.HashData(
                    bytes
                )
            );

        return Result(
            DataRelativePathAggregateNamespaceManifestReadState.Read,
            manifestChildName,
            manifest:
                record,
            manifestIncarnation:
                incarnation,
            length:
                length,
            manifestSha256:
                manifestSha256
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
        DataRelativePathAggregateNamespaceManifestReaderResult
        Result(
            DataRelativePathAggregateNamespaceManifestReadState state,
            string? manifestChildName,
            DataRelativePathAggregateNamespaceManifestRecord?
                manifest = null,
            LinuxOpenedFileIncarnationResult?
                manifestIncarnation = null,
            long? length = null,
            string? manifestSha256 = null,
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
            ManifestSha256:
                manifestSha256,
            Error:
                error
        );
    }
}
