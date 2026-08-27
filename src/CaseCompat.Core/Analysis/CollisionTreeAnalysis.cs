using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Analysis;

public sealed record CollisionTree(
    RecursiveCollisionFinding Root,
    IReadOnlyList<RecursiveCollisionFinding> Descendants
);

public sealed record CollisionTreeAnalysis(
    int RawFindings,
    int DirectoryCollisionFindings,
    int FileCollisionFindings,
    int OtherCollisionFindings,
    IReadOnlyList<CollisionTree> Trees,
    IReadOnlyList<RecursiveCollisionFinding> UnassignedFindings
);
