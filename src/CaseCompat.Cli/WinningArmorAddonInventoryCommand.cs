using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.LoadOrder;

public static class WinningArmorAddonInventoryCommand
{
    public static int Run(string[] args)
    {
        if (args.Length < 5 ||
            args.Length > 6)
        {
            Console.Error.WriteLine(
                "Error: winning-armor-addon-inventory requires " +
                "a Data root, Plugins.txt, loadorder.txt, " +
                "Skyrim.ccc, and optional path search."
            );

            return 2;
        }

        try
        {
            SkyrimRuntimeLoadOrder loadOrder =
                SkyrimRuntimeLoadOrderReader.Read(
                    pluginsPath: args[2],
                    loadOrderPath: args[3]
                );

            SkyrimRuntimePluginSet runtimePluginSet =
                SkyrimRuntimePluginSetReader.Read(
                    loadOrder,
                    args[4]
                );

            if (!runtimePluginSet.IsConsistent)
            {
                Console.Error.WriteLine(
                    "Error: runtime plugin set is inconsistent."
                );

                return 4;
            }

            SkyrimWinningArmorAddonInventoryResult result =
                SkyrimWinningArmorAddonInventory.Inspect(
                    dataRoot: args[1],
                    runtimePluginSet: runtimePluginSet
                );

            Console.WriteLine(
                "CaseCompat Winning ArmorAddon Inventory"
            );
            Console.WriteLine(
                "======================================"
            );
            Console.WriteLine();

            Console.WriteLine(
                $"Runtime-active plugins:    {result.RuntimeActivePluginCount:N0}"
            );

            Console.WriteLine(
                $"Plugins opened:            {result.PluginsOpened:N0}"
            );

            Console.WriteLine(
                $"Missing plugin files:      {result.MissingPluginFiles.Count:N0}"
            );

            Console.WriteLine(
                $"Plugin read errors:        {result.ReadErrors.Count:N0}"
            );

            Console.WriteLine(
                $"Winner search complete:    {(result.SearchComplete ? "YES" : "NO")}"
            );

            Console.WriteLine(
                $"Winning ArmorAddons:       {result.WinningArmorAddonCount:N0}"
            );

            Console.WriteLine(
                $"Winning model references: {result.WinningModelReferenceCount:N0}"
            );

            if (args.Length == 6)
            {
                string filter =
                    args[5];

                SkyrimWinningArmorAddonRecord[] matches =
                    result.Winners
                        .Where(record =>
                            record.ModelReferences.Any(reference =>
                                reference.GivenPath.Contains(
                                    filter,
                                    StringComparison.OrdinalIgnoreCase
                                ) ||
                                reference.DataRelativePath.Contains(
                                    filter,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                        )
                        .ToArray();

                Console.WriteLine();
                Console.WriteLine(
                    $"Path filter:              {filter}"
                );

                Console.WriteLine(
                    $"Matching winners:         {matches.Length:N0}"
                );

                foreach (
                    SkyrimWinningArmorAddonRecord record
                    in matches)
                {
                    Console.WriteLine();
                    Console.WriteLine(
                        $"FormKey:   {record.FormKey}"
                    );

                    Console.WriteLine(
                        $"EditorID:  {record.EditorId ?? "(none)"}"
                    );

                    Console.WriteLine(
                        $"Provider:  {record.WinningPluginName}"
                    );

                    Console.WriteLine(
                        $"Order:     {record.WinningLoadOrderIndex}"
                    );

                    foreach (
                        SkyrimArmorAddonModelReference reference
                        in record.ModelReferences.Where(reference =>
                            reference.GivenPath.Contains(
                                filter,
                                StringComparison.OrdinalIgnoreCase
                            ) ||
                            reference.DataRelativePath.Contains(
                                filter,
                                StringComparison.OrdinalIgnoreCase
                            )
                        ))
                    {
                        Console.WriteLine(
                            $"  {reference.Field}: " +
                            $"{reference.DataRelativePath}"
                        );
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine(
                "Read-only inventory: no files were modified."
            );

            return result.SearchComplete
                ? 0
                : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Winning ArmorAddon inventory error: {ex.Message}"
            );

            return 3;
        }
    }
}
