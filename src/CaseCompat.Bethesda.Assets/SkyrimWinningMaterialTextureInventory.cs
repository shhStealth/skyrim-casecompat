namespace CaseCompat.Bethesda.Assets;

public sealed record SkyrimMaterialReadError(
    string MaterialPhysicalPath,
    string Error
);

// "SearchComplete" here means every real material path could at least be
// opened and read as bytes - it does not mean every material's own
// internal format was understood. A material file this project's reader
// cannot parse (corrupted, or an unrecognized signature) yields zero
// texture references for that one file rather than an inventory-wide
// failure, the same "one bad leaf doesn't abort everything" philosophy
// applied elsewhere in this project - only a genuine I/O failure
// (permissions, a file removed between discovery and this read) counts as
// incomplete.
public sealed record SkyrimWinningMaterialTextureInventoryResult(
    string DataRoot,
    IReadOnlyList<string> MaterialPhysicalPaths,
    IReadOnlyList<SkyrimMaterialReadError> ReadErrors,
    IReadOnlyList<SkyrimMaterialFileTextureReference> References
)
{
    public bool SearchComplete =>
        ReadErrors.Count == 0;

    public int MaterialCount =>
        MaterialPhysicalPaths.Count;

    public int ReferenceCount =>
        References.Count;
}

// Opens each distinct real material path exactly once (regardless of how
// many meshes reference it) and extracts its texture references via
// SkyrimMaterialFileTextureReferenceExtractor.
public static class SkyrimWinningMaterialTextureInventory
{
    public static SkyrimWinningMaterialTextureInventoryResult Inspect(
        string dataRoot,
        IReadOnlyList<string> materialPhysicalPaths,
        SkyrimMaterialTextureExtractionCache? cache = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            dataRoot
        );

        ArgumentNullException.ThrowIfNull(
            materialPhysicalPaths
        );

        string fullDataRoot =
            Path.GetFullPath(
                dataRoot
            );

        string[] distinctPaths =
            materialPhysicalPaths
                .Distinct(
                    StringComparer.Ordinal
                )
                .ToArray();

        var references =
            new List<SkyrimMaterialFileTextureReference>();

        var readErrors =
            new List<SkyrimMaterialReadError>();

        foreach (string materialPhysicalPath in distinctPaths)
        {
            FileStream stream;

            try
            {
                stream =
                    File.OpenRead(
                        materialPhysicalPath
                    );
            }
            catch (Exception ex) when (
                ex is IOException or
                UnauthorizedAccessException or
                System.Security.SecurityException)
            {
                readErrors.Add(
                    new SkyrimMaterialReadError(
                        MaterialPhysicalPath:
                            materialPhysicalPath,
                        Error:
                            ex.Message
                    )
                );

                continue;
            }

            bool haveIdentity =
                SkyrimAssetFileIdentity.TryInspect(
                    materialPhysicalPath,
                    out SkyrimAssetFileIdentity identity
                );

            if (
                cache is not null &&
                haveIdentity &&
                cache.TryGet(
                    identity,
                    materialPhysicalPath,
                    out IReadOnlyList<SkyrimMaterialFileTextureReference>
                        cached))
            {
                stream.Dispose();

                references.AddRange(
                    cached
                );

                continue;
            }

            using (stream)
            {
                // A material file this project's hand-written reader
                // cannot parse (corrupted, or a field layout this
                // reader's version-gating did not anticipate) yields
                // zero texture references for that one file, not an
                // inventory-wide failure - the same "one bad leaf"
                // philosophy applied to mesh parsing in
                // SkyrimWinningMeshTextureInventory.
                try
                {
                    IReadOnlyList<SkyrimMaterialFileTextureReference>
                        extracted =
                            SkyrimMaterialFileTextureReferenceExtractor
                                .Extract(
                                    stream,
                                    materialPhysicalPath
                                );

                    references.AddRange(
                        extracted
                    );

                    if (
                        cache is not null &&
                        haveIdentity)
                    {
                        cache.Store(
                            identity,
                            extracted
                        );
                    }
                }
                catch (Exception)
                {
                    continue;
                }
            }
        }

        return new SkyrimWinningMaterialTextureInventoryResult(
            DataRoot:
                fullDataRoot,
            MaterialPhysicalPaths:
                distinctPaths,
            ReadErrors:
                readErrors.ToArray(),
            References:
                references.ToArray()
        );
    }
}
