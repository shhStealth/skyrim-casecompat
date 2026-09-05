using CaseCompat.Core.Analysis;

namespace CaseCompat.Core.Repair;

/*
 * Durable observational representation of one physical regular-file
 * representation in a complete aggregate Windows namespace snapshot.
 *
 * Snapshot retains the existing repair-source compatibility shape, while
 * InodeGeneration preserves the generation-aware evidence that must not be
 * silently discarded when aggregate namespace evidence is persisted.
 *
 * This record grants no repair or execution authority.
 */
public sealed record
    DataRelativePathAggregateNamespaceManifestFileRepresentation(
        string RelativePath,
        DataRelativePathRepairSourceSnapshot Snapshot,
        uint InodeGeneration
    );

/*
 * Durable observational representation of one Windows-logical regular-file
 * leaf and every physical representation observed for that leaf.
 *
 * State is classification only. It does not select a provider or source.
 */
public sealed record
    DataRelativePathAggregateNamespaceManifestLogicalLeaf(
        string WindowsLogicalPath,
        DataRelativePathAggregateLogicalLeafState State,
        IReadOnlyList<
            DataRelativePathAggregateNamespaceManifestFileRepresentation
        > PhysicalRepresentations
    );

/*
 * Schema-v1 durable aggregate namespace evidence.
 *
 * This sidecar is intentionally independent from repair-plan and batch
 * execution authority. Persisting or validating this record does not make a
 * plan executable and does not change any existing batch coverage policy.
 */
public sealed record DataRelativePathAggregateNamespaceManifestRecord(
    int SchemaVersion,
    DateTimeOffset CreatedUtc,
    string DataRoot,
    string RootWindowsLogicalPath,
    IReadOnlyList<string> DataRootChildNames,
    IReadOnlyList<WindowsNamespaceDirectoryLookupObservation>
        DirectoryLookupObservations,
    IReadOnlyList<WindowsNamespaceDirectoryIncarnationObservation>
        DirectoryIncarnationObservations,
    IReadOnlyList<DataRelativePathAggregateNamespaceManifestLogicalLeaf>
        LogicalLeaves
)
{
    public const int SchemaVersion1 =
        1;

    public const int CurrentSchemaVersion =
        SchemaVersion1;
}

