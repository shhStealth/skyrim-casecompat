namespace CaseCompat.Core.Analysis;

public enum CollisionTreeAssetContentState
{
    SingleOccurrence,
    Identical,
    DifferentSize,
    DifferentContent,
    AmbiguousWithinBranch,
    Unreadable
}

public sealed record CollisionTreeContentOccurrence(
    CollisionTreeAssetOccurrence Occurrence,
    string? Sha256
);

public sealed record CollisionTreeAssetContentAnalysis(
    CollisionTreeLogicalAsset NamespaceAsset,
    CollisionTreeAssetContentState State,
    IReadOnlyList<CollisionTreeContentOccurrence> Occurrences,
    string? Error
);

public sealed record CollisionTreeContentAnalysis(
    CollisionTreeNamespaceAnalysis NamespaceAnalysis,
    IReadOnlyList<CollisionTreeAssetContentAnalysis> Assets
)
{
    public int SingleOccurrence =>
        Assets.Count(asset =>
            asset.State ==
            CollisionTreeAssetContentState.SingleOccurrence);

    public int Identical =>
        Assets.Count(asset =>
            asset.State ==
            CollisionTreeAssetContentState.Identical);

    public int DifferentSize =>
        Assets.Count(asset =>
            asset.State ==
            CollisionTreeAssetContentState.DifferentSize);

    public int DifferentContent =>
        Assets.Count(asset =>
            asset.State ==
            CollisionTreeAssetContentState.DifferentContent);

    public int AmbiguousWithinBranch =>
        Assets.Count(asset =>
            asset.State ==
            CollisionTreeAssetContentState.AmbiguousWithinBranch);

    public int Unreadable =>
        Assets.Count(asset =>
            asset.State ==
            CollisionTreeAssetContentState.Unreadable);

    public int ContentConflicts =>
        DifferentSize +
        DifferentContent;
}
