namespace CaseCompat.Core.Analysis;

public enum BranchSplitState
{
    Equivalent,
    OneSidedDivergence,
    BidirectionalDivergence,
    ContentConflict
}

public sealed record BranchSplitClassification(
    BranchSplitState State,
    int OnlyInA,
    int OnlyInB,
    int IdenticalOverlaps,
    int DifferentSizeOverlaps,
    int DifferentContentOverlaps
)
{
    public int ConflictingOverlaps =>
        DifferentSizeOverlaps +
        DifferentContentOverlaps;

    public bool NamespaceDiverges =>
        OnlyInA > 0 ||
        OnlyInB > 0;
}