public static class DataRelativePathAggregateNamespaceManifest
{
    public static string? Validate(
        DataRelativePathAggregateNamespaceManifestRecord manifest)
    {
        ArgumentNullException.ThrowIfNull(
            manifest
        );

        if (
            manifest.SchemaVersion !=
            DataRelativePathAggregateNamespaceManifestRecord.SchemaVersion1)
        {
            return
                $"Unsupported aggregate namespace manifest schema version " +
                $"{manifest.SchemaVersion}.";
        }

        if (manifest.CreatedUtc == default)
        {
            return
                "The aggregate namespace manifest creation timestamp is " +
                "missing.";
        }

        if (string.IsNullOrWhiteSpace(manifest.DataRoot))
        {
            return
                "The aggregate namespace manifest Data root is required.";
        }

        if (
            !TryNormalizeAbsolutePath(
                manifest.DataRoot,
                out string dataRoot))
        {
            return
                "The aggregate namespace manifest Data root must be an " +
                "absolute valid path.";
        }

        if (
            !string.Equals(
                dataRoot,
                manifest.DataRoot,
                StringComparison.Ordinal))
        {
            return
                "The aggregate namespace manifest Data root is not in " +
                "canonical absolute form.";
        }

        if (
            string.IsNullOrWhiteSpace(
                manifest.RootWindowsLogicalPath))
        {
            return
                "The aggregate namespace root Windows-logical path is " +
                "required.";
        }

        if (
            manifest.RootWindowsLogicalPath.Contains('/') ||
            manifest.RootWindowsLogicalPath.Contains('\\'))
        {
            return
                "The aggregate namespace root Windows-logical path must " +
                "identify exactly one direct Data child.";
        }

        string canonicalRoot;

        try
        {
            canonicalRoot =
                WindowsLogicalPath
                    .FromRelativePath(
                        manifest.RootWindowsLogicalPath
                    )
                    .Value;
        }
        catch (Exception ex)
        {
            return
                $"The aggregate namespace root Windows-logical path is " +
                $"invalid: {ex.Message}";
        }

        if (
            !string.Equals(
                canonicalRoot,
                manifest.RootWindowsLogicalPath,
                StringComparison.Ordinal))
        {
            return
                "The aggregate namespace root Windows-logical path is not " +
                "in canonical WindowsLogicalPath form.";
        }

        if (manifest.DataRootChildNames is null)
        {
            return
                "The aggregate namespace manifest requires complete " +
                "Data-root child-name evidence.";
        }

        if (manifest.DirectoryLookupObservations is null)
        {
            return
                "The aggregate namespace manifest requires directory " +
                "lookup observations.";
        }

        if (manifest.DirectoryIncarnationObservations is null)
        {
            return
                "The aggregate namespace manifest requires directory " +
                "incarnation observations.";
        }

        if (manifest.LogicalLeaves is null)
        {
            return
                "The aggregate namespace manifest requires a logical-leaf " +
                "collection.";
        }

        var dataRootChildNames =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        var equivalentRootNames =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        string? previousDataRootChildName =
            null;

        foreach (string? childName in manifest.DataRootChildNames)
        {
            if (!IsValidDirectChildName(childName))
            {
                return
                    "The aggregate namespace Data-root child-name inventory " +
                    "contains an invalid direct child name.";
            }

            if (!dataRootChildNames.Add(childName!))
            {
                return
                    $"The aggregate namespace Data-root child-name " +
                    $"inventory contains duplicate exact name " +
                    $"'{childName}'.";
            }

            if (
                previousDataRootChildName is not null &&
                string.CompareOrdinal(
                    previousDataRootChildName,
                    childName) >= 0)
            {
                return
                    "The aggregate namespace Data-root child-name inventory " +
                    "is not in strict ordinal order.";
            }

            previousDataRootChildName =
                childName;

            string childLogicalPath;

            try
            {
                childLogicalPath =
                    WindowsLogicalPath
                        .FromRelativePath(
                            childName!
                        )
                        .Value;
            }
            catch (Exception ex)
            {
                return
                    $"Data-root child name '{childName}' cannot be mapped " +
                    $"to the Windows namespace: {ex.Message}";
            }

            if (
                string.Equals(
                    childLogicalPath,
                    manifest.RootWindowsLogicalPath,
                    StringComparison.Ordinal))
            {
                equivalentRootNames.Add(
                    childName!
                );
            }
        }

        if (equivalentRootNames.Count == 0)
        {
            return
                "The Data-root child-name inventory contains no physical " +
                "representative of the requested Windows-logical namespace " +
                "root.";
        }

        var lookupsByRelativePath =
            new Dictionary<
                string,
                WindowsNamespaceDirectoryLookupObservation
            >(
                StringComparer.Ordinal
            );

        string? previousLookupRelativePath =
            null;

        foreach (
            WindowsNamespaceDirectoryLookupObservation? observation
            in manifest.DirectoryLookupObservations)
        {
            if (observation is null)
            {
                return
                    "The aggregate namespace directory lookup collection " +
                    "contains a null observation.";
            }

            if (
                string.IsNullOrWhiteSpace(
                    observation.FullPath) ||
                string.IsNullOrWhiteSpace(
                    observation.RelativePath))
            {
                return
                    "A directory lookup observation has an incomplete path.";
            }

            if (
                previousLookupRelativePath is not null &&
                string.CompareOrdinal(
                    previousLookupRelativePath,
                    observation.RelativePath) >= 0)
            {
                return
                    "Directory lookup observations are not in strict " +
                    "ordinal relative-path order.";
            }

            previousLookupRelativePath =
                observation.RelativePath;

            if (
                !TryProjectPhysicalRelativePath(
                    dataRoot,
                    observation.RelativePath,
                    allowDataRoot:
                        true,
                    out string projectedLookupFullPath,
                    out string? lookupPathError))
            {
                return
                    $"Directory lookup observation " +
                    $"'{observation.RelativePath}' has an invalid physical " +
                    $"relative path: {lookupPathError}";
            }

            if (
                !string.Equals(
                    observation.FullPath,
                    projectedLookupFullPath,
                    StringComparison.Ordinal))
            {
                return
                    $"Directory lookup observation " +
                    $"'{observation.RelativePath}' FullPath does not match " +
                    "its physical relative path beneath the manifest Data " +
                    "root.";
            }

            if (
                observation.Error is not null ||
                observation.CasefoldEnabled is null ||
                observation.RawFlags is null)
            {
                return
                    $"Directory lookup observation " +
                    $"'{observation.RelativePath}' is incomplete.";
            }

            if (
                !RelativeDirectoryBelongsToAggregateRoot(
                    observation.RelativePath,
                    equivalentRootNames))
            {
                return
                    $"Directory lookup observation " +
                    $"'{observation.RelativePath}' lies outside the " +
                    "aggregate namespace roots.";
            }

            if (
                !lookupsByRelativePath.TryAdd(
                    observation.RelativePath,
                    observation))
            {
                return
                    $"Duplicate directory lookup observation for relative " +
                    $"path '{observation.RelativePath}'.";
            }
        }

        if (
            !lookupsByRelativePath.TryGetValue(
                ".",
                out WindowsNamespaceDirectoryLookupObservation?
                    dataRootLookup))
        {
            return
                "The aggregate namespace manifest has no Data-root lookup " +
                "observation.";
        }

        if (
            !string.Equals(
                dataRootLookup.FullPath,
                manifest.DataRoot,
                StringComparison.Ordinal))
        {
            return
                "The Data-root lookup observation does not bind the " +
                "manifest Data root.";
        }

        var incarnationsByRelativePath =
            new Dictionary<
                string,
                WindowsNamespaceDirectoryIncarnationObservation
            >(
                StringComparer.Ordinal
            );

        string? previousIncarnationRelativePath =
            null;

        foreach (
            WindowsNamespaceDirectoryIncarnationObservation? observation
            in manifest.DirectoryIncarnationObservations)
        {
            if (observation is null)
            {
                return
                    "The aggregate namespace directory incarnation " +
                    "collection contains a null observation.";
            }

            if (
                string.IsNullOrWhiteSpace(
                    observation.FullPath) ||
                string.IsNullOrWhiteSpace(
                    observation.RelativePath))
            {
                return
                    "A directory incarnation observation has an incomplete " +
                    "path.";
            }

            if (
                previousIncarnationRelativePath is not null &&
                string.CompareOrdinal(
                    previousIncarnationRelativePath,
                    observation.RelativePath) >= 0)
            {
                return
                    "Directory incarnation observations are not in strict " +
                    "ordinal relative-path order.";
            }

            previousIncarnationRelativePath =
                observation.RelativePath;

            if (
                !TryProjectPhysicalRelativePath(
                    dataRoot,
                    observation.RelativePath,
                    allowDataRoot:
                        true,
                    out string projectedIncarnationFullPath,
                    out string? incarnationPathError))
            {
                return
                    $"Directory incarnation observation " +
                    $"'{observation.RelativePath}' has an invalid physical " +
                    $"relative path: {incarnationPathError}";
            }

            if (
                !string.Equals(
                    observation.FullPath,
                    projectedIncarnationFullPath,
                    StringComparison.Ordinal))
            {
                return
                    $"Directory incarnation observation " +
                    $"'{observation.RelativePath}' FullPath does not match " +
                    "its physical relative path beneath the manifest Data " +
                    "root.";
            }

            if (
                observation.Error is not null ||
                observation.DeviceMajor is null ||
                observation.DeviceMinor is null ||
                observation.Inode is null ||
                observation.MountId is null ||
                observation.InodeGeneration is null)
            {
                return
                    $"Directory incarnation observation " +
                    $"'{observation.RelativePath}' is incomplete.";
            }

            if (
                !RelativeDirectoryBelongsToAggregateRoot(
                    observation.RelativePath,
                    equivalentRootNames))
            {
                return
                    $"Directory incarnation observation " +
                    $"'{observation.RelativePath}' lies outside the " +
                    "aggregate namespace roots.";
            }

            if (
                !incarnationsByRelativePath.TryAdd(
                    observation.RelativePath,
                    observation))
            {
                return
                    $"Duplicate directory incarnation observation for " +
                    $"relative path '{observation.RelativePath}'.";
            }
        }

        if (
            !incarnationsByRelativePath.TryGetValue(
                ".",
                out WindowsNamespaceDirectoryIncarnationObservation?
                    dataRootIncarnation))
        {
            return
                "The aggregate namespace manifest has no Data-root " +
                "incarnation observation.";
        }

        if (
            !string.Equals(
                dataRootIncarnation.FullPath,
                manifest.DataRoot,
                StringComparison.Ordinal))
        {
            return
                "The Data-root incarnation observation does not bind the " +
                "manifest Data root.";
        }

        if (
            lookupsByRelativePath.Count !=
            incarnationsByRelativePath.Count)
        {
            return
                "Directory lookup and incarnation observation sets do not " +
                "describe the same physical directory set.";
        }

        foreach (
            KeyValuePair<
                string,
                WindowsNamespaceDirectoryLookupObservation
            > pair
            in lookupsByRelativePath)
        {
            if (
                !incarnationsByRelativePath.TryGetValue(
                    pair.Key,
                    out WindowsNamespaceDirectoryIncarnationObservation?
                        incarnation))
            {
                return
                    $"Directory '{pair.Key}' has lookup evidence but no " +
                    "incarnation evidence.";
            }

            if (
                !string.Equals(
                    pair.Value.FullPath,
                    incarnation.FullPath,
                    StringComparison.Ordinal))
            {
                return
                    $"Directory '{pair.Key}' lookup and incarnation " +
                    "evidence disagree on the physical path.";
            }
        }

        foreach (string relativePath in lookupsByRelativePath.Keys)
        {
            if (
                string.Equals(
                    relativePath,
                    ".",
                    StringComparison.Ordinal))
            {
                continue;
            }

            string? parentRelativePath =
                ParentRelativePath(
                    relativePath
                );

            if (
                parentRelativePath is null ||
                !lookupsByRelativePath.ContainsKey(
                    parentRelativePath) ||
                !incarnationsByRelativePath.ContainsKey(
                    parentRelativePath))
            {
                return
                    $"Directory '{relativePath}' has no complete " +
                    "parent-directory evidence.";
            }
        }

        foreach (string rootName in equivalentRootNames)
        {
            if (!lookupsByRelativePath.ContainsKey(rootName))
            {
                return
                    $"Windows-equivalent Data-root child '{rootName}' has " +
                    "no complete directory lookup evidence.";
            }

            if (!incarnationsByRelativePath.ContainsKey(rootName))
            {
                return
                    $"Windows-equivalent Data-root child '{rootName}' has " +
                    "no complete directory incarnation evidence.";
            }
        }

        string? previousLogicalPath =
            null;

        var logicalPaths =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        var allPhysicalPaths =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        foreach (
            DataRelativePathAggregateNamespaceManifestLogicalLeaf? leaf
            in manifest.LogicalLeaves)
        {
            if (leaf is null)
            {
                return
                    "The aggregate namespace logical-leaf collection " +
                    "contains a null leaf.";
            }

            if (string.IsNullOrWhiteSpace(leaf.WindowsLogicalPath))
            {
                return
                    "An aggregate namespace logical leaf has no " +
                    "Windows-logical path.";
            }

            string canonicalLeaf;

            try
            {
                canonicalLeaf =
                    WindowsLogicalPath
                        .FromRelativePath(
                            leaf.WindowsLogicalPath
                        )
                        .Value;
            }
            catch (Exception ex)
            {
                return
                    $"Logical leaf '{leaf.WindowsLogicalPath}' is invalid: " +
                    $"{ex.Message}";
            }

            if (
                !string.Equals(
                    canonicalLeaf,
                    leaf.WindowsLogicalPath,
                    StringComparison.Ordinal))
            {
                return
                    $"Logical leaf '{leaf.WindowsLogicalPath}' is not in " +
                    "canonical WindowsLogicalPath form.";
            }

            string requiredPrefix =
                $"{manifest.RootWindowsLogicalPath}/";

            if (
                !leaf.WindowsLogicalPath.StartsWith(
                    requiredPrefix,
                    StringComparison.Ordinal))
            {
                return
                    $"Logical leaf '{leaf.WindowsLogicalPath}' lies outside " +
                    "the aggregate namespace root.";
            }

            if (!logicalPaths.Add(leaf.WindowsLogicalPath))
            {
                return
                    $"Logical leaf '{leaf.WindowsLogicalPath}' occurs more " +
                    "than once.";
            }

            if (
                previousLogicalPath is not null &&
                string.CompareOrdinal(
                    previousLogicalPath,
                    leaf.WindowsLogicalPath) >= 0)
            {
                return
                    "Aggregate namespace logical leaves are not in strict " +
                    "ordinal Windows-logical-path order.";
            }

            previousLogicalPath =
                leaf.WindowsLogicalPath;

            if (
                leaf.PhysicalRepresentations is null ||
                leaf.PhysicalRepresentations.Count == 0)
            {
                return
                    $"Logical leaf '{leaf.WindowsLogicalPath}' has no " +
                    "physical representations.";
            }

            string? previousPhysicalPath =
                null;

            var snapshots =
                new List<DataRelativePathRepairSourceSnapshot>(
                    leaf.PhysicalRepresentations.Count
                );

            foreach (
                DataRelativePathAggregateNamespaceManifestFileRepresentation?
                    representation
                in leaf.PhysicalRepresentations)
            {
                if (representation is null)
                {
                    return
                        $"Logical leaf '{leaf.WindowsLogicalPath}' contains " +
                        "a null physical representation.";
                }

                if (
                    string.IsNullOrWhiteSpace(
                        representation.RelativePath))
                {
                    return
                        $"Logical leaf '{leaf.WindowsLogicalPath}' contains " +
                        "a representation with no relative path.";
                }

                if (
                    !TryProjectPhysicalRelativePath(
                        dataRoot,
                        representation.RelativePath,
                        allowDataRoot:
                            false,
                        out string projectedRepresentationFullPath,
                        out string? representationPathError))
                {
                    return
                        $"Physical representation relative path " +
                        $"'{representation.RelativePath}' is invalid: " +
                        representationPathError;
                }

                string representationLogicalPath;

                try
                {
                    representationLogicalPath =
                        WindowsLogicalPath
                            .FromRelativePath(
                                representation.RelativePath
                            )
                            .Value;
                }
                catch (Exception ex)
                {
                    return
                        $"Physical representation relative path " +
                        $"'{representation.RelativePath}' is invalid: " +
                        $"{ex.Message}";
                }

                if (
                    !string.Equals(
                        representationLogicalPath,
                        leaf.WindowsLogicalPath,
                        StringComparison.Ordinal))
                {
                    return
                        $"Physical representation " +
                        $"'{representation.RelativePath}' does not map to " +
                        $"logical leaf '{leaf.WindowsLogicalPath}'.";
                }

                string? rootComponent =
                    FirstRelativeComponent(
                        representation.RelativePath
                    );

                if (
                    rootComponent is null ||
                    !equivalentRootNames.Contains(rootComponent))
                {
                    return
                        $"Physical representation " +
                        $"'{representation.RelativePath}' lies outside the " +
                        "discovered aggregate namespace roots.";
                }

                DataRelativePathRepairSourceSnapshot? snapshot =
                    representation.Snapshot;

                if (snapshot is null)
                {
                    return
                        $"Physical representation " +
                        $"'{representation.RelativePath}' has no snapshot.";
                }

                string? snapshotError =
                    ValidateSnapshot(
                        snapshot
                    );

                if (snapshotError is not null)
                {
                    return
                        $"Physical representation " +
                        $"'{representation.RelativePath}' is invalid: " +
                        snapshotError;
                }

                if (
                    !string.Equals(
                        snapshot.PhysicalPath,
                        projectedRepresentationFullPath,
                        StringComparison.Ordinal))
                {
                    return
                        $"Physical representation " +
                        $"'{representation.RelativePath}' snapshot path " +
                        "does not match its physical relative path beneath " +
                        "the manifest Data root.";
                }

                if (!allPhysicalPaths.Add(snapshot.PhysicalPath))
                {
                    return
                        $"Physical path '{snapshot.PhysicalPath}' occurs " +
                        "more than once in the aggregate namespace " +
                        "manifest.";
                }

                if (
                    previousPhysicalPath is not null &&
                    string.CompareOrdinal(
                        previousPhysicalPath,
                        snapshot.PhysicalPath) >= 0)
                {
                    return
                        $"Logical leaf '{leaf.WindowsLogicalPath}' physical " +
                        "representations are not in strict ordinal " +
                        "physical-path order.";
                }

                previousPhysicalPath =
                    snapshot.PhysicalPath;

                snapshots.Add(
                    snapshot
                );

                string? parentRelativePath =
                    ParentRelativePath(
                        representation.RelativePath
                    );

                if (
                    parentRelativePath is null ||
                    !incarnationsByRelativePath.ContainsKey(
                        parentRelativePath))
                {
                    return
                        $"Physical representation " +
                        $"'{representation.RelativePath}' has no " +
                        "generation-aware parent-directory evidence.";
                }
            }

            DataRelativePathAggregateLogicalLeaf classified =
                DataRelativePathAggregateLogicalLeafClassifier
                    .Classify(
                        leaf.WindowsLogicalPath,
                        snapshots
                    );

            if (classified.State != leaf.State)
            {
                return
                    $"Logical leaf '{leaf.WindowsLogicalPath}' classification " +
                    $"does not match its persisted physical content " +
                    "evidence.";
            }
        }

        return null;
    }

