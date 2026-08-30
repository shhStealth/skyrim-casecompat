using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace CaseCompat.Filesystem.Linux;

public static class LinuxOpenedFileSnapshot
{
    // Linux uapi:
    // #define AT_EMPTY_PATH 0x1000
    private const int AtEmptyPath = 0x1000;

    private const uint StatxBasicStats = 0x000007ff;
    private const uint StatxMountId = 0x00001000;

    // Linux inode type bits.
    private const ushort SIfmt = 0xF000;
    private const ushort SIfreg = 0x8000;

    [StructLayout(
        LayoutKind.Explicit,
        Size = 256)]
    private struct StatxBuffer
    {
        [FieldOffset(0)]
        public uint Mask;

        [FieldOffset(16)]
        public uint LinkCount;

        [FieldOffset(28)]
        public ushort Mode;

        [FieldOffset(32)]
        public ulong Inode;

        [FieldOffset(40)]
        public ulong Size;

        [FieldOffset(136)]
        public uint DeviceMajor;

        [FieldOffset(140)]
        public uint DeviceMinor;

        [FieldOffset(144)]
        public ulong MountId;
    }

    [DllImport(
        "libc",
        EntryPoint = "statx",
        SetLastError = true)]
    private static extern int Statx(
        int dirfd,
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        string pathname,
        int flags,
        uint mask,
        out StatxBuffer statxbuf
    );

    public static LinuxOpenedFileSnapshotResult Capture(
        LinuxNoFollowPathHandle openedPath)
    {
        ArgumentNullException.ThrowIfNull(
            openedPath
        );

        return Capture(
            openedPath,
            openedPath.FullPath
        );
    }

    public static LinuxOpenedFileSnapshotResult Capture(
        ILinuxOpenedHandle openedHandle,
        string displayPath)
    {
        ArgumentNullException.ThrowIfNull(
            openedHandle
        );

        ArgumentNullException.ThrowIfNull(
            displayPath
        );

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                LinuxOpenedFileSnapshotState
                    .UnsupportedPlatform,
                displayPath,
                error:
                    "Opened-file snapshotting is " +
                    "supported on Linux only."
            );
        }

        SafeFileHandle handle =
            openedHandle.Handle;

        if (
            handle.IsInvalid ||
            handle.IsClosed)
        {
            return Result(
                LinuxOpenedFileSnapshotState
                    .InvalidHandle,
                displayPath,
                error:
                    "The opened file handle is invalid or closed."
            );
        }

        bool addedRef =
            false;

        try
        {
            handle.DangerousAddRef(
                ref addedRef
            );

            int fd =
                checked(
                    (int)handle
                        .DangerousGetHandle()
                        .ToInt64()
                );

            if (
                Statx(
                    fd,
                    string.Empty,
                    AtEmptyPath,
                    StatxBasicStats |
                    StatxMountId,
                    out StatxBuffer metadata
                ) < 0)
            {
                int errno =
                    Marshal.GetLastPInvokeError();

                return Result(
                    LinuxOpenedFileSnapshotState
                        .MetadataUnavailable,
                    displayPath,
                    errno:
                        errno
                );
            }

            if (
                (metadata.Mode & SIfmt) !=
                SIfreg)
            {
                return Result(
                    LinuxOpenedFileSnapshotState
                        .NotRegularFile,
                    displayPath,
                    error:
                        "The opened target is not a regular file."
                );
            }

            if (
                metadata.Size >
                long.MaxValue)
            {
                return Result(
                    LinuxOpenedFileSnapshotState
                        .SizeUnavailable,
                    displayPath,
                    error:
                        "The opened file size cannot be " +
                        "represented by System.Int64."
                );
            }

            long size =
                (long)metadata.Size;

            var identity =
                new LinuxFileIdentityResult(
                    FullPath:
                        displayPath,
                    DeviceMajor:
                        metadata.DeviceMajor,
                    DeviceMinor:
                        metadata.DeviceMinor,
                    Inode:
                        metadata.Inode,
                    LinkCount:
                        metadata.LinkCount,
                    MountId:
                        metadata.MountId,
                    Error:
                        null
                );

            string sha256;

            try
            {
                sha256 =
                    ComputeSha256(
                        handle,
                        size
                    );
            }
            catch (Exception ex)
            {
                return Result(
                    LinuxOpenedFileSnapshotState
                        .HashFailed,
                    displayPath,
                    identity:
                        identity,
                    size:
                        size,
                    error:
                        ex.Message
                );
            }

            long sizeAfterHash;

            try
            {
                sizeAfterHash =
                    RandomAccess.GetLength(
                        handle
                    );
            }
            catch (Exception ex)
            {
                return Result(
                    LinuxOpenedFileSnapshotState
                        .SizeUnavailable,
                    displayPath,
                    identity:
                        identity,
                    size:
                        size,
                    error:
                        ex.Message
                );
            }

            if (sizeAfterHash != size)
            {
                return Result(
                    LinuxOpenedFileSnapshotState
                        .SizeChangedDuringHash,
                    displayPath,
                    identity:
                        identity,
                    size:
                        sizeAfterHash,
                    error:
                        "The opened file size changed while " +
                        "its contents were being hashed."
                );
            }

            return new LinuxOpenedFileSnapshotResult(
                State:
                    LinuxOpenedFileSnapshotState
                        .Captured,
                FullPath:
                    displayPath,
                Identity:
                    identity,
                Size:
                    size,
                Sha256:
                    sha256,
                Errno:
                    null,
                Error:
                    null
            );
        }
        catch (
            ObjectDisposedException ex)
        {
            return Result(
                LinuxOpenedFileSnapshotState
                    .InvalidHandle,
                displayPath,
                error:
                    ex.Message
            );
        }
        catch (
            OverflowException ex)
        {
            return Result(
                LinuxOpenedFileSnapshotState
                    .InvalidHandle,
                displayPath,
                error:
                    ex.Message
            );
        }
        finally
        {
            if (addedRef)
            {
                handle.DangerousRelease();
            }
        }
    }

    private static string ComputeSha256(
        SafeFileHandle handle,
        long expectedSize)
    {
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

            int read =
                RandomAccess.Read(
                    handle,
                    buffer.AsSpan(
                        0,
                        requested
                    ),
                    offset
                );

            if (read == 0)
            {
                throw new IOException(
                    "Unexpected end of file while hashing " +
                    "the opened descriptor."
                );
            }

            hash.AppendData(
                buffer,
                0,
                read
            );

            offset +=
                read;
        }

        byte[] digest =
            hash.GetHashAndReset();

        return Convert.ToHexString(
            digest
        );
    }

    private static LinuxOpenedFileSnapshotResult Result(
        LinuxOpenedFileSnapshotState state,
        string fullPath,
        LinuxFileIdentityResult? identity = null,
        long? size = null,
        int? errno = null,
        string? error = null)
    {
        if (
            error is null &&
            errno is int value)
        {
            error =
                new Win32Exception(
                    value
                ).Message;
        }

        return new LinuxOpenedFileSnapshotResult(
            State:
                state,
            FullPath:
                fullPath,
            Identity:
                identity,
            Size:
                size,
            Sha256:
                null,
            Errno:
                errno,
            Error:
                error
        );
    }
}
