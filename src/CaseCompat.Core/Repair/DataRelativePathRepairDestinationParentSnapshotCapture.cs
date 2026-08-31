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

        LinuxOpenedDirectorySnapshotResult openedSnapshot =
            LinuxOpenedDirectorySnapshot.Capture(
                openedParent
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
                openedParent.FullPath,
                openState:
                    opened.State,
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
                openedParent.FullPath,
                openState:
                    opened.State,
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
                    openedParent.FullPath,
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
            openedParent.FullPath,
            openState:
                opened.State,
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
