using CaseCompat.Filesystem.Linux;

if (args.Length == 0)
{
    ShowUsage();
    return 1;
}

string command = args[0].ToLowerInvariant();

switch (command)
{
    case "help":
    case "--help":
    case "-h":
        ShowUsage();
        return 0;

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

    case "armor-addon-models":
        return ArmorAddonModelsCommand.Run(args);

    case "resolve-data-path":
        return ResolveDataPathCommand.Run(args);

    case "repair-plan":
        return RepairPlanCommand.Run(args);

    case "repair-plan-batch":
        return RepairPlanBatchCommand.Run(args);

    case "repair-status":
        return RepairStatusCommand.Run(args);

    case "repair-status-batch":
        return RepairStatusBatchCommand.Run(args);

    case "repair-apply-batch":
        return RepairApplyBatchCommand.Run(args);

    case "repair-apply":
        return RepairApplyCommand.Run(args);

    case "repair-rollback-batch":
        return RepairRollbackBatchCommand.Run(args);

    case "repair-rollback":
        return RepairRollbackCommand.Run(args);

    case "resolve-armor-addon-models":
        return ResolveArmorAddonModelsCommand.Run(args);

    case "armor-records":
        return ArmorRecordsCommand.Run(args);

    case "load-order-probe":
        return LoadOrderProbeCommand.Run(args);

    case "armor-addon-winner":
        return ArmorAddonWinnerCommand.Run(args);

    case "effective-armor-addon-models":
        return EffectiveArmorAddonModelsCommand.Run(args);

    case "winning-armor-addon-inventory":
        return WinningArmorAddonInventoryCommand.Run(args);

    case "effective-armor-addon-scan":
        return EffectiveArmorAddonScanCommand.Run(args);

    case "archive-candidate-index":
        return ArchiveCandidateIndexCommand.Run(args);

    case "runtime-plugin-set":
        return RuntimePluginSetCommand.Run(args);

    case "runtime-archive-evidence":
        return RuntimeArchiveEvidenceCommand.Run(args);

    case "effective-armor-addon-archive-candidates":
        return EffectiveArmorAddonArchiveCandidatesCommand.Run(args);

    case "armor-addon-snapshot-diagnostics":
        return ArmorAddonSnapshotDiagnosticsCommand.Run(args);

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
        "  casecompat --help"
    );

    Console.WriteLine();
    Console.WriteLine("Repair workflow");
    Console.WriteLine("---------------");
    Console.WriteLine(
        "  repair-plan      Create and persist a repair plan; " +
        "does not modify Skyrim Data."
    );
    Console.WriteLine(
        "  repair-plan-batch  Preflight multiple paths and persist " +
        "independent safe repair plans; does not modify Skyrim Data."
    );
    Console.WriteLine(
        "  repair-status    Inspect persisted repair state; read-only."
    );
    Console.WriteLine(
        "  repair-status-batch  Inspect observed batch child plans; " +
        "read-only."
    );
    Console.WriteLine(
        "  repair-apply-batch   Apply a verified completed repair batch."
    );
    Console.WriteLine(
        "  repair-apply     Apply a persisted repair plan."
    );
    Console.WriteLine(
        "  repair-rollback-batch  Roll back a verified completed batch."
    );
    Console.WriteLine(
        "  repair-rollback  Roll back CaseCompat-owned repair changes."
    );
    Console.WriteLine();
    Console.WriteLine(
        "  Recommended: repair-plan -> repair-status -> " +
        "repair-apply -> repair-status"
    );
    Console.WriteLine(
        "  Batch:       repair-plan-batch -> repair-status-batch -> " +
        "repair-apply-batch -> repair-status-batch"
    );
    Console.WriteLine(
        "  Batch undo:  repair-rollback-batch -> repair-status-batch"
    );
    Console.WriteLine(
        "  Recovery:    repair-rollback -> repair-status"
    );

    Console.WriteLine();
    Console.WriteLine("Repair command usage:");
    Console.WriteLine(
        "  casecompat repair-plan <Skyrim Data directory> " +
        "<Data-relative file path> <journal directory> " +
        "[manifest file name]"
    );
    Console.WriteLine(
        "  casecompat repair-plan-batch <Skyrim Data directory> " +
        "<path-list file> <batch directory> [manifest file name]"
    );
    Console.WriteLine(
        "  casecompat repair-status <journal directory> " +
        "<Skyrim Data directory>"
    );
    Console.WriteLine(
        "  casecompat repair-status <journal directory> " +
        "<manifest file name> <Skyrim Data directory>"
    );
    Console.WriteLine(
        "  casecompat repair-status-batch <batch directory> " +
        "<Skyrim Data directory>"
    );
    Console.WriteLine(
        "  casecompat repair-status-batch <batch directory> " +
        "<manifest file name> <Skyrim Data directory>"
    );
    Console.WriteLine(
        "  casecompat repair-apply-batch <batch directory> " +
        "<Skyrim Data directory>"
    );
    Console.WriteLine(
        "  casecompat repair-apply-batch <batch directory> " +
        "<manifest file name> <Skyrim Data directory>"
    );
    Console.WriteLine(
        "  casecompat repair-apply <journal directory> " +
        "<Skyrim Data directory>"
    );
    Console.WriteLine(
        "  casecompat repair-apply <journal directory> " +
        "<manifest file name> <Skyrim Data directory>"
    );
    Console.WriteLine(
        "  casecompat repair-rollback-batch <batch directory> " +
        "<Skyrim Data directory>"
    );
    Console.WriteLine(
        "  casecompat repair-rollback-batch <batch directory> " +
        "<manifest file name> <Skyrim Data directory>"
    );
    Console.WriteLine(
        "  casecompat repair-rollback <journal directory> " +
        "<Skyrim Data directory>"
    );
    Console.WriteLine(
        "  casecompat repair-rollback <journal directory> " +
        "<manifest file name> <Skyrim Data directory>"
    );

    Console.WriteLine();
    Console.WriteLine(
        "  Default repair plan manifest file name: repair-plan.json"
    );
    Console.WriteLine();

    Console.WriteLine("Other commands:");
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
    Console.WriteLine(
        "  casecompat armor-addon-models <plugin path> [search]"
    );
    Console.WriteLine(
        "  casecompat resolve-data-path <Data root> " +
        "<Data-relative file path>"
    );
    Console.WriteLine(
        "  casecompat resolve-armor-addon-models <Data root> " +
        "<plugin path> [path search]"
    );
    Console.WriteLine(
        "  casecompat armor-records <plugin path>"
    );
    Console.WriteLine(
        "  casecompat load-order-probe <Plugins.txt> <loadorder.txt>"
    );
    Console.WriteLine(
        "  casecompat armor-addon-winner <Data root> <Plugins.txt> " +
        "<loadorder.txt> <Skyrim.ccc> <FormKey>"
    );
    Console.WriteLine(
        "  casecompat effective-armor-addon-models <Data root> " +
        "<Plugins.txt> <loadorder.txt> <Skyrim.ccc> <FormKey> " +
        "[path search]"
    );
    Console.WriteLine(
        "  casecompat winning-armor-addon-inventory <Data root> " +
        "<Plugins.txt> <loadorder.txt> <Skyrim.ccc> [path search]"
    );
    Console.WriteLine(
        "  casecompat effective-armor-addon-scan <Data root> " +
        "<Plugins.txt> <loadorder.txt> <Skyrim.ccc> [path search]"
    );
    Console.WriteLine(
        "  casecompat archive-candidate-index <Data root> " +
        "[requested asset path]"
    );
    Console.WriteLine(
        "  casecompat runtime-plugin-set <Plugins.txt> " +
        "<loadorder.txt> <Skyrim.ccc>"
    );
    Console.WriteLine(
        "  casecompat runtime-archive-evidence <Data root> " +
        "<Plugins.txt> <loadorder.txt> <Skyrim.ccc> <INI directory>"
    );
    Console.WriteLine(
        "  casecompat effective-armor-addon-archive-candidates " +
        "<Data root> <Plugins.txt> <loadorder.txt> <Skyrim.ccc> " +
        "<INI directory> [path search]"
    );
    Console.WriteLine(
        "  casecompat armor-addon-snapshot-diagnostics " +
        "<Data root> <Plugins.txt> <loadorder.txt> <Skyrim.ccc> " +
        "<INI directory> [path search]"
    );
}
