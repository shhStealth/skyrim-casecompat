using CaseCompat.Core.Analysis;
using CaseCompat.Filesystem.Linux;

public static class CollisionTreeCommand
{
    public static int Run(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine(
                "Error: collision-tree requires a directory."
            );

            return 2;
        }

        RecursiveCollisionScanResult scan;

        try
        {
            scan = RecursiveCollisionScanner.Scan(args[1]);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 3;
        }

        CollisionTreeAnalysis analysis =
            CollisionTreeAnalyzer.Analyze(
                scan.Findings
            );

        Console.WriteLine(
            "CaseCompat Collision Tree Analysis"
        );

        Console.WriteLine(
            "=================================="
        );

        Console.WriteLine();
        Console.WriteLine($"Root: {scan.RootPath}");
        Console.WriteLine();

        Console.WriteLine(
            $"Directories scanned:              {scan.DirectoriesScanned:N0}"
        );

        Console.WriteLine(
            $"Filesystem entries examined:      {scan.EntriesScanned:N0}"
        );

        Console.WriteLine(
            $"Raw collision groups:              {analysis.RawFindings:N0}"
        );

        Console.WriteLine(
            $"Directory collision groups:        {analysis.DirectoryCollisionFindings:N0}"
        );

        Console.WriteLine(
            $"File collision groups:             {analysis.FileCollisionFindings:N0}"
        );

        Console.WriteLine(
            $"Other collision groups:            {analysis.OtherCollisionFindings:N0}"
        );

        Console.WriteLine(
            $"Top-level directory split trees:   {analysis.Trees.Count:N0}"
        );

        Console.WriteLine(
            $"Unassigned collision groups:       {analysis.UnassignedFindings.Count:N0}"
        );

        Console.WriteLine(
            $"Symbolic links skipped:            {scan.SymbolicLinksSkipped:N0}"
        );

        Console.WriteLine(
            $"Duplicate physical dirs skipped:   " +
            $"{scan.DuplicatePhysicalDirectoriesSkipped:N0}"
        );

        Console.WriteLine(
            $"Scan errors:                       {scan.Errors.Count:N0}"
        );

        foreach (CollisionTree tree in analysis.Trees)
        {
            RecursiveCollisionFinding root =
                tree.Root;

            DirectoryCaseCollision collision =
                root.Collision;

            Console.WriteLine();
            Console.WriteLine("COLLISION TREE ROOT");
            Console.WriteLine("-------------------");

            Console.WriteLine(
                $"Depth:              {root.Depth}"
            );

            Console.WriteLine(
                $"Parent:             {collision.ParentPath}"
            );

            string casefold =
                root.ParentCasefoldEnabled switch
                {
                    true => "ENABLED",
                    false => "disabled",
                    null => "unknown"
                };

            Console.WriteLine(
                $"Parent casefold:    {casefold}"
            );

            Console.WriteLine(
                $"Logical key:        {collision.LogicalName}"
            );

            Console.WriteLine(
                $"Nested collisions:  {tree.Descendants.Count:N0}"
            );

            Console.WriteLine(
                "Physical roots:"
            );

            foreach (
                DirectoryCollisionMember member
                in collision.Members)
            {
                LinuxFileIdentityResult identity =
                    LinuxFileIdentity.Inspect(
                        member.FullPath
                    );

                Console.WriteLine(
                    $"  {member.Name}/"
                );

                if (identity.Success)
                {
                    Console.WriteLine(
                        $"    dev={identity.DeviceMajor}:" +
                        $"{identity.DeviceMinor} " +
                        $"inode={identity.Inode}"
                    );
                }
            }
        }

        if (scan.Errors.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("SCAN WARNINGS");
            Console.WriteLine("-------------");

            foreach (RecursiveScanError error in scan.Errors)
            {
                Console.WriteLine(
                    $"{error.Path}: {error.Message}"
                );
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            "Read-only analysis: no files were modified."
        );

        return 0;
    }
}
