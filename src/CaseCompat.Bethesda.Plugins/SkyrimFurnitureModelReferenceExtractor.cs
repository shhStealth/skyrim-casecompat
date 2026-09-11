using Mutagen.Bethesda.Skyrim;

namespace CaseCompat.Bethesda.Plugins;

// Pure extraction of genuine consumer-request provenance from one
// Furniture's single Model property. No filesystem access, winner
// selection, consumer-spelling classification, or mutation occurs here.
public static class SkyrimFurnitureModelReferenceExtractor
{
    public static IReadOnlyList<SkyrimFurnitureModelReference> Extract(
        IFurnitureGetter furniture)
    {
        ArgumentNullException.ThrowIfNull(
            furniture
        );

        var references =
            new List<SkyrimFurnitureModelReference>();

        IModelGetter? model =
            furniture.Model;

        if (model is null)
        {
            return references.ToArray();
        }

        var file =
            model.File;

        if (file.IsNull)
        {
            return references.ToArray();
        }

        string givenPath =
            file.GivenPath;

        if (string.IsNullOrWhiteSpace(
                givenPath))
        {
            return references.ToArray();
        }

        references.Add(
            new SkyrimFurnitureModelReference(
                FormKey:
                    furniture.FormKey.ToString(),
                EditorId:
                    furniture.EditorID,
                Field:
                    "Model",
                GivenPath:
                    givenPath,
                DataRelativePath:
                    file.DataRelativePath.ToString()
            )
        );

        return references.ToArray();
    }
}
