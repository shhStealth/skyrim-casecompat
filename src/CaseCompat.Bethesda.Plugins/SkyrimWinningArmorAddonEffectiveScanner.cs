using CaseCompat.Core.Findings;
using CaseCompat.Core.Resolution;

namespace CaseCompat.Bethesda.Plugins;

public sealed record SkyrimAssetPathResolutionError(
    string RequestedPath,
    int AffectedReferenceCount,
    string Error
);

public sealed record SkyrimWinningArmorAddonEffectiveScanResult(
    SkyrimWinningArmorAddonInventoryResult Inventory,
    int UniqueRequestedPathCount,
    IReadOnlyList<SkyrimAssetPathResolutionError> ResolutionErrors,
    IReadOnlyList<EffectiveAssetReferenceFinding> Findings
)
{
    public int ResolvedUniquePathCount =>
        UniqueRequestedPathCount -
        ResolutionErrors.Count;

    public int AvoidedResolutionCalls =>
        Inventory.WinningModelReferenceCount -
        UniqueRequestedPathCount;

    public bool ResolutionSearchComplete =>
        ResolutionErrors.Count == 0;

    public bool Complete =>
        Inventory.SearchComplete &&
        ResolutionSearchComplete;
}

public static class SkyrimWinningArmorAddonEffectiveScanner
{
    private sealed record PendingReference(
        SkyrimWinningArmorAddonRecord Record,
        SkyrimArmorAddonModelReference Reference
    );

    public static SkyrimWinningArmorAddonEffectiveScanResult Inspect(
        SkyrimWinningArmorAddonInventoryResult inventory)
    {
        ArgumentNullException.ThrowIfNull(
            inventory
        );

        PendingReference[] pending =
            inventory.Winners
                .SelectMany(record =>
                    record.ModelReferences.Select(
                        reference =>
                            new PendingReference(
                                Record: record,
                                Reference: reference
                            )
                    )
                )
                .ToArray();

        IGrouping<string, PendingReference>[] pathGroups =
            pending
                .GroupBy(
                    item =>
                        item.Reference.DataRelativePath,
                    StringComparer.Ordinal
                )
                .ToArray();

        var findings =
            new List<EffectiveAssetReferenceFinding>(
                pending.Length
            );

        var resolutionErrors =
            new List<SkyrimAssetPathResolutionError>();

        foreach (
            IGrouping<string, PendingReference> group
            in pathGroups)
        {
            DataRelativePathResolution resolution;

            try
            {
                resolution =
                    DataRelativePathResolver.ResolveFile(
                        inventory.DataRoot,
                        group.Key
                    );
            }
            catch (Exception ex)
            {
                resolutionErrors.Add(
                    new SkyrimAssetPathResolutionError(
                        RequestedPath:
                            group.Key,
                        AffectedReferenceCount:
                            group.Count(),
                        Error:
                            ex.Message
                    )
                );

                continue;
            }

            foreach (
                PendingReference item
                in group)
            {
                findings.Add(
                    new EffectiveAssetReferenceFinding(
                        ConsumerKind:
                            "ArmorAddon",
                        ConsumerFormKey:
                            item.Record.FormKey,
                        ConsumerEditorId:
                            item.Record.EditorId,
                        WinningPluginName:
                            item.Record.WinningPluginName,
                        WinningLoadOrderIndex:
                            item.Record.WinningLoadOrderIndex,
                        WinnerSearchComplete:
                            inventory.SearchComplete,
                        ReferenceField:
                            item.Reference.Field,
                        RawPath:
                            item.Reference.GivenPath,
                        RequestedPath:
                            item.Reference.DataRelativePath,
                        Resolution:
                            resolution
                    )
                );
            }
        }

        return new SkyrimWinningArmorAddonEffectiveScanResult(
            Inventory:
                inventory,
            UniqueRequestedPathCount:
                pathGroups.Length,
            ResolutionErrors:
                resolutionErrors.ToArray(),
            Findings:
                findings.ToArray()
        );
    }
}
