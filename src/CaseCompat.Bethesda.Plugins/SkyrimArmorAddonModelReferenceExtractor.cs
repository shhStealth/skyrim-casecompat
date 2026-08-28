using Mutagen.Bethesda.Skyrim;

namespace CaseCompat.Bethesda.Plugins;

public static class SkyrimArmorAddonModelReferenceExtractor
{
    public static IReadOnlyList<SkyrimArmorAddonModelReference> Extract(
        IArmorAddonGetter armorAddon)
    {
        ArgumentNullException.ThrowIfNull(
            armorAddon
        );

        var references =
            new List<SkyrimArmorAddonModelReference>();

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

        return references.ToArray();
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
