using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Analysis;
using CaseCompat.Core.Repair;

namespace CaseCompat.Bethesda.Assets;

// Authority state for projecting genuine external-material-file requests
// (BSLightingShaderProperty.RootMaterialName) embedded in mesh shader
// properties into the generic aggregate consumer-spelling model. Shares
// the same underlying SkyrimWinningMeshTextureInventoryResult as
// SkyrimWinningMeshTextureAggregateConsumerSpellingEvidenceProjector -
// this is a different filter over the same scan, not a separate scan.
public enum
    SkyrimWinningMeshMaterialPathAggregateConsumerSpellingEvidenceProjectionState
{
    Complete,
    IncompleteWinnerSearch,
    IndeterminateConsumerPathEvidence
}

public sealed record
    SkyrimWinningMeshMaterialPathAggregateConsumerSpellingEvidenceProjectionResult(
        SkyrimWinningMeshTextureInventoryResult Inventory,
        SkyrimWinningMeshMaterialPathAggregateConsumerSpellingEvidenceProjectionState
            State,
        IReadOnlyList<DataRelativePathAggregateConsumerSpellingEvidence>
            Evidence,
        string? Error
    ) : ISkyrimWinningConsumerSpellingEvidenceSource
{
    public string SourceName =>
        "MeshMaterialPath";

    public string DataRoot =>
        Inventory.DataRoot;

    public bool WinnerSearchComplete =>
        Inventory.SearchComplete;

    public bool ConsumerPathEvidenceComplete =>
        State ==
        SkyrimWinningMeshMaterialPathAggregateConsumerSpellingEvidenceProjectionState
            .Complete;

    public int EvidenceCount =>
        Evidence.Count;
}

// Pure projection of Kind == MaterialFile references into aggregate
// consumer-spelling evidence. An external .bgsm/.bgem material path is
// itself a consumer request that must be resolved to a real, correctly-
// cased file before that material's own texture slots can be read - see
// SkyrimMaterialFileTextureReferenceExtractor, which runs against this
// projection's resolved candidates in a second round, not against raw
// evidence here.
public static class
    SkyrimWinningMeshMaterialPathAggregateConsumerSpellingEvidenceProjector
{
    public static
        SkyrimWinningMeshMaterialPathAggregateConsumerSpellingEvidenceProjectionResult
        Project(
            SkyrimWinningMeshTextureInventoryResult inventory)
    {
        ArgumentNullException.ThrowIfNull(
            inventory
        );

        if (inventory.References is null)
        {
            throw new ArgumentException(
                "The mesh texture inventory must retain its reference " +
                "collection.",
                nameof(inventory)
            );
        }

        if (!inventory.SearchComplete)
        {
            return new
                SkyrimWinningMeshMaterialPathAggregateConsumerSpellingEvidenceProjectionResult(
                    Inventory:
                        inventory,
                    State:
                        SkyrimWinningMeshMaterialPathAggregateConsumerSpellingEvidenceProjectionState
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
            SkyrimNifTextureReference? reference
            in inventory.References)
        {
            if (reference is null)
            {
                return Indeterminate(
                    inventory,
                    "The mesh texture inventory contains a null reference."
                );
            }

            if (
                reference.Kind !=
                SkyrimNifTextureReferenceKind.MaterialFile)
            {
                continue;
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
                    $"Mesh '{reference.MeshPhysicalPath}' " +
                    $"[{reference.SlotOrFieldName}] contains invalid " +
                    $"material path '{consumerRequestedPath}': {parseError}"
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
                    $"Mesh '{reference.MeshPhysicalPath}' " +
                    $"[{reference.SlotOrFieldName}] material path " +
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
            SkyrimWinningMeshMaterialPathAggregateConsumerSpellingEvidenceProjectionResult(
                Inventory:
                    inventory,
                State:
                    SkyrimWinningMeshMaterialPathAggregateConsumerSpellingEvidenceProjectionState
                        .Complete,
                Evidence:
                    evidence,
                Error:
                    null
            );
    }

    private static
        SkyrimWinningMeshMaterialPathAggregateConsumerSpellingEvidenceProjectionResult
        Indeterminate(
            SkyrimWinningMeshTextureInventoryResult inventory,
            string error)
    {
        return new
            SkyrimWinningMeshMaterialPathAggregateConsumerSpellingEvidenceProjectionResult(
                Inventory:
                    inventory,
                State:
                    SkyrimWinningMeshMaterialPathAggregateConsumerSpellingEvidenceProjectionState
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
