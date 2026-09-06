using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairSourceGenerationBindingState
{
    Bound,

    InvalidSourceSnapshot,
    SourceOutsideDataRoot,
    InvalidRelativePath,

    DirectoryEnumerationFailed,
    ExactDirectorySpellingUnavailable,
    DirectoryOpenFailed,

    FileEnumerationFailed,
    ExactFileSpellingUnavailable,
    FileOpenFailed,

    InitialIncarnationUnavailable,
    ContentObservationFailed,

    PostObservationReacquisitionFailed,
    PostObservationIncarnationUnavailable,
    SourceIncarnationChanged,

    SourceSnapshotMismatch
}

public sealed record DataRelativePathRepairSourceGenerationBinding(
    DataRelativePathRepairSourceGenerationBindingState State,
    uint? SourceInodeGeneration,
    string? Error
)
{
    public bool Success =>
        State ==
            DataRelativePathRepairSourceGenerationBindingState.Bound &&
        SourceInodeGeneration is not null;
}

/*
 * Freshly bind one persisted repair SourceSnapshot to current,
 * descriptor-backed Linux filesystem evidence.
 *
 * This type deliberately consumes no aggregate namespace sidecar evidence.
 * Inode generation comes only from LinuxOpenedFileIncarnation.Capture() on
 * the currently reacquired source descriptor.
 *
 * The source path is reacquired again after stable content observation so a
 * retained-but-unlinked/replaced descriptor cannot be mistaken for current
 * Data-namespace authority.
 *
 * Read-only only. No repair, persistence, provider precedence, or execution
 * authority is granted by this binder.
 */
public static class DataRelativePathRepairSourceGenerationBinder
{
    public static DataRelativePathRepairSourceGenerationBinding Bind(
        LinuxNoFollowPathHandle dataRoot,
        DataRelativePathRepairSourceSnapshot sourceSnapshot)
    {
        return BindCore(
            dataRoot,
            sourceSnapshot,
            afterStableContentObservation:
                null
        );
    }

    internal static DataRelativePathRepairSourceGenerationBinding BindCore(
        LinuxNoFollowPathHandle dataRoot,
        DataRelativePathRepairSourceSnapshot sourceSnapshot,
        Action? afterStableContentObservation)
    {
        ArgumentNullException.ThrowIfNull(
            dataRoot
        );

        ArgumentNullException.ThrowIfNull(
            sourceSnapshot
        );

        if (!TryValidateSnapshotShape(
                sourceSnapshot,
                out string? shapeError))
        {
            return Result(
                DataRelativePathRepairSourceGenerationBindingState
                    .InvalidSourceSnapshot,
                error:
                    shapeError
            );
        }

        if (!TryProjectSourceRelativePath(
                dataRoot.FullPath,
                sourceSnapshot.PhysicalPath,
                out string[]? components,
                out string? projectedFullPath,
                out DataRelativePathRepairSourceGenerationBindingState
                    pathFailureState,
                out string? pathError))
        {
            return Result(
                pathFailureState,
                error:
                    pathError
            );
        }

        SourceAcquisition initial =
            AcquireExactSource(
                dataRoot,
                components!
            );

        if (!initial.Success)
        {
            return Result(
                initial.State,
                error:
                    initial.Error
            );
        }

        using (initial)
        {
            LinuxOpenedFileIncarnationResult initialIncarnation =
                LinuxOpenedFileIncarnation.Capture(
                    initial.OpenedFile!
                );

            if (
                !initialIncarnation.Success ||
                initialIncarnation.Identity is null)
            {
                return Result(
                    DataRelativePathRepairSourceGenerationBindingState
                        .InitialIncarnationUnavailable,
                    error:
                        initialIncarnation.Error ??
                        initialIncarnation.State.ToString()
                );
            }

            LinuxOpenedFileContentObservationResult content =
                LinuxOpenedFileContentObservation.Observe(
                    initial.OpenedFile!,
                    projectedFullPath!
                );

            if (!content.Success)
            {
                return Result(
                    DataRelativePathRepairSourceGenerationBindingState
                        .ContentObservationFailed,
                    error:
                        content.Error ??
                        content.State.ToString()
                );
            }

            if (
                content.Before?.Identity is null ||
                content.After?.Identity is null ||
                !content.Before.Identity.SameObjectAs(
                    initialIncarnation.Identity.PhysicalIdentity) ||
                !content.After.Identity.SameObjectAs(
                    initialIncarnation.Identity.PhysicalIdentity))
            {
                return Result(
                    DataRelativePathRepairSourceGenerationBindingState
                        .ContentObservationFailed,
                    error:
                        "Stable content evidence did not remain bound to " +
                        "the initial source physical identity."
                );
            }

            afterStableContentObservation?.Invoke();

            SourceAcquisition post =
                AcquireExactSource(
                    dataRoot,
                    components!
                );

            if (!post.Success)
            {
                post.Dispose();

                return Result(
                    DataRelativePathRepairSourceGenerationBindingState
                        .PostObservationReacquisitionFailed,
                    error:
                        post.Error ??
                        post.State.ToString()
                );
            }

            using (post)
            {
                LinuxOpenedFileIncarnationResult postIncarnation =
                    LinuxOpenedFileIncarnation.Capture(
                        post.OpenedFile!
                    );

                if (
                    !postIncarnation.Success ||
                    postIncarnation.Identity is null)
                {
                    return Result(
                        DataRelativePathRepairSourceGenerationBindingState
                            .PostObservationIncarnationUnavailable,
                        error:
                            postIncarnation.Error ??
                            postIncarnation.State.ToString()
                    );
                }

                if (!initialIncarnation.Identity.SameIncarnationAs(
                        postIncarnation.Identity))
                {
                    return Result(
                        DataRelativePathRepairSourceGenerationBindingState
                            .SourceIncarnationChanged,
                        error:
                            "The Data namespace no longer names the same " +
                            "generation-aware source file incarnation after " +
                            "content observation."
                    );
                }
            }

            if (!SnapshotMatchesObservedEvidence(
                    sourceSnapshot,
                    projectedFullPath!,
                    initialIncarnation.Identity,
                    content))
            {
                return Result(
                    DataRelativePathRepairSourceGenerationBindingState
                        .SourceSnapshotMismatch,
                    error:
                        "The freshly observed source identity, size, or " +
                        "SHA-256 does not match the persisted source snapshot."
                );
            }

            return Result(
                DataRelativePathRepairSourceGenerationBindingState.Bound,
                sourceInodeGeneration:
                    initialIncarnation.Identity.InodeGeneration
            );
        }
    }

