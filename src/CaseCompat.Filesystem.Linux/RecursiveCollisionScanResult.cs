namespace CaseCompat.Filesystem.Linux;

public sealed record RecursiveCollisionFinding(
    int Depth,
    DirectoryCaseCollision Collision,
    bool? ParentCasefoldEnabled,
    long? ParentRawFlags
);

public sealed record RecursiveScanError(
    string Path,
    string Message
);

public sealed record RecursiveCollisionScanResult(
    string RootPath,
    long DirectoriesScanned,
    long EntriesScanned,
    long SymbolicLinksSkipped,
    long DuplicatePhysicalDirectoriesSkipped,
    IReadOnlyList<RecursiveCollisionFinding> Findings,
    IReadOnlyList<RecursiveScanError> Errors
);
