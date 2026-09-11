namespace CaseCompat.Bethesda.Assets;

// Cross-pass cache for material (.bgsm/.bgem) texture-reference
// extraction - mirrors SkyrimMeshTextureExtractionCache exactly, see
// SkyrimAssetFileIdentity for why keying by device+inode is safe.
public sealed class SkyrimMaterialTextureExtractionCache
{
    private readonly Dictionary<
        SkyrimAssetFileIdentity,
        IReadOnlyList<SkyrimMaterialFileTextureReference>
    > _byIdentity =
        new();

    public bool TryGet(
        SkyrimAssetFileIdentity identity,
        string currentPhysicalPath,
        out IReadOnlyList<SkyrimMaterialFileTextureReference> references)
    {
        if (!_byIdentity.TryGetValue(
                identity,
                out IReadOnlyList<SkyrimMaterialFileTextureReference>?
                    cached))
        {
            references =
                Array.Empty<SkyrimMaterialFileTextureReference>();

            return false;
        }

        references =
            cached
                .Select(
                    reference =>
                        reference with
                        {
                            MaterialPhysicalPath =
                                currentPhysicalPath
                        }
                )
                .ToArray();

        return true;
    }

    public void Store(
        SkyrimAssetFileIdentity identity,
        IReadOnlyList<SkyrimMaterialFileTextureReference> references)
    {
        _byIdentity[identity] =
            references;
    }
}
