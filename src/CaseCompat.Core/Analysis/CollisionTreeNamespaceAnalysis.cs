using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Analysis;

public sealed record CollisionTreeBranch(
    int Index,
    DirectoryCollisionMember Root,
    BranchInventory Inventory
);

public sealed record CollisionTreeAssetOccurrence(
    int BranchIndex,
    BranchFile File
);

public sealed record CollisionTreeLogicalAsset(
    WindowsLogicalPath LogicalPath,
    IReadOnlyList<CollisionTreeAssetOccurrence> Occurrences,
    int BranchesPresent,
    bool PresentInEveryBranch,
    bool AmbiguousWithinBranch
);

public sealed record CollisionTreeNamespaceAnalysis(
    CollisionTree Tree,
    IReadOnlyList<CollisionTreeBranch> Branches,
    IReadOnlyList<CollisionTreeLogicalAsset> Assets,
    int PresentInEveryBranch,
    int PartialPresence,
    int AmbiguousLogicalAssets,
    IReadOnlyList<string> Errors
)
{
    public bool NamespaceDiverges =>
        PartialPresence > 0;

    public bool HasAmbiguity =>
        AmbiguousLogicalAssets > 0;
}
