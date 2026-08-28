using CaseCompat.Core.Findings;
using CaseCompat.Core.Resolution;

namespace CaseCompat.Bethesda.Plugins;

public static class SkyrimEffectiveArmorAddonFindingBuilder
{
    public static IReadOnlyList<EffectiveAssetReferenceFinding> Build(
        SkyrimTargetArmorAddonWinnerResult winner)
    {
        ArgumentNullException.ThrowIfNull(
            winner
        );

        if (!winner.Found ||
            winner.WinningPluginName is null ||
            winner.WinningLoadOrderIndex is null)
        {
            return Array.Empty<EffectiveAssetReferenceFinding>();
        }

        var findings =
            new List<EffectiveAssetReferenceFinding>();

        foreach (
            SkyrimArmorAddonModelReference reference
            in winner.WinningModelReferences)
        {
            DataRelativePathResolution resolution =
                DataRelativePathResolver.ResolveFile(
                    winner.DataRoot,
                    reference.DataRelativePath
                );

            findings.Add(
                new EffectiveAssetReferenceFinding(
                    ConsumerKind:
                        "ArmorAddon",
                    ConsumerFormKey:
                        reference.FormKey,
                    ConsumerEditorId:
                        reference.EditorId,
                    WinningPluginName:
                        winner.WinningPluginName,
                    WinningLoadOrderIndex:
                        winner.WinningLoadOrderIndex.Value,
                    WinnerSearchComplete:
                        winner.SearchComplete,
                    ReferenceField:
                        reference.Field,
                    RawPath:
                        reference.GivenPath,
                    RequestedPath:
                        reference.DataRelativePath,
                    Resolution:
                        resolution
                )
            );
        }

        return findings.ToArray();
    }
}
