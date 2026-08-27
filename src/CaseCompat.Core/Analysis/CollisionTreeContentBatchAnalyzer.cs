namespace CaseCompat.Core.Analysis;

public static class CollisionTreeContentBatchAnalyzer
{
    public static CollisionTreeContentBatchAnalysis Analyze(
        CollisionTreeNamespaceBatchAnalysis namespaceBatch)
    {
        ArgumentNullException.ThrowIfNull(namespaceBatch);

        CollisionTreeContentBatchItem[] trees =
            namespaceBatch.Trees
                .Select(item =>
                    new CollisionTreeContentBatchItem(
                        NamespaceItem: item,
                        ContentAnalysis:
                            CollisionTreeContentAnalyzer.Analyze(
                                item.Analysis
                            )
                    )
                )
                .ToArray();

        return new CollisionTreeContentBatchAnalysis(
            Trees: trees
        );
    }
}