    private static string? ValidateSnapshot(
        DataRelativePathRepairSourceSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.PhysicalPath))
        {
            return
                "The snapshot physical path is missing.";
        }

        if (snapshot.Size < 0)
        {
            return
                "The snapshot size is negative.";
        }

        if (!IsSha256(snapshot.Sha256))
        {
            return
                "The snapshot SHA-256 is malformed.";
        }

        if (
            snapshot.Identity is null ||
            !snapshot.Identity.Success ||
            snapshot.Identity.DeviceMajor is null ||
            snapshot.Identity.DeviceMinor is null ||
            snapshot.Identity.Inode is null ||
            snapshot.Identity.LinkCount is null ||
            snapshot.Identity.MountId is null ||
            snapshot.Identity.Error is not null)
        {
            return
                "The snapshot physical identity is incomplete.";
        }

        if (
            !string.Equals(
                snapshot.Identity.FullPath,
                snapshot.PhysicalPath,
                StringComparison.Ordinal))
        {
            return
                "The snapshot physical identity path does not match the " +
                "snapshot physical path.";
        }

        return null;
    }

    private static bool TryNormalizeAbsolutePath(
        string? path,
        out string normalized)
    {
        normalized =
            string.Empty;

        if (
            string.IsNullOrWhiteSpace(path) ||
            !Path.IsPathFullyQualified(path))
        {
            return false;
        }

        try
        {
            normalized =
                Path.GetFullPath(
                    path
                );

            return true;
        }
        catch
        {
            normalized =
                string.Empty;

            return false;
        }
    }

    private static bool TryProjectPhysicalRelativePath(
        string dataRoot,
        string? relativePath,
        bool allowDataRoot,
        out string projectedFullPath,
        out string? error)
    {
        projectedFullPath =
            string.Empty;

        error =
            null;

        if (
            allowDataRoot &&
            string.Equals(
                relativePath,
                ".",
                StringComparison.Ordinal))
        {
            projectedFullPath =
                dataRoot;

            return true;
        }

        if (
            string.IsNullOrWhiteSpace(relativePath) ||
            string.Equals(
                relativePath,
                ".",
                StringComparison.Ordinal))
        {
            error =
                "path is empty or identifies the Data root.";

            return false;
        }

        if (Path.IsPathFullyQualified(relativePath))
        {
            error =
                "path is not Data-relative.";

            return false;
        }

        try
        {
            projectedFullPath =
                Path.GetFullPath(
                    Path.Combine(
                        dataRoot,
                        relativePath
                    )
                );

            string canonicalRelativePath =
                Path.GetRelativePath(
                    dataRoot,
                    projectedFullPath
                );

            canonicalRelativePath =
                canonicalRelativePath.Replace(
                    Path.DirectorySeparatorChar,
                    '/'
                );

            string[] components =
                canonicalRelativePath.Split(
                    '/',
                    StringSplitOptions.None
                );

            if (
                components.Length == 0 ||
                components.Any(component =>
                    component.Length == 0 ||
                    string.Equals(
                        component,
                        ".",
                        StringComparison.Ordinal) ||
                    string.Equals(
                        component,
                        "..",
                        StringComparison.Ordinal)))
            {
                projectedFullPath =
                    string.Empty;

                error =
                    "path contains an empty or traversal component.";

                return false;
            }

            if (
                !string.Equals(
                    canonicalRelativePath,
                    relativePath,
                    StringComparison.Ordinal))
            {
                projectedFullPath =
                    string.Empty;

                error =
                    "path is not in canonical physical Data-relative form.";

                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            projectedFullPath =
                string.Empty;

            error =
                ex.Message;

            return false;
        }
    }

    private static bool IsValidDirectChildName(
        string? value)
    {
        return
            !string.IsNullOrEmpty(value) &&
            value is not "." and not ".." &&
            !value.Contains('/') &&
            !value.Contains('\0');
    }

    private static bool RelativeDirectoryBelongsToAggregateRoot(
        string relativePath,
        IReadOnlySet<string> equivalentRootNames)
    {
        if (
            string.Equals(
                relativePath,
                ".",
                StringComparison.Ordinal))
        {
            return true;
        }

        string? firstComponent =
            FirstRelativeComponent(
                relativePath
            );

        return
            firstComponent is not null &&
            equivalentRootNames.Contains(
                firstComponent
            );
    }

    private static string? FirstRelativeComponent(
        string relativePath)
    {
        if (
            string.IsNullOrWhiteSpace(relativePath) ||
            relativePath == ".")
        {
            return null;
        }

        int separatorIndex =
            relativePath.IndexOf('/');

        if (separatorIndex < 0)
        {
            return relativePath;
        }

        if (separatorIndex == 0)
        {
            return null;
        }

        return relativePath[..separatorIndex];
    }

    private static string? ParentRelativePath(
        string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        int separatorIndex =
            relativePath.LastIndexOf('/');

        if (separatorIndex < 0)
        {
            return ".";
        }

        if (separatorIndex == 0)
        {
            return null;
        }

        return relativePath[..separatorIndex];
    }

    private static bool IsSha256(
        string? value)
    {
        return
            value is not null &&
            value.Length == 64 &&
            value.All(character =>
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
}
