using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.LoadOrder;

public static class ArmorAddonWinnerCommand
{
    public static int Run(string[] args)
    {
        if (args.Length != 6)
        {
            Console.Error.WriteLine(
                "Error: armor-addon-winner requires " +
                "a Data root, Plugins.txt, loadorder.txt, " +
                "Skyrim.ccc, and ArmorAddon FormKey."
            );

            return 2;
        }

        SkyrimRuntimeLoadOrder loadOrder;
        SkyrimRuntimePluginSet runtimePluginSet;
        SkyrimTargetArmorAddonWinnerResult result;

        try
        {
            loadOrder =
                SkyrimRuntimeLoadOrderReader.Read(
                    pluginsPath: args[2],
                    loadOrderPath: args[3]
                );

            runtimePluginSet =
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

            result =
                SkyrimTargetArmorAddonWinnerProbe.Inspect(
                    dataRoot: args[1],
                    runtimePluginSet: runtimePluginSet,
                    targetFormKey: args[5]
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"ArmorAddon winner probe error: {ex.Message}"
            );

            return 3;
        }

        Console.WriteLine(
            "CaseCompat Target ArmorAddon Winner"
        );
        Console.WriteLine(
            "==================================="
        );
        Console.WriteLine();

        Console.WriteLine(
            $"Data root:                 {result.DataRoot}"
        );

        Console.WriteLine(
            $"Target FormKey:            {result.TargetFormKey}"
        );

        Console.WriteLine(
            $"Runtime-active plugins:    {result.RuntimeActivePluginCount:N0}"
        );

        Console.WriteLine(
            $"Plugins opened:            {result.PluginsChecked:N0}"
        );

        Console.WriteLine(
            $"Missing plugin files:      {result.MissingPluginFiles.Count:N0}"
        );

        Console.WriteLine(
            $"Plugin read errors:        {result.ReadErrors.Count:N0}"
        );

        Console.WriteLine();

        if (result.Found)
        {
            Console.WriteLine(
                "Winner found:              YES"
            );

            Console.WriteLine(
                $"Winning plugin:           {result.WinningPluginName}"
            );

            Console.WriteLine(
                $"Load-order index:         {result.WinningLoadOrderIndex}"
            );

            Console.WriteLine(
                $"EditorID:                 {result.WinningEditorId ?? "(none)"}"
            );
        }
        else
        {
            Console.WriteLine(
                "Winner found:              NO"
            );
        }

        if (result.MissingPluginFiles.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine(
                "First missing plugin files:"
            );

            foreach (
                string plugin
                in result.MissingPluginFiles.Take(20))
            {
                Console.WriteLine(
                    $"  {plugin}"
                );
            }
        }

        if (result.ReadErrors.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine(
                "First plugin read errors:"
            );

            foreach (
                SkyrimPluginReadError error
                in result.ReadErrors.Take(20))
            {
                Console.WriteLine(
                    $"  {error.PluginName}: {error.Error}"
                );
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            "Scope: runtime-active plugins."
        );

        Console.WriteLine(
            "Read-only probe: no files were modified."
        );

        return result.Found
            ? 0
            : 1;
    }
}
