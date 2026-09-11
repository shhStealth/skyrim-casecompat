using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Analysis;
using CaseCompat.Core.Repair;

namespace CaseCompat.Bethesda.Assets;

// Authority state for projecting genuine texture-slot requests embedded in
// resolved .bgsm/.bgem material files into the generic aggregate
// consumer-spelling model. Mirrors
// SkyrimWinningMeshTextureAggregateConsumerSpellingEvidenceProjector
// exactly - "winner search" completeness here means every real material
// path could be opened and read (see
// SkyrimWinningMaterialTextureInventoryResult.SearchComplete).
public enum
    SkyrimWinningMaterialTextureAggregateConsumerSpellingEvidenceProjectionState
{
    Complete,
    IncompleteWinnerSearch,
    IndeterminateConsumerPathEvidence
}

public sealed record
    SkyrimWinningMaterialTextureAggregateConsumerSpellingEvidenceProjectionResult(
        SkyrimWinningMaterialTextureInventoryResult Inventory,
        SkyrimWinningMaterialTextureAggregateConsumerSpellingEvidenceProjectionState
            State,
        IReadOnlyList<DataRelativePathAggregateConsumerSpellingEvidence>
            Evidence,
        string? Error
    ) : ISkyrimWinningConsumerSpellingEvidenceSource
{
    public string SourceName =>
        "MaterialTexture";

    public string DataRoot =>
        Inventory.DataRoot;

    public bool WinnerSearchComplete =>
        Inventory.SearchComplete;

    public bool ConsumerPathEvidenceComplete =>
        State ==
        SkyrimWinningMaterialTextureAggregateConsumerSpellingEvidenceProjectionState
            .Complete;

    public int EvidenceCount =>
        Evidence.Count;
}

// Pure projection of a resolved material file inventory's texture
// references into aggregate consumer-spelling evidence.
public static class
    SkyrimWinningMaterialTextureAggregateConsumerSpellingEvidenceProjector
{
    public static
        SkyrimWinningMaterialTextureAggregateConsumerSpellingEvidenceProjectionResult
        Project(
            SkyrimWinningMaterialTextureInventoryResult inventory)
    {
        ArgumentNullException.ThrowIfNull(
            inventory
        );

        if (inventory.References is null)
        {
            throw new ArgumentException(
                "The material texture inventory must retain its " +
                "reference collection.",
                nameof(inventory)
            );
        }

        if (!inventory.SearchComplete)
        {
            return new
                SkyrimWinningMaterialTextureAggregateConsumerSpellingEvidenceProjectionResult(
                    Inventory:
                        inventory,
                    State:
                        SkyrimWinningMaterialTextureAggregateConsumerSpellingEvidenceProjectionState
                            .IncompleteWinnerSearch,
                    Evidence:
                        Array.Empty<
                            DataRelativePathAggregateConsumerSpellingEvidence
                        >(),
                    Error:
                        null
                );
        }

        var requestedPathsByLogicalPath =
            new Dictionary<string, List<string>>(
                StringComparer.Ordinal
            );

        foreach (
            SkyrimMaterialFileTextureReference? reference
            in inventory.References)
        {
            if (reference is null)
            {
                return Indeterminate(
                    inventory,
                    "The material texture inventory contains a null " +
                    "reference."
                );
            }

            string consumerRequestedPath =
                reference.GivenPath;

            if (!WindowsDataRelativePathParser.TryParse(
                    consumerRequestedPath,
                    out string[] components,
                    out string? parseError))
            {
                return Indeterminate(
                    inventory,
                    $"Material '{reference.MaterialPhysicalPath}' " +
                    $"[{reference.SlotName}] contains invalid texture " +
                    $"path '{consumerRequestedPath}': {parseError}"
                );
            }

            string normalizedRequestedPath =
                string.Join(
                    "/",
                    components
                );

            string windowsLogicalPath;

            try
            {
                windowsLogicalPath =
                    WindowsLogicalPath
                        .FromRelativePath(
                            normalizedRequestedPath
                        )
                        .Value;
            }
            catch (Exception ex)
            {
                return Indeterminate(
                    inventory,
                    $"Material '{reference.MaterialPhysicalPath}' " +
                    $"[{reference.SlotName}] texture path " +
                    $"'{consumerRequestedPath}' cannot be mapped to the " +
                    $"Windows namespace: {ex.Message}"
                );
            }

            if (!requestedPathsByLogicalPath.TryGetValue(
                    windowsLogicalPath,
                    out List<string>? requestedPaths))
            {
                requestedPaths =
                    new List<string>();

                requestedPathsByLogicalPath.Add(
                    windowsLogicalPath,
                    requestedPaths
                );
            }

            requestedPaths.Add(
                consumerRequestedPath
            );
        }

        DataRelativePathAggregateConsumerSpellingEvidence[] evidence =
            requestedPathsByLogicalPath
                .OrderBy(
                    pair =>
                        pair.Key,
                    StringComparer.Ordinal
                )
                .Select(
                    pair =>
                        DataRelativePathAggregateConsumerSpellingClassifier
                            .Classify(
                                pair.Key,
                                pair.Value.ToArray()
                            )
                )
                .ToArray();

        return new
            SkyrimWinningMaterialTextureAggregateConsumerSpellingEvidenceProjectionResult(
                Inventory:
                    inventory,
                State:
                    SkyrimWinningMaterialTextureAggregateConsumerSpellingEvidenceProjectionState
                        .Complete,
                Evidence:
                    evidence,
                Error:
                    null
            );
    }

    private static
        SkyrimWinningMaterialTextureAggregateConsumerSpellingEvidenceProjectionResult
        Indeterminate(
            SkyrimWinningMaterialTextureInventoryResult inventory,
            string error)
    {
        return new
            SkyrimWinningMaterialTextureAggregateConsumerSpellingEvidenceProjectionResult(
                Inventory:
                    inventory,
                State:
                    SkyrimWinningMaterialTextureAggregateConsumerSpellingEvidenceProjectionState
                        .IndeterminateConsumerPathEvidence,
                Evidence:
                    Array.Empty<
                        DataRelativePathAggregateConsumerSpellingEvidence
                    >(),
                Error:
                    error
            );
    }
}
