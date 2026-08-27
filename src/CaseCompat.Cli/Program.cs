using CaseCompat.Filesystem.Linux;

if (args.Length == 0)
{
    ShowUsage();
    return 1;
}

string command = args[0].ToLowerInvariant();

if (command == "doctor")
{
    if (args.Length != 2)
    {
        Console.Error.WriteLine("Error: doctor requires a Skyrim Data directory.");
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
    Console.WriteLine($"Platform:       {(result.IsLinux ? "Linux" : "Unsupported")}");
    Console.WriteLine($"Requested path: {result.RequestedPath}");
    Console.WriteLine($"Resolved path:  {result.FullPath}");
    Console.WriteLine($"Directory:      {(result.Exists ? "FOUND" : "NOT FOUND")}");
    Console.WriteLine();
    Console.WriteLine("Read-only check: no files were modified.");

    if (!result.IsLinux)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine("CaseCompat currently supports Linux only.");
        return 4;
    }

    if (!result.Exists)
    {
        return 5;
    }

    return 0;
}

Console.Error.WriteLine($"Unknown command: {args[0]}");
Console.Error.WriteLine();
ShowUsage();
return 2;

static void ShowUsage()
{
    Console.WriteLine("CaseCompat");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  casecompat doctor <Skyrim Data directory>");
}
