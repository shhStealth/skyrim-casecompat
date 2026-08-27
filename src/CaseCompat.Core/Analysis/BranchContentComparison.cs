namespace CaseCompat.Core.Analysis;

public enum BranchContentState
{
    NotApplicable,
    Identical,
    DifferentSize,
    DifferentContent
}

public sealed record BranchContentFileComparison(
    BranchFileComparison NamespaceComparison,
    BranchContentState ContentState,
    string? Sha256A,
    string? Sha256B
);

public sealed record BranchContentComparison(
    BranchComparison NamespaceComparison,
    IReadOnlyList<BranchContentFileComparison> Files
)
{
    public int Identical =>
        Files.Count(file =>
            file.ContentState ==
            BranchContentState.Identical);

    public int DifferentSize =>
        Files.Count(file =>
            file.ContentState ==
            BranchContentState.DifferentSize);

    public int DifferentContent =>
        Files.Count(file =>
            file.ContentState ==
            BranchContentState.DifferentContent);
}
