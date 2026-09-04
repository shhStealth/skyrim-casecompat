namespace CaseCompat.Core.Analysis;

/*
 * Pure snapshot lookup.
 *
 * Authority is limited to evidence already recorded by
 * WindowsNamespaceAnalyzer. This class performs no filesystem access,
 * pathname reopen, hashing, provider selection, or repair operation.
 */
public static class WindowsNamespaceSnapshotFileResolver
{
    private sealed record SnapshotEntry(
        string RelativePath,
        string ParentRelativePath,
        string Name,
        WindowsLogicalPath LogicalPath,
        WindowsNamespacePhysicalParticipant Participant
    );

    /*
     * WindowsNamespaceAnalysis is immutable snapshot evidence for the
     * lifetime of a lookup operation graph.
     *
     * Preparing that evidence is comparatively expensive on large
     * deployments: validating/building physical entries and directory
     * observations walks the entire analyzed namespace.
     *
     * ConditionalWeakTable keeps one derived index per analysis object
     * without extending the lifetime of the source analysis itself.
     */
    private sealed record AnalysisShapeValidation(
        bool Valid,
        string? Error
    );

    private static readonly
        System.Runtime.CompilerServices.ConditionalWeakTable<
            WindowsNamespaceAnalysis,
            AnalysisShapeValidation
        > AnalysisShapeValidations =
            new();

    private sealed record PreparedSnapshot(
        bool Valid,
        string? Error,
        Dictionary<
            string,
            WindowsNamespaceDirectoryLookupObservation
        > LookupObservations,
        Dictionary<
            string,
            SnapshotEntry[]
        > DirectChildrenByParent,
        Dictionary<
            string,
            string[]
        > ChildNamesByParent,
        Dictionary<
            string,
            HashSet<string>
        > ExactChildNamesByParent,
        Dictionary<
            string,
            Dictionary<
                WindowsLogicalPath,
                string[]
            >
        > WindowsEquivalentChildNamesByParent,
        string[]? DataRootChildNames,
        bool HasRootParticipant
    );

    private static readonly
        System.Runtime.CompilerServices.ConditionalWeakTable<
            WindowsNamespaceAnalysis,
            PreparedSnapshot
        > PreparedSnapshots =
            new();

