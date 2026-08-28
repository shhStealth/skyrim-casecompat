using CaseCompat.Core.LoadOrder;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Archives;
using Mutagen.Bethesda.Plugins;
using Noggog;
using System.IO.Abstractions;

namespace CaseCompat.Bethesda.Plugins;

public sealed record SkyrimRuntimeArchivePluginAssociation(
    string PluginName,
    int LoadOrderIndex
);

public sealed record SkyrimRuntimeArchiveIniListing(
    string IniName,
    string IniPath,
    string IniKey,
    int IndexWithinKey,
    int ListingIndex
);

public sealed record SkyrimRuntimeArchiveEvidenceEntry(
    string ArchiveName,
    string ArchivePath,
    IReadOnlyList<SkyrimRuntimeArchivePluginAssociation> PluginAssociations,
    IReadOnlyList<SkyrimRuntimeArchiveIniListing> IniListings
)
{
    public bool HasPluginAssociation =>
        PluginAssociations.Count > 0;

    public bool IsIniListed =>
        IniListings.Count > 0;

    public bool HasRuntimeEvidence =>
        HasPluginAssociation ||
        IsIniListed;
}

public sealed record SkyrimRuntimeArchiveMissingIniArchive(
    string ArchiveName,
    IReadOnlyList<SkyrimRuntimeArchiveIniListing> IniListings
);

public sealed record SkyrimRuntimeArchiveAssociationError(
    string PluginName,
    int LoadOrderIndex,
    string ArchiveName,
    string Error
);

public sealed record SkyrimRuntimeArchiveIniReadError(
    string IniName,
    string IniPath,
    string Error
);

public sealed record SkyrimRuntimeArchiveIniProvenanceError(
    string IniName,
    string IniPath,
    string Error
);

public sealed record SkyrimRuntimeArchiveEvidenceResult(
    string DataRoot,
    string IniDirectory,
    IReadOnlyList<SkyrimRuntimeArchiveEvidenceEntry> Archives,
    IReadOnlyList<SkyrimRuntimeArchiveMissingIniArchive> MissingIniArchives,
    IReadOnlyList<SkyrimRuntimeArchiveAssociationError> AssociationErrors,
    IReadOnlyList<SkyrimRuntimeArchiveIniReadError> IniReadErrors,
    IReadOnlyList<SkyrimRuntimeArchiveIniProvenanceError> IniProvenanceErrors
)
{
    public int PhysicalArchiveCount =>
        Archives.Count;

    public int PluginAssociatedArchiveCount =>
        Archives.Count(archive =>
            archive.HasPluginAssociation
        );

    public int IniListedPhysicalArchiveCount =>
        Archives.Count(archive =>
            archive.IsIniListed
        );

    public int RuntimeEvidencedArchiveCount =>
        Archives.Count(archive =>
            archive.HasRuntimeEvidence
        );

    public int NoRuntimeEvidenceArchiveCount =>
        PhysicalArchiveCount -
        RuntimeEvidencedArchiveCount;

    public int MultiPluginAssociationArchiveCount =>
        Archives.Count(archive =>
            archive.PluginAssociations.Count > 1
        );

    public int MaximumPluginAssociationsPerArchive =>
        Archives.Count == 0
            ? 0
            : Archives.Max(archive =>
                archive.PluginAssociations.Count
            );

    public bool SearchComplete =>
        AssociationErrors.Count == 0 &&
        IniReadErrors.Count == 0 &&
        IniProvenanceErrors.Count == 0;

    public bool TryGetEvidence(
        string archiveName,
        out SkyrimRuntimeArchiveEvidenceEntry? evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            archiveName
        );

        evidence =
            Archives.FirstOrDefault(archive =>
                string.Equals(
                    archive.ArchiveName,
                    archiveName,
                    StringComparison.OrdinalIgnoreCase
                )
            );

        return evidence is not null;
    }
}