    private static bool TryValidateSnapshotShape(
        DataRelativePathRepairSourceSnapshot snapshot,
        out string? error)
    {
        error =
            null;

        if (string.IsNullOrWhiteSpace(
                snapshot.PhysicalPath))
        {
            error =
                "The source physical path is required.";

            return false;
        }

        string canonical;

        try
        {
            if (!Path.IsPathFullyQualified(
                    snapshot.PhysicalPath))
            {
                error =
                    "The source physical path must be absolute.";

                return false;
            }

            canonical =
                Path.GetFullPath(
                    snapshot.PhysicalPath
                );
        }
        catch (Exception ex)
        {
            error =
                $"The source physical path is invalid: {ex.Message}";

            return false;
        }

        if (!string.Equals(
                canonical,
                snapshot.PhysicalPath,
                StringComparison.Ordinal))
        {
            error =
                "The source physical path is not in canonical absolute form.";

            return false;
        }

        if (snapshot.Size < 0)
        {
            error =
                "The source snapshot size is negative.";

            return false;
        }

        if (!IsSha256(
                snapshot.Sha256))
        {
            error =
                "The source snapshot SHA-256 is malformed.";

            return false;
        }

        if (
            snapshot.Identity is null ||
            !snapshot.Identity.Success ||
            snapshot.Identity.Error is not null ||
            snapshot.Identity.DeviceMajor is null ||
            snapshot.Identity.DeviceMinor is null ||
            snapshot.Identity.Inode is null ||
            snapshot.Identity.MountId is null)
        {
            error =
                "The source snapshot does not contain complete physical " +
                "identity evidence.";

            return false;
        }

        if (!string.Equals(
                snapshot.Identity.FullPath,
                snapshot.PhysicalPath,
                StringComparison.Ordinal))
        {
            error =
                "The source snapshot identity FullPath does not match the " +
                "source physical path.";

            return false;
        }

        return true;
    }

