namespace CaseCompat.Bethesda.Assets;

public sealed record SkyrimMeshReadError(
    string MeshPhysicalPath,
    string Error
);

// "SearchComplete" here means every real mesh path could at least be
// opened and read as bytes - it does not mean every mesh's own internal
// format was understood. A mesh NiflySharp cannot parse (corrupted,
// unusually old/new, or otherwise incompatible) yields zero texture
// references for that one mesh rather than an inventory-wide failure,
// the same "one bad leaf doesn't abort everything" philosophy applied
// elsewhere in this project - only a genuine I/O failure (permissions, a
// file removed between discovery and this read) counts as incomplete.
public sealed record SkyrimWinningMeshTextureInventoryResult(
    string DataRoot,
    IReadOnlyList<string> MeshPhysicalPaths,
    IReadOnlyList<SkyrimMeshReadError> ReadErrors,
    IReadOnlyList<SkyrimNifTextureReference> References
)
{
    public bool SearchComplete =>
        ReadErrors.Count == 0;

    public int MeshCount =>
        MeshPhysicalPaths.Count;

    public int ReferenceCount =>
        References.Count;
}

// Opens each distinct real mesh path exactly once (regardless of how
// many consumers reference it) and extracts its texture/material
// references via SkyrimNifTextureReferenceExtractor.
public static class SkyrimWinningMeshTextureInventory
{
    public static SkyrimWinningMeshTextureInventoryResult Inspect(
        string dataRoot,
        IReadOnlyList<string> meshPhysicalPaths,
        SkyrimMeshTextureExtractionCache? cache = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            dataRoot
        );

        ArgumentNullException.ThrowIfNull(
            meshPhysicalPaths
        );

        string fullDataRoot =
            Path.GetFullPath(
                dataRoot
            );

        string[] distinctPaths =
            meshPhysicalPaths
                .Distinct(
                    StringComparer.Ordinal
                )
                .ToArray();

        var references =
            new List<SkyrimNifTextureReference>();

        var readErrors =
            new List<SkyrimMeshReadError>();

        foreach (string meshPhysicalPath in distinctPaths)
        {
            FileStream stream;

            try
            {
                stream =
                    File.OpenRead(
                        meshPhysicalPath
                    );
            }
            catch (Exception ex) when (
                ex is IOException or
                UnauthorizedAccessException or
                System.Security.SecurityException)
            {
                readErrors.Add(
                    new SkyrimMeshReadError(
                        MeshPhysicalPath:
                            meshPhysicalPath,
                        Error:
                            ex.Message
                    )
                );

                continue;
            }

            bool haveIdentity =
                SkyrimAssetFileIdentity.TryInspect(
                    meshPhysicalPath,
                    out SkyrimAssetFileIdentity identity
                );

            if (
                cache is not null &&
                haveIdentity &&
                cache.TryGet(
                    identity,
                    meshPhysicalPath,
                    out IReadOnlyList<SkyrimNifTextureReference> cached))
            {
                stream.Dispose();

                references.AddRange(
                    cached
                );

                continue;
            }

            using (stream)
            {
                // NiflySharp is a third-party parser given untrusted,
                // arbitrarily malformed binary content from installed
                // mods - a mesh it cannot handle (including its own
                // internal null-reference bugs on unusual data) yields
                // zero texture references for that one mesh, not an
                // inventory-wide failure. Confirmed necessary via a real
                // install: one real .nif crashed NifFile.Load with a
                // NullReferenceException deep inside NiflySharp itself.
                try
                {
                    IReadOnlyList<SkyrimNifTextureReference> extracted =
                        SkyrimNifTextureReferenceExtractor.Extract(
                            stream,
                            meshPhysicalPath
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

        return new SkyrimWinningMeshTextureInventoryResult(
            DataRoot:
                fullDataRoot,
            MeshPhysicalPaths:
                distinctPaths,
            ReadErrors:
                readErrors.ToArray(),
            References:
                references.ToArray()
        );
    }
}
