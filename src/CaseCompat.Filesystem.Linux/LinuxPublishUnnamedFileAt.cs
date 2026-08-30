using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CaseCompat.Filesystem.Linux;

public static class LinuxPublishUnnamedFileAt
{
    private const int AtFdcwd =
        -100;

    private const int AtSymlinkFollow =
        0x400;

    // Linux errno values used for classification.
    private const int EPerm = 1;
    private const int ENoEnt = 2;
    private const int EBadF = 9;
    private const int EExist = 17;
    private const int EXdev = 18;
    private const int ENotDir = 20;
    private const int ERofs = 30;

    [DllImport(
        "libc",
        EntryPoint = "linkat",
        SetLastError = true)]
    private static extern int LinkAt(
        int olddirfd,
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        string oldpath,
        int newdirfd,
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        string newpath,
        int flags
    );

    public static LinuxPublishUnnamedFileAtResult Publish(
        LinuxUnnamedFileHandle source,
        LinuxNoFollowPathHandle destinationParent,
        string childName)
    {
        ArgumentNullException.ThrowIfNull(
            source
        );

        ArgumentNullException.ThrowIfNull(
            destinationParent
        );

        if (!IsValidChildName(childName))
        {
            return Result(
                LinuxPublishUnnamedFileAtState
                    .InvalidName,
                childName,
                error:
                    "The publication name must identify exactly " +
                    "one direct child and cannot be '.', '..', " +
                    "or contain path separators or NUL."
            );
        }

        if (!OperatingSystem.IsLinux())
        {
            return Result(
                LinuxPublishUnnamedFileAtState
                    .UnsupportedPlatform,
                childName,
                error:
                    "Unnamed-file publication is supported " +
                    "on Linux only."
            );
        }

        SafeFileHandle sourceHandle =
            source.Handle;

        SafeFileHandle parentHandle =
            destinationParent.Handle;

        if (
            sourceHandle.IsInvalid ||
            sourceHandle.IsClosed)
        {
            return Result(
                LinuxPublishUnnamedFileAtState
                    .InvalidSourceHandle,
                childName,
                error:
                    "The unnamed source descriptor is invalid " +
                    "or closed."
            );
        }

        if (
            parentHandle.IsInvalid ||
            parentHandle.IsClosed)
        {
            return Result(
                LinuxPublishUnnamedFileAtState
                    .InvalidParentHandle,
                childName,
                error:
                    "The destination-parent descriptor is " +
                    "invalid or closed."
            );
        }

        bool sourceRef =
            false;

        bool parentRef =
            false;

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
                    LinuxPublishUnnamedFileAtState
                        .InvalidSourceHandle,
                    childName,
                    error:
                        ex.Message
                );
            }

            try
            {
                parentHandle.DangerousAddRef(
                    ref parentRef
                );
            }
            catch (ObjectDisposedException ex)
            {
                return Result(
                    LinuxPublishUnnamedFileAtState
                        .InvalidParentHandle,
                    childName,
                    error:
                        ex.Message
                );
            }

            int sourceFd;

            int parentFd;

            try
            {
                sourceFd =
                    checked(
                        (int)sourceHandle
                            .DangerousGetHandle()
                            .ToInt64()
                    );

                parentFd =
                    checked(
                        (int)parentHandle
                            .DangerousGetHandle()
                            .ToInt64()
                    );
            }
            catch (OverflowException ex)
            {
                return Result(
                    LinuxPublishUnnamedFileAtState
                        .InvalidSourceHandle,
                    childName,
                    error:
                        ex.Message
                );
            }

            string sourcePath =
                $"/proc/self/fd/{sourceFd}";

            if (
                LinkAt(
                    AtFdcwd,
                    sourcePath,
                    parentFd,
                    childName,
                    AtSymlinkFollow
                ) == 0)
            {
                return Result(
                    LinuxPublishUnnamedFileAtState
                        .Published,
                    childName
                );
            }

            int errno =
                Marshal.GetLastPInvokeError();

            LinuxPublishUnnamedFileAtState state =
                errno switch
                {
                    EExist =>
                        LinuxPublishUnnamedFileAtState
                            .DestinationExists,

                    EXdev =>
                        LinuxPublishUnnamedFileAtState
                            .DifferentFilesystem,

                    ENotDir =>
                        LinuxPublishUnnamedFileAtState
                            .ParentNotDirectory,

                    EBadF =>
                        LinuxPublishUnnamedFileAtState
                            .InvalidParentHandle,

                    ENoEnt =>
                        LinuxPublishUnnamedFileAtState
                            .SourceDescriptorUnavailable,

                    EPerm or ERofs =>
                        LinuxPublishUnnamedFileAtState
                            .PublicationDenied,

                    _ =>
                        LinuxPublishUnnamedFileAtState
                            .PublishFailed
                };

            return Result(
                state,
                childName,
                errno:
                    errno
            );
        }
        finally
        {
            if (parentRef)
            {
                parentHandle.DangerousRelease();
            }

            if (sourceRef)
            {
                sourceHandle.DangerousRelease();
            }
        }
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

    private static LinuxPublishUnnamedFileAtResult Result(
        LinuxPublishUnnamedFileAtState state,
        string? childName,
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

        return new LinuxPublishUnnamedFileAtResult(
            State:
                state,
            ChildName:
                childName ?? string.Empty,
            Errno:
                errno,
            Error:
                error
        );
    }
}
