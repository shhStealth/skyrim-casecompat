using CaseCompat.Bethesda.Plugins;

public static class RecordInventoryCommand
{
    public static int Run(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine(
                "Error: record-inventory requires a plugin path."
            );

            return 2;
        }

        SkyrimRecordInventoryResult result;

        try
        {
            result =
                SkyrimRecordInventory.Inspect(
                    args[1]
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Record inventory error: {ex.Message}"
            );

            return 3;
        }

        Console.WriteLine(
            "CaseCompat Skyrim Record Inventory"
        );

        Console.WriteLine(
            "=================================="
        );

        Console.WriteLine();

        Console.WriteLine(
            $"Path:              {result.FullPath}"
        );

        Console.WriteLine(
            $"ModKey:            {result.ModKey}"
        );

        Console.WriteLine(
            $"Major records:     {result.TotalMajorRecords:N0}"
        );

        Console.WriteLine();

        Console.WriteLine(
            $"Armor:             {result.Armors:N0}"
        );

        Console.WriteLine(
            $"Armor Addons:      {result.ArmorAddons:N0}"
        );

        Console.WriteLine(
            $"Statics:           {result.Statics:N0}"
        );

        Console.WriteLine(
            $"Weapons:           {result.Weapons:N0}"
        );

        Console.WriteLine(
            $"NPCs:              {result.Npcs:N0}"
        );

        Console.WriteLine(
            $"Texture Sets:      {result.TextureSets:N0}"
        );

        Console.WriteLine();

        Console.WriteLine(
            "Read-only inventory: plugin was not modified."
        );

        return 0;
    }
}
