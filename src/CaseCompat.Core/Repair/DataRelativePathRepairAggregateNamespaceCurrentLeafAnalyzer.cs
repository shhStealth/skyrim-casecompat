using CaseCompat.Core.Analysis;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Repair;

public enum
    DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
{
    Analyzed,

    InvalidInput,
    DataRootEnumerationFailed,

    EquivalentRootOpenFailed,

    IntermediateEnumerationFailed,
    IntermediateEquivalentObjectConflict,
    IntermediateDirectoryOpenFailed,

    FinalEnumerationFailed,
    FinalEquivalentObjectConflict,
    FinalFileOpenFailed,

    FileIncarnationUnavailable,
    FileContentObservationFailed,

    HierarchyRevalidationFailed
}

public sealed record
    DataRelativePathRepairAggregateNamespaceCurrentFileRepresentation(
        string RelativePath,
        LinuxFileIncarnationIdentity IncarnationIdentity,
        long Size,
        string Sha256
    );

public sealed record
    DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis(
        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
            State,
        string RootWindowsLogicalPath,
        WindowsLogicalPath LogicalPath,
        IReadOnlyList<string> PhysicalRootNames,
        IReadOnlyList<
            DataRelativePathRepairAggregateNamespaceCurrentFileRepresentation
        > Representations,
        string? Error
    )
{
    public bool Success =>
        State ==
        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
            .Analyzed;
}

/*
 * Targeted, read-only CURRENT namespace evidence for one Windows-logical
 * regular-file leaf.
 *
 * This deliberately does not build a complete WindowsNamespaceAnalysis and
 * does not recursively hash unrelated namespace leaves. Filesystem authority
 * comes only from the retained Data descriptor and descriptor-relative
 * enumeration/open primitives.
 *
 * The result is point-in-time evidence only. It grants no mutation authority.
 */
