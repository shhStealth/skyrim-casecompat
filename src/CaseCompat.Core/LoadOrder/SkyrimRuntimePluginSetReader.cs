namespace CaseCompat.Core.LoadOrder;

public static class SkyrimRuntimePluginSetReader
{
    private static readonly string[] CoreMasterNames =
    {
        "Skyrim.esm",
        "Update.esm",
        "Dawnguard.esm",
        "HearthFires.esm",
        "Dragonborn.esm"
    };

    public static SkyrimRuntimePluginSet Read(
        SkyrimRuntimeLoadOrder loadOrder,
        string skyrimCccPath)
    {
        ArgumentNullException.ThrowIfNull(
            loadOrder
        );

        ArgumentException.ThrowIfNullOrWhiteSpace(
            skyrimCccPath
        );

        string fullSkyrimCccPath =
            Path.GetFullPath(
                skyrimCccPath
            );

        if (!File.Exists(fullSkyrimCccPath))
        {
            throw new FileNotFoundException(
                "Skyrim.ccc was not found.",
                fullSkyrimCccPath
            );
        }

        string[] cccNames =
            ReadSkyrimCcc(
                fullSkyrimCccPath
            );

        string[] duplicateCccEntries =
            FindDuplicates(
                cccNames
            );

        var coreSet =
            new HashSet<string>(
                CoreMasterNames,
                StringComparer.OrdinalIgnoreCase
            );

        var cccSet =
            new HashSet<string>(
                cccNames,
                StringComparer.OrdinalIgnoreCase
            );

        var explicitSet =
            new HashSet<string>(
                loadOrder.ExplicitlyActivePluginNames,
                StringComparer.OrdinalIgnoreCase
            );

        var loadOrderSet =
            new HashSet<string>(
                loadOrder.LoadOrderEntries
                    .Select(entry =>
                        entry.PluginName
                    ),
                StringComparer.OrdinalIgnoreCase
            );

        string[] missingCoreMasters =
            CoreMasterNames
                .Where(name =>
                    !loadOrderSet.Contains(
                        name
                    )
                )
                .ToArray();

        string[] missingCccPlugins =
            cccNames
                .Where(name =>
                    !loadOrderSet.Contains(
                        name
                    )
                )
                .Distinct(
                    StringComparer.OrdinalIgnoreCase
                )
                .ToArray();

        SkyrimRuntimePluginSetEntry[] entries =
            loadOrder.LoadOrderEntries
                .Select(entry =>
                {
                    SkyrimRuntimePluginActivationSource
                        sources =
                            SkyrimRuntimePluginActivationSource.None;

                    if (coreSet.Contains(
                            entry.PluginName))
                    {
                        sources |=
                            SkyrimRuntimePluginActivationSource
                                .CoreMaster;
                    }

                    if (cccSet.Contains(
                            entry.PluginName))
                    {
                        sources |=
                            SkyrimRuntimePluginActivationSource
                                .SkyrimCcc;
                    }

                    if (explicitSet.Contains(
                            entry.PluginName))
                    {
                        sources |=
                            SkyrimRuntimePluginActivationSource
                                .ExplicitPluginsTxt;
                    }

                    return new SkyrimRuntimePluginSetEntry(
                        LoadOrderIndex:
                            entry.LoadOrderIndex,
                        PluginName:
                            entry.PluginName,
                        ActivationSources:
                            sources
                    );
                })
                .ToArray();

        return new SkyrimRuntimePluginSet(
            SourceLoadOrder:
                loadOrder,
            SkyrimCccPath:
                fullSkyrimCccPath,
            SkyrimCccEntryCount:
                cccNames.Length,
            SkyrimCccPluginNames:
                cccNames,
            LoadOrderEntries:
                entries,
            MissingCoreMasters:
                missingCoreMasters,
            MissingSkyrimCccPlugins:
                missingCccPlugins,
            DuplicateSkyrimCccEntries:
                duplicateCccEntries
        );
    }

    private static string[] ReadSkyrimCcc(
        string path)
    {
        return File.ReadLines(path)
            .Select(NormalizeLine)
            .Where(line =>
                line.Length > 0 &&
                !line.StartsWith(
                    "#",
                    StringComparison.Ordinal
                )
            )
            .ToArray();
    }

    private static string NormalizeLine(
        string rawLine)
    {
        return rawLine
            .Trim()
            .TrimStart('\uFEFF')
            .Trim();
    }

    private static string[] FindDuplicates(
        IEnumerable<string> names)
    {
        return names
            .GroupBy(
                name => name,
                StringComparer.OrdinalIgnoreCase
            )
            .Where(group =>
                group.Count() > 1
            )
            .Select(group =>
                group.First()
            )
            .OrderBy(
                name => name,
                StringComparer.OrdinalIgnoreCase
            )
            .ToArray();
    }
}
