using CaseCompat.Core.LoadOrder;

public static class RuntimePluginSetCommand
{
    public static int Run(string[] args)
    {
        if (args.Length != 4)
        {
            Console.Error.WriteLine(
                "Error: runtime-plugin-set requires " +
                "Plugins.txt, loadorder.txt, and Skyrim.ccc."
            );

            return 2;
        }

        try
        {
            SkyrimRuntimeLoadOrder loadOrder =
                SkyrimRuntimeLoadOrderReader.Read(
                    pluginsPath:
                        args[1],
                    loadOrderPath:
                        args[2]
                );

            SkyrimRuntimePluginSet result =
                SkyrimRuntimePluginSetReader.Read(
                    loadOrder,
                    args[3]
                );

            Console.WriteLine(
                "CaseCompat Skyrim Runtime Plugin Set"
            );

            Console.WriteLine(
                "===================================="
            );

            Console.WriteLine();

            Console.WriteLine(
                $"Load-order entries:             {loadOrder.LoadOrderEntryCount,6:N0}"
            );

            Console.WriteLine(
                $"Plugins.txt explicitly active:  {loadOrder.ExplicitlyActiveCount,6:N0}"
            );

            Console.WriteLine(
                $"Skyrim.ccc entries:             {result.SkyrimCccEntryCount,6:N0}"
            );

            Console.WriteLine();

            Console.WriteLine(
                $"Runtime-active entries:         {result.RuntimeActiveCount,6:N0}"
            );

            Console.WriteLine(
                $"Load-order-only entries:        {result.LoadOrderOnlyCount,6:N0}"
            );

            Console.WriteLine();

            PrintSourceCount(
                result,
                "Core master",
                SkyrimRuntimePluginActivationSource.CoreMaster
            );

            PrintSourceCount(
                result,
                "Skyrim.ccc",
                SkyrimRuntimePluginActivationSource.SkyrimCcc
            );

            PrintSourceCount(
                result,
                "Explicit Plugins.txt",
                SkyrimRuntimePluginActivationSource.ExplicitPluginsTxt
            );

            int multiSourceCount =
                result.LoadOrderEntries.Count(entry =>
                {
                    int value =
                        (int)entry.ActivationSources;

                    return value != 0 &&
                        (value & (value - 1)) != 0;
                });

            Console.WriteLine(
                $"Multiple activation sources:    {multiSourceCount,6:N0}"
            );

            Console.WriteLine();

            Console.WriteLine(
                $"Missing core masters:           {result.MissingCoreMasters.Count,6:N0}"
            );

            Console.WriteLine(
                $"Missing Skyrim.ccc plugins:     {result.MissingSkyrimCccPlugins.Count,6:N0}"
            );

            Console.WriteLine(
                $"Duplicate Skyrim.ccc entries:   {result.DuplicateSkyrimCccEntries.Count,6:N0}"
            );

            Console.WriteLine(
                $"Source load order consistent:   {(loadOrder.IsConsistent ? "YES" : "NO")}"
            );

            Console.WriteLine(
                $"Runtime plugin set consistent:  {(result.IsConsistent ? "YES" : "NO")}"
            );

            Console.WriteLine();

            Console.WriteLine(
                "Load-order-only entries:"
            );

            if (result.LoadOrderOnlyEntries.Count == 0)
            {
                Console.WriteLine(
                    "  (none)"
                );
            }
            else
            {
                foreach (
                    SkyrimRuntimePluginSetEntry entry
                    in result.LoadOrderOnlyEntries)
                {
                    Console.WriteLine(
                        $"  {entry.LoadOrderIndex,5}  {entry.PluginName}"
                    );
                }
            }

            Console.WriteLine();
            Console.WriteLine(
                "Activation evidence only: no files were modified."
            );

            return result.IsConsistent
                ? 0
                : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Runtime plugin set error: {ex.Message}"
            );

            return 3;
        }
    }

    private static void PrintSourceCount(
        SkyrimRuntimePluginSet result,
        string label,
        SkyrimRuntimePluginActivationSource source)
    {
        int count =
            result.LoadOrderEntries.Count(entry =>
                entry.IsActivatedBy(
                    source
                )
            );

        Console.WriteLine(
            $"{label + " activation:",-31}{count,6:N0}"
        );
    }
}
