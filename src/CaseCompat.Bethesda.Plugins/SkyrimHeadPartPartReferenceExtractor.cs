using Mutagen.Bethesda.Skyrim;

namespace CaseCompat.Bethesda.Plugins;

/*
 * Pure extraction of genuine consumer-request provenance from one HeadPart.
 *
 * Every non-null/non-empty Parts[*].FileName is retained regardless of
 * PartType. The Mutagen asset link supplies both the original GivenPath and
 * its DataRelativePath interpretation; exact path casing is not rewritten.
 *
 * Source order is preserved and PartIndex remains the index of the original
 * Parts entry.
 *
 * No filesystem access, loose/provider/archive lookup, Windows-logical
 * grouping, consumer-spelling classification, physical comparison, hashing,
 * winner selection, persistence, repair planning, authorization, execution,
 * rollback, or recovery occurs here.
 */
public static class SkyrimHeadPartPartReferenceExtractor
{
    public static IReadOnlyList<SkyrimHeadPartPartReference> Extract(
        IHeadPartGetter headPart)
    {
        ArgumentNullException.ThrowIfNull(
            headPart
        );

        var references =
            new List<SkyrimHeadPartPartReference>(
                headPart.Parts.Count
            );

        for (
            int partIndex = 0;
            partIndex < headPart.Parts.Count;
            ++partIndex)
        {
            IPartGetter part =
                headPart.Parts[
                    partIndex
                ];

            var file =
                part.FileName;

            if (file is null || file.IsNull)
            {
                continue;
            }

            string givenPath =
                file.GivenPath;

            if (string.IsNullOrWhiteSpace(
                    givenPath))
            {
                continue;
            }

            references.Add(
                new SkyrimHeadPartPartReference(
                    FormKey:
                        headPart.FormKey.ToString(),
                    EditorId:
                        headPart.EditorID,
                    PartIndex:
                        partIndex,
                    PartType:
                        part.PartType,
                    GivenPath:
                        givenPath,
                    DataRelativePath:
                        file.DataRelativePath.ToString()
                )
            );
        }

        return references.ToArray();
    }
}