public static class
    DataRelativePathRepairAggregateNamespaceCurrentLeafAnalyzer
{
    public static
        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis
        Analyze(
            LinuxNoFollowPathHandle dataRoot,
            string rootWindowsLogicalPath,
            string requestedPath,
            LinuxNoFollowPathHandle? aliasesDirectory = null)
    {
        return AnalyzeCore(
            dataRoot,
            rootWindowsLogicalPath,
            requestedPath,
            aliasesDirectory,
            afterRootEnumeration:
                null,
            afterRepresentationContentObservation:
                null
        );
    }

    /*
     * Test-only race seams.
     *
     * Core intentionally has no public callback surface here. Tests may invoke
     * this internal overload reflectively so production callers cannot weaken
     * or influence the observation sequence.
     */
    internal static
        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis
        AnalyzeCore(
            LinuxNoFollowPathHandle dataRoot,
            string rootWindowsLogicalPath,
            string requestedPath,
            LinuxNoFollowPathHandle? aliasesDirectory,
            Action? afterRootEnumeration,
            Action? afterRepresentationContentObservation)
    {
        ArgumentNullException.ThrowIfNull(
            dataRoot
        );

        if (
            !OperatingSystem.IsLinux() ||
            !TryNormalizeRootLogicalPath(
                rootWindowsLogicalPath,
                out string rootLogical) ||
            !TryNormalizeRequestedPath(
                requestedPath,
                out string normalizedRequested,
                out string[] requestedComponents))
        {
            return Invalid(
                rootWindowsLogicalPath,
                requestedPath,
                "The aggregate namespace current-leaf request is invalid."
            );
        }

        WindowsLogicalPath logicalPath;

        try
        {
            logicalPath =
                WindowsLogicalPath.FromRelativePath(
                    normalizedRequested
                );
        }
        catch (ArgumentException ex)
        {
            return Invalid(
                rootLogical,
                requestedPath,
                ex.Message
            );
        }

        if (
            requestedComponents.Length < 2 ||
            !string.Equals(
                requestedComponents[0].ToUpperInvariant(),
                rootLogical,
                StringComparison.Ordinal))
        {
            return Invalid(
                rootLogical,
                normalizedRequested,
                "The requested path does not map to the supplied " +
                "Windows-logical namespace root."
            );
        }

        LinuxEnumerateDirectoryAtResult rootEnumeration =
            LinuxEnumerateDirectoryAt.Enumerate(
                dataRoot
            );

        if (!rootEnumeration.Success)
        {
            return Result(
                DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                    .DataRootEnumerationFailed,
                rootLogical,
                logicalPath,
                [],
                [],
                rootEnumeration.Error ??
                    rootEnumeration.State.ToString()
            );
        }

        string[] physicalRootNames =
            FindEquivalentNames(
                rootEnumeration.ChildNames,
                requestedComponents[0]
            );

        afterRootEnumeration?.Invoke();

        var branches =
            new List<DirectoryBranch>();

        var observedRepresentations =
            new List<ObservedRepresentation>();

        try
        {
            foreach (string rootName in physicalRootNames)
            {
                LinuxOpenChildDirectoryReadOnlyAtResult opened =
                    LinuxOpenChildDirectoryReadOnlyAt.Open(
                        dataRoot,
                        rootName
                    );

                if (
                    !opened.Success ||
                    opened.OpenedDirectory is null)
                {
                    bool rootIsVerifiedKnownAlias =
                        opened.State is
                            LinuxOpenChildDirectoryReadOnlyAtState
                                .ChildSymbolicLinkRejected or
                            LinuxOpenChildDirectoryReadOnlyAtState
                                .NotDirectory &&
                        aliasesDirectory is not null &&
                        IsVerifiedKnownAlias(
                            aliasesDirectory,
                            dataRoot,
                            dataRoot.FullPath,
                            rootName
                        );

                    if (rootIsVerifiedKnownAlias)
                    {
                        // This root name is one of this project's own
                        // aliases, not a second, genuinely separate
                        // object - its real target is a distinct entry
                        // in physicalRootNames (an alias's link and
                        // target are always case-insensitive twins of
                        // the same requested component) and will be
                        // opened as its own root branch below.
                        continue;
                    }

                    return Result(
                        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                            .EquivalentRootOpenFailed,
                        rootLogical,
                        logicalPath,
                        physicalRootNames,
                        [],
                        opened.Error ??
                            opened.State.ToString()
                    );
                }

                LinuxNoFollowPathHandle rootDirectory =
                    opened.OpenedDirectory;

                LinuxOpenedDirectoryIncarnationResult incarnation =
                    LinuxOpenedDirectoryIncarnation.Capture(
                        rootDirectory
                    );

                if (
                    !incarnation.Success ||
                    incarnation.Identity is null)
                {
                    rootDirectory.Dispose();

                    return Result(
                        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                            .EquivalentRootOpenFailed,
                        rootLogical,
                        logicalPath,
                        physicalRootNames,
                        [],
                        incarnation.Error ??
                            incarnation.State.ToString()
                    );
                }

                branches.Add(
                    new DirectoryBranch(
                        rootDirectory,
                        [rootName],
                        [incarnation.Identity]
                    )
                );
            }

            for (
                int componentIndex = 1;
                componentIndex <
                    requestedComponents.Length - 1;
                componentIndex++)
            {
                string requestedComponent =
                    requestedComponents[
                        componentIndex
                    ];

                var nextBranches =
                    new List<DirectoryBranch>();

                foreach (
                    DirectoryBranch branch
                    in branches)
                {
                    LinuxEnumerateDirectoryAtResult enumeration =
                        LinuxEnumerateDirectoryAt.Enumerate(
                            branch.Directory
                        );

                    if (!enumeration.Success)
                    {
                        DisposeAll(
                            nextBranches
                        );

                        return Result(
                            DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                                .IntermediateEnumerationFailed,
                            rootLogical,
                            logicalPath,
                            physicalRootNames,
                            [],
                            enumeration.Error ??
                                enumeration.State.ToString()
                        );
                    }

                    string[] matches =
                        FindEquivalentNames(
                            enumeration.ChildNames,
                            requestedComponent
                        );

                    foreach (string match in matches)
                    {
                        LinuxOpenChildDirectoryReadOnlyAtResult opened =
                            LinuxOpenChildDirectoryReadOnlyAt.Open(
                                branch.Directory,
                                match
                            );

                        if (
                            !opened.Success ||
                            opened.OpenedDirectory is null)
                        {
                            bool objectConflict =
                                opened.State is
                                    LinuxOpenChildDirectoryReadOnlyAtState
                                        .ChildSymbolicLinkRejected or
                                    LinuxOpenChildDirectoryReadOnlyAtState
                                        .NotDirectory;

                            if (
                                objectConflict &&
                                aliasesDirectory is not null &&
                                IsVerifiedKnownAlias(
                                    aliasesDirectory,
                                    branch.Directory,
                                    branch.Directory.FullPath,
                                    match
                                ))
                            {
                                // This match is one of this project's own
                                // aliases, not a second, genuinely
                                // separate object - its real target is a
                                // case-insensitive twin of the same
                                // requested component, so it is also
                                // present in matches and will be opened
                                // as its own branch in this same loop.
                                continue;
                            }

                            DisposeAll(
                                nextBranches
                            );

                            return Result(
                                objectConflict
                                    ? DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                                        .IntermediateEquivalentObjectConflict
                                    : DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                                        .IntermediateDirectoryOpenFailed,
                                rootLogical,
                                logicalPath,
                                physicalRootNames,
                                [],
                                opened.Error ??
                                    opened.State.ToString()
                            );
                        }

                        LinuxNoFollowPathHandle childDirectory =
                            opened.OpenedDirectory;

                        LinuxOpenedDirectoryIncarnationResult incarnation =
                            LinuxOpenedDirectoryIncarnation.Capture(
                                childDirectory
                            );

                        if (
                            !incarnation.Success ||
                            incarnation.Identity is null)
                        {
                            childDirectory.Dispose();

                            DisposeAll(
                                nextBranches
                            );

                            return Result(
                                DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                                    .IntermediateDirectoryOpenFailed,
                                rootLogical,
                                logicalPath,
                                physicalRootNames,
                                [],
                                incarnation.Error ??
                                    incarnation.State.ToString()
                            );
                        }

                        nextBranches.Add(
                            new DirectoryBranch(
                                childDirectory,
                                branch.PhysicalComponents
                                    .Append(
                                        match
                                    )
                                    .ToArray(),
                                branch.DirectoryIncarnations
                                    .Append(
                                        incarnation.Identity
                                    )
                                    .ToArray()
                            )
                        );
                    }
                }

                DisposeAll(
                    branches
                );

                branches =
                    nextBranches;

                if (branches.Count == 0)
                {
                    break;
                }
            }

            if (branches.Count > 0)
            {
                string finalRequestedName =
                    requestedComponents[^1];

                foreach (
                    DirectoryBranch branch
                    in branches)
                {
                    LinuxEnumerateDirectoryAtResult enumeration =
                        LinuxEnumerateDirectoryAt.Enumerate(
                            branch.Directory
                        );

                    if (!enumeration.Success)
                    {
                        return Result(
                            DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                                .FinalEnumerationFailed,
                            rootLogical,
                            logicalPath,
                            physicalRootNames,
                            [],
                            enumeration.Error ??
                                enumeration.State.ToString()
                        );
                    }

                    string[] matches =
                        FindEquivalentNames(
                            enumeration.ChildNames,
                            finalRequestedName
                        );

                    foreach (string match in matches)
                    {
                        LinuxOpenChildRegularFileReadOnlyAtResult opened =
                            LinuxOpenChildRegularFileReadOnlyAt.Open(
                                branch.Directory,
                                match
                            );

                        if (
                            !opened.Success ||
                            opened.OpenedFile is null)
                        {
                            bool objectConflict =
                                opened.State ==
                                LinuxOpenChildRegularFileReadOnlyAtState
                                    .ChildNotRegularFile;

                            return Result(
                                objectConflict
                                    ? DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                                        .FinalEquivalentObjectConflict
                                    : DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                                        .FinalFileOpenFailed,
                                rootLogical,
                                logicalPath,
                                physicalRootNames,
                                [],
                                opened.Error ??
                                    opened.State.ToString()
                            );
                        }

                        using LinuxOpenedChildHandle openedFile =
                            opened.OpenedFile;

                        LinuxOpenedFileIncarnationResult incarnation =
                            LinuxOpenedFileIncarnation.Capture(
                                openedFile
                            );

                        if (
                            !incarnation.Success ||
                            incarnation.Identity is null)
                        {
                            return Result(
                                DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                                    .FileIncarnationUnavailable,
                                rootLogical,
                                logicalPath,
                                physicalRootNames,
                                [],
                                incarnation.Error ??
                                    incarnation.State.ToString()
                            );
                        }

                        string[] physicalComponents =
                            branch.PhysicalComponents
                                .Append(
                                    match
                                )
                                .ToArray();

                        string relativePath =
                            string.Join(
                                '/',
                                physicalComponents
                            );

                        LinuxOpenedFileContentObservationResult content =
                            LinuxOpenedFileContentObservation.Observe(
                                openedFile,
                                Path.Combine(
                                    dataRoot.FullPath,
                                    relativePath
                                        .Replace(
                                            '/',
                                            Path.DirectorySeparatorChar
                                        )
                                )
                            );

                        if (
                            !content.Success ||
                            content.Size is null ||
                            content.Sha256 is null)
                        {
                            return Result(
                                DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                                    .FileContentObservationFailed,
                                rootLogical,
                                logicalPath,
                                physicalRootNames,
                                [],
                                content.Error ??
                                    content.State.ToString()
                            );
                        }

                        afterRepresentationContentObservation
                            ?.Invoke();

                        observedRepresentations.Add(
                            new ObservedRepresentation(
                                PublicRepresentation:
                                    new(
                                        RelativePath:
                                            relativePath,
                                        IncarnationIdentity:
                                            incarnation.Identity,
                                        Size:
                                            content.Size.Value,
                                        Sha256:
                                            content.Sha256
                                    ),
                                DirectoryComponents:
                                    branch.PhysicalComponents
                                        .ToArray(),
                                DirectoryIncarnations:
                                    branch.DirectoryIncarnations
                                        .ToArray(),
                                FileName:
                                    match
                            )
                        );
                    }
                }
            }
        }
        finally
        {
            DisposeAll(
                branches
            );
        }

        LinuxEnumerateDirectoryAtResult rootRevalidation =
            LinuxEnumerateDirectoryAt.Enumerate(
                dataRoot
            );

        if (!rootRevalidation.Success)
        {
            return Result(
                DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                    .HierarchyRevalidationFailed,
                rootLogical,
                logicalPath,
                physicalRootNames,
                [],
                rootRevalidation.Error ??
                    rootRevalidation.State.ToString()
            );
        }

        string[] finalPhysicalRoots =
            FindEquivalentNames(
                rootRevalidation.ChildNames,
                requestedComponents[0]
            );

        if (
            !physicalRootNames.SequenceEqual(
                finalPhysicalRoots,
                StringComparer.Ordinal))
        {
            return Result(
                DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                    .HierarchyRevalidationFailed,
                rootLogical,
                logicalPath,
                physicalRootNames,
                [],
                "The Windows-equivalent Data-root child set changed " +
                "during current-leaf observation."
            );
        }

        foreach (
            ObservedRepresentation observed
            in observedRepresentations)
        {
            string? revalidationError =
                RevalidateRepresentation(
                    dataRoot,
                    observed
                );

            if (revalidationError is not null)
            {
                return Result(
                    DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                        .HierarchyRevalidationFailed,
                    rootLogical,
                    logicalPath,
                    physicalRootNames,
                    [],
                    revalidationError
                );
            }
        }

        DataRelativePathRepairAggregateNamespaceCurrentFileRepresentation[]
            representations =
                observedRepresentations
                    .Select(
                        item =>
                            item.PublicRepresentation
                    )
                    .OrderBy(
                        item =>
                            item.RelativePath,
                        StringComparer.Ordinal
                    )
                    .ToArray();

        return Result(
            DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                .Analyzed,
            rootLogical,
            logicalPath,
            physicalRootNames,
            representations,
            error:
                null
        );
    }

    private static string?
        RevalidateRepresentation(
            LinuxNoFollowPathHandle dataRoot,
            ObservedRepresentation observed)
    {
        LinuxNoFollowPathHandle? currentDirectory =
            null;

        try
        {
            for (
                int index = 0;
                index <
                    observed.DirectoryComponents.Length;
                index++)
            {
                LinuxNoFollowPathHandle parent =
                    currentDirectory ??
                    dataRoot;

                LinuxOpenChildDirectoryReadOnlyAtResult opened =
                    LinuxOpenChildDirectoryReadOnlyAt.Open(
                        parent,
                        observed.DirectoryComponents[
                            index
                        ]
                    );

                if (
                    !opened.Success ||
                    opened.OpenedDirectory is null)
                {
                    return
                        "A previously observed physical directory " +
                        "could not be reacquired during hierarchy " +
                        "revalidation: " +
                        (
                            opened.Error ??
                            opened.State.ToString()
                        );
                }

                LinuxNoFollowPathHandle next =
                    opened.OpenedDirectory;

                LinuxOpenedDirectoryIncarnationResult incarnation =
                    LinuxOpenedDirectoryIncarnation.Capture(
                        next
                    );

                if (
                    !incarnation.Success ||
                    incarnation.Identity is null ||
                    !observed.DirectoryIncarnations[
                            index
                        ]
                        .SameIncarnationAs(
                            incarnation.Identity
                        ))
                {
                    next.Dispose();

                    return
                        "A previously observed physical directory " +
                        "changed incarnation during hierarchy " +
                        "revalidation.";
                }

                currentDirectory?.Dispose();

                currentDirectory =
                    next;
            }

            if (currentDirectory is null)
            {
                return
                    "Current-leaf hierarchy revalidation did not " +
                    "retain the physical namespace root.";
            }

            LinuxOpenChildRegularFileReadOnlyAtResult openedFile =
                LinuxOpenChildRegularFileReadOnlyAt.Open(
                    currentDirectory,
                    observed.FileName
                );

            if (
                !openedFile.Success ||
                openedFile.OpenedFile is null)
            {
                return
                    "The previously observed regular file could not " +
                    "be reacquired during hierarchy revalidation: " +
                    (
                        openedFile.Error ??
                        openedFile.State.ToString()
                    );
            }

            using LinuxOpenedChildHandle file =
                openedFile.OpenedFile;

            LinuxOpenedFileIncarnationResult fileIncarnation =
                LinuxOpenedFileIncarnation.Capture(
                    file
                );

            if (
                !fileIncarnation.Success ||
                fileIncarnation.Identity is null ||
                !observed.PublicRepresentation
                    .IncarnationIdentity
                    .SameIncarnationAs(
                        fileIncarnation.Identity
                    ))
            {
                return
                    "The previously observed regular file changed " +
                    "incarnation during hierarchy revalidation.";
            }

            LinuxOpenedFileContentObservationResult content =
                LinuxOpenedFileContentObservation.Observe(
                    file,
                    observed.PublicRepresentation.RelativePath
                );

            if (
                !content.Success ||
                content.Size !=
                    observed.PublicRepresentation.Size ||
                !string.Equals(
                    content.Sha256,
                    observed.PublicRepresentation.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                return
                    "The previously observed regular file changed " +
                    "content during hierarchy revalidation.";
            }

            return null;
        }
        finally
        {
            currentDirectory?.Dispose();
        }
    }

    // A registry hit alone is a hint, never a trusted fact: this also
    // re-reads the symlink's actual current target and requires it to
    // still name exactly the registered target before trusting it for
    // anything, per this project's "always re-derive fresh proof
    // immediately before mutating or trusting" rule. Mirrors
    // DataRelativePathTargetedConsumerCaseRepairPlanProjector's own
    // IsVerifiedKnownAlias - duplicated rather than shared, per this
    // codebase's convention that each type owns its own validation.
    private static bool IsVerifiedKnownAlias(
        LinuxNoFollowPathHandle aliasesDirectory,
        ILinuxOpenedHandle parent,
        string parentPath,
        string linkName)
    {
        DataRelativePathRepairAliasRegistryLookupResult lookup =
            DataRelativePathRepairAliasRegistry.TryFind(
                aliasesDirectory,
                parentPath,
                linkName
            );

        if (!lookup.Success)
        {
            return false;
        }

        LinuxReadSymlinkAtResult read =
            LinuxReadSymlinkAt.Read(
                parent,
                linkName
            );

        return
            read.Success &&
            string.Equals(
                read.Target,
                lookup.Record!.TargetName,
                StringComparison.Ordinal
            );
    }

    private static string[] FindEquivalentNames(
        IReadOnlyList<string> childNames,
        string requestedComponent)
    {
        string logical =
            requestedComponent.ToUpperInvariant();

        return childNames
            .Where(
                name =>
                    IsValidPhysicalComponent(
                        name
                    ) &&
                    string.Equals(
                        name.ToUpperInvariant(),
                        logical,
                        StringComparison.Ordinal
                    )
            )
            .OrderBy(
                name =>
                    name,
                StringComparer.Ordinal
            )
            .ToArray();
    }

    private static bool TryNormalizeRootLogicalPath(
        string? value,
        out string normalized)
    {
        normalized =
            string.Empty;

        if (
            string.IsNullOrWhiteSpace(
                value
            ) ||
            !IsValidPhysicalComponent(
                value
            ))
        {
            return false;
        }

        string candidate =
            value.ToUpperInvariant();

        if (
            !string.Equals(
                value,
                candidate,
                StringComparison.Ordinal))
        {
            return false;
        }

        normalized =
            candidate;

        return true;
    }

    private static bool TryNormalizeRequestedPath(
        string? value,
        out string normalized,
        out string[] components)
    {
        normalized =
            string.Empty;

        components =
            [];

        if (
            string.IsNullOrWhiteSpace(
                value
            ) ||
            value.Contains('\0') ||
            Path.IsPathRooted(
                value
            ) ||
            value.StartsWith(
                '\\'))
        {
            return false;
        }

        string slashNormalized =
            value.Replace(
                '\\',
                '/'
            );

        components =
            slashNormalized.Split(
                '/',
                StringSplitOptions.None
            );

        if (
            components.Length == 0 ||
            components.Any(
                component =>
                    !IsValidPhysicalComponent(
                        component
                    )))
        {
            components =
                [];

            return false;
        }

        normalized =
            string.Join(
                '/',
                components
            );

        return true;
    }

    private static bool IsValidPhysicalComponent(
        string? value)
    {
        return
            !string.IsNullOrEmpty(
                value) &&
            value is not "." and not ".." &&
            !value.Contains('/') &&
            !value.Contains('\\') &&
            !value.Contains('\0');
    }

    private static void DisposeAll(
        IEnumerable<DirectoryBranch> branches)
    {
        foreach (
            DirectoryBranch branch
            in branches)
        {
            branch.Dispose();
        }
    }

    private static
        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis
        Invalid(
            string? root,
            string? requestedPath,
            string error)
    {
        WindowsLogicalPath logical =
            new(
                string.Empty
            );

        if (
            !string.IsNullOrWhiteSpace(
                requestedPath))
        {
            try
            {
                logical =
                    WindowsLogicalPath.FromRelativePath(
                        requestedPath
                    );
            }
            catch (ArgumentException)
            {
                // Invalid input retains the empty logical-path sentinel.
            }
        }

        return Result(
            DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                .InvalidInput,
            root ??
                string.Empty,
            logical,
            [],
            [],
            error
        );
    }

    private static
        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis
        Result(
            DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                state,
            string rootWindowsLogicalPath,
            WindowsLogicalPath logicalPath,
            IReadOnlyList<string> physicalRootNames,
            IReadOnlyList<
                DataRelativePathRepairAggregateNamespaceCurrentFileRepresentation
            > representations,
            string? error)
    {
        return new(
            State:
                state,
            RootWindowsLogicalPath:
                rootWindowsLogicalPath,
            LogicalPath:
                logicalPath,
            PhysicalRootNames:
                physicalRootNames,
            Representations:
                representations,
            Error:
                error
        );
    }

    private sealed class DirectoryBranch :
        IDisposable
    {
        public DirectoryBranch(
            LinuxNoFollowPathHandle directory,
            string[] physicalComponents,
            LinuxDirectoryIncarnationIdentity[]
                directoryIncarnations)
        {
            Directory =
                directory;

            PhysicalComponents =
                physicalComponents;

            DirectoryIncarnations =
                directoryIncarnations;
        }

        public LinuxNoFollowPathHandle Directory
        {
            get;
        }

        public string[] PhysicalComponents
        {
            get;
        }

        public LinuxDirectoryIncarnationIdentity[]
            DirectoryIncarnations
        {
            get;
        }

        public void Dispose()
        {
            Directory.Dispose();
        }
    }

    private sealed record ObservedRepresentation(
        DataRelativePathRepairAggregateNamespaceCurrentFileRepresentation
            PublicRepresentation,
        string[] DirectoryComponents,
        LinuxDirectoryIncarnationIdentity[] DirectoryIncarnations,
        string FileName
    );
}
