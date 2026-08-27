using CaseCompat.Core.Analysis;
using CaseCompat.Filesystem.Linux;

public static class ContentSummaryCommand
{
    public static int Run(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine(
                "Error: content-summary requires a directory."
            );

            return 2;
        }

        RecursiveCollisionScanResult scan;

        try
        {
            scan = RecursiveCollisionScanner.Scan(
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

        CollisionTreeNamespaceBatchAnalysis namespaceBatch =
            CollisionTreeNamespaceBatchAnalyzer.Analyze(
                trees
            );

        CollisionTreeContentBatchAnalysis contentBatch =
            CollisionTreeContentBatchAnalyzer.Analyze(
                namespaceBatch
            );

        Console.WriteLine(
            "CaseCompat Collision Content Summary"
        );

        Console.WriteLine(
            "===================================="
        );

        Console.WriteLine();
        Console.WriteLine($"Root: {scan.RootPath}");
        Console.WriteLine();

        Console.WriteLine(
            $"Collision trees:                 {contentBatch.Trees.Count:N0}"
        );

        Console.WriteLine(
            $"Trees with identical duplicates: {contentBatch.TreesWithIdenticalDuplicates:N0}"
        );

        Console.WriteLine(
            $"Trees with content conflicts:    {contentBatch.TreesWithContentConflicts:N0}"
        );

        Console.WriteLine(
            $"Trees with ambiguity:            {contentBatch.TreesWithAmbiguity:N0}"
        );

        Console.WriteLine(
            $"Trees with unreadable assets:    {contentBatch.TreesWithUnreadableAssets:N0}"
        );

        Console.WriteLine();
        Console.WriteLine("Logical asset states");
        Console.WriteLine("--------------------");

        Console.WriteLine(
            $"Single occurrence:               {contentBatch.TotalSingleOccurrence:N0}"
        );

        Console.WriteLine(
            $"Identical multi-branch:           {contentBatch.TotalIdentical:N0}"
        );

        Console.WriteLine(
            $"Different size:                   {contentBatch.TotalDifferentSize:N0}"
        );

        Console.WriteLine(
            $"Same size, different SHA-256:     {contentBatch.TotalDifferentContent:N0}"
        );

        Console.WriteLine(
            $"Ambiguous within branch:          {contentBatch.TotalAmbiguous:N0}"
        );

        Console.WriteLine(
            $"Unreadable:                       {contentBatch.TotalUnreadable:N0}"
        );

        Console.WriteLine();
        Console.WriteLine(
            "Single  Identical  DiffSize  DiffHash  Ambig  Unread  Logical key"
        );

        Console.WriteLine(
            "------  ---------  --------  --------  -----  ------  -----------"
        );

        foreach (
            CollisionTreeContentBatchItem item
            in contentBatch.Trees)
        {
            CollisionTreeContentAnalysis content =
                item.ContentAnalysis;

            string logicalKey =
                item.NamespaceItem
                    .Tree
                    .Root
                    .Collision
                    .LogicalName;

            Console.WriteLine(
                $"{content.SingleOccurrence,6:N0}  " +
                $"{content.Identical,9:N0}  " +
                $"{content.DifferentSize,8:N0}  " +
                $"{content.DifferentContent,8:N0}  " +
                $"{content.AmbiguousWithinBranch,5:N0}  " +
                $"{content.Unreadable,6:N0}  " +
                logicalKey
            );
        }

        Console.WriteLine();
        Console.WriteLine(
            "Selective hashing was used: only unambiguous, " +
            "multi-occurrence, same-size assets required SHA-256."
        );

        Console.WriteLine(
            "Read-only analysis: no files were modified."
        );

        return 0;
    }
}
