namespace CaseCompat.Core.Resolution;

public static class DataRelativePathCaseMismatchTopologyClassifier
{
    public static DataRelativePathCaseMismatchTopologyState
        Classify(
            DataRelativePathResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(
            resolution
        );

        if (resolution.LinuxResolves)
        {
            return DataRelativePathCaseMismatchTopologyState
                .LinuxResolvable;
        }

        if (
            resolution.CandidateSearchErrors.Count >
            0)
        {
            return DataRelativePathCaseMismatchTopologyState
                .IncompleteCandidateSearch;
        }

        if (
            resolution.EquivalentPhysicalCandidates.Count ==
            0)
        {
            return DataRelativePathCaseMismatchTopologyState
                .NoEquivalentCandidate;
        }

        if (
            resolution.EquivalentPhysicalCandidates.Count >
            1)
        {
            return DataRelativePathCaseMismatchTopologyState
                .MultipleEquivalentCandidates;
        }

        if (
            resolution.FailedComponentIndex is not int
                failedIndex ||
            failedIndex < 0)
        {
            return DataRelativePathCaseMismatchTopologyState
                .MissingFailureStep;
        }

        string[] requestedComponents =
            SplitComponents(
                resolution.RequestedPath
            );

        if (
            failedIndex >=
            requestedComponents.Length)
        {
            return DataRelativePathCaseMismatchTopologyState
                .MissingFailureStep;
        }

        PathResolutionStep[] failedSteps =
            resolution.Steps
                .Where(step =>
                    step.ComponentIndex ==
                    failedIndex
                )
                .ToArray();

        if (failedSteps.Length != 1)
        {
            return DataRelativePathCaseMismatchTopologyState
                .MissingFailureStep;
        }

        PathResolutionStep failedStep =
            failedSteps[0];

        if (
            failedStep.Kind !=
                PathResolutionStepKind.Missing ||
            failedStep.ParentCasefoldEnabled !=
                false ||
            !string.IsNullOrWhiteSpace(
                failedStep.ParentCasefoldError
            ) ||
            !string.Equals(
                failedStep.RequestedComponent,
                requestedComponents[failedIndex],
                StringComparison.Ordinal
            ))
        {
            return DataRelativePathCaseMismatchTopologyState
                .UnsupportedFailureShape;
        }

        string root =
            Path.GetFullPath(
                resolution.DataRoot
            );

        string candidate =
            Path.GetFullPath(
                resolution
                    .EquivalentPhysicalCandidates[0]
            );

        string candidateRelative =
            Path.GetRelativePath(
                root,
                candidate
            );

        string[] candidateComponents =
            SplitComponents(
                candidateRelative
            );

        if (
            Path.IsPathRooted(
                candidateRelative
            ) ||
            candidateComponents.Any(component =>
                component == ".."
            ))
        {
            return DataRelativePathCaseMismatchTopologyState
                .CandidateOutsideDataRoot;
        }

        if (
            candidateComponents.Length !=
            requestedComponents.Length)
        {
            return DataRelativePathCaseMismatchTopologyState
                .CandidateComponentCountMismatch;
        }

        var priorSteps =
            new List<PathResolutionStep>();

        for (
            int index = 0;
            index < failedIndex;
            index++)
        {
            PathResolutionStep[] matchingSteps =
                resolution.Steps
                    .Where(step =>
                        step.ComponentIndex ==
                        index
                    )
                    .ToArray();

            if (matchingSteps.Length != 1)
            {
                return DataRelativePathCaseMismatchTopologyState
                    .PriorTraversalIncomplete;
            }

            PathResolutionStep step =
                matchingSteps[0];

            if (
                step.Kind !=
                    PathResolutionStepKind
                        .ExactSpelling &&
                step.Kind !=
                    PathResolutionStepKind
                        .CasefoldEquivalent)
            {
                return DataRelativePathCaseMismatchTopologyState
                    .PriorTraversalIncomplete;
            }

            if (
                string.IsNullOrEmpty(
                    step.SelectedPhysicalName
                ))
            {
                return DataRelativePathCaseMismatchTopologyState
                    .PriorTraversalIncomplete;
            }

            priorSteps.Add(
                step
            );
        }

        foreach (
            PathResolutionStep step
            in priorSteps)
        {
            if (
                !string.Equals(
                    candidateComponents[
                        step.ComponentIndex
                    ],
                    step.SelectedPhysicalName,
                    StringComparison.Ordinal
                ))
            {
                return DataRelativePathCaseMismatchTopologyState
                    .CandidateBranchesBeforeFailure;
            }
        }

        if (
            priorSteps.Any(step =>
                step.EquivalentPhysicalNames.Count >
                1
            ))
        {
            return DataRelativePathCaseMismatchTopologyState
                .PriorEquivalentHierarchySplit;
        }

        if (
            failedStep
                .EquivalentPhysicalNames
                .Count ==
            0)
        {
            return DataRelativePathCaseMismatchTopologyState
                .FailedComponentNoEquivalent;
        }

        if (
            failedStep
                .EquivalentPhysicalNames
                .Count >
            1)
        {
            return DataRelativePathCaseMismatchTopologyState
                .FailedComponentMultipleEquivalents;
        }

        if (
            !string.Equals(
                candidateComponents[
                    failedIndex
                ],
                failedStep
                    .EquivalentPhysicalNames[0],
                StringComparison.Ordinal
            ))
        {
            return DataRelativePathCaseMismatchTopologyState
                .CandidateDoesNotMatchFailedEquivalent;
        }

        return DataRelativePathCaseMismatchTopologyState
            .DirectStrictCaseMismatch;
    }

    private static string[] SplitComponents(
        string path)
    {
        return path
            .Replace(
                '\\',
                '/'
            )
            .Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries
            );
    }
}
