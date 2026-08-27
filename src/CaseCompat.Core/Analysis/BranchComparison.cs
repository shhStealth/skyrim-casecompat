namespace CaseCompat.Core.Analysis;

public enum BranchFilePresence
{
    OnlyInA,
    OnlyInB,
    PresentInBoth
}

public sealed record BranchFileComparison(
    WindowsLogicalPath LogicalPath,
    BranchFile? FileA,
    BranchFile? FileB,
    BranchFilePresence Presence
);

public sealed record BranchComparison(
    BranchInventory BranchA,
    BranchInventory BranchB,
    IReadOnlyList<BranchFileComparison> Files
)
{
    public int OnlyInA =>
        Files.Count(file =>
            file.Presence ==
            BranchFilePresence.OnlyInA);

    public int OnlyInB =>
        Files.Count(file =>
            file.Presence ==
            BranchFilePresence.OnlyInB);

    public int PresentInBoth =>
        Files.Count(file =>
            file.Presence ==
            BranchFilePresence.PresentInBoth);
}
