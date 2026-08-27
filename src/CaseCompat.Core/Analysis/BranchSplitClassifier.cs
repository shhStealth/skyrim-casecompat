namespace CaseCompat.Core.Analysis;

public static class BranchSplitClassifier
{
    public static BranchSplitClassification Classify(
        BranchContentComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);

        BranchComparison namespaceComparison =
            comparison.NamespaceComparison;

        int onlyInA =
            namespaceComparison.OnlyInA;

        int onlyInB =
            namespaceComparison.OnlyInB;

        int conflicts =
            comparison.DifferentSize +
            comparison.DifferentContent;

        BranchSplitState state;

        if (conflicts > 0)
        {
            state =
                BranchSplitState.ContentConflict;
        }
        else if (onlyInA > 0 &&
                 onlyInB > 0)
        {
            state =
                BranchSplitState.BidirectionalDivergence;
        }
        else if (onlyInA > 0 ||
                 onlyInB > 0)
        {
            state =
                BranchSplitState.OneSidedDivergence;
        }
        else
        {
            state =
                BranchSplitState.Equivalent;
        }

        return new BranchSplitClassification(
            State: state,
            OnlyInA: onlyInA,
            OnlyInB: onlyInB,
            IdenticalOverlaps:
                comparison.Identical,
            DifferentSizeOverlaps:
                comparison.DifferentSize,
            DifferentContentOverlaps:
                comparison.DifferentContent
        );
    }
}
