using CaseCompat.Core.Analysis;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Resolution;

public static class DataRelativePathResolver
{
    private sealed record PhysicalEntry(
        string Name,
        string FullPath,
        bool IsDirectory,
        bool IsSymbolicLink
    );

    public static DataRelativePathResolution ResolveFile(
        string dataRoot,
        string dataRelativePath)
    {
        return ResolveFile(
            dataRoot,
            dataRelativePath,
            LinuxDirectoryFlags.Inspect
        );
    }

    public static DataRelativePathResolution ResolveFile(
        string dataRoot,
        string dataRelativePath,
        Func<string, DirectoryCasefoldResult> inspectCasefold)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            dataRoot
        );

        ArgumentException.ThrowIfNullOrWhiteSpace(
            dataRelativePath
        );

        ArgumentNullException.ThrowIfNull(
            inspectCasefold
        );

        string root =
            Path.GetFullPath(dataRoot);

        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(root);
        }

        FileAttributes rootAttributes =
            File.GetAttributes(root);

        if ((rootAttributes &
             FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException(
                "The Data root must not be a symbolic link."
            );
        }

        string[] components =
            SplitRelativePath(dataRelativePath);

        var steps =
            new List<PathResolutionStep>();

        string currentParent =
            root;

        bool linuxResolves =
            true;

        string? resolvedPhysicalPath =
            null;

        int? failedComponentIndex =
            null;

        string? failureReason =
            null;

        for (
            int index = 0;
            index < components.Length;
            index++)
        {
            string component =
                components[index];

            bool finalComponent =
                index == components.Length - 1;

            DirectoryCasefoldResult casefold;

            try
            {
                casefold =
                    inspectCasefold(currentParent);
            }
            catch (Exception ex)
            {
                casefold =
                    new DirectoryCasefoldResult(
                        FullPath: currentParent,
                        Exists: true,
                        CasefoldEnabled: null,
                        RawFlags: null,
                        Error: ex.Message
                    );
            }

            IReadOnlyList<PhysicalEntry> entries;

            try
            {
                entries =
                    EnumerateEquivalentEntries(
                        currentParent,
                        component
                    );
            }
            catch (Exception ex)
            {
                steps.Add(
                    new PathResolutionStep(
                        ComponentIndex: index,
                        RequestedComponent: component,
                        ParentPhysicalPath:
                            currentParent,
                        ParentCasefoldEnabled:
                            casefold.CasefoldEnabled,
                        ParentCasefoldError:
                            casefold.Error,
                        Kind:
                            PathResolutionStepKind
                                .EnumerationError,
                        SelectedPhysicalName: null,
                        EquivalentPhysicalNames:
                            Array.Empty<string>()
                    )
                );

                linuxResolves = false;
                failedComponentIndex = index;
                failureReason =
                    $"Directory enumeration failed: " +
                    ex.Message;

                break;
            }

            string[] equivalentNames =
                entries
                    .Select(entry => entry.Name)
                    .OrderBy(
                        name => name,
                        StringComparer.Ordinal
                    )
                    .ToArray();

            PhysicalEntry? exact =
                entries.FirstOrDefault(entry =>
                    string.Equals(
                        entry.Name,
                        component,
                        StringComparison.Ordinal
                    )
                );

            if (exact is not null)
            {
                PathResolutionStepKind? invalidKind =
                    ValidateEntryKind(
                        exact,
                        finalComponent
                    );

                if (invalidKind is not null)
                {
                    steps.Add(
                        CreateStep(
                            index,
                            component,
                            currentParent,
                            casefold,
                            invalidKind.Value,
                            exact.Name,
                            equivalentNames
                        )
                    );

                    linuxResolves = false;
                    failedComponentIndex = index;
                    failureReason =
                        DescribeInvalidEntry(
                            invalidKind.Value,
                            component
                        );

                    break;
                }

                steps.Add(
                    CreateStep(
                        index,
                        component,
                        currentParent,
                        casefold,
                        PathResolutionStepKind
                            .ExactSpelling,
                        exact.Name,
                        equivalentNames
                    )
                );

                if (finalComponent)
                {
                    resolvedPhysicalPath =
                        exact.FullPath;
                }
                else
                {
                    currentParent =
                        exact.FullPath;
                }

                continue;
            }

            PhysicalEntry[] usable =
                entries
                    .Where(entry =>
                        ValidateEntryKind(
                            entry,
                            finalComponent
                        ) is null
                    )
                    .ToArray();

            if (usable.Length == 0)
            {
                steps.Add(
                    CreateStep(
                        index,
                        component,
                        currentParent,
                        casefold,
                        PathResolutionStepKind.Missing,
                        null,
                        equivalentNames
                    )
                );

                linuxResolves = false;
                failedComponentIndex = index;
                failureReason =
                    $"Requested component " +
                    $"\"{component}\" does not resolve.";

                break;
            }

            if (casefold.CasefoldEnabled is null)
            {
                steps.Add(
                    CreateStep(
                        index,
                        component,
                        currentParent,
                        casefold,
                        PathResolutionStepKind
                            .CasefoldUnknown,
                        null,
                        equivalentNames
                    )
                );

                linuxResolves = false;
                failedComponentIndex = index;
                failureReason =
                    $"Exact spelling is absent for " +
                    $"\"{component}\" and parent " +
                    "casefold state is unknown.";

                break;
            }

            if (!casefold.CasefoldEnabled.Value)
            {
                steps.Add(
                    CreateStep(
                        index,
                        component,
                        currentParent,
                        casefold,
                        PathResolutionStepKind.Missing,
                        null,
                        equivalentNames
                    )
                );

                linuxResolves = false;
                failedComponentIndex = index;
                failureReason =
                    $"Exact spelling \"{component}\" " +
                    "is absent under a strict parent.";

                break;
            }

            if (usable.Length != 1)
            {
                steps.Add(
                    CreateStep(
                        index,
                        component,
                        currentParent,
                        casefold,
                        PathResolutionStepKind
                            .AmbiguousEquivalent,
                        null,
                        equivalentNames
                    )
                );

                linuxResolves = false;
                failedComponentIndex = index;
                failureReason =
                    $"Multiple equivalent physical " +
                    $"entries exist for \"{component}\".";

                break;
            }

            PhysicalEntry selected =
                usable[0];

            steps.Add(
                CreateStep(
                    index,
                    component,
                    currentParent,
                    casefold,
                    PathResolutionStepKind
                        .CasefoldEquivalent,
                    selected.Name,
                    equivalentNames
                )
            );

            if (finalComponent)
            {
                resolvedPhysicalPath =
                    selected.FullPath;
            }
            else
            {
                currentParent =
                    selected.FullPath;
            }
        }

        if (linuxResolves &&
            resolvedPhysicalPath is null)
        {
            linuxResolves = false;
            failureReason =
                "Resolution ended without a file target.";
        }

        var candidateErrors =
            new List<string>();

        IReadOnlyList<string> candidates =
            FindEquivalentPhysicalCandidates(
                root,
                components,
                candidateErrors
            );

        return new DataRelativePathResolution(
            DataRoot: root,
            RequestedPath: string.Join(
                '/',
                components
            ),
            LinuxResolves: linuxResolves,
            ResolvedPhysicalPath:
                resolvedPhysicalPath,
            FailedComponentIndex:
                failedComponentIndex,
            FailureReason:
                failureReason,
            Steps:
                steps.ToArray(),
            EquivalentPhysicalCandidates:
                candidates,
            CandidateSearchErrors:
                candidateErrors.ToArray()
        );
    }

    private static string[] SplitRelativePath(
        string path)
    {
        if (Path.IsPathRooted(path) ||
            path.StartsWith('\\'))
        {
            throw new ArgumentException(
                "A Data-relative path is required.",
                nameof(path)
            );
        }

        string[] components =
            path.Split(
                ['/', '\\'],
                StringSplitOptions.RemoveEmptyEntries
            );

        if (components.Length == 0)
        {
            throw new ArgumentException(
                "The path contains no components.",
                nameof(path)
            );
        }

        if (components.Any(component =>
            component is "." or ".."))
        {
            throw new ArgumentException(
                "Relative traversal components are not allowed.",
                nameof(path)
            );
        }

        return components;
    }

    private static IReadOnlyList<PhysicalEntry>
        EnumerateEquivalentEntries(
            string directory,
            string requestedName)
    {
        var entries =
            new List<PhysicalEntry>();

        foreach (
            string path
            in Directory.EnumerateFileSystemEntries(
                directory))
        {
            string name =
                Path.GetFileName(path);

            if (!LogicallyEquivalent(
                    name,
                    requestedName))
            {
                continue;
            }

            FileAttributes attributes =
                File.GetAttributes(path);

            entries.Add(
                new PhysicalEntry(
                    Name: name,
                    FullPath: path,
                    IsDirectory:
                        (attributes &
                         FileAttributes.Directory) != 0,
                    IsSymbolicLink:
                        (attributes &
                         FileAttributes.ReparsePoint) != 0
                )
            );
        }

        return entries;
    }

    private static bool LogicallyEquivalent(
        string first,
        string second)
    {
        return
            WindowsLogicalPath.FromRelativePath(first) ==
            WindowsLogicalPath.FromRelativePath(second);
    }

    private static PathResolutionStepKind?
        ValidateEntryKind(
            PhysicalEntry entry,
            bool finalComponent)
    {
        if (entry.IsSymbolicLink)
        {
            return PathResolutionStepKind
                .SymbolicLinkRejected;
        }

        if (!finalComponent &&
            !entry.IsDirectory)
        {
            return PathResolutionStepKind
                .NotDirectory;
        }

        if (finalComponent &&
            entry.IsDirectory)
        {
            return PathResolutionStepKind
                .NotFile;
        }

        return null;
    }

    private static PathResolutionStep CreateStep(
        int index,
        string component,
        string parent,
        DirectoryCasefoldResult casefold,
        PathResolutionStepKind kind,
        string? selectedName,
        IReadOnlyList<string> equivalentNames)
    {
        return new PathResolutionStep(
            ComponentIndex: index,
            RequestedComponent: component,
            ParentPhysicalPath: parent,
            ParentCasefoldEnabled:
                casefold.CasefoldEnabled,
            ParentCasefoldError:
                casefold.Error,
            Kind: kind,
            SelectedPhysicalName:
                selectedName,
            EquivalentPhysicalNames:
                equivalentNames
        );
    }

    private static string DescribeInvalidEntry(
        PathResolutionStepKind kind,
        string component)
    {
        return kind switch
        {
            PathResolutionStepKind
                .SymbolicLinkRejected =>
                $"Symbolic link rejected for " +
                $"\"{component}\".",

            PathResolutionStepKind
                .NotDirectory =>
                $"\"{component}\" is not a directory.",

            PathResolutionStepKind
                .NotFile =>
                $"\"{component}\" is a directory, " +
                "not a file.",

            _ =>
                $"Invalid entry for \"{component}\"."
        };
    }

    private static IReadOnlyList<string>
        FindEquivalentPhysicalCandidates(
            string root,
            IReadOnlyList<string> components,
            List<string> errors)
    {
        IReadOnlyList<string> parents =
            [root];

        for (
            int index = 0;
            index < components.Count;
            index++)
        {
            string component =
                components[index];

            bool finalComponent =
                index == components.Count - 1;

            var next =
                new HashSet<string>(
                    StringComparer.Ordinal
                );

            foreach (string parent in parents)
            {
                IReadOnlyList<PhysicalEntry> entries;

                try
                {
                    entries =
                        EnumerateEquivalentEntries(
                            parent,
                            component
                        );
                }
                catch (Exception ex)
                {
                    errors.Add(
                        $"{parent}: {ex.Message}"
                    );

                    continue;
                }

                foreach (PhysicalEntry entry in entries)
                {
                    if (ValidateEntryKind(
                            entry,
                            finalComponent) is not null)
                    {
                        continue;
                    }

                    next.Add(entry.FullPath);
                }
            }

            if (next.Count == 0)
            {
                return Array.Empty<string>();
            }

            parents =
                next
                    .OrderBy(
                        path => path,
                        StringComparer.Ordinal
                    )
                    .ToArray();
        }

        return parents;
    }
}
