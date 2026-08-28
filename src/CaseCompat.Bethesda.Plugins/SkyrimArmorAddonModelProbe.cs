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

            var worldModel =
                armorAddon.WorldModel;

            if (worldModel is not null)
            {
                AddReference(
                    references,
                    armorAddon,
                    "WorldModel.Male",
                    worldModel.Male
                );

                AddReference(
                    references,
                    armorAddon,
                    "WorldModel.Female",
                    worldModel.Female
                );
            }

            var firstPersonModel =
                armorAddon.FirstPersonModel;

            if (firstPersonModel is not null)
            {
                AddReference(
                    references,
                    armorAddon,
                    "FirstPersonModel.Male",
                    firstPersonModel.Male
                );

                AddReference(
                    references,
                    armorAddon,
                    "FirstPersonModel.Female",
                    firstPersonModel.Female
                );
            }
        }

        return new SkyrimArmorAddonModelProbeResult(
            FullPath: fullPath,
            ModKey: mod.ModKey.ToString(),
            ArmorAddonsExamined: armorAddonsExamined,
            References: references.ToArray()
        );
    }

    private static void AddReference(
        List<SkyrimArmorAddonModelReference> references,
        IArmorAddonGetter armorAddon,
        string field,
        IModelGetter? model)
    {
        if (model is null)
        {
            return;
        }

        var file =
            model.File;

        if (file.IsNull)
        {
            return;
        }

        string givenPath =
            file.GivenPath;

        if (string.IsNullOrWhiteSpace(givenPath))
        {
            return;
        }

        references.Add(
            new SkyrimArmorAddonModelReference(
                FormKey:
                    armorAddon.FormKey.ToString(),
                EditorId:
                    armorAddon.EditorID,
                Field:
                    field,
                GivenPath:
                    givenPath,
                DataRelativePath:
                    file.DataRelativePath.ToString()
            )
        );
    }
}
