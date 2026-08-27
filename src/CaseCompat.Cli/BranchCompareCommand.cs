using CaseCompat.Core.Analysis;

public static class BranchCompareCommand
{
    private const int MaxDisplayedDifferences = 100;

    public static int Run(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine(
                "Error: compare-branches requires two directories."
            );

            return 2;
        }

        BranchInventory branchA;
        BranchInventory branchB;

        try
        {
            branchA = BranchInventoryScanner.Scan(args[1]);
            branchB = BranchInventoryScanner.Scan(args[2]);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 3;
        }

        Console.WriteLine("CaseCompat Branch Comparison");
        Console.WriteLine("============================");
        Console.WriteLine();

        Console.WriteLine($"Branch A: {branchA.RootPath}");
        Console.WriteLine($"Branch B: {branchB.RootPath}");
        Console.WriteLine();

        Console.WriteLine($"Branch A files:             {branchA.Files.Count:N0}");
        Console.WriteLine($"Branch B files:             {branchB.Files.Count:N0}");
        Console.WriteLine($"Branch A directories:       {branchA.DirectoriesScanned:N0}");
        Console.WriteLine($"Branch B directories:       {branchB.DirectoriesScanned:N0}");
        Console.WriteLine($"Branch A symlinks skipped:  {branchA.SymbolicLinksSkipped:N0}");
        Console.WriteLine($"Branch B symlinks skipped:  {branchB.SymbolicLinksSkipped:N0}");
        Console.WriteLine($"Branch A scan errors:       {branchA.Errors.Count:N0}");
        Console.WriteLine($"Branch B scan errors:       {branchB.Errors.Count:N0}");

        if (branchA.Errors.Count > 0 ||
            branchB.Errors.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("INVENTORY ERRORS");
            Console.WriteLine("----------------");

            foreach (string error in branchA.Errors)
            {
                Console.WriteLine($"A: {error}");
            }

            foreach (string error in branchB.Errors)
            {
                Console.WriteLine($"B: {error}");
            }

            Console.WriteLine();
            Console.WriteLine(
                "Comparison stopped because an inventory was incomplete."
            );

            return 4;
        }

        BranchComparison comparison;

        try
        {
            comparison = BranchComparer.Compare(
                branchA,
                branchB
            );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                $"Comparison error: {ex.Message}"
            );

            return 5;
        }

        Console.WriteLine();
        Console.WriteLine("Logical namespace comparison");
        Console.WriteLine("----------------------------");

        Console.WriteLine(
            $"Only in A:          {comparison.OnlyInA:N0}"
        );

        Console.WriteLine(
            $"Only in B:          {comparison.OnlyInB:N0}"
        );

        Console.WriteLine(
            $"Present in both:    {comparison.PresentInBoth:N0}"
        );

        bool namespaceDiverges =
            comparison.OnlyInA > 0 ||
            comparison.OnlyInB > 0;

        bool bidirectionalSplit =
            comparison.OnlyInA > 0 &&
            comparison.OnlyInB > 0;

        Console.WriteLine();
        Console.WriteLine(
            $"Namespace divergence: " +
            $"{(namespaceDiverges ? "YES" : "NO")}"
        );

        Console.WriteLine(
            $"Bidirectional split:   " +
            $"{(bidirectionalSplit ? "YES" : "NO")}"
        );

        PrintDifferences(
            "ONLY IN A",
            comparison.Files.Where(file =>
                file.Presence ==
                BranchFilePresence.OnlyInA)
        );

        PrintDifferences(
            "ONLY IN B",
            comparison.Files.Where(file =>
                file.Presence ==
                BranchFilePresence.OnlyInB)
        );

        Console.WriteLine();
        Console.WriteLine(
            "Note: 'Present in both' currently means the same " +
            "Windows-logical path exists in both branches."
        );

        Console.WriteLine(
            "File contents have not been hashed or compared yet."
        );

        Console.WriteLine();
        Console.WriteLine(
            "Read-only comparison: no files were modified."
        );

        return 0;
    }

    private static void PrintDifferences(
        string heading,
        IEnumerable<BranchFileComparison> files)
    {
        BranchFileComparison[] matches =
            files.ToArray();

        if (matches.Length == 0)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine(heading);
        Console.WriteLine(
            new string('-', heading.Length)
        );

        foreach (
            BranchFileComparison file
            in matches.Take(MaxDisplayedDifferences))
        {
            Console.WriteLine(
                $"  {file.LogicalPath}"
            );

            if (file.FileA is not null)
            {
                Console.WriteLine(
                    $"    A: {file.FileA.RelativePath}"
                );
            }

            if (file.FileB is not null)
            {
                Console.WriteLine(
                    $"    B: {file.FileB.RelativePath}"
                );
            }
        }

        if (matches.Length > MaxDisplayedDifferences)
        {
            Console.WriteLine(
                $"  ... {matches.Length - MaxDisplayedDifferences:N0} " +
                "additional differences omitted"
            );
        }
    }
}
