namespace CaseCompat.Core.Analysis;

/*
 * Pure interpretation of one descriptor-bound WindowsNamespaceAnalysis
 * snapshot.
 *
 * This model says only what the recorded physical namespace and recorded
 * per-directory lookup semantics prove about one requested file path.
 *
 * It does not identify:
 *
 * - mod ownership;
 * - deployment priority;
 * - provider precedence;
 * - plugin/archive winners;
 * - canonical spelling;
 * - repair eligibility.
 */
public enum WindowsNamespaceSnapshotFileLookupState
{
    Resolved,
    Missing,
    CasefoldUnknown,
    CasefoldEquivalenceUnknown,
    AmbiguousEquivalent,
    NotDirectory,
    NotFile,
    UnsupportedObject,
    IncompleteAnalysis,
    InvalidRequestedPath,
    RequestOutsideAnalyzedNamespace,
    InvalidSnapshotEvidence
}

public enum WindowsNamespaceSnapshotFileLookupStepKind
{
    ExactSpelling,
    CasefoldEquivalent,
    Missing,
    CasefoldUnknown,
    CasefoldEquivalenceUnknown,
    AmbiguousEquivalent,
    NotDirectory,
    NotFile,
    UnsupportedObject
}

public sealed record WindowsNamespaceSnapshotFileLookupStep(
    int ComponentIndex,
    string RequestedComponent,
    string ParentPhysicalRelativePath,
    bool? ParentCasefoldEnabled,
    WindowsNamespaceSnapshotFileLookupStepKind Kind,
    string? SelectedPhysicalName,
    IReadOnlyList<string> WindowsEquivalentPhysicalNames
);

public sealed record WindowsNamespaceSnapshotFileLookup(
    WindowsNamespaceAnalysis Analysis,
    string? RequestedRelativePath,
    WindowsLogicalPath? RequestedLogicalPath,
    WindowsNamespaceSnapshotFileLookupState State,
    WindowsNamespacePhysicalParticipant? ResolvedParticipant,
    int? FailedComponentIndex,
    IReadOnlyList<WindowsNamespaceSnapshotFileLookupStep> Steps,
    string? Error
)
{
    public bool Success =>
        State ==
            WindowsNamespaceSnapshotFileLookupState.Resolved &&
        ResolvedParticipant is not null;

    public string? ResolvedPhysicalRelativePath =>
        Success
            ? ResolvedParticipant!.RelativePath
            : null;
}
