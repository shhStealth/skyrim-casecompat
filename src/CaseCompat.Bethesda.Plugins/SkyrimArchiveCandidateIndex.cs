using CaseCompat.Core.Analysis;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Archives;
using Noggog;
using System.IO.Abstractions;

namespace CaseCompat.Bethesda.Plugins;

public sealed record SkyrimArchiveAssetProvider(
    string ArchiveName,
    string ArchivePath,
    string InternalPath,
    uint Size
);

public sealed record SkyrimArchiveReadError(
    string ArchiveName,
    string ArchivePath,
    string Error
);

public sealed record SkyrimArchiveCandidateIndexResult(
    string DataRoot,
    int ArchivesDiscovered,
    int ArchivesRead,
    long TotalFileEntries,
    long DuplicateLogicalEntriesWithinArchive,
    IReadOnlyList<SkyrimArchiveReadError> ReadErrors,
    IReadOnlyDictionary<
        WindowsLogicalPath,
        IReadOnlyList<SkyrimArchiveAssetProvider>
    > Assets
)
{
    public bool SearchComplete =>
        ReadErrors.Count == 0 &&
        ArchivesRead == ArchivesDiscovered;

    public int UniqueLogicalAssetCount =>
        Assets.Count;

    public int MultiProviderAssetCount =>
        Assets.Count(pair =>
            pair.Value
                .Select(provider =>
                    provider.ArchivePath
                )
                .Distinct(
                    StringComparer.Ordinal
                )
                .Skip(1)
                .Any()
        );

    public int MaximumProviderCount =>
        Assets.Count == 0
            ? 0
            : Assets.Max(pair =>
                pair.Value.Count
            );

    public bool TryGetProviders(
        string requestedPath,
        out IReadOnlyList<SkyrimArchiveAssetProvider> providers)
    {
        WindowsLogicalPath logicalPath =
            WindowsLogicalPath.FromRelativePath(
                requestedPath
            );

        if (Assets.TryGetValue(
                logicalPath,
                out IReadOnlyList<SkyrimArchiveAssetProvider>?
                    found))
        {
            providers = found;
            return true;
        }

        providers =
            Array.Empty<SkyrimArchiveAssetProvider>();

        return false;
    }
}

public static class SkyrimArchiveCandidateIndex
{
    public static SkyrimArchiveCandidateIndexResult Inspect(
        string dataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            dataRoot
        );

        string fullDataRoot =
            Path.GetFullPath(
                dataRoot
            );

        if (!Directory.Exists(fullDataRoot))
        {
            throw new DirectoryNotFoundException(
                fullDataRoot
            );
        }

        string[] archivePaths =
            Directory
                .EnumerateFiles(
                    fullDataRoot,
                    "*",
                    SearchOption.TopDirectoryOnly
                )
                .Where(path =>
                    string.Equals(
                        Path.GetExtension(path),
                        ".bsa",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                .OrderBy(
                    path => path,
                    StringComparer.OrdinalIgnoreCase
                )
                .ThenBy(
                    path => path,
                    StringComparer.Ordinal
                )
                .ToArray();

        var fileSystem =
            new FileSystem();

        var assets =
            new Dictionary<
                WindowsLogicalPath,
                List<SkyrimArchiveAssetProvider>
            >();

        var readErrors =
            new List<SkyrimArchiveReadError>();

        int archivesRead = 0;

        long totalFileEntries = 0;

        long duplicateLogicalEntriesWithinArchive =
            0;

        foreach (
            string archivePath
            in archivePaths)
        {
            string archiveName =
                Path.GetFileName(
                    archivePath
                );

            try
            {
                IArchiveReader reader =
                    Archive.CreateReader(
                        GameRelease.SkyrimSE,
                        new FilePath(archivePath),
                        fileSystem
                    );

                var pendingProviders =
                    new List<(
                        WindowsLogicalPath LogicalPath,
                        SkyrimArchiveAssetProvider Provider
                    )>();

                var seenLogicalPaths =
                    new HashSet<WindowsLogicalPath>();

                long archiveDuplicates = 0;

                foreach (
                    IArchiveFile file
                    in reader.Files)
                {
                    WindowsLogicalPath logicalPath =
                        WindowsLogicalPath
                            .FromRelativePath(
                                file.Path
                            );

                    if (!seenLogicalPaths.Add(
                            logicalPath))
                    {
                        archiveDuplicates++;
                    }

                    pendingProviders.Add(
                        (
                            LogicalPath:
                                logicalPath,
                            Provider:
                                new SkyrimArchiveAssetProvider(
                                    ArchiveName:
                                        archiveName,
                                    ArchivePath:
                                        archivePath,
                                    InternalPath:
                                        file.Path,
                                    Size:
                                        file.Size
                                )
                        )
                    );
                }

                foreach (
                    var pending
                    in pendingProviders)
                {
                    if (!assets.TryGetValue(
                            pending.LogicalPath,
                            out List<SkyrimArchiveAssetProvider>?
                                providers))
                    {
                        providers =
                            new List<SkyrimArchiveAssetProvider>();

                        assets.Add(
                            pending.LogicalPath,
                            providers
                        );
                    }

                    providers.Add(
                        pending.Provider
                    );
                }

                archivesRead++;

                totalFileEntries +=
                    pendingProviders.Count;

                duplicateLogicalEntriesWithinArchive +=
                    archiveDuplicates;
            }
            catch (Exception ex)
            {
                readErrors.Add(
                    new SkyrimArchiveReadError(
                        ArchiveName:
                            archiveName,
                        ArchivePath:
                            archivePath,
                        Error:
                            ex.Message
                    )
                );
            }
        }

        IReadOnlyDictionary<
            WindowsLogicalPath,
            IReadOnlyList<SkyrimArchiveAssetProvider>
        > frozenAssets =
            assets.ToDictionary(
                pair =>
                    pair.Key,
                pair =>
                    (IReadOnlyList<SkyrimArchiveAssetProvider>)
                        pair.Value.ToArray()
            );

        return new SkyrimArchiveCandidateIndexResult(
            DataRoot:
                fullDataRoot,
            ArchivesDiscovered:
                archivePaths.Length,
            ArchivesRead:
                archivesRead,
            TotalFileEntries:
                totalFileEntries,
            DuplicateLogicalEntriesWithinArchive:
                duplicateLogicalEntriesWithinArchive,
            ReadErrors:
                readErrors.ToArray(),
            Assets:
                frozenAssets
        );
    }
}
