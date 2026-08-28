using CaseCompat.Filesystem.Linux;

if (args.Length == 0)
{
    ShowUsage();
    return 1;
}

string command = args[0].ToLowerInvariant();

switch (command)
{
    case "doctor":
        return RunDoctor(args);

    case "collisions":
        return RunCollisions(args);

    case "collision-tree":
        return CollisionTreeCommand.Run(args);

    case "compare-branches":
        return BranchCompareCommand.Run(args);

    case "namespace-summary":
        return NamespaceSummaryCommand.Run(args);

    case "content-summary":
        return ContentSummaryCommand.Run(args);

    case "plugin-probe":
        return PluginProbeCommand.Run(args);

    case "record-inventory":
        return RecordInventoryCommand.Run(args);

    default:
        Console.Error.WriteLine($"Unknown command: {args[0]}");
        Console.Error.WriteLine();
        ShowUsage();
        return 2;
}

static int RunDoctor(string[] args)
{
    if (args.Length != 2)
    {
        Console.Error.WriteLine(
            "Error: doctor requires a Skyrim Data directory."
        );
        Console.Error.WriteLine();
        ShowUsage();
        return 2;
    }

    DirectoryProbeResult result;

    try
    {
        result = DirectoryProbe.Inspect(args[1]);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 3;
    }

    Console.WriteLine("CaseCompat Doctor");
    Console.WriteLine("=================");
    Console.WriteLine();
    Console.WriteLine(
        $"Platform:       {(result.IsLinux ? "Linux" : "Unsupported")}"
    );
    Console.WriteLine($"Requested path: {result.RequestedPath}");
    Console.WriteLine($"Resolved path:  {result.FullPath}");
    Console.WriteLine(
        $"Directory:      {(result.Exists ? "FOUND" : "NOT FOUND")}"
    );

    if (!result.IsLinux)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine(
            "CaseCompat currently supports Linux only."
        );
        return 4;
    }

    if (!result.Exists)
    {
        return 5;
    }

    Console.WriteLine();
    Console.WriteLine("Directory casefold inspection");
    Console.WriteLine("-----------------------------");

    string[] pathsToInspect =
    [
        result.FullPath,
        Path.Combine(result.FullPath, "meshes"),
        Path.Combine(result.FullPath, "meshes", "Terrain"),
        Path.Combine(result.FullPath, "meshes", "Terrain", "tamriel")
    ];

    foreach (string path in pathsToInspect)
    {
        PrintCasefoldResult(
            LinuxDirectoryFlags.Inspect(path)
        );

        PrintIdentityResult(
            LinuxFileIdentity.Inspect(path)
        );
    }

    string lowercaseMeshes =
        Path.Combine(result.FullPath, "meshes");

    string uppercaseMeshes =
        Path.Combine(result.FullPath, "Meshes");

    LinuxFileIdentityResult lowerIdentity =
        LinuxFileIdentity.Inspect(lowercaseMeshes);

    LinuxFileIdentityResult upperIdentity =
        LinuxFileIdentity.Inspect(uppercaseMeshes);

    Console.WriteLine();
    Console.WriteLine("Casefold alias check");
    Console.WriteLine("--------------------");
    Console.WriteLine($"meshes: {lowercaseMeshes}");
    Console.WriteLine($"Meshes: {uppercaseMeshes}");

    if (lowerIdentity.Success && upperIdentity.Success)
    {
        Console.WriteLine(
            "Same physical object: " +
            (lowerIdentity.SameObjectAs(upperIdentity) ? "YES" : "NO")
        );

        Console.WriteLine(
            $"meshes inode: {lowerIdentity.Inode}"
        );

        Console.WriteLine(
            $"Meshes inode: {upperIdentity.Inode}"
        );
    }
    else
    {
        Console.WriteLine(
            "Same physical object: unable to determine"
        );
    }

    Console.WriteLine();
    Console.WriteLine(
        "Read-only check: no files were modified."
    );

    return 0;
}

static int RunCollisions(string[] args)
{
    if (args.Length != 2)
    {
        Console.Error.WriteLine(
            "Error: collisions requires a directory."
        );
        Console.Error.WriteLine();
        ShowUsage();
        return 2;
    }

    string directory;

    try
    {
        directory = Path.GetFullPath(args[1]);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 3;
    }

    IReadOnlyList<DirectoryCaseCollision> collisions;

    try
    {
        collisions =
            DirectoryCollisionScanner.Scan(directory);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 3;
    }

    Console.WriteLine("CaseCompat Collision Scan");
    Console.WriteLine("=========================");
    Console.WriteLine();
    Console.WriteLine($"Directory: {directory}");
    Console.WriteLine();
    Console.WriteLine(
        $"Case-equivalent collision groups: {collisions.Count}"
    );

    foreach (DirectoryCaseCollision collision in collisions)
    {
        Console.WriteLine();
        Console.WriteLine("CASE COLLISION");
        Console.WriteLine("--------------");
        Console.WriteLine(
            $"Logical key: {collision.LogicalName}"
        );

        foreach (DirectoryCollisionMember member in collision.Members)
        {
            LinuxFileIdentityResult identity =
                LinuxFileIdentity.Inspect(member.FullPath);

            string kind =
                member.IsDirectory ? "directory" : "file";

            Console.WriteLine(
                $"  {member.Name}  [{kind}]"
            );

            if (identity.Success)
            {
                Console.WriteLine(
                    $"    dev={identity.DeviceMajor}:{identity.DeviceMinor} " +
                    $"inode={identity.Inode}"
                );
            }
            else
            {
                Console.WriteLine(
                    $"    identity unavailable: {identity.Error}"
                );
            }
        }
    }

    Console.WriteLine();
    Console.WriteLine(
        "Read-only scan: no files were modified."
    );

    return 0;
}

static void PrintCasefoldResult(
    DirectoryCasefoldResult result
)
{
    string state;

    if (!result.Exists)
    {
        state = "NOT FOUND";
    }
    else if (result.Error is not null)
    {
        state = $"UNAVAILABLE ({result.Error})";
    }
    else
    {
        state = result.CasefoldEnabled == true
            ? "ENABLED"
            : "disabled";
    }

    string flags = result.RawFlags.HasValue
        ? $"  flags=0x{result.RawFlags.Value:X8}"
        : string.Empty;

    Console.WriteLine(result.FullPath);
    Console.WriteLine(
        $"  casefold: {state}{flags}"
    );
}

static void PrintIdentityResult(
    LinuxFileIdentityResult result
)
{
    if (!result.Success)
    {
        Console.WriteLine(
            $"  identity: unavailable ({result.Error})"
        );
        return;
    }

    Console.WriteLine(
        $"  identity: dev={result.DeviceMajor}:{result.DeviceMinor} " +
        $"inode={result.Inode} links={result.LinkCount} " +
        $"mount={result.MountId}"
    );
}

static void ShowUsage()
{
    Console.WriteLine("CaseCompat");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine(
        "  casecompat doctor <Skyrim Data directory>"
    );
    Console.WriteLine(
        "  casecompat collisions <directory>"
    );
    Console.WriteLine(
        "  casecompat collision-tree <directory>"
    );

    Console.WriteLine(
        "  casecompat compare-branches <directory A> <directory B>"
    );
    Console.WriteLine(
        "  casecompat namespace-summary <directory>"
    );
    Console.WriteLine(
        "  casecompat content-summary <directory>"
    );
    Console.WriteLine(
        "  casecompat plugin-probe <plugin path>"
    );
    Console.WriteLine(
        "  casecompat record-inventory <plugin path>"
    );
}
