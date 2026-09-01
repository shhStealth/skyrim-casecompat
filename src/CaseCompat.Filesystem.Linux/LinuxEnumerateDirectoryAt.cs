using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace CaseCompat.Filesystem.Linux;

public static class LinuxEnumerateDirectoryAt
{
    private const int ORdonly =
        0;

    private const int ODirectory =
        0x10000;

    private const int ONoFollow =
        0x20000;

    private const int OCloexec =
        0x80000;

    private const int EIntr =
        4;

    private const int EBadF =
        9;

    private const int ENotDir =
        20;

    /*
     * struct linux_dirent64:
     *
     *   ino64_t        d_ino;       // offset 0,  8 bytes
     *   off64_t        d_off;       // offset 8,  8 bytes
     *   unsigned short d_reclen;    // offset 16, 2 bytes
     *   unsigned char  d_type;      // offset 18, 1 byte
     *   char           d_name[];    // offset 19
     *
     * d_reclen includes the terminating NUL and any record padding.
     */
    private const int RecordLengthOffset =
        16;

    private const int NameOffset =
        19;

    private const int MinimumRecordLength =
        NameOffset + 1;

    private const int BufferSize =
        64 * 1024;

    [DllImport(
        "libc",
        EntryPoint = "openat",
        SetLastError = true)]
    private static extern int OpenAt(
        int dirfd,
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        string pathname,
        int flags
    );

    [DllImport(
        "libc",
        EntryPoint = "getdents64",
        SetLastError = true)]
    private static extern nint GetDents64(
        int fd,
        IntPtr directoryEntries,
        nuint count
    );

    [DllImport(
        "libc",
        EntryPoint = "close",
        SetLastError = true)]
    private static extern int Close(
        int fd
    );

    public static LinuxEnumerateDirectoryAtResult
        Enumerate(
            LinuxNoFollowPathHandle directory)
    {
        ArgumentNullException.ThrowIfNull(
            directory
        );

        return Enumerate(
            (ILinuxOpenedHandle)directory
        );
    }

