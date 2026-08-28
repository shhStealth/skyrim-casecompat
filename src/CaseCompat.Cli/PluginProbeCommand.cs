using CaseCompat.Bethesda.Plugins;

public static class PluginProbeCommand
{
    public static int Run(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine(
                "Error: plugin-probe requires a plugin path."
            );

            return 2;
        }

        SkyrimPluginProbeResult result;

        try
        {
            result =
                SkyrimPluginProbe.Inspect(
                    args[1]
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Plugin probe error: {ex.Message}"
            );

            return 3;
        }

        Console.WriteLine(
            "CaseCompat Skyrim Plugin Probe"
        );

        Console.WriteLine(
            "=============================="
        );

        Console.WriteLine();

        Console.WriteLine(
            $"Path:    {result.FullPath}"
        );

        Console.WriteLine(
            $"ModKey:  {result.ModKey}"
        );

        Console.WriteLine(
            $"Masters: {result.MasterCount:N0}"
        );

        foreach (string master in result.Masters)
        {
            Console.WriteLine(
                $"  {master}"
            );
        }

        Console.WriteLine();
        Console.WriteLine(
            "Read-only probe: plugin was not modified."
        );

        return 0;
    }
}
