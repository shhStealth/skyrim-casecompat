using CaseCompat.Core.Analysis;

public static class BranchCompareCommand
{
    private const int MaxDisplayedDifferences = 50;
    private const int MaxDisplayedConflicts = 50;

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

        BranchComparison namespaceComparison;

        try
        {
            namespaceComparison = BranchComparer.Compare(
                branchA,
                branchB
            );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                $"Namespace comparison error: {ex.Message}"
            );

            return 5;
        }

        Console.WriteLine();
        Console.WriteLine("Logical namespace comparison");
        Console.WriteLine("----------------------------");

        Console.WriteLine(
            $"Only in A:          {namespaceComparison.OnlyInA:N0}"
        );

        Console.WriteLine(
            $"Only in B:          {namespaceComparison.OnlyInB:N0}"
        );

        Console.WriteLine(
            $"Present in both:    {namespaceComparison.PresentInBoth:N0}"
        );

        bool namespaceDiverges =
            namespaceComparison.OnlyInA > 0 ||
            namespaceComparison.OnlyInB > 0;

        bool bidirectionalSplit =
            namespaceComparison.OnlyInA > 0 &&
            namespaceComparison.OnlyInB > 0;

        Console.WriteLine();
        Console.WriteLine(
            $"Namespace divergence: {YesNo(namespaceDiverges)}"
        );

        Console.WriteLine(
            $"Bidirectional split:   {YesNo(bidirectionalSplit)}"
        );

        BranchContentComparison contentComparison;

        try
        {
            contentComparison =
                BranchContentComparer.Compare(
                    namespaceComparison
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                $"Content comparison error: {ex.Message}"
            );

            return 6;
        }

        Console.WriteLine();
        Console.WriteLine("Overlapping file content");
        Console.WriteLine("------------------------");

        Console.WriteLine(
            $"Byte-identical:              {contentComparison.Identical:N0}"
        );

        Console.WriteLine(
            $"Different size:              {contentComparison.DifferentSize:N0}"
        );

        Console.WriteLine(
            $"Same size, different SHA-256:{contentComparison.DifferentContent,5:N0}"
        );

        int conflictingOverlaps =
            contentComparison.DifferentSize +
            contentComparison.DifferentContent;

        Console.WriteLine();
        Console.WriteLine(
            $"Conflicting overlaps: {conflictingOverlaps:N0}"
        );

        PrintNamespaceDifferences(
            "ONLY IN A",
            namespaceComparison.Files.Where(file =>
                file.Presence ==
                BranchFilePresence.OnlyInA)
        );

        PrintNamespaceDifferences(
            "ONLY IN B",
            namespaceComparison.Files.Where(file =>
                file.Presence ==
                BranchFilePresence.OnlyInB)
        );

        PrintContentConflicts(
            contentComparison.Files.Where(file =>
                file.ContentState ==
                    BranchContentState.DifferentSize ||
                file.ContentState ==
                    BranchContentState.DifferentContent)
        );

        Console.WriteLine();
        Console.WriteLine(
            "Hashing policy: one-sided files are not hashed; " +
            "different-size overlaps are not hashed; only " +
            "same-size overlaps require SHA-256 comparison."
        );

        Console.WriteLine();
        Console.WriteLine(
            "Read-only comparison: no files were modified."
        );

        return 0;
    }

    private static void PrintNamespaceDifferences(
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

    private static void PrintContentConflicts(
        IEnumerable<BranchContentFileComparison> files)
    {
        BranchContentFileComparison[] conflicts =
            files.ToArray();

        if (conflicts.Length == 0)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("OVERLAPPING CONTENT CONFLICTS");
        Console.WriteLine("-----------------------------");

        foreach (
            BranchContentFileComparison conflict
            in conflicts.Take(MaxDisplayedConflicts))
        {
            BranchFileComparison file =
                conflict.NamespaceComparison;

            Console.WriteLine(
                $"  {file.LogicalPath}"
            );

            Console.WriteLine(
                $"    state: {conflict.ContentState}"
            );

            if (file.FileA is not null)
            {
                Console.WriteLine(
                    $"    A: {file.FileA.RelativePath} " +
                    $"({file.FileA.Size:N0} bytes)"
                );
            }

            if (file.FileB is not null)
            {
                Console.WriteLine(
                    $"    B: {file.FileB.RelativePath} " +
                    $"({file.FileB.Size:N0} bytes)"
                );
            }

            if (conflict.Sha256A is not null)
            {
                Console.WriteLine(
                    $"    SHA256 A: {conflict.Sha256A}"
                );
            }

            if (conflict.Sha256B is not null)
            {
                Console.WriteLine(
                    $"    SHA256 B: {conflict.Sha256B}"
                );
            }
        }

        if (conflicts.Length > MaxDisplayedConflicts)
        {
            Console.WriteLine(
                $"  ... {conflicts.Length - MaxDisplayedConflicts:N0} " +
                "additional conflicts omitted"
            );
        }
    }

    private static string YesNo(bool value)
    {
        return value ? "YES" : "NO";
    }
}
