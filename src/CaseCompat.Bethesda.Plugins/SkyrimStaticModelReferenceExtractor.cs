using Mutagen.Bethesda.Skyrim;

namespace CaseCompat.Bethesda.Plugins;

// Pure extraction of genuine consumer-request provenance from one
// Static's single Model property. No filesystem access, winner
// selection, consumer-spelling classification, or mutation occurs here.
public static class SkyrimStaticModelReferenceExtractor
{
    public static IReadOnlyList<SkyrimStaticModelReference> Extract(
        IStaticGetter staticRecord)
    {
        ArgumentNullException.ThrowIfNull(
            staticRecord
        );

        var references =
            new List<SkyrimStaticModelReference>();

        IModelGetter? model =
            staticRecord.Model;

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
            new SkyrimStaticModelReference(
                FormKey:
                    staticRecord.FormKey.ToString(),
                EditorId:
                    staticRecord.EditorID,
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
