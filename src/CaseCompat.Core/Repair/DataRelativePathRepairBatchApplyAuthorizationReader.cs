using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;
using System.Text.Json;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairBatchApplyAuthorizationReadState
{
    Read,

    InvalidAuthorizationName,
    UnsupportedPlatform,
    InvalidAuthorizationDirectoryHandle,
    AuthorizationDirectoryNotDirectory,

    AuthorizationUnavailable,
    AuthorizationSymbolicLinkRejected,
    AuthorizationOpenFailed,

    AuthorizationIdentityFailed,
    AuthorizationNotRegularFile,

    AuthorizationLengthUnavailable,
    AuthorizationTooLarge,
    UnexpectedEndOfFile,
    ReadFailed,
    LengthChangedDuringRead,

    DeserializeFailed,
    AuthorizationInvalid
}

public sealed record DataRelativePathRepairBatchApplyAuthorizationReaderResult(
    DataRelativePathRepairBatchApplyAuthorizationReadState State,
    string AuthorizationChildName,
    DataRelativePathRepairBatchApplyAuthorizationRecord? Authorization,
    LinuxOpenedFileIncarnationResult?
        AuthorizationIncarnation,
    long? Length,
    string? AuthorizationSha256,
    string? Error
)
{
    public bool Success =>
        State ==
        DataRelativePathRepairBatchApplyAuthorizationReadState.Read &&
        Authorization is not null &&
        AuthorizationSha256 is not null;

    public LinuxFileIncarnationIdentity?
        AuthorizationIncarnationIdentity =>
            AuthorizationIncarnation?.Identity;
}

public static class DataRelativePathRepairBatchApplyAuthorizationReader
{
    /*
     * A repair batch authorization is expected to remain bounded and small
     * enough for defensive in-memory validation. Keep the same defensive
     * upper bound used by operation journals.
     */
    public const long MaxAuthorizationBytes =
        4L * 1024L * 1024L;

    public static DataRelativePathRepairBatchApplyAuthorizationReaderResult
        Read(
            LinuxNoFollowPathHandle authorizationDirectory,
            string authorizationChildName)
    {
        ArgumentNullException.ThrowIfNull(
            authorizationDirectory
        );

        return Read(
            (ILinuxOpenedHandle)authorizationDirectory,
            authorizationChildName
        );
    }

