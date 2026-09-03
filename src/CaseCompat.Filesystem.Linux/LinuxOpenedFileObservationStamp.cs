using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CaseCompat.Filesystem.Linux;

/*
 * Capture read-only observational change metadata from an already retained
 * regular-file descriptor.
 *
 * No pathname is consulted by this primitive.
 *
 * This stamp is intentionally observational rather than a write-exclusion
 * mechanism. A future stable-content observer can capture one stamp before
 * hashing and another afterward, rejecting the content evidence whenever
 * the stamps differ.
 */
public static class LinuxOpenedFileObservationStamp
{
    // Linux uapi:
    // #define AT_EMPTY_PATH 0x1000
    private const int AtEmptyPath =
        0x1000;

    /*
     * Ask remote filesystems to synchronize attributes rather than knowingly
     * accepting an approximate cached timestamp observation.
     *
     * On ordinary local filesystems this does not imply a network round trip.
     */
    private const int AtStatxForceSync =
        0x2000;

    private const uint StatxMtime =
        0x00000040;

    private const uint StatxCtime =
        0x00000080;

    private const uint StatxSize =
        0x00000200;

    private const uint RequiredMetadataMask =
        StatxMtime |
        StatxCtime |
        StatxSize;

    [StructLayout(
        LayoutKind.Sequential,
        Size = 16)]
    private struct StatxTimestamp
    {
        public long Seconds;
        public uint Nanoseconds;
        public int Reserved;
    }

    [StructLayout(
        LayoutKind.Explicit,
        Size = 256)]
    private struct StatxBuffer
    {
        [FieldOffset(0)]
        public uint Mask;

        [FieldOffset(40)]
        public ulong Size;

        [FieldOffset(96)]
        public StatxTimestamp ChangeTime;

        [FieldOffset(112)]
        public StatxTimestamp ModificationTime;
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

    public static LinuxOpenedFileObservationStampResult Capture(
        ILinuxOpenedHandle openedFile)
    {
        ArgumentNullException.ThrowIfNull(
            openedFile
        );

        LinuxOpenedFileIdentityResult identity =
            LinuxOpenedFileIdentity.Capture(
                openedFile
            );

        if (
            identity.State ==
            LinuxOpenedFileIdentityState
                .UnsupportedPlatform)
        {
            return Result(
                LinuxOpenedFileObservationStampState
                    .UnsupportedPlatform,
                identity:
                    identity,
                errno:
                    identity.Errno,
                error:
                    identity.Error ??
                    identity.State.ToString()
            );
        }

        if (
            identity.State ==
            LinuxOpenedFileIdentityState
                .InvalidHandle)
        {
            return Result(
                LinuxOpenedFileObservationStampState
                    .InvalidHandle,
                identity:
                    identity,
                errno:
                    identity.Errno,
                error:
                    identity.Error ??
                    identity.State.ToString()
            );
        }

        if (
            identity.State ==
            LinuxOpenedFileIdentityState
                .NotRegularFile)
        {
            return Result(
                LinuxOpenedFileObservationStampState
                    .NotRegularFile,
                identity:
                    identity,
                errno:
                    identity.Errno,
                error:
                    identity.Error ??
                    "The opened descriptor does not refer " +
                    "to a regular file."
            );
        }

        if (
            !identity.Success ||
            identity.MountId is null)
        {
            return Result(
                LinuxOpenedFileObservationStampState
                    .IdentityUnavailable,
                identity:
                    identity,
                errno:
                    identity.Errno,
                error:
                    identity.Error ??
                    "Complete opened-file physical identity " +
                    "is unavailable."
            );
        }

        SafeFileHandle handle =
            openedFile.Handle;

        if (
            handle.IsInvalid ||
            handle.IsClosed)
        {
            return Result(
                LinuxOpenedFileObservationStampState
                    .InvalidHandle,
                identity:
                    identity,
                error:
                    "The opened file descriptor is invalid " +
                    "or closed."
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
                    AtEmptyPath |
                    AtStatxForceSync,
                    RequiredMetadataMask,
                    out StatxBuffer metadata
                ) < 0)
            {
                int errno =
                    Marshal.GetLastPInvokeError();

                return Result(
                    LinuxOpenedFileObservationStampState
                        .MetadataUnavailable,
                    identity:
                        identity,
                    errno:
                        errno
                );
            }

            if (
                (metadata.Mask &
                    RequiredMetadataMask) !=
                RequiredMetadataMask)
            {
                return Result(
                    LinuxOpenedFileObservationStampState
                        .MetadataUnavailable,
                    identity:
                        identity,
                    error:
                        "The filesystem did not provide all " +
                        "required size, ctime, and mtime fields."
                );
            }

            if (
                metadata.Size >
                long.MaxValue)
            {
                return Result(
                    LinuxOpenedFileObservationStampState
                        .SizeUnavailable,
                    identity:
                        identity,
                    error:
                        "The opened file size cannot be " +
                        "represented by System.Int64."
                );
            }

            return new LinuxOpenedFileObservationStampResult(
                State:
                    LinuxOpenedFileObservationStampState
                        .Captured,
                Identity:
                    identity,
                Size:
                    (long)metadata.Size,
                ChangeTimeSeconds:
                    metadata.ChangeTime.Seconds,
                ChangeTimeNanoseconds:
                    metadata.ChangeTime.Nanoseconds,
                ModificationTimeSeconds:
                    metadata.ModificationTime.Seconds,
                ModificationTimeNanoseconds:
                    metadata.ModificationTime.Nanoseconds,
                Errno:
                    null,
                Error:
                    null
            );
        }
        catch (ObjectDisposedException ex)
        {
            return Result(
                LinuxOpenedFileObservationStampState
                    .InvalidHandle,
                identity:
                    identity,
                error:
                    ex.Message
            );
        }
        catch (OverflowException ex)
        {
            return Result(
                LinuxOpenedFileObservationStampState
                    .InvalidHandle,
                identity:
                    identity,
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

    private static LinuxOpenedFileObservationStampResult Result(
        LinuxOpenedFileObservationStampState state,
        LinuxOpenedFileIdentityResult? identity = null,
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

        return new LinuxOpenedFileObservationStampResult(
            State:
                state,
            Identity:
                identity,
            Size:
                null,
            ChangeTimeSeconds:
                null,
            ChangeTimeNanoseconds:
                null,
            ModificationTimeSeconds:
                null,
            ModificationTimeNanoseconds:
                null,
            Errno:
                errno,
            Error:
                error
        );
    }
}
