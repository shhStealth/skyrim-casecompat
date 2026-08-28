using CaseCompat.Core.LoadOrder;

public static class LoadOrderProbeCommand
{
    public static int Run(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine(
                "Error: load-order-probe requires " +
                "Plugins.txt and loadorder.txt paths."
            );

            return 2;
        }

        SkyrimRuntimeLoadOrder result;

        try
        {
            result =
                SkyrimRuntimeLoadOrderReader.Read(
                    pluginsPath: args[1],
                    loadOrderPath: args[2]
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Load-order probe error: {ex.Message}"
            );

            return 3;
        }

        Console.WriteLine(
            "CaseCompat Skyrim Runtime Load Order"
        );
        Console.WriteLine(
            "===================================="
        );
        Console.WriteLine();

        Console.WriteLine(
            $"Plugins.txt:              {result.PluginsPath}"
        );

        Console.WriteLine(
            $"loadorder.txt:            {result.LoadOrderPath}"
        );

        Console.WriteLine();
        Console.WriteLine(
            $"Plugins.txt entries:      {result.PluginsFileEntryCount:N0}"
        );

        Console.WriteLine(
            $"Explicitly active:        {result.ExplicitlyActiveCount:N0}"
        );

        Console.WriteLine(
            $"Load-order entries:       {result.LoadOrderEntryCount:N0}"
        );

        Console.WriteLine(
            $"Missing active:           {result.MissingActivePlugins.Count:N0}"
        );

        Console.WriteLine(
            $"Duplicate plugins:        {result.DuplicatePluginsFileEntries.Count:N0}"
        );

        Console.WriteLine(
            $"Duplicate load-order:     {result.DuplicateLoadOrderEntries.Count:N0}"
        );

        Console.WriteLine(
            $"Relative-order failures: {result.RelativeOrderFailures.Count:N0}"
        );

        Console.WriteLine(
            $"Consistent:               {(result.IsConsistent ? "YES" : "NO")}"
        );

        if (result.MissingActivePlugins.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine(
                "Missing active plugins:"
            );

            foreach (
                string plugin
                in result.MissingActivePlugins.Take(20))
            {
                Console.WriteLine(
                    $"  {plugin}"
                );
            }
        }

        if (result.RelativeOrderFailures.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine(
                "Relative-order failures:"
            );

            foreach (
                SkyrimRuntimeLoadOrderOrderFailure failure
                in result.RelativeOrderFailures.Take(20))
            {
                Console.WriteLine(
                    $"  {failure.PluginName}: " +
                    $"{failure.LoadOrderIndex} <= " +
                    $"{failure.PreviousLoadOrderIndex}"
                );
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            "Read-only probe: no files were modified."
        );

        return result.IsConsistent
            ? 0
            : 1;
    }
}
