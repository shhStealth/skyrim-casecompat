using CaseCompat.Core.LoadOrder;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;

namespace CaseCompat.Bethesda.Plugins;

public sealed record SkyrimPluginReadError(
    string PluginName,
    string FullPath,
    string Error
);

public sealed record SkyrimTargetArmorAddonWinnerResult(
    string DataRoot,
    FormKey TargetFormKey,
    int ExplicitlyActivePluginCount,
    int PluginsChecked,
    IReadOnlyList<string> MissingPluginFiles,
    IReadOnlyList<SkyrimPluginReadError> ReadErrors,
    string? WinningPluginName,
    int? WinningLoadOrderIndex,
    string? WinningEditorId,
    IReadOnlyList<SkyrimArmorAddonModelReference> WinningModelReferences
)
{
    public bool Found =>
        WinningPluginName is not null;

    public bool SearchComplete =>
        MissingPluginFiles.Count == 0 &&
        ReadErrors.Count == 0;
}

public static class SkyrimTargetArmorAddonWinnerProbe
{
    public static SkyrimTargetArmorAddonWinnerResult Inspect(
        string dataRoot,
        SkyrimRuntimeLoadOrder loadOrder,
        string targetFormKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            dataRoot
        );

        ArgumentNullException.ThrowIfNull(
            loadOrder
        );

        ArgumentException.ThrowIfNullOrWhiteSpace(
            targetFormKey
        );

        string fullDataRoot =
            Path.GetFullPath(dataRoot);

        if (!Directory.Exists(fullDataRoot))
        {
            throw new DirectoryNotFoundException(
                fullDataRoot
            );
        }

        FormKey formKey =
            FormKey.Factory(
                targetFormKey
            );

        SkyrimRuntimeLoadOrderEntry[] active =
            loadOrder
                .OrderedExplicitlyActiveEntries
                .OrderByDescending(entry =>
                    entry.LoadOrderIndex
                )
                .ToArray();

        var missingFiles =
            new List<string>();

        var readErrors =
            new List<SkyrimPluginReadError>();

        int pluginsChecked = 0;

        foreach (
            SkyrimRuntimeLoadOrderEntry entry
            in active)
        {
            string pluginPath =
                Path.Combine(
                    fullDataRoot,
                    entry.PluginName
                );

            if (!File.Exists(pluginPath))
            {
                missingFiles.Add(
                    entry.PluginName
                );

                continue;
            }

            pluginsChecked++;

            try
            {
                using var mod =
                    SkyrimMod.CreateFromBinaryOverlay(
                        pluginPath,
                        SkyrimRelease.SkyrimSE
                    );

                var cache =
                    mod.ArmorAddons.RecordCache;

                if (!cache.ContainsKey(formKey))
                {
                    continue;
                }

                IArmorAddonGetter winner =
                    cache[formKey];

                return new SkyrimTargetArmorAddonWinnerResult(
                    DataRoot:
                        fullDataRoot,
                    TargetFormKey:
                        formKey,
                    ExplicitlyActivePluginCount:
                        active.Length,
                    PluginsChecked:
                        pluginsChecked,
                    MissingPluginFiles:
                        missingFiles.ToArray(),
                    ReadErrors:
                        readErrors.ToArray(),
                    WinningPluginName:
                        entry.PluginName,
                    WinningLoadOrderIndex:
                        entry.LoadOrderIndex,
                    WinningEditorId:
                        winner.EditorID,
                    WinningModelReferences:
                        SkyrimArmorAddonModelReferenceExtractor
                            .Extract(winner)
                );
            }
            catch (Exception ex)
            {
                readErrors.Add(
                    new SkyrimPluginReadError(
                        PluginName:
                            entry.PluginName,
                        FullPath:
                            pluginPath,
                        Error:
                            ex.Message
                    )
                );
            }
        }

        return new SkyrimTargetArmorAddonWinnerResult(
            DataRoot:
                fullDataRoot,
            TargetFormKey:
                formKey,
            ExplicitlyActivePluginCount:
                active.Length,
            PluginsChecked:
                pluginsChecked,
            MissingPluginFiles:
                missingFiles.ToArray(),
            ReadErrors:
                readErrors.ToArray(),
            WinningPluginName:
                null,
            WinningLoadOrderIndex:
                null,
            WinningEditorId:
                null,
            WinningModelReferences:
                Array.Empty<SkyrimArmorAddonModelReference>()
        );
    }
}
