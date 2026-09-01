using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum
    DataRelativePathRepairDestinationParentSnapshotCaptureState
{
    Captured,

    InvalidDataRoot,
    InvalidParentPath,
    ParentOutsideDataRoot,

    ParentOpenFailed,
    SnapshotFailed,
    ParentCasefoldNotStrict
}

public sealed record
    DataRelativePathRepairDestinationParentSnapshotCaptureResult(
        DataRelativePathRepairDestinationParentSnapshotCaptureState
            State,
        string DataRoot,
        string ParentPath,
        LinuxNoFollowPathOpenState? OpenState,
        LinuxOpenedDirectorySnapshotResult? OpenedSnapshot,
        DataRelativePathRepairDestinationParentSnapshot? Snapshot,
        string? Error
    )
{
    public bool Success =>
        State ==
            DataRelativePathRepairDestinationParentSnapshotCaptureState
                .Captured &&
        Snapshot is not null;
}

public static class
    DataRelativePathRepairDestinationParentSnapshotCapture
{
    /*
     * Capture current destination-parent evidence for a path that is
     * already expected to exist beneath an independently trusted Data
     * root.
     *
     * This is intentionally snapshot capture, not durable mutation
     * authority:
     *
     *   - traversal is no-follow beneath trustedDataRoot;
     *   - identity and directory flags come from the exact opened
     *     descriptor;
     *   - casefold-enabled parents are rejected;
     *   - no generation-aware directory incarnation is captured here.
     *
     * A later executor must still reacquire/revalidate this snapshot and
     * obtain whatever stronger incarnation evidence its mutation requires.
     */
    public static
        DataRelativePathRepairDestinationParentSnapshotCaptureResult
        Capture(
            string trustedDataRoot,
            string parentPath)
    {
        if (
            !TryNormalizeAbsolutePath(
                trustedDataRoot,
                out string dataRoot))
        {
            return Result(
                DataRelativePathRepairDestinationParentSnapshotCaptureState
                    .InvalidDataRoot,
                trustedDataRoot,
                parentPath,
                error:
                    "The trusted Data root must be an absolute valid path."
            );
        }

        if (
            !TryNormalizeAbsolutePath(
                parentPath,
                out string fullParentPath))
        {
            return Result(
                DataRelativePathRepairDestinationParentSnapshotCaptureState
                    .InvalidParentPath,
                dataRoot,
                parentPath,
                error:
                    "The destination parent must be an absolute valid path."
            );
        }

        string relativePath;

        try
        {
            relativePath =
                Path.GetRelativePath(
                    dataRoot,
                    fullParentPath
                );
        }
        catch (
            Exception ex)
            when (
                ex is ArgumentException or
                NotSupportedException or
                PathTooLongException)
        {
            return Result(
                DataRelativePathRepairDestinationParentSnapshotCaptureState
                    .InvalidParentPath,
                dataRoot,
                fullParentPath,
                error:
                    ex.Message
            );
        }

        if (
            IsOutsideRoot(
                relativePath))
        {
            return Result(
                DataRelativePathRepairDestinationParentSnapshotCaptureState
                    .ParentOutsideDataRoot,
                dataRoot,
                fullParentPath,
                error:
                    "The destination parent is outside the trusted " +
                    "Data root."
            );
        }

        LinuxNoFollowPathOpenResult opened =
            relativePath == "."
                ? LinuxNoFollowPath.OpenRootReadOnly(
                    dataRoot
                )
                : LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                    dataRoot,
                    relativePath
                );

        if (
            !opened.Success ||
            opened.OpenedPath is null)
        {
            return Result(
                DataRelativePathRepairDestinationParentSnapshotCaptureState
                    .ParentOpenFailed,
                dataRoot,
                fullParentPath,
                openState:
                    opened.State,
                error:
                    opened.Error ??
                    opened.State.ToString()
            );
        }

        using LinuxNoFollowPathHandle openedParent =
            opened.OpenedPath;

        return CaptureOpenedParent(
            dataRoot,
            fullParentPath,
            opened.State,
            openedParent
        );
    }

    /*
     * Capture a destination-parent snapshot relative to a Data-root
     * descriptor that the caller already opened and retained.
     *
     * This overload deliberately never reopens the Data-root pathname.
     * Every descendant component is opened with openat() through
     * LinuxOpenChildReadOnlyAt, beginning from the retained descriptor.
     *
     * The retained root itself remains owned by the caller.
     */
    public static
        DataRelativePathRepairDestinationParentSnapshotCaptureResult
        Capture(
            LinuxNoFollowPathHandle trustedDataRoot,
            string parentPath)
    {
        ArgumentNullException.ThrowIfNull(
            trustedDataRoot
        );

        if (
            !TryNormalizeAbsolutePath(
                trustedDataRoot.RootPath,
                out string dataRoot))
        {
            return Result(
                DataRelativePathRepairDestinationParentSnapshotCaptureState
                    .InvalidDataRoot,
                trustedDataRoot.RootPath,
                parentPath,
                error:
                    "The retained trusted Data root must describe " +
                    "an absolute valid path."
            );
        }

        if (
            !TryNormalizeAbsolutePath(
                parentPath,
                out string fullParentPath))
        {
            return Result(
                DataRelativePathRepairDestinationParentSnapshotCaptureState
                    .InvalidParentPath,
                dataRoot,
                parentPath,
                error:
                    "The destination parent must be an absolute valid path."
            );
        }

        string relativePath;

        try
        {
            relativePath =
                Path.GetRelativePath(
                    dataRoot,
                    fullParentPath
                );
        }
        catch (
            Exception ex)
            when (
                ex is ArgumentException or
                NotSupportedException or
                PathTooLongException)
        {
            return Result(
                DataRelativePathRepairDestinationParentSnapshotCaptureState
                    .InvalidParentPath,
                dataRoot,
                fullParentPath,
                error:
                    ex.Message
            );
        }

        if (
            IsOutsideRoot(
                relativePath))
        {
            return Result(
                DataRelativePathRepairDestinationParentSnapshotCaptureState
                    .ParentOutsideDataRoot,
                dataRoot,
                fullParentPath,
                error:
                    "The destination parent is outside the retained " +
                    "trusted Data root."
            );
        }

        if (relativePath == ".")
        {
            return CaptureOpenedParent(
                dataRoot,
                fullParentPath,
                LinuxNoFollowPathOpenState.Opened,
                trustedDataRoot
            );
        }

        string[] components =
            relativePath.Split(
                ['/', '\\'],
                StringSplitOptions
                    .RemoveEmptyEntries
            );

        if (
            components.Length == 0 ||
            components.Any(component =>
                component is "." or ".." ||
                component.Contains('\0')))
        {
            return Result(
                DataRelativePathRepairDestinationParentSnapshotCaptureState
                    .InvalidParentPath,
                dataRoot,
                fullParentPath,
                error:
                    "The destination parent contains an invalid " +
                    "relative path component."
            );
        }

        ILinuxOpenedHandle current =
            trustedDataRoot;

        LinuxOpenedChildHandle? ownedCurrent =
            null;

        try
        {
            foreach (string component in components)
            {
                LinuxOpenChildReadOnlyAtResult opened =
                    LinuxOpenChildReadOnlyAt.Open(
                        current,
                        component
                    );

                if (
                    !opened.Success ||
                    opened.OpenedChild is null)
                {
                    return Result(
                        DataRelativePathRepairDestinationParentSnapshotCaptureState
                            .ParentOpenFailed,
                        dataRoot,
                        fullParentPath,
                        error:
                            opened.Error ??
                            opened.State.ToString()
                    );
                }

                LinuxOpenedChildHandle next =
                    opened.OpenedChild;

                /*
                 * next owns an independent descriptor. Once it has been
                 * obtained through openat(), an earlier intermediate
                 * descriptor can be closed without changing what next
                 * references.
                 *
                 * The caller-owned trustedDataRoot is never disposed here.
                 */
                ownedCurrent?.Dispose();

                ownedCurrent =
                    next;

                current =
                    next;
            }

            return CaptureOpenedParent(
                dataRoot,
                fullParentPath,
                openState:
                    null,
                current
            );
        }
        finally
        {
            ownedCurrent?.Dispose();
        }
    }

    private static
        DataRelativePathRepairDestinationParentSnapshotCaptureResult
        CaptureOpenedParent(
            string dataRoot,
            string fullParentPath,
            LinuxNoFollowPathOpenState? openState,
            ILinuxOpenedHandle openedParent)
    {
        LinuxOpenedDirectorySnapshotResult openedSnapshot =
            LinuxOpenedDirectorySnapshot.Capture(
                openedParent,
                fullParentPath
            );

        if (
            !openedSnapshot.Success ||
            openedSnapshot.Identity is not
                LinuxFileIdentityResult identity ||
            openedSnapshot.CasefoldEnabled is not
                bool casefoldEnabled ||
            openedSnapshot.RawFlags is not
                long rawFlags)
        {
            return Result(
                DataRelativePathRepairDestinationParentSnapshotCaptureState
                    .SnapshotFailed,
                dataRoot,
                fullParentPath,
                openState:
                    openState,
                openedSnapshot:
                    openedSnapshot,
                error:
                    openedSnapshot.Error ??
                    openedSnapshot.State.ToString()
            );
        }

        if (casefoldEnabled)
        {
            return Result(
                DataRelativePathRepairDestinationParentSnapshotCaptureState
                    .ParentCasefoldNotStrict,
                dataRoot,
                fullParentPath,
                openState:
                    openState,
                openedSnapshot:
                    openedSnapshot,
                error:
                    "A repair destination parent must be strict; " +
                    "the opened directory is casefold-enabled."
            );
        }

        var snapshot =
            new DataRelativePathRepairDestinationParentSnapshot(
                PhysicalPath:
                    fullParentPath,
                Identity:
                    identity,
                CasefoldEnabled:
                    casefoldEnabled,
                RawFlags:
                    rawFlags
            );

        return Result(
            DataRelativePathRepairDestinationParentSnapshotCaptureState
                .Captured,
            dataRoot,
            fullParentPath,
            openState:
                openState,
            openedSnapshot:
                openedSnapshot,
            snapshot:
                snapshot
        );
    }

    private static bool TryNormalizeAbsolutePath(
        string? path,
        out string normalized)
    {
        normalized =
            string.Empty;

        if (
            string.IsNullOrWhiteSpace(
                path
            ) ||
            path.Contains('\0') ||
            !Path.IsPathFullyQualified(
                path
            ))
        {
            return false;
        }

        try
        {
            normalized =
                TrimTrailingSeparators(
                    Path.GetFullPath(
                        path
                    )
                );

            return true;
        }
        catch (
            Exception ex)
            when (
                ex is ArgumentException or
                NotSupportedException or
                PathTooLongException)
        {
            return false;
        }
    }

    private static string TrimTrailingSeparators(
        string path)
    {
        string root =
            Path.GetPathRoot(
                path
            ) ??
            string.Empty;

        if (
            string.Equals(
                path,
                root,
                StringComparison.Ordinal))
        {
            return path;
        }

        return path.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        );
    }

    private static bool IsOutsideRoot(
        string relativePath)
    {
        return
            Path.IsPathFullyQualified(
                relativePath
            ) ||
            relativePath == ".." ||
            relativePath.StartsWith(
                "../",
                StringComparison.Ordinal
            ) ||
            relativePath.StartsWith(
                "..\\",
                StringComparison.Ordinal
            );
    }

    private static
        DataRelativePathRepairDestinationParentSnapshotCaptureResult
        Result(
            DataRelativePathRepairDestinationParentSnapshotCaptureState
                state,
            string? dataRoot,
            string? parentPath,
            LinuxNoFollowPathOpenState? openState = null,
            LinuxOpenedDirectorySnapshotResult? openedSnapshot = null,
            DataRelativePathRepairDestinationParentSnapshot?
                snapshot = null,
            string? error = null)
    {
        return new(
            State:
                state,
            DataRoot:
                dataRoot ??
                string.Empty,
            ParentPath:
                parentPath ??
                string.Empty,
            OpenState:
                openState,
            OpenedSnapshot:
                openedSnapshot,
            Snapshot:
                snapshot,
            Error:
                error
        );
    }
}
