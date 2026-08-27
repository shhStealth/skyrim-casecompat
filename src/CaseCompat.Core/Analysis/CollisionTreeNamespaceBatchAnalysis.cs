namespace CaseCompat.Core.Analysis;

public sealed record CollisionTreeNamespaceBatchItem(
    CollisionTree Tree,
    CollisionTreeNamespaceAnalysis Analysis
);

public sealed record CollisionTreeNamespaceBatchAnalysis(
    IReadOnlyList<CollisionTreeNamespaceBatchItem> Trees
)
{
    public int DivergentTrees =>
        Trees.Count(item =>
            item.Analysis.NamespaceDiverges);

    public int EquivalentNamespaceTrees =>
        Trees.Count(item =>
            !item.Analysis.NamespaceDiverges);

    public int AmbiguousTrees =>
        Trees.Count(item =>
            item.Analysis.HasAmbiguity);

    public int TreesWithErrors =>
        Trees.Count(item =>
            item.Analysis.Errors.Count > 0);
}