    private static bool TryProjectSourceRelativePath(
        string dataRoot,
        string sourcePath,
        out string[]? components,
        out string? projectedFullPath,
        out DataRelativePathRepairSourceGenerationBindingState failureState,
        out string? error)
    {
        components =
            null;
        projectedFullPath =
            null;
        failureState =
            DataRelativePathRepairSourceGenerationBindingState
                .InvalidRelativePath;
        error =
            null;

        string canonicalDataRoot;

        try
        {
            canonicalDataRoot =
                Path.GetFullPath(
                    dataRoot
                );
        }
        catch (Exception ex)
        {
            error =
                $"The retained Data-root display path is invalid: " +
                $"{ex.Message}";

            return false;
        }

        string relativePath;

        try
        {
            relativePath =
                Path.GetRelativePath(
                    canonicalDataRoot,
                    sourcePath
                )
                .Replace(
                    '\\',
                    '/'
                );
        }
        catch (Exception ex)
        {
            error =
                $"The source path cannot be projected beneath Data: " +
                $"{ex.Message}";

            return false;
        }

        if (
            relativePath == "." ||
            relativePath.Length == 0 ||
            Path.IsPathRooted(
                relativePath) ||
            relativePath.StartsWith(
                "../",
                StringComparison.Ordinal) ||
            relativePath == "..")
        {
            failureState =
                DataRelativePathRepairSourceGenerationBindingState
                    .SourceOutsideDataRoot;
            error =
                "The source physical path is not strictly beneath the " +
                "trusted Data root.";

            return false;
        }

        string[] candidateComponents =
            relativePath.Split(
                '/',
                StringSplitOptions.None
            );

        if (
            candidateComponents.Length == 0 ||
            candidateComponents.Any(
                component =>
                    !IsValidComponent(
                        component)))
        {
            error =
                "The source Data-relative path contains an invalid physical " +
                "component.";

            return false;
        }

        string recomposed;

        try
        {
            recomposed =
                Path.GetFullPath(
                    Path.Combine(
                        new[]
                        {
                            canonicalDataRoot
                        }
                        .Concat(
                            candidateComponents
                        )
                        .ToArray()
                    )
                );
        }
        catch (Exception ex)
        {
            error =
                $"The source Data-relative path cannot be recomposed: " +
                $"{ex.Message}";

            return false;
        }

        if (!string.Equals(
                recomposed,
                sourcePath,
                StringComparison.Ordinal))
        {
            error =
                "The source physical path does not exactly round-trip " +
                "through its Data-relative component sequence.";

            return false;
        }

        components =
            candidateComponents;
        projectedFullPath =
            recomposed;

        return true;
    }

    private static SourceAcquisition AcquireExactSource(
        LinuxNoFollowPathHandle dataRoot,
        IReadOnlyList<string> components)
    {
        if (components.Count == 0)
        {
            return SourceAcquisition.Failure(
                DataRelativePathRepairSourceGenerationBindingState
                    .InvalidRelativePath,
                "The source path has no components."
            );
        }

        LinuxNoFollowPathHandle currentDirectory =
            dataRoot;

        LinuxNoFollowPathHandle? ownedDirectory =
            null;

        try
        {
            for (
                int index = 0;
                index < components.Count - 1;
                index++)
            {
                string component =
                    components[index];

                LinuxEnumerateDirectoryAtResult enumeration =
                    LinuxEnumerateDirectoryAt.Enumerate(
                        currentDirectory
                    );

                if (!enumeration.Success)
                {
                    return SourceAcquisition.Failure(
                        DataRelativePathRepairSourceGenerationBindingState
                            .DirectoryEnumerationFailed,
                        enumeration.Error ??
                            enumeration.State.ToString()
                    );
                }

                if (enumeration.ChildNames.Count(
                        name =>
                            string.Equals(
                                name,
                                component,
                                StringComparison.Ordinal)) != 1)
                {
                    return SourceAcquisition.Failure(
                        DataRelativePathRepairSourceGenerationBindingState
                            .ExactDirectorySpellingUnavailable,
                        $"Exact directory spelling '{component}' is not " +
                        "present beneath the retained parent descriptor."
                    );
                }

                LinuxOpenChildDirectoryReadOnlyAtResult opened =
                    LinuxOpenChildDirectoryReadOnlyAt.Open(
                        currentDirectory,
                        component
                    );

                if (
                    !opened.Success ||
                    opened.OpenedDirectory is null)
                {
                    return SourceAcquisition.Failure(
                        DataRelativePathRepairSourceGenerationBindingState
                            .DirectoryOpenFailed,
                        opened.Error ??
                            opened.State.ToString()
                    );
                }

                LinuxNoFollowPathHandle next =
                    opened.OpenedDirectory;

                ownedDirectory?.Dispose();
                ownedDirectory =
                    next;
                currentDirectory =
                    next;
            }

            string finalName =
                components[^1];

            LinuxEnumerateDirectoryAtResult finalEnumeration =
                LinuxEnumerateDirectoryAt.Enumerate(
                    currentDirectory
                );

            if (!finalEnumeration.Success)
            {
                return SourceAcquisition.Failure(
                    DataRelativePathRepairSourceGenerationBindingState
                        .FileEnumerationFailed,
                    finalEnumeration.Error ??
                        finalEnumeration.State.ToString()
                );
            }

            if (finalEnumeration.ChildNames.Count(
                    name =>
                        string.Equals(
                            name,
                            finalName,
                            StringComparison.Ordinal)) != 1)
            {
                return SourceAcquisition.Failure(
                    DataRelativePathRepairSourceGenerationBindingState
                        .ExactFileSpellingUnavailable,
                    $"Exact file spelling '{finalName}' is not present " +
                    "beneath the retained parent descriptor."
                );
            }

            LinuxOpenChildRegularFileReadOnlyAtResult openedFile =
                LinuxOpenChildRegularFileReadOnlyAt.Open(
                    currentDirectory,
                    finalName
                );

            if (
                !openedFile.Success ||
                openedFile.OpenedFile is null)
            {
                return SourceAcquisition.Failure(
                    DataRelativePathRepairSourceGenerationBindingState
                        .FileOpenFailed,
                    openedFile.Error ??
                        openedFile.State.ToString()
                );
            }

            LinuxOpenedChildHandle retained =
                openedFile.OpenedFile;

            ownedDirectory?.Dispose();
            ownedDirectory =
                null;

            return SourceAcquisition.Successful(
                retained
            );
        }
        finally
        {
            ownedDirectory?.Dispose();
        }
    }

