using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;

namespace CaseCompat.Bethesda.Plugins;

public static class SkyrimRecordInventory
{
    public static SkyrimRecordInventoryResult Inspect(
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

        int total = 0;
        int armors = 0;
        int armorAddons = 0;
        int statics = 0;
        int weapons = 0;
        int npcs = 0;
        int textureSets = 0;

        foreach (
            var record
            in mod.EnumerateMajorRecords())
        {
            total++;

            if (record is IArmorGetter)
            {
                armors++;
            }

            if (record is IArmorAddonGetter)
            {
                armorAddons++;
            }

            if (record is IStaticGetter)
            {
                statics++;
            }

            if (record is IWeaponGetter)
            {
                weapons++;
            }

            if (record is INpcGetter)
            {
                npcs++;
            }

            if (record is ITextureSetGetter)
            {
                textureSets++;
            }
        }

        return new SkyrimRecordInventoryResult(
            FullPath: fullPath,
            ModKey: mod.ModKey.ToString(),
            TotalMajorRecords: total,
            Armors: armors,
            ArmorAddons: armorAddons,
            Statics: statics,
            Weapons: weapons,
            Npcs: npcs,
            TextureSets: textureSets
        );
    }
}
