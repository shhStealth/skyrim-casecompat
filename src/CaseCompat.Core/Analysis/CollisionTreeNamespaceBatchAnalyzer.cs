namespace CaseCompat.Core.Analysis;

public static class CollisionTreeNamespaceBatchAnalyzer
{
    public static CollisionTreeNamespaceBatchAnalysis Analyze(
        CollisionTreeAnalysis treeAnalysis)
    {
        ArgumentNullException.ThrowIfNull(treeAnalysis);

        CollisionTreeNamespaceBatchItem[] items =
            treeAnalysis.Trees
                .Select(tree =>
                {
                    CollisionTreeNamespaceAnalysis analysis =
                        CollisionTreeNamespaceAnalyzer.Analyze(
                            tree
                        );

                    return new CollisionTreeNamespaceBatchItem(
                        Tree: tree,
                        Analysis: analysis
                    );
                })
                .OrderBy(
                    item =>
                        item.Tree.Root.Collision.ParentPath,
                    StringComparer.Ordinal)
                .ThenBy(
                    item =>
                        item.Tree.Root.Collision.LogicalName,
                    StringComparer.Ordinal)
                .ToArray();

        return new CollisionTreeNamespaceBatchAnalysis(
            Trees: items
        );
    }
}
