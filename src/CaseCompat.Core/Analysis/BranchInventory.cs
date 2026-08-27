namespace CaseCompat.Core.Analysis;

public sealed record BranchFile(
    string PhysicalPath,
    string RelativePath,
    WindowsLogicalPath LogicalPath,
    long Size
);

public sealed record BranchInventory(
    string RootPath,
    IReadOnlyList<BranchFile> Files,
    long DirectoriesScanned,
    long SymbolicLinksSkipped,
    IReadOnlyList<string> Errors
);
