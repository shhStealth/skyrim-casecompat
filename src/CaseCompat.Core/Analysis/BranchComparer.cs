namespace CaseCompat.Core.Analysis;

public static class BranchComparer
{
    public static BranchComparison Compare(
        BranchInventory branchA,
        BranchInventory branchB)
    {
        ArgumentNullException.ThrowIfNull(branchA);
        ArgumentNullException.ThrowIfNull(branchB);

        Dictionary<WindowsLogicalPath, BranchFile> a =
            BuildIndex(branchA);

        Dictionary<WindowsLogicalPath, BranchFile> b =
            BuildIndex(branchB);

        WindowsLogicalPath[] keys =
            a.Keys
                .Concat(b.Keys)
                .Distinct()
                .OrderBy(
                    key => key.Value,
                    StringComparer.Ordinal
                )
                .ToArray();

        var comparisons =
            new List<BranchFileComparison>();

        foreach (WindowsLogicalPath key in keys)
        {
            a.TryGetValue(key, out BranchFile? fileA);
            b.TryGetValue(key, out BranchFile? fileB);

            BranchFilePresence presence =
                (fileA, fileB) switch
                {
                    (not null, not null) =>
                        BranchFilePresence.PresentInBoth,

                    (not null, null) =>
                        BranchFilePresence.OnlyInA,

                    (null, not null) =>
                        BranchFilePresence.OnlyInB,

                    _ => throw new InvalidOperationException()
                };

            comparisons.Add(
                new BranchFileComparison(
                    LogicalPath: key,
                    FileA: fileA,
                    FileB: fileB,
                    Presence: presence
                )
            );
        }

        return new BranchComparison(
            BranchA: branchA,
            BranchB: branchB,
            Files: comparisons
        );
    }

    private static Dictionary<
        WindowsLogicalPath,
        BranchFile
    > BuildIndex(BranchInventory inventory)
    {
        var result =
            new Dictionary<
                WindowsLogicalPath,
                BranchFile
            >();

        foreach (BranchFile file in inventory.Files)
        {
            if (!result.TryAdd(
                    file.LogicalPath,
                    file))
            {
                throw new InvalidOperationException(
                    "A single physical branch contains " +
                    "multiple files with the same " +
                    $"Windows-logical path: " +
                    $"{file.LogicalPath}"
                );
            }
        }

        return result;
    }
}
