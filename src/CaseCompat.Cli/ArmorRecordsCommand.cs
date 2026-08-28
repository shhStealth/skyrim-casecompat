using CaseCompat.Bethesda.Plugins;

public static class ArmorRecordsCommand
{
    public static int Run(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine(
                "Error: armor-records requires a plugin path."
            );

            return 2;
        }

        SkyrimArmorRecordProbeResult result;

        try
        {
            result =
                SkyrimArmorRecordProbe.Inspect(
                    args[1]
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Armor record probe error: {ex.Message}"
            );

            return 3;
        }

        Console.WriteLine(
            "CaseCompat Skyrim Armor Record Probe"
        );
        Console.WriteLine(
            "====================================="
        );
        Console.WriteLine();

        Console.WriteLine(
            $"Path:          {result.FullPath}"
        );
        Console.WriteLine(
            $"ModKey:        {result.ModKey}"
        );
        Console.WriteLine(
            $"Armor records: {result.RecordCount:N0}"
        );

        foreach (SkyrimArmorRecord record in result.Records)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"FormKey:  {record.FormKey}"
            );
            Console.WriteLine(
                $"EditorID: {record.EditorId ?? "(none)"}"
            );
            Console.WriteLine(
                $"Armature: {record.ArmorAddonFormKeys.Count:N0}"
            );

            foreach (
                string formKey
                in record.ArmorAddonFormKeys)
            {
                Console.WriteLine(
                    $"  {formKey}"
                );
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            "Read-only probe: plugin was not modified."
        );

        return 0;
    }
}
