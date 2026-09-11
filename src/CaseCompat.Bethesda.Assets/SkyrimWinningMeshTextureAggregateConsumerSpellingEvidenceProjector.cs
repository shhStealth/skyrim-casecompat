using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Analysis;
using CaseCompat.Core.Repair;

namespace CaseCompat.Bethesda.Assets;

// Authority state for projecting genuine texture-slot requests embedded
// in mesh shader properties into the generic aggregate consumer-spelling
// model. Mirrors the Bethesda record-type projectors' precedence
// exactly, even though there is no plugin winning-record concept here -
// "winner search" completeness means every mesh could be opened and read
// (see SkyrimWinningMeshTextureInventoryResult.SearchComplete).
public enum
    SkyrimWinningMeshTextureAggregateConsumerSpellingEvidenceProjectionState
{
    Complete,
    IncompleteWinnerSearch,
    IndeterminateConsumerPathEvidence
}

public sealed record
    SkyrimWinningMeshTextureAggregateConsumerSpellingEvidenceProjectionResult(
        SkyrimWinningMeshTextureInventoryResult Inventory,
        SkyrimWinningMeshTextureAggregateConsumerSpellingEvidenceProjectionState
            State,
        IReadOnlyList<DataRelativePathAggregateConsumerSpellingEvidence>
            Evidence,
        string? Error
    ) : ISkyrimWinningConsumerSpellingEvidenceSource
{
    public string SourceName =>
        "MeshTexture";

    public string DataRoot =>
        Inventory.DataRoot;

    public bool WinnerSearchComplete =>
        Inventory.SearchComplete;

    public bool ConsumerPathEvidenceComplete =>
        State ==
        SkyrimWinningMeshTextureAggregateConsumerSpellingEvidenceProjectionState
            .Complete;

    public int EvidenceCount =>
        Evidence.Count;
}

// Pure projection of Kind == Texture references into aggregate
// consumer-spelling evidence. Kind == MaterialFile references are
// deliberately excluded here - see
// SkyrimWinningMeshMaterialPathAggregateConsumerSpellingEvidenceProjector,
// which projects those into their own separate evidence set, since an
// external material path is itself a consumer request that must be
// resolved to a real .bgsm/.bgem file before its own texture slots can
// be read.
public static class
    SkyrimWinningMeshTextureAggregateConsumerSpellingEvidenceProjector
{
    public static
        SkyrimWinningMeshTextureAggregateConsumerSpellingEvidenceProjectionResult
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
                SkyrimWinningMeshTextureAggregateConsumerSpellingEvidenceProjectionResult(
                    Inventory:
                        inventory,
                    State:
                        SkyrimWinningMeshTextureAggregateConsumerSpellingEvidenceProjectionState
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
                SkyrimNifTextureReferenceKind.Texture)
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
                    $"texture path '{consumerRequestedPath}': {parseError}"
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
                    $"[{reference.SlotOrFieldName}] texture path " +
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
            SkyrimWinningMeshTextureAggregateConsumerSpellingEvidenceProjectionResult(
                Inventory:
                    inventory,
                State:
                    SkyrimWinningMeshTextureAggregateConsumerSpellingEvidenceProjectionState
                        .Complete,
                Evidence:
                    evidence,
                Error:
                    null
            );
    }

    private static
        SkyrimWinningMeshTextureAggregateConsumerSpellingEvidenceProjectionResult
        Indeterminate(
            SkyrimWinningMeshTextureInventoryResult inventory,
            string error)
    {
        return new
            SkyrimWinningMeshTextureAggregateConsumerSpellingEvidenceProjectionResult(
                Inventory:
                    inventory,
                State:
                    SkyrimWinningMeshTextureAggregateConsumerSpellingEvidenceProjectionState
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
