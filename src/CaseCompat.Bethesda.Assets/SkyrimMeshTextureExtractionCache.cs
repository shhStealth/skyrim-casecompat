namespace CaseCompat.Bethesda.Assets;

// Cross-pass cache for mesh texture-reference extraction - see
// SkyrimAssetFileIdentity for why keying by device+inode rather than
// path is safe across a wizard convergence loop's repeated rescans.
// GivenPath/SlotOrFieldName/Kind are intrinsic to file content and are
// reused as-is on a hit; MeshPhysicalPath is re-stamped to the current
// pass's path so diagnostics never show a stale path from an earlier
// pass.
public sealed class SkyrimMeshTextureExtractionCache
{
    private readonly Dictionary<
        SkyrimAssetFileIdentity,
        IReadOnlyList<SkyrimNifTextureReference>
    > _byIdentity =
        new();

    public bool TryGet(
        SkyrimAssetFileIdentity identity,
        string currentPhysicalPath,
        out IReadOnlyList<SkyrimNifTextureReference> references)
    {
        if (!_byIdentity.TryGetValue(
                identity,
                out IReadOnlyList<SkyrimNifTextureReference>? cached))
        {
            references =
                Array.Empty<SkyrimNifTextureReference>();

            return false;
        }

        references =
            cached
                .Select(
                    reference =>
                        reference with
                        {
                            MeshPhysicalPath =
                                currentPhysicalPath
                        }
                )
                .ToArray();

        return true;
    }

    public void Store(
        SkyrimAssetFileIdentity identity,
        IReadOnlyList<SkyrimNifTextureReference> references)
    {
        _byIdentity[identity] =
            references;
    }
}
