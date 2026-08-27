using CaseCompat.Core.Analysis;
using CaseCompat.Filesystem.Linux;

public static class NamespaceSummaryCommand
{
    public static int Run(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine(
                "Error: namespace-summary requires a directory."
            );

            return 2;
        }

        RecursiveCollisionScanResult scan;

        try
        {
            scan =
                RecursiveCollisionScanner.Scan(
                    args[1]
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Scan error: {ex.Message}"
            );

            return 3;
        }

        CollisionTreeAnalysis trees =
            CollisionTreeAnalyzer.Analyze(
                scan.Findings
            );

        CollisionTreeNamespaceBatchAnalysis batch =
            CollisionTreeNamespaceBatchAnalyzer.Analyze(
                trees
            );

        Console.WriteLine(
            "CaseCompat Collision Namespace Summary"
        );

        Console.WriteLine(
            "======================================"
        );

        Console.WriteLine();
        Console.WriteLine($"Root: {scan.RootPath}");
        Console.WriteLine();

        Console.WriteLine(
            $"Directories scanned:            {scan.DirectoriesScanned:N0}"
        );

        Console.WriteLine(
            $"Filesystem entries examined:    {scan.EntriesScanned:N0}"
        );

        Console.WriteLine(
            $"Raw collision groups:            {trees.RawFindings:N0}"
        );

        Console.WriteLine(
            $"Top-level split trees:           {trees.Trees.Count:N0}"
        );

        Console.WriteLine(
            $"Namespace-divergent trees:       {batch.DivergentTrees:N0}"
        );

        Console.WriteLine(
            $"Equivalent-namespace trees:      {batch.EquivalentNamespaceTrees:N0}"
        );

        Console.WriteLine(
            $"Trees with ambiguity:            {batch.AmbiguousTrees:N0}"
        );

        Console.WriteLine(
            $"Trees with inventory errors:     {batch.TreesWithErrors:N0}"
        );

        Console.WriteLine(
            $"Scan errors:                     {scan.Errors.Count:N0}"
        );

        Console.WriteLine();
        Console.WriteLine(
            "Branches  Assets  Shared  Partial  Ambig  Errors  Logical key"
        );

        Console.WriteLine(
            "--------  ------  ------  -------  -----  ------  -----------"
        );

        foreach (
            CollisionTreeNamespaceBatchItem item
            in batch.Trees)
        {
            CollisionTreeNamespaceAnalysis analysis =
                item.Analysis;

            string logicalKey =
                item.Tree.Root.Collision.LogicalName;

            Console.WriteLine(
                $"{analysis.Branches.Count,8:N0}  " +
                $"{analysis.Assets.Count,6:N0}  " +
                $"{analysis.PresentInEveryBranch,6:N0}  " +
                $"{analysis.PartialPresence,7:N0}  " +
                $"{analysis.AmbiguousLogicalAssets,5:N0}  " +
                $"{analysis.Errors.Count,6:N0}  " +
                logicalKey
            );
        }

        Console.WriteLine();
        Console.WriteLine(
            "No file contents were hashed by this command."
        );

        Console.WriteLine(
            "Read-only analysis: no files were modified."
        );

        return 0;
    }
}