    private static bool SnapshotMatchesObservedEvidence(
        DataRelativePathRepairSourceSnapshot snapshot,
        string projectedFullPath,
        LinuxFileIncarnationIdentity incarnation,
        LinuxOpenedFileContentObservationResult content)
    {
        LinuxOpenedFileIdentityResult actual =
            incarnation.PhysicalIdentity;

        return
            string.Equals(
                snapshot.PhysicalPath,
                projectedFullPath,
                StringComparison.Ordinal) &&
            snapshot.Identity.DeviceMajor ==
                actual.DeviceMajor &&
            snapshot.Identity.DeviceMinor ==
                actual.DeviceMinor &&
            snapshot.Identity.Inode ==
                actual.Inode &&
            snapshot.Identity.MountId ==
                actual.MountId &&
            snapshot.Size ==
                content.Size &&
            string.Equals(
                snapshot.Sha256,
                content.Sha256,
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidComponent(
        string component)
    {
        return
            component.Length > 0 &&
            component is not "." and not ".." &&
            !component.Contains('/') &&
            !component.Contains('\\') &&
            !component.Contains('\0') &&
            !(
                component.Length >= 2 &&
                char.IsLetter(
                    component[0]) &&
                component[1] == ':'
            );
    }

    private static bool IsSha256(
        string? value)
    {
        return
            value is not null &&
            value.Length == 64 &&
            value.All(
                character =>
                    (
                        character >= '0' &&
                        character <= '9'
                    ) ||
                    (
                        character >= 'a' &&
                        character <= 'f'
                    ) ||
                    (
                        character >= 'A' &&
                        character <= 'F'
                    )
            );
    }

    private static DataRelativePathRepairSourceGenerationBinding Result(
        DataRelativePathRepairSourceGenerationBindingState state,
        uint? sourceInodeGeneration = null,
        string? error = null)
    {
        return new(
            State:
                state,
            SourceInodeGeneration:
                sourceInodeGeneration,
            Error:
                error
        );
    }

    private sealed class SourceAcquisition
        : IDisposable
    {
        private SourceAcquisition(
            DataRelativePathRepairSourceGenerationBindingState state,
            LinuxOpenedChildHandle? openedFile,
            string? error)
        {
            State =
                state;
            OpenedFile =
                openedFile;
            Error =
                error;
        }

        public DataRelativePathRepairSourceGenerationBindingState State
        {
            get;
        }

        public LinuxOpenedChildHandle? OpenedFile
        {
            get;
            private set;
        }

        public string? Error
        {
            get;
        }

        public bool Success =>
            OpenedFile is not null;

        public static SourceAcquisition Successful(
            LinuxOpenedChildHandle openedFile)
        {
            return new(
                DataRelativePathRepairSourceGenerationBindingState.Bound,
                openedFile,
                error:
                    null
            );
        }

        public static SourceAcquisition Failure(
            DataRelativePathRepairSourceGenerationBindingState state,
            string? error)
        {
            return new(
                state,
                openedFile:
                    null,
                error
            );
        }

        public void Dispose()
        {
            OpenedFile?.Dispose();
            OpenedFile =
                null;
        }
    }
}
