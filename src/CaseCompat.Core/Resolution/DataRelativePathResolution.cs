namespace CaseCompat.Core.Resolution;

public enum PathResolutionStepKind
{
    ExactSpelling,
    CasefoldEquivalent,
    Missing,
    CasefoldUnknown,
    AmbiguousEquivalent,
    NotDirectory,
    NotFile,
    SymbolicLinkRejected,
    EnumerationError
}

public sealed record PathResolutionStep(
    int ComponentIndex,
    string RequestedComponent,
    string ParentPhysicalPath,
    bool? ParentCasefoldEnabled,
    string? ParentCasefoldError,
    PathResolutionStepKind Kind,
    string? SelectedPhysicalName,
    IReadOnlyList<string> EquivalentPhysicalNames
);

public sealed record DataRelativePathResolution(
    string DataRoot,
    string RequestedPath,
    bool LinuxResolves,
    string? ResolvedPhysicalPath,
    int? FailedComponentIndex,
    string? FailureReason,
    IReadOnlyList<PathResolutionStep> Steps,
    IReadOnlyList<string> EquivalentPhysicalCandidates,
    IReadOnlyList<string> CandidateSearchErrors
)
{
    public int CandidateCount =>
        EquivalentPhysicalCandidates.Count;
}