public static class SkyrimRuntimeArchiveEvidence
{
    private static readonly string[] IniArchiveKeys =
    {
        "sResourceArchiveList",
        "sResourceArchiveList2"
    };

    private sealed record ParsedIniArchiveListing(
        string ArchiveName,
        string IniKey,
        int IndexWithinKey,
        int ListingIndex
    );

    private static IReadOnlyList<ParsedIniArchiveListing>
        ParseIniArchiveListings(
            string iniPath)
    {
        var firstValues =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase
            );

        bool inArchiveSection =
            false;

        foreach (
            string rawLine
            in File.ReadLines(iniPath))
        {
            string line =
                rawLine
                    .Trim()
                    .TrimStart('\uFEFF')
                    .Trim();

            if (line.Length == 0 ||
                line.StartsWith(
                    ";",
                    StringComparison.Ordinal) ||
                line.StartsWith(
                    "#",
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith(
                    "[",
                    StringComparison.Ordinal) &&
                line.EndsWith(
                    "]",
                    StringComparison.Ordinal))
            {
                string section =
                    line[1..^1].Trim();

                inArchiveSection =
                    string.Equals(
                        section,
                        "Archive",
                        StringComparison.OrdinalIgnoreCase
                    );

                continue;
            }

            if (!inArchiveSection)
            {
                continue;
            }

            int equalsIndex =
                line.IndexOf('=');

            if (equalsIndex < 0)
            {
                continue;
            }

            string key =
                line[..equalsIndex].Trim();

            string? canonicalKey =
                IniArchiveKeys.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate,
                        key,
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            if (canonicalKey is null ||
                firstValues.ContainsKey(
                    canonicalKey
                ))
            {
                continue;
            }

            firstValues.Add(
                canonicalKey,
                line[(equalsIndex + 1)..].Trim()
            );
        }

        var result =
            new List<ParsedIniArchiveListing>();

        int listingIndex =
            0;

        foreach (
            string key
            in IniArchiveKeys)
        {
            if (!firstValues.TryGetValue(
                    key,
                    out string? value))
            {
                continue;
            }

            string[] names =
                value.Split(
                    ',',
                    StringSplitOptions.None
                );

            int indexWithinKey =
                0;

            foreach (string rawName in names)
            {
                string archiveName =
                    rawName.Trim();

                if (archiveName.Length == 0)
                {
                    continue;
                }

                result.Add(
                    new ParsedIniArchiveListing(
                        ArchiveName:
                            archiveName,
                        IniKey:
                            key,
                        IndexWithinKey:
                            indexWithinKey,
                        ListingIndex:
                            listingIndex
                    )
                );

                indexWithinKey++;
                listingIndex++;
            }
        }

        return result;
    }

    private sealed class ArchiveBuilder
    {
        public ArchiveBuilder(
            string archiveName,
            string archivePath)
        {
            ArchiveName =
                archiveName;

            ArchivePath =
                archivePath;
        }

        public string ArchiveName { get; }

        public string ArchivePath { get; }

        public List<SkyrimRuntimeArchivePluginAssociation>
            PluginAssociations { get; } = new();

        public List<SkyrimRuntimeArchiveIniListing>
            IniListings { get; } = new();
    }

    public static SkyrimRuntimeArchiveEvidenceResult Inspect(
        string dataRoot,
        SkyrimRuntimePluginSet runtimePluginSet,
        string iniDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            dataRoot
        );

        ArgumentNullException.ThrowIfNull(
            runtimePluginSet
        );

        ArgumentException.ThrowIfNullOrWhiteSpace(
            iniDirectory
        );

        if (!runtimePluginSet.IsConsistent)
        {
            throw new ArgumentException(
                "Runtime plugin set must be consistent.",
                nameof(runtimePluginSet)
            );
        }

        string fullDataRoot =
            Path.GetFullPath(
                dataRoot
            );

        string fullIniDirectory =
            Path.GetFullPath(
                iniDirectory
            );

        if (!Directory.Exists(fullDataRoot))
        {
            throw new DirectoryNotFoundException(
                fullDataRoot
            );
        }

