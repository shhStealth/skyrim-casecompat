namespace CaseCompat.Core.Resolution;

public enum DataRelativePathCaseMismatchTopologyState
{
    LinuxResolvable,
    IncompleteCandidateSearch,
    NoEquivalentCandidate,
    MultipleEquivalentCandidates,

    MissingFailureStep,
    UnsupportedFailureShape,

    CandidateOutsideDataRoot,
    CandidateComponentCountMismatch,

    PriorTraversalIncomplete,
    CandidateBranchesBeforeFailure,
    PriorEquivalentHierarchySplit,

    FailedComponentNoEquivalent,
    FailedComponentMultipleEquivalents,
    CandidateDoesNotMatchFailedEquivalent,

    DirectStrictCaseMismatch
}
