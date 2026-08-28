namespace CaseCompat.Core.LoadOrder;

public static class SkyrimRuntimeLoadOrderReader
{
    private sealed record PluginsFileEntry(
        string PluginName,
        bool ExplicitlyActive
    );

    public static SkyrimRuntimeLoadOrder Read(
        string pluginsPath,
        string loadOrderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            pluginsPath
        );

        ArgumentException.ThrowIfNullOrWhiteSpace(
            loadOrderPath
        );

        string fullPluginsPath =
            Path.GetFullPath(pluginsPath);

        string fullLoadOrderPath =
            Path.GetFullPath(loadOrderPath);

        if (!File.Exists(fullPluginsPath))
        {
            throw new FileNotFoundException(
                "Plugins.txt was not found.",
                fullPluginsPath
            );
        }

        if (!File.Exists(fullLoadOrderPath))
        {
            throw new FileNotFoundException(
                "loadorder.txt was not found.",
                fullLoadOrderPath
            );
        }

        PluginsFileEntry[] pluginEntries =
            ReadPluginsFile(fullPluginsPath);

        string[] loadOrderNames =
            ReadLoadOrderFile(fullLoadOrderPath);

        string[] activeNames =
            pluginEntries
                .Where(entry =>
                    entry.ExplicitlyActive
                )
                .Select(entry =>
                    entry.PluginName
                )
                .ToArray();

        string[] duplicatePluginEntries =
            FindDuplicates(
                pluginEntries.Select(entry =>
                    entry.PluginName
                )
            );

        string[] duplicateLoadOrderEntries =
            FindDuplicates(loadOrderNames);

        var loadOrderPositions =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase
            );

        for (
            int index = 0;
            index < loadOrderNames.Length;
            index++)
        {
            loadOrderPositions.TryAdd(
                loadOrderNames[index],
                index
            );
        }

        var missingActivePlugins =
            new List<string>();

        var relativeOrderFailures =
            new List<
                SkyrimRuntimeLoadOrderOrderFailure
            >();

        int previousLoadOrderIndex = -1;

        foreach (string pluginName in activeNames)
        {
            if (!loadOrderPositions.TryGetValue(
                    pluginName,
                    out int loadOrderIndex))
            {
                missingActivePlugins.Add(
                    pluginName
                );

                continue;
            }

            if (loadOrderIndex <=
                previousLoadOrderIndex)
            {
                relativeOrderFailures.Add(
                    new SkyrimRuntimeLoadOrderOrderFailure(
                        PluginName: pluginName,
                        LoadOrderIndex:
                            loadOrderIndex,
                        PreviousLoadOrderIndex:
                            previousLoadOrderIndex
                    )
                );
            }

            previousLoadOrderIndex =
                loadOrderIndex;
        }

        var activeSet =
            new HashSet<string>(
                activeNames,
                StringComparer.OrdinalIgnoreCase
            );

        SkyrimRuntimeLoadOrderEntry[] orderedEntries =
            loadOrderNames
                .Select(
                    (pluginName, index) =>
                        new SkyrimRuntimeLoadOrderEntry(
                            LoadOrderIndex: index,
                            PluginName: pluginName,
                            ExplicitlyActive:
                                activeSet.Contains(
                                    pluginName
                                )
                        )
                )
                .ToArray();

        return new SkyrimRuntimeLoadOrder(
            PluginsPath: fullPluginsPath,
            LoadOrderPath: fullLoadOrderPath,
            PluginsFileEntryCount:
                pluginEntries.Length,
            ExplicitlyActivePluginNames:
                activeNames,
            LoadOrderEntries:
                orderedEntries,
            MissingActivePlugins:
                missingActivePlugins.ToArray(),
            DuplicatePluginsFileEntries:
                duplicatePluginEntries,
            DuplicateLoadOrderEntries:
                duplicateLoadOrderEntries,
            RelativeOrderFailures:
                relativeOrderFailures.ToArray()
        );
    }

    private static PluginsFileEntry[]
        ReadPluginsFile(string path)
    {
        var entries =
            new List<PluginsFileEntry>();

        foreach (string rawLine in File.ReadLines(path))
        {
            string line =
                NormalizeLine(rawLine);

            if (line.Length == 0 ||
                line.StartsWith(
                    "#",
                    StringComparison.Ordinal
                ))
            {
                continue;
            }

            bool active =
                line.StartsWith(
                    "*",
                    StringComparison.Ordinal
                );

            string pluginName =
                active
                    ? line[1..].Trim()
                    : line;

            if (pluginName.Length == 0)
            {
                continue;
            }

            entries.Add(
                new PluginsFileEntry(
                    PluginName: pluginName,
                    ExplicitlyActive: active
                )
            );
        }

        return entries.ToArray();
    }

    private static string[]
        ReadLoadOrderFile(string path)
    {
        var entries =
            new List<string>();

        foreach (string rawLine in File.ReadLines(path))
        {
            string line =
                NormalizeLine(rawLine);

            if (line.Length == 0 ||
                line.StartsWith(
                    "#",
                    StringComparison.Ordinal
                ))
            {
                continue;
            }

            entries.Add(line);
        }

        return entries.ToArray();
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
