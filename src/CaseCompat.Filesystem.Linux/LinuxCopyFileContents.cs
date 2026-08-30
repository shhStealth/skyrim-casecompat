using Microsoft.Win32.SafeHandles;
using System.Security.Cryptography;

namespace CaseCompat.Filesystem.Linux;

public static class LinuxCopyFileContents
{
    public static LinuxCopyFileContentsResult CopyAndVerify(
        ILinuxOpenedHandle source,
        ILinuxOpenedHandle destination,
        long expectedSize,
        string expectedSha256)
    {
        ArgumentNullException.ThrowIfNull(
            source
        );

        ArgumentNullException.ThrowIfNull(
            destination
        );

        if (expectedSize < 0)
        {
            return Result(
                LinuxCopyFileContentsState
                    .InvalidExpectedSize,
                expectedSize,
                expectedSha256,
                error:
                    "The expected source size cannot be negative."
            );
        }

        if (!IsSha256(expectedSha256))
        {
            return Result(
                LinuxCopyFileContentsState
                    .InvalidExpectedSha256,
                expectedSize,
                expectedSha256,
                error:
                    "The expected SHA-256 must contain exactly " +
                    "64 hexadecimal characters."
            );
        }

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                LinuxCopyFileContentsState
                    .UnsupportedPlatform,
                expectedSize,
                expectedSha256,
                error:
                    "Descriptor-to-descriptor verified copying " +
                    "is supported on Linux only."
            );
        }

        SafeFileHandle sourceHandle =
            source.Handle;

        SafeFileHandle destinationHandle =
            destination.Handle;

        if (
            sourceHandle.IsInvalid ||
            sourceHandle.IsClosed)
        {
            return Result(
                LinuxCopyFileContentsState
                    .InvalidSourceHandle,
                expectedSize,
                expectedSha256,
                error:
                    "The source file handle is invalid or closed."
            );
        }

        if (
            destinationHandle.IsInvalid ||
            destinationHandle.IsClosed)
        {
            return Result(
                LinuxCopyFileContentsState
                    .InvalidDestinationHandle,
                expectedSize,
                expectedSha256,
                error:
                    "The destination file handle is invalid or closed."
            );
        }

        bool sourceRef =
            false;

        bool destinationRef =
            false;

        long bytesCopied =
            0;

        string? actualSha256 =
            null;

        try
        {
            try
            {
                sourceHandle.DangerousAddRef(
                    ref sourceRef
                );
            }
            catch (ObjectDisposedException ex)
            {
                return Result(
                    LinuxCopyFileContentsState
                        .InvalidSourceHandle,
                    expectedSize,
                    expectedSha256,
                    error:
                        ex.Message
                );
            }

            try
            {
                destinationHandle.DangerousAddRef(
                    ref destinationRef
                );
            }
            catch (ObjectDisposedException ex)
            {
                return Result(
                    LinuxCopyFileContentsState
                        .InvalidDestinationHandle,
                    expectedSize,
                    expectedSha256,
                    error:
                        ex.Message
                );
            }

            long sourceLength;

            try
            {
                sourceLength =
                    RandomAccess.GetLength(
                        sourceHandle
                    );
            }
            catch (Exception ex)
            {
                return Result(
                    LinuxCopyFileContentsState
                        .SourceLengthUnavailable,
                    expectedSize,
                    expectedSha256,
                    error:
                        ex.Message
                );
            }

            if (
                sourceLength !=
                expectedSize)
            {
                return Result(
                    LinuxCopyFileContentsState
                        .SourceSizeChanged,
                    expectedSize,
                    expectedSha256,
                    error:
                        "The opened source size no longer " +
                        "matches the projected size."
                );
            }

            long destinationLength;

            try
            {
                destinationLength =
                    RandomAccess.GetLength(
                        destinationHandle
                    );
            }
            catch (Exception ex)
            {
                return Result(
                    LinuxCopyFileContentsState
                        .DestinationLengthUnavailable,
                    expectedSize,
                    expectedSha256,
                    error:
                        ex.Message
                );
            }

            if (destinationLength != 0)
            {
                return Result(
                    LinuxCopyFileContentsState
                        .DestinationNotEmpty,
                    expectedSize,
                    expectedSha256,
                    error:
                        "The destination descriptor must refer " +
                        "to an empty newly-created file."
                );
            }

            const int BufferSize =
                128 * 1024;

            byte[] buffer =
                new byte[BufferSize];

            using IncrementalHash hash =
                IncrementalHash.CreateHash(
                    HashAlgorithmName.SHA256
                );

            long offset =
                0;

            while (offset < expectedSize)
            {
                int requested =
                    (int)Math.Min(
                        buffer.Length,
                        expectedSize - offset
                    );

                int read;

                try
                {
                    read =
                        RandomAccess.Read(
                            sourceHandle,
                            buffer.AsSpan(
                                0,
                                requested
                            ),
                            offset
                        );
                }
                catch (Exception ex)
                {
                    return Result(
                        LinuxCopyFileContentsState
                            .ReadFailed,
                        expectedSize,
                        expectedSha256,
                        bytesCopied,
                        actualSha256,
                        ex.Message
                    );
                }

                if (read == 0)
                {
                    return Result(
                        LinuxCopyFileContentsState
                            .UnexpectedEndOfSource,
                        expectedSize,
                        expectedSha256,
                        bytesCopied,
                        actualSha256,
                        "Unexpected end of source while copying."
                    );
                }

                try
                {
                    RandomAccess.Write(
                        destinationHandle,
                        buffer.AsSpan(
                            0,
                            read
                        ),
                        offset
                    );
                }
                catch (Exception ex)
                {
                    return Result(
                        LinuxCopyFileContentsState
                            .WriteFailed,
                        expectedSize,
                        expectedSha256,
                        bytesCopied,
                        actualSha256,
                        ex.Message
                    );
                }

                // Hash only after the entire span was successfully
                // written. RandomAccess.Write either writes the full
                // span or throws.
                hash.AppendData(
                    buffer,
                    0,
                    read
                );

                offset +=
                    read;

                bytesCopied +=
                    read;
            }

            byte[] digest =
                hash.GetHashAndReset();

            actualSha256 =
                Convert.ToHexString(
                    digest
                );

            long sourceLengthAfter;

            try
            {
                sourceLengthAfter =
                    RandomAccess.GetLength(
                        sourceHandle
                    );
            }
            catch (Exception ex)
            {
                return Result(
                    LinuxCopyFileContentsState
                        .SourceLengthUnavailable,
                    expectedSize,
                    expectedSha256,
                    bytesCopied,
                    actualSha256,
                    ex.Message
                );
            }

            if (
                sourceLengthAfter !=
                expectedSize)
            {
                return Result(
                    LinuxCopyFileContentsState
                        .SourceSizeChangedDuringCopy,
                    expectedSize,
                    expectedSha256,
                    bytesCopied,
                    actualSha256,
                    "The source size changed while it was " +
                    "being copied."
                );
            }

            long destinationLengthAfter;

            try
            {
                destinationLengthAfter =
                    RandomAccess.GetLength(
                        destinationHandle
                    );
            }
            catch (Exception ex)
            {
                return Result(
                    LinuxCopyFileContentsState
                        .DestinationLengthUnavailable,
                    expectedSize,
                    expectedSha256,
                    bytesCopied,
                    actualSha256,
                    ex.Message
                );
            }

            if (
                destinationLengthAfter !=
                expectedSize)
            {
                return Result(
                    LinuxCopyFileContentsState
                        .DestinationSizeMismatch,
                    expectedSize,
                    expectedSha256,
                    bytesCopied,
                    actualSha256,
                    "The destination size does not match " +
                    "the expected copied byte count."
                );
            }

            if (
                !string.Equals(
                    actualSha256,
                    expectedSha256,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return Result(
                    LinuxCopyFileContentsState
                        .HashMismatch,
                    expectedSize,
                    expectedSha256,
                    bytesCopied,
                    actualSha256,
                    "The bytes copied from the source do not " +
                    "match the projected SHA-256."
                );
            }

            return Result(
                LinuxCopyFileContentsState
                    .CopiedAndVerified,
                expectedSize,
                expectedSha256,
                bytesCopied,
                actualSha256
            );
        }
        finally
        {
            if (destinationRef)
            {
                destinationHandle.DangerousRelease();
            }

            if (sourceRef)
            {
                sourceHandle.DangerousRelease();
            }
        }
    }

    private static bool IsSha256(
        string? value)
    {
        if (
            value is null ||
            value.Length != 64)
        {
            return false;
        }

        foreach (
            char character
            in value)
        {
            bool hexadecimal =
                character is >= '0' and <= '9' ||
                character is >= 'A' and <= 'F' ||
                character is >= 'a' and <= 'f';

            if (!hexadecimal)
            {
                return false;
            }
        }

        return true;
    }

    private static LinuxCopyFileContentsResult Result(
        LinuxCopyFileContentsState state,
        long expectedSize,
        string? expectedSha256,
        long bytesCopied = 0,
        string? actualSha256 = null,
        string? error = null)
    {
        return new LinuxCopyFileContentsResult(
            State:
                state,
            ExpectedSize:
                expectedSize,
            ExpectedSha256:
                expectedSha256 ?? string.Empty,
            BytesCopied:
                bytesCopied,
            ActualSha256:
                actualSha256,
            Error:
                error
        );
    }
}