    public static WindowsNamespaceSnapshotFileLookup Resolve(
        WindowsNamespaceAnalysis analysis,
        string? requestedRelativePath)
    {
        ArgumentNullException.ThrowIfNull(
            analysis
        );

        if (!TryParseRequestedPath(
                requestedRelativePath,
                out string[] requestedComponents,
                out string? requestError))
        {
            return Failure(
                analysis,
                requestedRelativePath,
                requestedLogicalPath:
                    null,
                WindowsNamespaceSnapshotFileLookupState
                    .InvalidRequestedPath,
                failedComponentIndex:
                    null,
                steps:
                    Array.Empty<
                        WindowsNamespaceSnapshotFileLookupStep
                    >(),
                requestError
            );
        }

        WindowsLogicalPath requestedLogicalPath =
            WindowsLogicalPath.FromRelativePath(
                string.Join(
                    "/",
                    requestedComponents
                )
            );

        AnalysisShapeValidation shapeValidation =
            AnalysisShapeValidations.GetValue(
                analysis,
                ValidateAnalysisShape
            );

        if (!shapeValidation.Valid)
        {
            return Failure(
                analysis,
                requestedRelativePath,
                requestedLogicalPath,
                WindowsNamespaceSnapshotFileLookupState
                    .InvalidSnapshotEvidence,
                failedComponentIndex:
                    null,
                steps:
                    Array.Empty<
                        WindowsNamespaceSnapshotFileLookupStep
                    >(),
                shapeValidation.Error
            );
        }

        if (!analysis.Complete)
        {
            return Failure(
                analysis,
                requestedRelativePath,
                requestedLogicalPath,
                WindowsNamespaceSnapshotFileLookupState
                    .IncompleteAnalysis,
                failedComponentIndex:
                    null,
                steps:
                    Array.Empty<
                        WindowsNamespaceSnapshotFileLookupStep
                    >(),
                "The source Windows-namespace analysis is incomplete."
            );
        }

        if (!IsWithinAnalyzedNamespace(
                analysis.RootLogicalPath,
                requestedLogicalPath))
        {
            return Failure(
                analysis,
                requestedRelativePath,
                requestedLogicalPath,
                WindowsNamespaceSnapshotFileLookupState
                    .RequestOutsideAnalyzedNamespace,
                failedComponentIndex:
                    null,
                steps:
                    Array.Empty<
                        WindowsNamespaceSnapshotFileLookupStep
                    >(),
                $"Requested Windows-logical path " +
                $"\"{requestedLogicalPath.Value}\" is outside analyzed " +
                $"namespace \"{analysis.RootLogicalPath.Value}\"."
            );
        }

        PreparedSnapshot prepared =
            PreparedSnapshots.GetValue(
                analysis,
                PrepareSnapshot
            );

        if (!prepared.Valid)
        {
            return Failure(
                analysis,
                requestedRelativePath,
                requestedLogicalPath,
                WindowsNamespaceSnapshotFileLookupState
                    .InvalidSnapshotEvidence,
                failedComponentIndex:
                    null,
                steps:
                    Array.Empty<
                        WindowsNamespaceSnapshotFileLookupStep
                    >(),
                prepared.Error
            );
        }

        Dictionary<
            string,
            WindowsNamespaceDirectoryLookupObservation
        > lookupObservations =
            prepared.LookupObservations;

        if (!prepared.HasRootParticipant)
        {
            return Failure(
                analysis,
                requestedRelativePath,
                requestedLogicalPath,
                WindowsNamespaceSnapshotFileLookupState
                    .InvalidSnapshotEvidence,
                failedComponentIndex:
                    0,
                steps:
                    Array.Empty<
                        WindowsNamespaceSnapshotFileLookupStep
                    >(),
                "The snapshot contains no physical participant for the " +
                "analyzed Windows-logical root."
            );
        }

        var steps =
            new List<
                WindowsNamespaceSnapshotFileLookupStep
            >();

        string currentParent =
            ".";

        for (
            int index = 0;
            index < requestedComponents.Length;
            index++)
        {
            string requestedComponent =
                requestedComponents[index];

            bool finalComponent =
                index ==
                requestedComponents.Length - 1;

            if (!lookupObservations.TryGetValue(
                    currentParent,
                    out WindowsNamespaceDirectoryLookupObservation?
                        parentObservation))
            {
                return Failure(
                    analysis,
                    requestedRelativePath,
                    requestedLogicalPath,
                    WindowsNamespaceSnapshotFileLookupState
                        .InvalidSnapshotEvidence,
                    failedComponentIndex:
                        index,
                    steps,
                    $"No unique directory lookup observation exists for " +
                    $"physical parent \"{currentParent}\"."
                );
            }

            SnapshotEntry[] directChildren =
                prepared.DirectChildrenByParent.TryGetValue(
                    currentParent,
                    out SnapshotEntry[]? preparedChildren
                )
                    ? preparedChildren
                    : Array.Empty<SnapshotEntry>();

            string[] physicalChildNames =
                string.Equals(
                    currentParent,
                    ".",
                    StringComparison.Ordinal
                )
                    ? prepared.DataRootChildNames!
                    : prepared.ChildNamesByParent.TryGetValue(
                        currentParent,
                        out string[]? preparedChildNames
                    )
                        ? preparedChildNames
                        : Array.Empty<string>();

            WindowsLogicalPath requestedComponentLogicalPath =
                WindowsLogicalPath.FromRelativePath(
                    requestedComponent
                );

            string[] windowsEquivalentNames;

            if (
                prepared.WindowsEquivalentChildNamesByParent.TryGetValue(
                    currentParent,
                    out Dictionary<
                        WindowsLogicalPath,
                        string[]
                    >? equivalentNamesByLogicalPath))
            {
                windowsEquivalentNames =
                    equivalentNamesByLogicalPath.TryGetValue(
                        requestedComponentLogicalPath,
                        out string[]? preparedEquivalentNames
                    )
                        ? preparedEquivalentNames
                        : Array.Empty<string>();
            }
            else
            {
                windowsEquivalentNames =
                    physicalChildNames
                        .Where(
                            name =>
                                WindowsEquivalentComponent(
                                    name,
                                    requestedComponent
                                )
                        )
                        .OrderBy(
                            name =>
                                name,
                            StringComparer.Ordinal
                        )
                        .ToArray();
            }

            bool exactNameExists;

            if (prepared.ExactChildNamesByParent.TryGetValue(
                    currentParent,
                    out HashSet<string>? preparedExactNames))
            {
                exactNameExists =
                    preparedExactNames.Contains(
                        requestedComponent
                    );
            }
            else
            {
                exactNameExists =
                    physicalChildNames.Any(
                        name =>
                            string.Equals(
                                name,
                                requestedComponent,
                                StringComparison.Ordinal
                            )
                    );
            }

            if (exactNameExists)
            {
                SnapshotEntry[] exactCandidates =
                    directChildren
                        .Where(
                            entry =>
                                string.Equals(
                                    entry.Name,
                                    requestedComponent,
                                    StringComparison.Ordinal
                                )
                        )
                        .ToArray();

                if (exactCandidates.Length != 1)
                {
                    return Failure(
                        analysis,
                        requestedRelativePath,
                        requestedLogicalPath,
                        WindowsNamespaceSnapshotFileLookupState
                            .InvalidSnapshotEvidence,
                        index,
                        steps,
                        $"Exact physical child \"{requestedComponent}\" " +
                        "is present in the recorded child-name inventory " +
                        "but does not have exactly one matching physical " +
                        "participant."
                    );
                }

                SnapshotEntry exact =
                    exactCandidates[0];
                WindowsNamespaceSnapshotFileLookupStepKind?
                    invalidKind =
                        ValidateSelectedKind(
                            exact.Participant,
                            finalComponent
                        );

                if (invalidKind is not null)
                {
                    steps.Add(
                        Step(
                            index,
                            requestedComponent,
                            currentParent,
                            parentObservation,
                            invalidKind.Value,
                            exact.Name,
                            windowsEquivalentNames
                        )
                    );

                    return Failure(
                        analysis,
                        requestedRelativePath,
                        requestedLogicalPath,
                        StateForStep(
                            invalidKind.Value
                        ),
                        index,
                        steps,
                        DescribeInvalidKind(
                            invalidKind.Value,
                            requestedComponent
                        )
                    );
                }

                steps.Add(
                    Step(
                        index,
                        requestedComponent,
                        currentParent,
                        parentObservation,
                        WindowsNamespaceSnapshotFileLookupStepKind
                            .ExactSpelling,
                        exact.Name,
                        windowsEquivalentNames
                    )
                );

                if (finalComponent)
                {
                    return Success(
                        analysis,
                        requestedRelativePath,
                        requestedLogicalPath,
                        exact.Participant,
                        steps
                    );
                }

                currentParent =
                    exact.RelativePath;

                continue;
            }

            if (
                parentObservation.Error is not null ||
                parentObservation.CasefoldEnabled is null)
            {
                steps.Add(
                    Step(
                        index,
                        requestedComponent,
                        currentParent,
                        parentObservation,
                        WindowsNamespaceSnapshotFileLookupStepKind
                            .CasefoldUnknown,
                        selectedPhysicalName:
                            null,
                        windowsEquivalentNames
                    )
                );

                return Failure(
                    analysis,
                    requestedRelativePath,
                    requestedLogicalPath,
                    WindowsNamespaceSnapshotFileLookupState
                        .CasefoldUnknown,
                    index,
                    steps,
                    $"Exact spelling \"{requestedComponent}\" is absent " +
                    $"and lookup semantics for physical parent " +
                    $"\"{currentParent}\" are unavailable."
                );
            }

            if (!parentObservation.CasefoldEnabled.Value)
            {
                steps.Add(
                    Step(
                        index,
                        requestedComponent,
                        currentParent,
                        parentObservation,
                        WindowsNamespaceSnapshotFileLookupStepKind
                            .Missing,
                        selectedPhysicalName:
                            null,
                        windowsEquivalentNames
                    )
                );

                return Failure(
                    analysis,
                    requestedRelativePath,
                    requestedLogicalPath,
                    WindowsNamespaceSnapshotFileLookupState
                        .Missing,
                    index,
                    steps,
                    $"Exact spelling \"{requestedComponent}\" is absent " +
                    $"under strict physical parent " +
                    $"\"{currentParent}\"."
                );
            }

            /*
             * The snapshot records that the parent has ext4 casefold
             * enabled, but it does not currently record the filesystem's
             * Unicode casefold table/encoding.
             *
             * ASCII A-Z/a-z equivalence is therefore the only
             * casefold-dependent equivalence this pure interpreter claims.
             *
             * If any child name involved in this parent is non-ASCII,
             * absence/selection may depend on Unicode casefold semantics
             * that are not present in the snapshot. Fail closed.
             */
            if (
                !IsAscii(
                    requestedComponent
                ) ||
                physicalChildNames.Any(
                    name =>
                        !IsAscii(
                            name
                        )
                ))
            {
                steps.Add(
                    Step(
                        index,
                        requestedComponent,
                        currentParent,
                        parentObservation,
                        WindowsNamespaceSnapshotFileLookupStepKind
                            .CasefoldEquivalenceUnknown,
                        selectedPhysicalName:
                            null,
                        windowsEquivalentNames
                    )
                );

                return Failure(
                    analysis,
                    requestedRelativePath,
                    requestedLogicalPath,
                    WindowsNamespaceSnapshotFileLookupState
                        .CasefoldEquivalenceUnknown,
                    index,
                    steps,
                    "The lookup depends on non-ASCII casefold " +
                    "equivalence that is not encoded in the snapshot."
                );
            }

            string[] asciiEquivalentNames =
                physicalChildNames
                    .Where(
                        name =>
                            AsciiCaseEquivalent(
                                name,
                                requestedComponent
                            )
                    )
                    .OrderBy(
                        name =>
                            name,
                        StringComparer.Ordinal
                    )
                    .ToArray();

            if (asciiEquivalentNames.Length == 0)
            {
                steps.Add(
                    Step(
                        index,
                        requestedComponent,
                        currentParent,
                        parentObservation,
                        WindowsNamespaceSnapshotFileLookupStepKind
                            .Missing,
                        selectedPhysicalName:
                            null,
                        windowsEquivalentNames
                    )
                );

                return Failure(
                    analysis,
                    requestedRelativePath,
                    requestedLogicalPath,
                    WindowsNamespaceSnapshotFileLookupState
                        .Missing,
                    index,
                    steps,
                    $"No ASCII casefold-equivalent physical child exists " +
                    $"for \"{requestedComponent}\"."
                );
            }

            if (asciiEquivalentNames.Length != 1)
            {
                steps.Add(
                    Step(
                        index,
                        requestedComponent,
                        currentParent,
                        parentObservation,
                        WindowsNamespaceSnapshotFileLookupStepKind
                            .AmbiguousEquivalent,
                        selectedPhysicalName:
                            null,
                        windowsEquivalentNames
                    )
                );

                return Failure(
                    analysis,
                    requestedRelativePath,
                    requestedLogicalPath,
                    WindowsNamespaceSnapshotFileLookupState
                        .AmbiguousEquivalent,
                    index,
                    steps,
                    $"Multiple ASCII casefold-equivalent physical " +
                    $"children exist for \"{requestedComponent}\"."
                );
            }

            string selectedPhysicalName =
                asciiEquivalentNames[0];

            SnapshotEntry[] selectedCandidates =
                directChildren
                    .Where(
                        entry =>
                            string.Equals(
                                entry.Name,
                                selectedPhysicalName,
                                StringComparison.Ordinal
                            )
                    )
                    .ToArray();

            if (selectedCandidates.Length != 1)
            {
                return Failure(
                    analysis,
                    requestedRelativePath,
                    requestedLogicalPath,
                    WindowsNamespaceSnapshotFileLookupState
                        .InvalidSnapshotEvidence,
                    index,
                    steps,
                    $"Casefold-selected physical child " +
                    $"\"{selectedPhysicalName}\" does not have exactly " +
                    "one matching physical participant."
                );
            }

            SnapshotEntry selected =
                selectedCandidates[0];

            WindowsNamespaceSnapshotFileLookupStepKind?
                selectedInvalidKind =
                    ValidateSelectedKind(
                        selected.Participant,
                        finalComponent
                    );

            if (selectedInvalidKind is not null)
            {
                steps.Add(
                    Step(
                        index,
                        requestedComponent,
                        currentParent,
                        parentObservation,
                        selectedInvalidKind.Value,
                        selected.Name,
                        windowsEquivalentNames
                    )
                );

                return Failure(
                    analysis,
                    requestedRelativePath,
                    requestedLogicalPath,
                    StateForStep(
                        selectedInvalidKind.Value
                    ),
                    index,
                    steps,
                    DescribeInvalidKind(
                        selectedInvalidKind.Value,
                        requestedComponent
                    )
                );
            }

            steps.Add(
                Step(
                    index,
                    requestedComponent,
                    currentParent,
                    parentObservation,
                    WindowsNamespaceSnapshotFileLookupStepKind
                        .CasefoldEquivalent,
                    selected.Name,
                    windowsEquivalentNames
                )
            );

            if (finalComponent)
            {
                return Success(
                    analysis,
                    requestedRelativePath,
                    requestedLogicalPath,
                    selected.Participant,
                    steps
                );
            }

            currentParent =
                selected.RelativePath;
        }

        return Failure(
            analysis,
            requestedRelativePath,
            requestedLogicalPath,
            WindowsNamespaceSnapshotFileLookupState
                .InvalidSnapshotEvidence,
            failedComponentIndex:
                null,
            steps,
            "Snapshot lookup ended without a file target."
        );
    }

