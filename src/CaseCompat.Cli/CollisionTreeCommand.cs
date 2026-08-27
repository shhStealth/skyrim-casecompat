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

        RecursiveCollisionScanResult result;

        try
        {
            result = RecursiveCollisionScanner.Scan(args[1]);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 3;
        }

        Console.WriteLine("CaseCompat Collision Tree Scan");
        Console.WriteLine("==============================");
        Console.WriteLine();

        Console.WriteLine($"Root: {result.RootPath}");
        Console.WriteLine();

        Console.WriteLine(
            $"Directories scanned:             {result.DirectoriesScanned:N0}"
        );

        Console.WriteLine(
            $"Filesystem entries examined:     {result.EntriesScanned:N0}"
        );

        Console.WriteLine(
            $"Collision groups found:           {result.Findings.Count:N0}"
        );

        Console.WriteLine(
            $"Symbolic links skipped:           {result.SymbolicLinksSkipped:N0}"
        );

        Console.WriteLine(
            $"Duplicate physical dirs skipped:  " +
            $"{result.DuplicatePhysicalDirectoriesSkipped:N0}"
        );

        Console.WriteLine(
            $"Scan errors:                      {result.Errors.Count:N0}"
        );

        foreach (RecursiveCollisionFinding finding in result.Findings)
        {
            DirectoryCaseCollision collision = finding.Collision;

            Console.WriteLine();
            Console.WriteLine("CASE COLLISION");
            Console.WriteLine("--------------");

            Console.WriteLine(
                $"Depth:           {finding.Depth}"
            );

            Console.WriteLine(
                $"Parent:          {collision.ParentPath}"
            );

            string casefold =
                finding.ParentCasefoldEnabled switch
                {
                    true => "ENABLED",
                    false => "disabled",
                    null => "unknown"
                };

            Console.WriteLine(
                $"Parent casefold: {casefold}"
            );

            Console.WriteLine(
                $"Logical key:     {collision.LogicalName}"
            );

            foreach (
                DirectoryCollisionMember member
                in collision.Members
            )
            {
                LinuxFileIdentityResult identity =
                    LinuxFileIdentity.Inspect(member.FullPath);

                string kind =
                    member.IsDirectory
                        ? "directory"
                        : "file";

                if (member.IsSymbolicLink)
                {
                    kind += ", symlink";
                }

                Console.WriteLine(
                    $"  {member.Name} [{kind}]"
                );

                if (identity.Success)
                {
                    Console.WriteLine(
                        $"    dev={identity.DeviceMajor}:" +
                        $"{identity.DeviceMinor} " +
                        $"inode={identity.Inode}"
                    );
                }
                else
                {
                    Console.WriteLine(
                        $"    identity unavailable: " +
                        $"{identity.Error}"
                    );
                }
            }
        }

        if (result.Errors.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("SCAN WARNINGS");
            Console.WriteLine("-------------");

            foreach (RecursiveScanError error in result.Errors)
            {
                Console.WriteLine(
                    $"{error.Path}: {error.Message}"
                );
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            "Read-only scan: no files were modified."
        );

        return 0;
    }
}
