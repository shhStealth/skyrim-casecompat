using Mutagen.Bethesda.Skyrim;

namespace CaseCompat.Bethesda.Plugins;

// Pure extraction of genuine consumer-request provenance from one
// Container's single Model property. No filesystem access, winner
// selection, consumer-spelling classification, or mutation occurs here.
public static class SkyrimContainerModelReferenceExtractor
{
    public static IReadOnlyList<SkyrimContainerModelReference> Extract(
        IContainerGetter container)
    {
        ArgumentNullException.ThrowIfNull(
            container
        );

        var references =
            new List<SkyrimContainerModelReference>();

        IModelGetter? model =
            container.Model;

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
            new SkyrimContainerModelReference(
                FormKey:
                    container.FormKey.ToString(),
                EditorId:
                    container.EditorID,
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