    public static LinuxEnumerateDirectoryAtResult
        Enumerate(
            ILinuxOpenedHandle directory)
    {
        ArgumentNullException.ThrowIfNull(
            directory
        );

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                LinuxEnumerateDirectoryAtState
                    .UnsupportedPlatform,
                error:
                    "Descriptor-relative directory enumeration " +
                    "is supported on Linux only."
            );
        }

        SafeFileHandle handle =
            directory.Handle;

        if (
            handle.IsInvalid ||
            handle.IsClosed)
        {
            return Result(
                LinuxEnumerateDirectoryAtState
                    .InvalidDirectoryHandle,
                error:
                    "The directory descriptor is invalid or closed."
            );
        }

        bool addedRef =
            false;

        int enumerationFd =
            -1;

        IntPtr buffer =
            IntPtr.Zero;

        try
        {
            handle.DangerousAddRef(
                ref addedRef
            );

            int retainedFd =
                checked(
                    (int)handle
                        .DangerousGetHandle()
                        .ToInt64()
                );

            /*
             * Do not dup() the retained directory descriptor.
             *
             * dup() would share the same open-file-description offset,
             * so consuming directory entries through the duplicate could
             * advance enumeration state visible through the caller's
             * retained descriptor.
             *
             * Instead, reopen "." relative to the retained descriptor.
             * This selects the already-open directory without depending
             * on its external pathname and gives this enumerator its own
             * independent open file description.
             */
            enumerationFd =
                OpenAt(
                    retainedFd,
                    ".",
                    ORdonly |
                    ODirectory |
                    ONoFollow |
                    OCloexec
                );

            if (enumerationFd < 0)
            {
                int errno =
                    Marshal.GetLastPInvokeError();

                LinuxEnumerateDirectoryAtState state =
                    errno switch
                    {
                        EBadF =>
                            LinuxEnumerateDirectoryAtState
                                .InvalidDirectoryHandle,

                        ENotDir =>
                            LinuxEnumerateDirectoryAtState
                                .NotDirectory,

                        _ =>
                            LinuxEnumerateDirectoryAtState
                                .EnumerationFailed
                    };

                return Result(
                    state,
                    errno:
                        errno
                );
            }

            buffer =
                Marshal.AllocHGlobal(
                    BufferSize
                );

            var names =
                new List<string>();

            var utf8 =
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier:
                        false,
                    throwOnInvalidBytes:
                        true
                );

            while (true)
            {
                nint readResult =
                    GetDents64(
                        enumerationFd,
                        buffer,
                        (nuint)BufferSize
                    );

                if (readResult < 0)
                {
                    int errno =
                        Marshal.GetLastPInvokeError();

                    if (errno == EIntr)
                    {
                        continue;
                    }

                    return Result(
                        LinuxEnumerateDirectoryAtState
                            .EnumerationFailed,
                        errno:
                            errno
                    );
                }

                if (readResult == 0)
                {
                    break;
                }

                if (readResult > BufferSize)
                {
                    return Result(
                        LinuxEnumerateDirectoryAtState
                            .InvalidDirectoryEntry,
                        error:
                            "getdents64 returned more bytes than the " +
                            "enumeration buffer can contain."
                    );
                }

                int bytesRead =
                    checked(
                        (int)readResult
                    );

                int offset =
                    0;

                while (offset < bytesRead)
                {
                    int remaining =
                        bytesRead - offset;

                    if (
                        remaining <
                        MinimumRecordLength)
                    {
                        return Result(
                            LinuxEnumerateDirectoryAtState
                                .InvalidDirectoryEntry,
                            error:
                                "A directory-entry record is truncated " +
                                "before its name field."
                        );
                    }

                    ushort recordLength =
                        unchecked(
                            (ushort)Marshal.ReadInt16(
                                buffer,
                                offset +
                                RecordLengthOffset
                            )
                        );

                    if (
                        recordLength <
                        MinimumRecordLength)
                    {
                        return Result(
                            LinuxEnumerateDirectoryAtState
                                .InvalidDirectoryEntry,
                            error:
                                "A directory-entry record has an " +
                                "invalid record length."
                        );
                    }

                    if (
                        recordLength >
                        remaining)
                    {
                        return Result(
                            LinuxEnumerateDirectoryAtState
                                .InvalidDirectoryEntry,
                            error:
                                "A directory-entry record extends " +
                                "past the bytes returned by getdents64."
                        );
                    }

                    int nameCapacity =
                        recordLength -
                        NameOffset;

                    byte[] nameBytes =
                        new byte[nameCapacity];

                    Marshal.Copy(
                        IntPtr.Add(
                            buffer,
                            offset +
                            NameOffset
                        ),
                        nameBytes,
                        0,
                        nameCapacity
                    );

                    int terminator =
                        Array.IndexOf(
                            nameBytes,
                            (byte)0
                        );

                    if (terminator < 0)
                    {
                        return Result(
                            LinuxEnumerateDirectoryAtState
                                .InvalidDirectoryEntry,
                            error:
                                "A directory-entry name is not " +
                                "NUL-terminated inside its record."
                        );
                    }

                    if (terminator == 0)
                    {
                        return Result(
                            LinuxEnumerateDirectoryAtState
                                .InvalidDirectoryEntry,
                            error:
                                "A directory-entry record contains " +
                                "an empty child name."
                        );
                    }

                    string name;

                    try
                    {
                        name =
                            utf8.GetString(
                                nameBytes,
                                0,
                                terminator
                            );
                    }
                    catch (
                        DecoderFallbackException ex)
                    {
                        return Result(
                            LinuxEnumerateDirectoryAtState
                                .InvalidDirectoryEntry,
                            error:
                                "A directory-entry name is not valid " +
                                $"UTF-8: {ex.Message}"
                        );
                    }

                    if (
                        name != "." &&
                        name != "..")
                    {
                        /*
                         * Linux filenames are byte strings and may
                         * legally contain characters such as '\'.
                         *
                         * Preserve the exact decoded direct-child name
                         * here. Callers that impose narrower naming
                         * rules must reject such names explicitly
                         * rather than having enumeration hide them.
                         */
                        names.Add(
                            name
                        );
                    }

                    offset +=
                        recordLength;
                }

                if (offset != bytesRead)
                {
                    return Result(
                        LinuxEnumerateDirectoryAtState
                            .InvalidDirectoryEntry,
                        error:
                            "Directory-entry parsing did not consume " +
                            "exactly the bytes returned by getdents64."
                    );
                }
            }

            names.Sort(
                StringComparer.Ordinal
            );

            return new LinuxEnumerateDirectoryAtResult(
                State:
                    LinuxEnumerateDirectoryAtState
                        .Enumerated,
                ChildNames:
                    names.ToArray(),
                Errno:
                    null,
                Error:
                    null
            );
        }
        catch (ObjectDisposedException ex)
        {
            return Result(
                LinuxEnumerateDirectoryAtState
                    .InvalidDirectoryHandle,
                error:
                    ex.Message
            );
        }
        catch (OverflowException ex)
        {
            return Result(
                LinuxEnumerateDirectoryAtState
                    .InvalidDirectoryHandle,
                error:
                    ex.Message
            );
        }
        catch (DllNotFoundException ex)
        {
            return Result(
                LinuxEnumerateDirectoryAtState
                    .UnsupportedPlatform,
                error:
                    ex.Message
            );
        }
        catch (EntryPointNotFoundException ex)
        {
            return Result(
                LinuxEnumerateDirectoryAtState
                    .UnsupportedPlatform,
                error:
                    ex.Message
            );
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(
                    buffer
                );
            }

            if (enumerationFd >= 0)
            {
                Close(
                    enumerationFd
                );
            }

            if (addedRef)
            {
                handle.DangerousRelease();
            }
        }
    }

    private static LinuxEnumerateDirectoryAtResult
        Result(
            LinuxEnumerateDirectoryAtState state,
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

        return new LinuxEnumerateDirectoryAtResult(
            State:
                state,
            ChildNames:
                [],
            Errno:
                errno,
            Error:
                error
        );
    }
}
