namespace CaseCompat.Core.Analysis;

public sealed record CollisionTreeContentBatchItem(
    CollisionTreeNamespaceBatchItem NamespaceItem,
    CollisionTreeContentAnalysis ContentAnalysis
);

public sealed record CollisionTreeContentBatchAnalysis(
    IReadOnlyList<CollisionTreeContentBatchItem> Trees
)
{
    public int TreesWithIdenticalDuplicates =>
        Trees.Count(item =>
            item.ContentAnalysis.Identical > 0);

    public int TreesWithContentConflicts =>
        Trees.Count(item =>
            item.ContentAnalysis.ContentConflicts > 0);

    public int TreesWithAmbiguity =>
        Trees.Count(item =>
            item.ContentAnalysis.AmbiguousWithinBranch > 0);

    public int TreesWithUnreadableAssets =>
        Trees.Count(item =>
            item.ContentAnalysis.Unreadable > 0);

    public int TotalSingleOccurrence =>
        Trees.Sum(item =>
            item.ContentAnalysis.SingleOccurrence);

    public int TotalIdentical =>
        Trees.Sum(item =>
            item.ContentAnalysis.Identical);

    public int TotalDifferentSize =>
        Trees.Sum(item =>
            item.ContentAnalysis.DifferentSize);

    public int TotalDifferentContent =>
        Trees.Sum(item =>
            item.ContentAnalysis.DifferentContent);

    public int TotalAmbiguous =>
        Trees.Sum(item =>
            item.ContentAnalysis.AmbiguousWithinBranch);

    public int TotalUnreadable =>
        Trees.Sum(item =>
            item.ContentAnalysis.Unreadable);
}
