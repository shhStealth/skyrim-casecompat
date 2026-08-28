using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.LoadOrder;
using Xunit;

namespace CaseCompat.Tests;

public sealed class SkyrimRuntimeArchiveEvidenceTests
{
    [Fact]
    public void Inspect_PluginAssociatedArchive_HasRuntimeEvidence()
    {
        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Data"
                )
            ).FullName;

        string iniDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Ini"
                )
            ).FullName;

        File.WriteAllText(
            Path.Combine(
                dataRoot,
                "Example.bsa"
            ),
            string.Empty
        );

        SkyrimRuntimePluginSet pluginSet =
            CreateRuntimePluginSet(
                temp.RootPath,
                "Example.esp"
            );

        SkyrimRuntimeArchiveEvidenceResult result =
            SkyrimRuntimeArchiveEvidence.Inspect(
                dataRoot,
                pluginSet,
                iniDirectory
            );

        SkyrimRuntimeArchiveEvidenceEntry archive =
            Assert.Single(
                result.Archives
            );

        Assert.Equal(
            "Example.bsa",
            archive.ArchiveName
        );

        Assert.True(
            archive.HasPluginAssociation
        );

        Assert.False(
            archive.IsIniListed
        );

        Assert.True(
            archive.HasRuntimeEvidence
        );

        SkyrimRuntimeArchivePluginAssociation association =
            Assert.Single(
                archive.PluginAssociations
            );

        Assert.Equal(
            "Example.esp",
            association.PluginName
        );

        Assert.Equal(
            5,
            association.LoadOrderIndex
        );

        Assert.Equal(
            1,
            result.PluginAssociatedArchiveCount
        );

        Assert.Equal(
            1,
            result.RuntimeEvidencedArchiveCount
        );

        Assert.True(
            result.SearchComplete
        );
    }

    [Fact]
    public void Inspect_IniListedPhysicalArchive_HasRuntimeEvidence()
    {
        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Data"
                )
            ).FullName;

        string iniDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Ini"
                )
            ).FullName;

        File.WriteAllText(
            Path.Combine(
                dataRoot,
                "CustomAssets.bsa"
            ),
            string.Empty
        );

        File.WriteAllText(
            Path.Combine(
                iniDirectory,
                "Skyrim.ini"
            ),
            "[Archive]\n" +
            "sResourceArchiveList=CustomAssets.bsa\n"
        );

        SkyrimRuntimePluginSet pluginSet =
            CreateRuntimePluginSet(
                temp.RootPath,
                "Example.esp"
            );

        SkyrimRuntimeArchiveEvidenceResult result =
            SkyrimRuntimeArchiveEvidence.Inspect(
                dataRoot,
                pluginSet,
                iniDirectory
            );

        SkyrimRuntimeArchiveEvidenceEntry archive =
            Assert.Single(
                result.Archives
            );

        Assert.False(
            archive.HasPluginAssociation
        );

        Assert.True(
            archive.IsIniListed
        );

        Assert.True(
            archive.HasRuntimeEvidence
        );

        SkyrimRuntimeArchiveIniListing listing =
            Assert.Single(
                archive.IniListings
            );

        Assert.Equal(
            "Skyrim.ini",
            listing.IniName
        );

        Assert.Equal(
            0,
            listing.ListingIndex
        );

        Assert.Equal(
            0,
            result.PluginAssociatedArchiveCount
        );

        Assert.Equal(
            1,
            result.IniListedPhysicalArchiveCount
        );

        Assert.Equal(
            1,
            result.RuntimeEvidencedArchiveCount
        );

        Assert.True(
            result.SearchComplete
        );
    }

    [Fact]
    public void Inspect_IniListings_PreserveReturnedSequence()
    {
        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Data"
                )
            ).FullName;

        string iniDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Ini"
                )
            ).FullName;

        File.WriteAllText(
            Path.Combine(
                dataRoot,
                "First.bsa"
            ),
            string.Empty
        );

        File.WriteAllText(
            Path.Combine(
                dataRoot,
                "Second.bsa"
            ),
            string.Empty
        );

        File.WriteAllText(
            Path.Combine(
                iniDirectory,
                "Skyrim.ini"
            ),
            "[Archive]\n" +
            "sResourceArchiveList=Second.bsa,First.bsa\n"
        );

        SkyrimRuntimePluginSet pluginSet =
            CreateRuntimePluginSet(
                temp.RootPath,
                "Example.esp"
            );

        SkyrimRuntimeArchiveEvidenceResult result =
            SkyrimRuntimeArchiveEvidence.Inspect(
                dataRoot,
                pluginSet,
                iniDirectory
            );

        SkyrimRuntimeArchiveEvidenceEntry first =
            Assert.Single(
                result.Archives,
                archive =>
                    archive.ArchiveName ==
                    "First.bsa"
            );

        SkyrimRuntimeArchiveEvidenceEntry second =
            Assert.Single(
                result.Archives,
                archive =>
                    archive.ArchiveName ==
                    "Second.bsa"
            );

        Assert.Equal(
            1,
            Assert.Single(
                first.IniListings
            ).ListingIndex
        );

        Assert.Equal(
            0,
            Assert.Single(
                second.IniListings
            ).ListingIndex
        );

        Assert.True(
            result.SearchComplete
        );
    }

    [Fact]
    public void Inspect_IniListedMissingArchive_IsReportedSeparately()
    {
        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Data"
                )
            ).FullName;

        string iniDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Ini"
                )
            ).FullName;

        File.WriteAllText(
            Path.Combine(
                iniDirectory,
                "Skyrim.ini"
            ),
            "[Archive]\n" +
            "sResourceArchiveList=MissingAssets.bsa\n"
        );

        SkyrimRuntimePluginSet pluginSet =
            CreateRuntimePluginSet(
                temp.RootPath,
                "Example.esp"
            );

        SkyrimRuntimeArchiveEvidenceResult result =
            SkyrimRuntimeArchiveEvidence.Inspect(
                dataRoot,
                pluginSet,
                iniDirectory
            );

        Assert.Empty(
            result.Archives
        );

        SkyrimRuntimeArchiveMissingIniArchive missing =
            Assert.Single(
                result.MissingIniArchives
            );

        Assert.Equal(
            "MissingAssets.bsa",
            missing.ArchiveName
        );

        Assert.Single(
            missing.IniListings
        );

        Assert.Equal(
            0,
            result.PhysicalArchiveCount
        );

        Assert.True(
            result.SearchComplete
        );
    }

    [Fact]
    public void Inspect_UnassociatedPhysicalArchive_HasNoRuntimeEvidence()
    {
        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Data"
                )
            ).FullName;

        string iniDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Ini"
                )
            ).FullName;

        File.WriteAllText(
            Path.Combine(
                dataRoot,
                "UnrelatedAssets.bsa"
            ),
            string.Empty
        );

        SkyrimRuntimePluginSet pluginSet =
            CreateRuntimePluginSet(
                temp.RootPath,
                "Example.esp"
            );

        SkyrimRuntimeArchiveEvidenceResult result =
            SkyrimRuntimeArchiveEvidence.Inspect(
                dataRoot,
                pluginSet,
                iniDirectory
            );

        SkyrimRuntimeArchiveEvidenceEntry archive =
            Assert.Single(
                result.Archives
            );

        Assert.False(
            archive.HasPluginAssociation
        );

        Assert.False(
            archive.IsIniListed
        );

        Assert.False(
            archive.HasRuntimeEvidence
        );

        Assert.Equal(
            1,
            result.NoRuntimeEvidenceArchiveCount
        );

        Assert.Equal(
            0,
            result.RuntimeEvidencedArchiveCount
        );

        Assert.True(
            result.SearchComplete
        );
    }

    private static SkyrimRuntimePluginSet
        CreateRuntimePluginSet(
            string root,
            string explicitPlugin)
    {
        string pluginsPath =
            Path.Combine(
                root,
                "Plugins.txt"
            );

        string loadOrderPath =
            Path.Combine(
                root,
                "loadorder.txt"
            );

        string cccPath =
            Path.Combine(
                root,
                "Skyrim.ccc"
            );

        File.WriteAllText(
            pluginsPath,
            $"*{explicitPlugin}\n"
        );

        File.WriteAllText(
            loadOrderPath,
            "Skyrim.esm\n" +
            "Update.esm\n" +
            "Dawnguard.esm\n" +
            "HearthFires.esm\n" +
            "Dragonborn.esm\n" +
            $"{explicitPlugin}\n"
        );

        File.WriteAllText(
            cccPath,
            string.Empty
        );

        SkyrimRuntimeLoadOrder loadOrder =
            SkyrimRuntimeLoadOrderReader.Read(
                pluginsPath,
                loadOrderPath
            );

        SkyrimRuntimePluginSet pluginSet =
            SkyrimRuntimePluginSetReader.Read(
                loadOrder,
                cccPath
            );

        Assert.True(
            pluginSet.IsConsistent
        );

        return pluginSet;
    }

    private sealed class TemporaryDirectory
        : IDisposable
    {
        public TemporaryDirectory()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-tests",
                    Guid.NewGuid()
                        .ToString("N")
                );

            Directory.CreateDirectory(
                RootPath
            );
        }

        public string RootPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(
                    RootPath,
                    recursive: true
                );
            }
        }
    }
}
