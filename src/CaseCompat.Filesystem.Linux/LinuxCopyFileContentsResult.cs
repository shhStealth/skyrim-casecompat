namespace CaseCompat.Filesystem.Linux;

public enum LinuxCopyFileContentsState
{
    CopiedAndVerified,

    UnsupportedPlatform,
    InvalidExpectedSize,
    InvalidExpectedSha256,

    InvalidSourceHandle,
    InvalidDestinationHandle,

    SourceLengthUnavailable,
    SourceSizeChanged,

    DestinationLengthUnavailable,
    DestinationNotEmpty,

    ReadFailed,
    UnexpectedEndOfSource,
    WriteFailed,

    SourceSizeChangedDuringCopy,
    DestinationSizeMismatch,
    HashMismatch
}

public sealed record LinuxCopyFileContentsResult(
    LinuxCopyFileContentsState State,
    long ExpectedSize,
    string ExpectedSha256,
    long BytesCopied,
    string? ActualSha256,
    string? Error
)
{
    public bool Success =>
        State ==
            LinuxCopyFileContentsState.CopiedAndVerified;
}