        if (!Directory.Exists(fullIniDirectory))
        {
            throw new DirectoryNotFoundException(
                fullIniDirectory
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

        var builders =
            archivePaths.ToDictionary(
                path =>
                    Path.GetFileName(path),
                path =>
                    new ArchiveBuilder(
                        archiveName:
                            Path.GetFileName(path),
                        archivePath:
                            path
                    ),
                StringComparer.OrdinalIgnoreCase
            );

        var associationErrors =
            new List<SkyrimRuntimeArchiveAssociationError>();

        foreach (
            SkyrimRuntimePluginSetEntry plugin
            in runtimePluginSet.OrderedRuntimeActiveEntries)
        {
            ModKey modKey;

            try
            {
                modKey =
                    ModKey.FromNameAndExtension(
                        plugin.PluginName
                    );
            }
            catch (Exception ex)
            {
                associationErrors.Add(
                    new SkyrimRuntimeArchiveAssociationError(
                        PluginName:
                            plugin.PluginName,
                        LoadOrderIndex:
                            plugin.LoadOrderIndex,
                        ArchiveName:
                            "(ModKey)",
                        Error:
                            ex.Message
                    )
                );

                continue;
            }

            foreach (
                ArchiveBuilder archive
                in builders.Values)
            {
                try
                {
                    bool applicable =
                        Archive.IsApplicable(
                            GameRelease.SkyrimSE,
                            modKey,
                            new FileName(
                                archive.ArchiveName
                            )
                        );

                    if (!applicable)
                    {
                        continue;
                    }

                    archive.PluginAssociations.Add(
                        new SkyrimRuntimeArchivePluginAssociation(
                            PluginName:
                                plugin.PluginName,
                            LoadOrderIndex:
                                plugin.LoadOrderIndex
                        )
                    );
                }
                catch (Exception ex)
                {
                    associationErrors.Add(
                        new SkyrimRuntimeArchiveAssociationError(
                            PluginName:
                                plugin.PluginName,
                            LoadOrderIndex:
                                plugin.LoadOrderIndex,
                            ArchiveName:
                                archive.ArchiveName,
                            Error:
                                ex.Message
                        )
                    );
                }
            }
        }

        var fileSystem =
            new FileSystem();

        var iniReadErrors =
            new List<SkyrimRuntimeArchiveIniReadError>();

        var iniProvenanceErrors =
            new List<SkyrimRuntimeArchiveIniProvenanceError>();

        var missingIniListings =
            new Dictionary<
                string,
                List<SkyrimRuntimeArchiveIniListing>
            >(
                StringComparer.OrdinalIgnoreCase
            );

        string[] iniPaths =
            Directory
                .EnumerateFiles(
                    fullIniDirectory,
                    "*",
                    SearchOption.TopDirectoryOnly
                )
                .Where(path =>
                    string.Equals(
                        Path.GetExtension(path),
                        ".ini",
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

        foreach (string iniPath in iniPaths)
        {
            string iniName =
                Path.GetFileName(
                    iniPath
                );

            try
            {
                string[] archiveNames =
                    Archive
                        .GetIniListings(
                            GameRelease.SkyrimSE,
                            new FilePath(iniPath),
                            fileSystem
                        )
                        .Select(name =>
                            name.ToString()
                        )
                        .ToArray();

                IReadOnlyList<ParsedIniArchiveListing>
                    parsedListings =
                        ParseIniArchiveListings(
                            iniPath
                        );

                bool provenanceMatches =
                    archiveNames.Length ==
                        parsedListings.Count;

                if (provenanceMatches)
                {
                    for (
                        int index = 0;
                        index < archiveNames.Length;
                        index++)
                    {
                        if (!string.Equals(
                                archiveNames[index],
                                parsedListings[index]
                                    .ArchiveName,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            provenanceMatches =
                                false;

                            break;
                        }
                    }
                }

                if (!provenanceMatches)
                {
                    iniProvenanceErrors.Add(
                        new SkyrimRuntimeArchiveIniProvenanceError(
                            IniName:
                                iniName,
                            IniPath:
                                iniPath,
                            Error:
                                "Raw INI archive-list provenance " +
                                "did not match Mutagen GetIniListings()."
                        )
                    );

                    continue;
                }

                for (
                    int listingIndex = 0;
                    listingIndex < archiveNames.Length;
                    listingIndex++)
                {
                    string archiveName =
                        archiveNames[listingIndex];

                    ParsedIniArchiveListing parsed =
                        parsedListings[listingIndex];

                    var listing =
                        new SkyrimRuntimeArchiveIniListing(
                            IniName:
                                iniName,
                            IniPath:
                                iniPath,
                            IniKey:
                                parsed.IniKey,
                            IndexWithinKey:
                                parsed.IndexWithinKey,
                            ListingIndex:
                                parsed.ListingIndex
                        );

                    if (builders.TryGetValue(
                            archiveName,
                            out ArchiveBuilder? archive))
                    {
                        archive.IniListings.Add(
                            listing
                        );

                        continue;
                    }

                    if (!missingIniListings.TryGetValue(
                            archiveName,
                            out var listings))
                    {
                        listings =
                            new List<SkyrimRuntimeArchiveIniListing>();

                        missingIniListings.Add(
                            archiveName,
                            listings
                        );
                    }

                    listings.Add(
                        listing
                    );
                }
            }
            catch (Exception ex)
            {
                iniReadErrors.Add(
                    new SkyrimRuntimeArchiveIniReadError(
                        IniName:
                            iniName,
                        IniPath:
                            iniPath,
                        Error:
                            ex.Message
                    )
                );
            }
        }

        SkyrimRuntimeArchiveEvidenceEntry[] archives =
            builders.Values
                .OrderBy(
                    archive =>
                        archive.ArchiveName,
                    StringComparer.OrdinalIgnoreCase
                )
                .ThenBy(
                    archive =>
                        archive.ArchiveName,
                    StringComparer.Ordinal
                )
                .Select(archive =>
                    new SkyrimRuntimeArchiveEvidenceEntry(
                        ArchiveName:
                            archive.ArchiveName,
                        ArchivePath:
                            archive.ArchivePath,
                        PluginAssociations:
                            archive.PluginAssociations
                                .OrderBy(association =>
                                    association.LoadOrderIndex
                                )
                                .ThenBy(
                                    association =>
                                        association.PluginName,
                                    StringComparer.OrdinalIgnoreCase
                                )
                                .ToArray(),
                        IniListings:
                            archive.IniListings
                                .OrderBy(
                                    listing =>
                                        listing.IniPath,
                                    StringComparer.Ordinal
                                )
                                .ThenBy(listing =>
                                    listing.ListingIndex
                                )
                                .ToArray()
                    )
                )
                .ToArray();

        SkyrimRuntimeArchiveMissingIniArchive[] missing =
            missingIniListings
                .OrderBy(
                    pair => pair.Key,
                    StringComparer.OrdinalIgnoreCase
                )
                .Select(pair =>
                    new SkyrimRuntimeArchiveMissingIniArchive(
                        ArchiveName:
                            pair.Key,
                        IniListings:
                            pair.Value
                                .OrderBy(
                                    listing =>
                                        listing.IniPath,
                                    StringComparer.Ordinal
                                )
                                .ThenBy(listing =>
                                    listing.ListingIndex
                                )
                                .ToArray()
                    )
                )
                .ToArray();

        return new SkyrimRuntimeArchiveEvidenceResult(
            DataRoot:
                fullDataRoot,
            IniDirectory:
                fullIniDirectory,
            Archives:
                archives,
            MissingIniArchives:
                missing,
            AssociationErrors:
                associationErrors.ToArray(),
            IniReadErrors:
                iniReadErrors.ToArray(),
            IniProvenanceErrors:
                iniProvenanceErrors.ToArray()
        );
    }
}