    public static DataRelativePathRepairBatchApplyAuthorizationReaderResult
        Read(
            ILinuxOpenedHandle authorizationDirectory,
            string authorizationChildName)
    {
        ArgumentNullException.ThrowIfNull(
            authorizationDirectory
        );

        if (
            !IsValidChildName(
                authorizationChildName))
        {
            return Result(
                DataRelativePathRepairBatchApplyAuthorizationReadState
                    .InvalidAuthorizationName,
                authorizationChildName,
                error:
                    "The authorization name must identify exactly one " +
                    "direct child."
            );
        }

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                DataRelativePathRepairBatchApplyAuthorizationReadState
                    .UnsupportedPlatform,
                authorizationChildName,
                error:
                    "Descriptor-backed batch-authorization reading is " +
                    "supported on Linux only."
            );
        }

        LinuxOpenChildReadOnlyAtResult opened =
            LinuxOpenChildReadOnlyAt.Open(
                authorizationDirectory,
                authorizationChildName
            );

        if (!opened.Success)
        {
            DataRelativePathRepairBatchApplyAuthorizationReadState state =
                opened.State switch
                {
                    LinuxOpenChildReadOnlyAtState
                        .UnsupportedPlatform =>
                            DataRelativePathRepairBatchApplyAuthorizationReadState
                                .UnsupportedPlatform,

                    LinuxOpenChildReadOnlyAtState
                        .InvalidParentHandle =>
                            DataRelativePathRepairBatchApplyAuthorizationReadState
                                .InvalidAuthorizationDirectoryHandle,

                    LinuxOpenChildReadOnlyAtState
                        .ParentNotDirectory =>
                            DataRelativePathRepairBatchApplyAuthorizationReadState
                                .AuthorizationDirectoryNotDirectory,

                    LinuxOpenChildReadOnlyAtState
                        .ChildUnavailable =>
                            DataRelativePathRepairBatchApplyAuthorizationReadState
                                .AuthorizationUnavailable,

                    LinuxOpenChildReadOnlyAtState
                        .ChildSymbolicLinkRejected =>
                            DataRelativePathRepairBatchApplyAuthorizationReadState
                                .AuthorizationSymbolicLinkRejected,

                    _ =>
                        DataRelativePathRepairBatchApplyAuthorizationReadState
                            .AuthorizationOpenFailed
                };

            return Result(
                state,
                authorizationChildName,
                error:
                    opened.Error ??
                    opened.State.ToString()
            );
        }

        using LinuxOpenedChildHandle authorization =
            opened.OpenedChild!;

        LinuxOpenedFileIncarnationResult incarnation =
            LinuxOpenedFileIncarnation.Capture(
                authorization
            );

        if (!incarnation.Success)
        {
            return Result(
                incarnation.State ==
                LinuxOpenedFileIncarnationState.NotRegularFile
                    ? DataRelativePathRepairBatchApplyAuthorizationReadState
                        .AuthorizationNotRegularFile
                    : DataRelativePathRepairBatchApplyAuthorizationReadState
                        .AuthorizationIdentityFailed,
                authorizationChildName,
                authorizationIncarnation:
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
                    authorization.Handle
                );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathRepairBatchApplyAuthorizationReadState
                    .AuthorizationLengthUnavailable,
                authorizationChildName,
                authorizationIncarnation:
                    incarnation,
                error:
                    ex.Message
            );
        }

        if (
            length < 0 ||
            length > MaxAuthorizationBytes)
        {
            return Result(
                DataRelativePathRepairBatchApplyAuthorizationReadState
                    .AuthorizationTooLarge,
                authorizationChildName,
                authorizationIncarnation:
                    incarnation,
                length:
                    length,
                error:
                    $"Authorization length {length} exceeds the supported " +
                    $"limit of {MaxAuthorizationBytes} bytes."
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
                        authorization.Handle,
                        bytes.AsSpan(
                            offset
                        ),
                        fileOffset:
                            offset
                    );

                if (read == 0)
                {
                    return Result(
                        DataRelativePathRepairBatchApplyAuthorizationReadState
                            .UnexpectedEndOfFile,
                        authorizationChildName,
                        authorizationIncarnation:
                            incarnation,
                        length:
                            length,
                        error:
                            "The opened authorization reached EOF before " +
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
                DataRelativePathRepairBatchApplyAuthorizationReadState
                    .ReadFailed,
                authorizationChildName,
                authorizationIncarnation:
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
                    authorization.Handle
                );
        }
        catch (Exception ex)
        {
            return Result(
                DataRelativePathRepairBatchApplyAuthorizationReadState
                    .AuthorizationLengthUnavailable,
                authorizationChildName,
                authorizationIncarnation:
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
                DataRelativePathRepairBatchApplyAuthorizationReadState
                    .LengthChangedDuringRead,
                authorizationChildName,
                authorizationIncarnation:
                    incarnation,
                length:
                    lengthAfterRead,
                error:
                    "The authorization file length changed while its " +
                    "opened descriptor was being read."
            );
        }

        DataRelativePathRepairBatchApplyAuthorizationRecord? record;

        try
        {
            record =
                DataRelativePathRepairBatchApplyAuthorizationJson
                    .Deserialize(
                        bytes
                    );
        }
        catch (JsonException ex)
        {
            return Result(
                DataRelativePathRepairBatchApplyAuthorizationReadState
                    .DeserializeFailed,
                authorizationChildName,
                authorizationIncarnation:
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
                DataRelativePathRepairBatchApplyAuthorizationReadState
                    .DeserializeFailed,
                authorizationChildName,
                authorizationIncarnation:
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
                DataRelativePathRepairBatchApplyAuthorizationReadState
                    .DeserializeFailed,
                authorizationChildName,
                authorizationIncarnation:
                    incarnation,
                length:
                    length,
                error:
                    "The authorization JSON produced no record."
            );
        }

        string? validationError =
            DataRelativePathRepairBatchApplyAuthorization.Validate(
                record
            );

        if (validationError is not null)
        {
            return Result(
                DataRelativePathRepairBatchApplyAuthorizationReadState
                    .AuthorizationInvalid,
                authorizationChildName,
                authorizationIncarnation:
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
         * produced the validated authorization record.
         */
        string authorizationSha256 =
            Convert.ToHexString(
                SHA256.HashData(
                    bytes
                )
            );

        return Result(
            DataRelativePathRepairBatchApplyAuthorizationReadState.Read,
            authorizationChildName,
            authorization:
                record,
            authorizationIncarnation:
                incarnation,
            length:
                length,
            authorizationSha256:
                authorizationSha256
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
        DataRelativePathRepairBatchApplyAuthorizationReaderResult
        Result(
            DataRelativePathRepairBatchApplyAuthorizationReadState state,
            string? authorizationChildName,
            DataRelativePathRepairBatchApplyAuthorizationRecord?
                authorization = null,
            LinuxOpenedFileIncarnationResult?
                authorizationIncarnation = null,
            long? length = null,
            string? authorizationSha256 = null,
            string? error = null)
    {
        return new(
            State:
                state,
            AuthorizationChildName:
                authorizationChildName ??
                string.Empty,
            Authorization:
                authorization,
            AuthorizationIncarnation:
                authorizationIncarnation,
            Length:
                length,
            AuthorizationSha256:
                authorizationSha256,
            Error:
                error
        );
    }
}
