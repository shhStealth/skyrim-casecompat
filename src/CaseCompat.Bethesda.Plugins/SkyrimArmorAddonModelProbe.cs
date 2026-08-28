using Mutagen.Bethesda.Skyrim;

namespace CaseCompat.Bethesda.Plugins;

public static class SkyrimArmorAddonModelProbe
{
    public static SkyrimArmorAddonModelProbeResult Inspect(
        string pluginPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            pluginPath
        );

        string fullPath =
            Path.GetFullPath(pluginPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "Plugin file was not found.",
                fullPath
            );
        }

        using var mod =
            SkyrimMod.CreateFromBinaryOverlay(
                fullPath,
                SkyrimRelease.SkyrimSE
            );

        var references =
            new List<SkyrimArmorAddonModelReference>();

        int armorAddonsExamined = 0;

        foreach (
            IArmorAddonGetter armorAddon
            in mod.EnumerateMajorRecords()
                .OfType<IArmorAddonGetter>())
        {
            armorAddonsExamined++;

            references.AddRange(
                SkyrimArmorAddonModelReferenceExtractor
                    .Extract(armorAddon)
            );
        }

        return new SkyrimArmorAddonModelProbeResult(
            FullPath: fullPath,
            ModKey: mod.ModKey.ToString(),
            ArmorAddonsExamined: armorAddonsExamined,
            References: references.ToArray()
        );
    }
}
