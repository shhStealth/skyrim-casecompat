using CaseCompat.Bethesda.Plugins;

public static class ResolveArmorAddonModelsCommand
{
    public static int Run(string[] args)
    {
        if (args.Length < 3 ||
            args.Length > 4)
        {
            Console.Error.WriteLine(
                "Error: resolve-armor-addon-models requires " +
                "a Data root, plugin path, and optional path search."
            );

            return 2;
        }

        SkyrimArmorAddonResolutionProbeResult result;

        try
        {
            result =
                SkyrimArmorAddonResolutionProbe.Inspect(
                    pluginPath: args[2],
                    dataRoot: args[1]
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"ArmorAddon resolution probe error: {ex.Message}"
            );

            return 3;
        }

        string? filter =
            args.Length == 4
                ? args[3]
                : null;

        IEnumerable<SkyrimArmorAddonReferenceResolution>
            displayed =
                result.References;

        if (!string.IsNullOrWhiteSpace(filter))
        {
            displayed =
                displayed.Where(item =>
                    item.Reference.GivenPath.Contains(
                        filter,
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    item.Reference.DataRelativePath.Contains(
                        filter,
                        StringComparison.OrdinalIgnoreCase
                    )
                );
        }

        SkyrimArmorAddonReferenceResolution[] items =
            displayed.ToArray();

        int displayedResolved =
            items.Count(item =>
                item.Resolution?.LinuxResolves == true
            );

        int displayedUnresolved =
            items.Count(item =>
                item.Resolution is not null &&
                !item.Resolution.LinuxResolves
            );

        int displayedErrors =
            items.Count(item =>
                item.Error is not null
            );

        Console.WriteLine(
            "CaseCompat ArmorAddon Reference Resolution"
        );
        Console.WriteLine(
            "=========================================="
        );
        Console.WriteLine();

        Console.WriteLine(
            $"Plugin:                {result.FullPath}"
        );

        Console.WriteLine(
            $"ModKey:                {result.ModKey}"
        );

        Console.WriteLine(
            $"Data root:             {result.DataRoot}"
        );

        Console.WriteLine(
            $"Armor Addons examined: {result.ArmorAddonsExamined:N0}"
        );

        Console.WriteLine(
            $"All model references:  {result.ReferenceCount:N0}"
        );

        if (!string.IsNullOrWhiteSpace(filter))
        {
            Console.WriteLine(
                $"Path filter:           {filter}"
            );
        }

        Console.WriteLine(
            $"Displayed references:  {items.Length:N0}"
        );

        Console.WriteLine(
            $"Linux resolves:        {displayedResolved:N0}"
        );

        Console.WriteLine(
            $"Linux unresolved:      {displayedUnresolved:N0}"
        );

        Console.WriteLine(
            $"Resolution errors:     {displayedErrors:N0}"
        );

        foreach (
            SkyrimArmorAddonReferenceResolution item
            in items)
        {
            SkyrimArmorAddonModelReference reference =
                item.Reference;

            Console.WriteLine();
            Console.WriteLine(
                $"FormKey:    {reference.FormKey}"
            );

            Console.WriteLine(
                $"EditorID:   {reference.EditorId ?? "(none)"}"
            );

            Console.WriteLine(
                $"Field:      {reference.Field}"
            );

            Console.WriteLine(
                $"Given:      {reference.GivenPath}"
            );

            Console.WriteLine(
                $"Requested:  {reference.DataRelativePath}"
            );

            if (item.Error is not null)
            {
                Console.WriteLine(
                    "Resolution: ERROR"
                );

                Console.WriteLine(
                    $"Error:      {item.Error}"
                );

                continue;
            }

            var resolution =
                item.Resolution!;

            Console.WriteLine(
                $"Resolution: " +
                $"{(resolution.LinuxResolves ? "RESOLVES" : "UNRESOLVED")}"
            );

            Console.WriteLine(
                $"Physical:   " +
                $"{resolution.ResolvedPhysicalPath ?? "(none)"}"
            );

            Console.WriteLine(
                $"Candidates: {resolution.CandidateCount}"
            );

            if (!resolution.LinuxResolves)
            {
                Console.WriteLine(
                    $"Failed at:  " +
                    $"{resolution.FailedComponentIndex?.ToString() ?? "(unknown)"}"
                );

                Console.WriteLine(
                    $"Failure:    " +
                    $"{resolution.FailureReason ?? "(unknown)"}"
                );
            }

            foreach (
                string candidate
                in resolution.EquivalentPhysicalCandidates)
            {
                Console.WriteLine(
                    $"Candidate:  {candidate}"
                );
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            "Read-only analysis: no files were modified."
        );

        return 0;
    }
}