    private static bool TryParseRequestedPath(
        string? requestedRelativePath,
        out string[] components,
        out string? error)
    {
        return WindowsDataRelativePathParser.TryParse(
            requestedRelativePath,
            out components,
            out error
        );
    }

    private static AnalysisShapeValidation ValidateAnalysisShape(
        WindowsNamespaceAnalysis analysis)
    {
        bool valid =
            TryValidateAnalysisShape(
                analysis,
                out string? error
            );

        return new AnalysisShapeValidation(
            Valid:
                valid,
            Error:
                error
        );
    }

    private static bool TryValidateAnalysisShape(
        WindowsNamespaceAnalysis analysis,
        out string? error)
    {
        error =
            null;

        if (
            analysis.Errors is null ||
            analysis.Nodes is null ||
            analysis.DirectoryLookupObservations is null)
        {
            error =
                "The Windows-namespace analysis contains a null " +
                "top-level collection.";

            return false;
        }

        if (string.IsNullOrWhiteSpace(
                analysis.RootLogicalPath.Value))
        {
            error =
                "The analyzed Windows-logical root is empty.";

            return false;
        }

        if (analysis.RootLogicalPath.Value.Contains(
                '/',
                StringComparison.Ordinal))
        {
            error =
                "The analyzed Windows-logical root is not one direct " +
                "Data child.";

            return false;
        }

        WindowsLogicalPath normalizedRoot;

        try
        {
            normalizedRoot =
                WindowsLogicalPath.FromRelativePath(
                    analysis.RootLogicalPath.Value
                );
        }
        catch (Exception ex)
        {
            error =
                $"The analyzed Windows-logical root is invalid: " +
                ex.Message;

            return false;
        }

        if (normalizedRoot !=
            analysis.RootLogicalPath)
        {
            error =
                "The analyzed Windows-logical root is not normalized.";

            return false;
        }

        if (analysis.DataRootChildNames is not null)
        {
            var seenRootChildNames =
                new HashSet<string>(
                    StringComparer.Ordinal
                );

            foreach (
                string? childName
                in analysis.DataRootChildNames)
            {
                if (
                    childName is null ||
                    childName.Length == 0 ||
                    childName.Contains(
                        '/',
                        StringComparison.Ordinal
                    ) ||
                    childName.Contains(
                        '\\',
                        StringComparison.Ordinal
                    ) ||
                    string.Equals(
                        childName,
                        ".",
                        StringComparison.Ordinal
                    ) ||
                    string.Equals(
                        childName,
                        "..",
                        StringComparison.Ordinal
                    ))
                {
                    error =
                        "The Data-root child-name inventory contains " +
                        "an invalid direct-child spelling.";

                    return false;
                }

                if (!seenRootChildNames.Add(
                        childName))
                {
                    error =
                        $"The Data-root child-name inventory contains " +
                        $"duplicate physical spelling \"{childName}\".";

                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsWithinAnalyzedNamespace(
        WindowsLogicalPath root,
        WindowsLogicalPath requested)
    {
        if (requested == root)
        {
            return true;
        }

        return requested.Value.StartsWith(
            root.Value + "/",
            StringComparison.Ordinal
        );
    }

    private static PreparedSnapshot PrepareSnapshot(
        WindowsNamespaceAnalysis analysis)
    {
        if (!TryBuildSnapshotEntries(
                analysis,
                out SnapshotEntry[] entries,
                out string? entryError))
        {
            return PreparationFailure(
                entryError
            );
        }

        if (!TryBuildLookupObservationMap(
                analysis,
                out Dictionary<
                    string,
                    WindowsNamespaceDirectoryLookupObservation
                > lookupObservations,
                out string? lookupError))
        {
            return PreparationFailure(
                lookupError
            );
        }

        Dictionary<
            string,
            SnapshotEntry[]
        > directChildrenByParent =
            entries
                .GroupBy(
                    entry =>
                        entry.ParentRelativePath,
                    StringComparer.Ordinal
                )
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group
                            .OrderBy(
                                entry =>
                                    entry.Name,
                                StringComparer.Ordinal
                            )
                            .ToArray(),
                    StringComparer.Ordinal
                );

        Dictionary<
            string,
            string[]
        > childNamesByParent =
            directChildrenByParent
                .ToDictionary(
                    pair =>
                        pair.Key,
                    pair =>
                        pair.Value
                            .Select(
                                entry =>
                                    entry.Name
                            )
                            .ToArray(),
                    StringComparer.Ordinal
                );

        string[]? dataRootChildNames =
            analysis.DataRootChildNames?
                .OrderBy(
                    name =>
                        name,
                    StringComparer.Ordinal
                )
                .ToArray();

        var lookupChildNamesByParent =
            new Dictionary<
                string,
                string[]
            >(
                childNamesByParent,
                StringComparer.Ordinal
            );

        if (dataRootChildNames is not null)
        {
            lookupChildNamesByParent["."] =
                dataRootChildNames;
        }
        else
        {
            /*
             * Preserve the old resolver's fallback behavior when a
             * Data-root child inventory is unavailable rather than
             * inventing namespace-wide authority for ".".
             */
            lookupChildNamesByParent.Remove(
                "."
            );
        }

        Dictionary<
            string,
            HashSet<string>
        > exactChildNamesByParent =
            lookupChildNamesByParent
                .ToDictionary(
                    pair =>
                        pair.Key,
                    pair =>
                        new HashSet<string>(
                            pair.Value,
                            StringComparer.Ordinal
                        ),
                    StringComparer.Ordinal
                );

        Dictionary<
            string,
            Dictionary<
                WindowsLogicalPath,
                string[]
            >
        > windowsEquivalentChildNamesByParent =
            lookupChildNamesByParent
                .ToDictionary(
                    pair =>
                        pair.Key,
                    pair =>
                        pair.Value
                            .GroupBy(
                                name =>
                                    WindowsLogicalPath.FromRelativePath(
                                        name
                                    )
                            )
                            .ToDictionary(
                                group =>
                                    group.Key,
                                group =>
                                    group
                                        .OrderBy(
                                            name =>
                                                name,
                                            StringComparer.Ordinal
                                        )
                                        .ToArray()
                            ),
                    StringComparer.Ordinal
                );

        bool hasRootParticipant =
            entries.Any(
                entry =>
                    entry.LogicalPath ==
                        analysis.RootLogicalPath
            );

        return new PreparedSnapshot(
            Valid:
                true,
            Error:
                null,
            LookupObservations:
                lookupObservations,
            DirectChildrenByParent:
                directChildrenByParent,
            ChildNamesByParent:
                childNamesByParent,
            ExactChildNamesByParent:
                exactChildNamesByParent,
            WindowsEquivalentChildNamesByParent:
                windowsEquivalentChildNamesByParent,
            DataRootChildNames:
                dataRootChildNames,
            HasRootParticipant:
                hasRootParticipant
        );
    }

    private static PreparedSnapshot PreparationFailure(
        string? error)
    {
        return new PreparedSnapshot(
            Valid:
                false,
            Error:
                error,
            LookupObservations:
                new Dictionary<
                    string,
                    WindowsNamespaceDirectoryLookupObservation
                >(
                    StringComparer.Ordinal
                ),
            DirectChildrenByParent:
                new Dictionary<
                    string,
                    SnapshotEntry[]
                >(
                    StringComparer.Ordinal
                ),
            ChildNamesByParent:
                new Dictionary<
                    string,
                    string[]
                >(
                    StringComparer.Ordinal
                ),
            ExactChildNamesByParent:
                new Dictionary<
                    string,
                    HashSet<string>
                >(
                    StringComparer.Ordinal
                ),
            WindowsEquivalentChildNamesByParent:
                new Dictionary<
                    string,
                    Dictionary<
                        WindowsLogicalPath,
                        string[]
                    >
                >(
                    StringComparer.Ordinal
                ),
            DataRootChildNames:
                null,
            HasRootParticipant:
                false
        );
    }

    private static bool TryBuildSnapshotEntries(
        WindowsNamespaceAnalysis analysis,
        out SnapshotEntry[] entries,
        out string? error)
    {
        var built =
            new List<SnapshotEntry>();

        var physicalPaths =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        foreach (WindowsNamespaceNode? node in analysis.Nodes)
        {
            if (
                node is null ||
                node.Participants is null ||
                string.IsNullOrWhiteSpace(
                    node.LogicalPath.Value
                ))
            {
                entries =
                    Array.Empty<SnapshotEntry>();

                error =
                    "The snapshot contains a null or malformed logical node.";

                return false;
            }

            foreach (
                WindowsNamespacePhysicalParticipant? participant
                in node.Participants)
            {
                if (participant is null)
                {
                    entries =
                        Array.Empty<SnapshotEntry>();

                    error =
                        "The snapshot contains a null physical participant.";

                    return false;
                }

                if (!TryNormalizePhysicalRelativePath(
                        participant.RelativePath,
                        out string physicalRelativePath,
                        out string[] physicalComponents,
                        out string? pathError))
                {
                    entries =
                        Array.Empty<SnapshotEntry>();

                    error =
                        $"Invalid physical participant path: {pathError}";

                    return false;
                }

                string physicalName =
                    physicalComponents[^1];

                if (!string.Equals(
                        participant.Name,
                        physicalName,
                        StringComparison.Ordinal))
                {
                    entries =
                        Array.Empty<SnapshotEntry>();

                    error =
                        $"Participant name \"{participant.Name}\" does not " +
                        $"match final physical path component " +
                        $"\"{physicalName}\".";

                    return false;
                }

                WindowsLogicalPath participantLogicalPath =
                    WindowsLogicalPath.FromRelativePath(
                        physicalRelativePath
                    );

                if (participantLogicalPath !=
                    node.LogicalPath)
                {
                    entries =
                        Array.Empty<SnapshotEntry>();

                    error =
                        $"Physical participant \"{physicalRelativePath}\" " +
                        "is stored under the wrong Windows-logical node.";

                    return false;
                }

                if (!IsWithinAnalyzedNamespace(
                        analysis.RootLogicalPath,
                        participantLogicalPath))
                {
                    entries =
                        Array.Empty<SnapshotEntry>();

                    error =
                        $"Physical participant \"{physicalRelativePath}\" " +
                        "lies outside the analyzed namespace.";

                    return false;
                }

                if (!physicalPaths.Add(
                        physicalRelativePath))
                {
                    entries =
                        Array.Empty<SnapshotEntry>();

                    error =
                        $"Duplicate physical participant path exists in " +
                        $"the snapshot: \"{physicalRelativePath}\".";

                    return false;
                }

                string parentRelativePath =
                    physicalComponents.Length == 1
                        ? "."
                        : string.Join(
                            "/",
                            physicalComponents[
                                ..^1
                            ]
                        );

                built.Add(
                    new SnapshotEntry(
                        RelativePath:
                            physicalRelativePath,
                        ParentRelativePath:
                            parentRelativePath,
                        Name:
                            physicalName,
                        LogicalPath:
                            participantLogicalPath,
                        Participant:
                            participant
                    )
                );
            }
        }

        entries =
            built
                .OrderBy(
                    entry =>
                        entry.RelativePath,
                    StringComparer.Ordinal
                )
                .ToArray();

        if (analysis.DataRootChildNames is not null)
        {
            var rootChildNames =
                new HashSet<string>(
                    analysis.DataRootChildNames,
                    StringComparer.Ordinal
                );

            SnapshotEntry? missingRootInventoryEntry =
                entries.FirstOrDefault(
                    entry =>
                        string.Equals(
                            entry.ParentRelativePath,
                            ".",
                            StringComparison.Ordinal
                        ) &&
                        !rootChildNames.Contains(
                            entry.Name
                        )
                );

            if (missingRootInventoryEntry is not null)
            {
                entries =
                    Array.Empty<SnapshotEntry>();

                error =
                    $"Root participant " +
                    $"\"{missingRootInventoryEntry.Name}\" is absent " +
                    "from the complete Data-root child-name inventory.";

                return false;
            }
        }

        error =
            null;

        return true;
    }

    private static bool TryBuildLookupObservationMap(
        WindowsNamespaceAnalysis analysis,
        out Dictionary<
            string,
            WindowsNamespaceDirectoryLookupObservation
        > observations,
        out string? error)
    {
        observations =
            new Dictionary<
                string,
                WindowsNamespaceDirectoryLookupObservation
            >(
                StringComparer.Ordinal
            );

        foreach (
            WindowsNamespaceDirectoryLookupObservation? observation
            in analysis.DirectoryLookupObservations)
        {
            if (observation is null)
            {
                error =
                    "The snapshot contains a null directory lookup " +
                    "observation.";

                return false;
            }

            string key;

            if (string.Equals(
                    observation.RelativePath,
                    ".",
                    StringComparison.Ordinal))
            {
                key =
                    ".";
            }
            else
            {
                if (!TryNormalizePhysicalRelativePath(
                        observation.RelativePath,
                        out string physicalRelativePath,
                        out _,
                        out string? pathError))
                {
                    error =
                        $"Invalid directory lookup observation path: " +
                        pathError;

                    return false;
                }

                key =
                    physicalRelativePath;
            }

            if (!observations.TryAdd(
                    key,
                    observation))
            {
                error =
                    $"Multiple directory lookup observations exist for " +
                    $"physical path \"{key}\".";

                return false;
            }
        }

        if (!observations.ContainsKey(
                "."))
        {
            error =
                "The snapshot does not contain the Data-root lookup " +
                "semantics observation.";

            return false;
        }

        error =
            null;

        return true;
    }

    private static bool TryNormalizePhysicalRelativePath(
        string? relativePath,
        out string normalized,
        out string[] components,
        out string? error)
    {
        normalized =
            string.Empty;

        components =
            Array.Empty<string>();

        error =
            null;

        if (string.IsNullOrWhiteSpace(
                relativePath))
        {
            error =
                "path is empty.";

            return false;
        }

        string replaced =
            relativePath.Replace(
                '\\',
                '/'
            );

        if (
            replaced.StartsWith(
                "/",
                StringComparison.Ordinal
            ) ||
            replaced.EndsWith(
                "/",
                StringComparison.Ordinal
            ))
        {
            error =
                "path is not a canonical Data-relative spelling.";

            return false;
        }

        components =
            replaced.Split(
                '/',
                StringSplitOptions.None
            );

        if (
            components.Length == 0 ||
            components.Any(
                component =>
                    component.Length == 0 ||
                    string.Equals(
                        component,
                        ".",
                        StringComparison.Ordinal
                    ) ||
                    string.Equals(
                        component,
                        "..",
                        StringComparison.Ordinal
                    )))
        {
            error =
                "path contains an empty or traversal component.";

            components =
                Array.Empty<string>();

            return false;
        }

        normalized =
            string.Join(
                "/",
                components
            );

        return true;
    }

    private static bool WindowsEquivalentComponent(
        string physicalName,
        string requestedComponent)
    {
        return
            WindowsLogicalPath.FromRelativePath(
                physicalName
            ) ==
            WindowsLogicalPath.FromRelativePath(
                requestedComponent
            );
    }

    private static bool IsAscii(
        string value)
    {
        return value.All(
            character =>
                character <= '\u007f'
        );
    }

    private static bool AsciiCaseEquivalent(
        string first,
        string second)
    {
        if (first.Length !=
            second.Length)
        {
            return false;
        }

        for (
            int index = 0;
            index < first.Length;
            index++)
        {
            char firstCharacter =
                FoldAscii(
                    first[index]
                );

            char secondCharacter =
                FoldAscii(
                    second[index]
                );

            if (firstCharacter !=
                secondCharacter)
            {
                return false;
            }
        }

        return true;
    }

    private static char FoldAscii(
        char value)
    {
        if (
            value is >= 'a' and <= 'z')
        {
            return (char)(
                value - 'a' + 'A'
            );
        }

        return value;
    }

    private static WindowsNamespaceSnapshotFileLookupStepKind?
        ValidateSelectedKind(
            WindowsNamespacePhysicalParticipant participant,
            bool finalComponent)
    {
        if (finalComponent)
        {
            return participant.Kind switch
            {
                WindowsNamespacePhysicalObjectKind.File =>
                    null,

                WindowsNamespacePhysicalObjectKind.Directory =>
                    WindowsNamespaceSnapshotFileLookupStepKind
                        .NotFile,

                WindowsNamespacePhysicalObjectKind.SymbolicLink =>
                    WindowsNamespaceSnapshotFileLookupStepKind
                        .UnsupportedObject,

                WindowsNamespacePhysicalObjectKind.Other =>
                    WindowsNamespaceSnapshotFileLookupStepKind
                        .UnsupportedObject,

                _ =>
                    WindowsNamespaceSnapshotFileLookupStepKind
                        .UnsupportedObject
            };
        }

        return participant.Kind switch
        {
            WindowsNamespacePhysicalObjectKind.Directory =>
                null,

            WindowsNamespacePhysicalObjectKind.File =>
                WindowsNamespaceSnapshotFileLookupStepKind
                    .NotDirectory,

            WindowsNamespacePhysicalObjectKind.SymbolicLink =>
                WindowsNamespaceSnapshotFileLookupStepKind
                    .UnsupportedObject,

            WindowsNamespacePhysicalObjectKind.Other =>
                WindowsNamespaceSnapshotFileLookupStepKind
                    .UnsupportedObject,

            _ =>
                WindowsNamespaceSnapshotFileLookupStepKind
                    .UnsupportedObject
        };
    }

    private static WindowsNamespaceSnapshotFileLookupState
        StateForStep(
            WindowsNamespaceSnapshotFileLookupStepKind kind)
    {
        return kind switch
        {
            WindowsNamespaceSnapshotFileLookupStepKind.NotDirectory =>
                WindowsNamespaceSnapshotFileLookupState.NotDirectory,

            WindowsNamespaceSnapshotFileLookupStepKind.NotFile =>
                WindowsNamespaceSnapshotFileLookupState.NotFile,

            WindowsNamespaceSnapshotFileLookupStepKind.UnsupportedObject =>
                WindowsNamespaceSnapshotFileLookupState.UnsupportedObject,

            _ =>
                WindowsNamespaceSnapshotFileLookupState
                    .InvalidSnapshotEvidence
        };
    }

    private static string DescribeInvalidKind(
        WindowsNamespaceSnapshotFileLookupStepKind kind,
        string component)
    {
        return kind switch
        {
            WindowsNamespaceSnapshotFileLookupStepKind.NotDirectory =>
                $"Requested intermediate component \"{component}\" " +
                "resolved to a regular file.",

            WindowsNamespaceSnapshotFileLookupStepKind.NotFile =>
                $"Requested final component \"{component}\" resolved " +
                "to a directory.",

            WindowsNamespaceSnapshotFileLookupStepKind.UnsupportedObject =>
                $"Requested component \"{component}\" resolved to an " +
                "unsupported filesystem object.",

            _ =>
                $"Requested component \"{component}\" has an invalid " +
                "snapshot object kind."
        };
    }

    private static WindowsNamespaceSnapshotFileLookupStep Step(
        int componentIndex,
        string requestedComponent,
        string parentPhysicalRelativePath,
        WindowsNamespaceDirectoryLookupObservation parentObservation,
        WindowsNamespaceSnapshotFileLookupStepKind kind,
        string? selectedPhysicalName,
        IReadOnlyList<string> windowsEquivalentPhysicalNames)
    {
        return new WindowsNamespaceSnapshotFileLookupStep(
            ComponentIndex:
                componentIndex,
            RequestedComponent:
                requestedComponent,
            ParentPhysicalRelativePath:
                parentPhysicalRelativePath,
            ParentCasefoldEnabled:
                parentObservation.CasefoldEnabled,
            Kind:
                kind,
            SelectedPhysicalName:
                selectedPhysicalName,
            WindowsEquivalentPhysicalNames:
                windowsEquivalentPhysicalNames
        );
    }

    private static WindowsNamespaceSnapshotFileLookup Success(
        WindowsNamespaceAnalysis analysis,
        string? requestedRelativePath,
        WindowsLogicalPath requestedLogicalPath,
        WindowsNamespacePhysicalParticipant resolvedParticipant,
        IReadOnlyList<WindowsNamespaceSnapshotFileLookupStep> steps)
    {
        return new WindowsNamespaceSnapshotFileLookup(
            Analysis:
                analysis,
            RequestedRelativePath:
                requestedRelativePath,
            RequestedLogicalPath:
                requestedLogicalPath,
            State:
                WindowsNamespaceSnapshotFileLookupState.Resolved,
            ResolvedParticipant:
                resolvedParticipant,
            FailedComponentIndex:
                null,
            Steps:
                steps.ToArray(),
            Error:
                null
        );
    }

    private static WindowsNamespaceSnapshotFileLookup Failure(
        WindowsNamespaceAnalysis analysis,
        string? requestedRelativePath,
        WindowsLogicalPath? requestedLogicalPath,
        WindowsNamespaceSnapshotFileLookupState state,
        int? failedComponentIndex,
        IReadOnlyList<WindowsNamespaceSnapshotFileLookupStep> steps,
        string? error)
    {
        return new WindowsNamespaceSnapshotFileLookup(
            Analysis:
                analysis,
            RequestedRelativePath:
                requestedRelativePath,
            RequestedLogicalPath:
                requestedLogicalPath,
            State:
                state,
            ResolvedParticipant:
                null,
            FailedComponentIndex:
                failedComponentIndex,
            Steps:
                steps.ToArray(),
            Error:
                error
        );
    }
}
