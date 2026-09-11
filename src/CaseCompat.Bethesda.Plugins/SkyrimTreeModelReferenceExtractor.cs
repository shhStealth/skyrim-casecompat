using Mutagen.Bethesda.Skyrim;

namespace CaseCompat.Bethesda.Plugins;

// Pure extraction of genuine consumer-request provenance from one
// Tree's single Model property. No filesystem access, winner
// selection, consumer-spelling classification, or mutation occurs here.
public static class SkyrimTreeModelReferenceExtractor
{
    public static IReadOnlyList<SkyrimTreeModelReference> Extract(
        ITreeGetter tree)
    {
        ArgumentNullException.ThrowIfNull(
            tree
        );

        var references =
            new List<SkyrimTreeModelReference>();

        IModelGetter? model =
            tree.Model;

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
            new SkyrimTreeModelReference(
                FormKey:
                    tree.FormKey.ToString(),
                EditorId:
                    tree.EditorID,
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
