using CaseCompat.Bethesda.Plugins;

public static class ArmorAddonModelsCommand
{
    public static int Run(string[] args)
    {
        if (args.Length < 2 ||
            args.Length > 3)
        {
            Console.Error.WriteLine(
                "Error: armor-addon-models requires " +
                "a plugin path and optional search text."
            );

            return 2;
        }

        SkyrimArmorAddonModelProbeResult result;

        try
        {
            result =
                SkyrimArmorAddonModelProbe.Inspect(
                    args[1]
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"ArmorAddon model probe error: {ex.Message}"
            );

            return 3;
        }

        string? filter =
            args.Length == 3
                ? args[2]
                : null;

        IEnumerable<SkyrimArmorAddonModelReference>
            references =
                result.References;

        if (!string.IsNullOrWhiteSpace(filter))
        {
            references =
                references.Where(reference =>
                    reference.GivenPath.Contains(
                        filter,
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    (reference.EditorId?.Contains(
                        filter,
                        StringComparison.OrdinalIgnoreCase
                    ) ?? false)
                );
        }

        SkyrimArmorAddonModelReference[] displayed =
            references.ToArray();

        Console.WriteLine(
            "CaseCompat ArmorAddon Model Probe"
        );

        Console.WriteLine(
            "================================"
        );

        Console.WriteLine();

        Console.WriteLine(
            $"Path:                 {result.FullPath}"
        );

        Console.WriteLine(
            $"ModKey:               {result.ModKey}"
        );

        Console.WriteLine(
            $"Armor Addons examined: {result.ArmorAddonsExamined:N0}"
        );

        Console.WriteLine(
            $"Model references:      {result.ReferenceCount:N0}"
        );

        if (!string.IsNullOrWhiteSpace(filter))
        {
            Console.WriteLine(
                $"Filter:                {filter}"
            );

            Console.WriteLine(
                $"Matching references:   {displayed.Length:N0}"
            );
        }

        foreach (
            SkyrimArmorAddonModelReference reference
            in displayed)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"FormKey:  {reference.FormKey}"
            );

            Console.WriteLine(
                $"EditorID: {reference.EditorId ?? "(none)"}"
            );

            Console.WriteLine(
                $"Field:    {reference.Field}"
            );

            Console.WriteLine(
                $"Given:    {reference.GivenPath}"
            );

            Console.WriteLine(
                $"Data:     {reference.DataRelativePath}"
            );
        }

        Console.WriteLine();

        Console.WriteLine(
            "Read-only probe: plugin was not modified."
        );

        return 0;
    }
}
