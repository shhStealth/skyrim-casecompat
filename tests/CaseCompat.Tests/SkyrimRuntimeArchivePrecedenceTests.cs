using CaseCompat.Bethesda.Plugins;

namespace CaseCompat.Tests;

public sealed class SkyrimRuntimeArchivePrecedenceTests
{
    [Fact]
    public void Resolve_SingleRuntimeProvider_IsWinner()
    {
        SkyrimArchiveAssetProvider provider =
            Provider(
                "Only.bsa"
            );

        var resolver =
            Resolver(
                PluginEvidence(
                    "Only.bsa",
                    10
                )
            );

        SkyrimRuntimeArchivePrecedenceDecision decision =
            resolver.Resolve(
                new[]
                {
                    provider
                }
            );

        Assert.Equal(
            SkyrimRuntimeArchivePrecedenceState
                .SingleRuntimeEvidencedProvider,
            decision.State
        );

        Assert.Same(
            provider,
            decision.WinningProvider
        );

        Assert.True(
            decision.HasWinner
        );

        Assert.False(
            decision.IsAmbiguous
        );
    }

    [Fact]
    public void Resolve_PluginProvider_BeatsIniProvider()
    {
        SkyrimArchiveAssetProvider ini =
            Provider(
                "Base.bsa"
            );

        SkyrimArchiveAssetProvider plugin =
            Provider(
                "Mod.bsa"
            );

        var resolver =
            Resolver(
                IniEvidence(
                    "Base.bsa",
                    "Skyrim.ini",
                    5
                ),
                PluginEvidence(
                    "Mod.bsa",
                    100
                )
            );

        SkyrimRuntimeArchivePrecedenceDecision decision =
            resolver.Resolve(
                new[]
                {
                    ini,
                    plugin
                }
            );

        Assert.Equal(
            SkyrimRuntimeArchivePrecedenceState
                .ResolvedPluginOverIni,
            decision.State
        );

        Assert.Same(
            plugin,
            decision.WinningProvider
        );
    }

    [Fact]
    public void Resolve_LaterPluginProvider_Wins()
    {
        SkyrimArchiveAssetProvider early =
            Provider(
                "Early.bsa"
            );

        SkyrimArchiveAssetProvider late =
            Provider(
                "Late.bsa"
            );

        var resolver =
            Resolver(
                PluginEvidence(
                    "Early.bsa",
                    10
                ),
                PluginEvidence(
                    "Late.bsa",
                    20
                )
            );

        SkyrimRuntimeArchivePrecedenceDecision decision =
            resolver.Resolve(
                new[]
                {
                    early,
                    late
                }
            );

        Assert.Equal(
            SkyrimRuntimeArchivePrecedenceState
                .ResolvedByPluginLoadOrder,
            decision.State
        );

        Assert.Same(
            late,
            decision.WinningProvider
        );
    }

    [Fact]
    public void Resolve_MultiplePluginCandidatesBeforeIni_UsesPluginOrder()
    {
        SkyrimArchiveAssetProvider ini =
            Provider(
                "Base.bsa"
            );

        SkyrimArchiveAssetProvider early =
            Provider(
                "Early.bsa"
            );

        SkyrimArchiveAssetProvider late =
            Provider(
                "Late.bsa"
            );

        var resolver =
            Resolver(
                IniEvidence(
                    "Base.bsa",
                    "Skyrim.ini",
                    5
                ),
                PluginEvidence(
                    "Early.bsa",
                    10
                ),
                PluginEvidence(
                    "Late.bsa",
                    20
                )
            );

        SkyrimRuntimeArchivePrecedenceDecision decision =
            resolver.Resolve(
                new[]
                {
                    ini,
                    early,
                    late
                }
            );

        Assert.Equal(
            SkyrimRuntimeArchivePrecedenceState
                .ResolvedByPluginLoadOrder,
            decision.State
        );

        Assert.Same(
            late,
            decision.WinningProvider
        );
    }

    [Fact]
    public void Resolve_LaterSameIniListing_Wins()
    {
        SkyrimArchiveAssetProvider early =
            Provider(
                "Early.bsa"
            );

        SkyrimArchiveAssetProvider late =
            Provider(
                "Late.bsa"
            );

        var resolver =
            Resolver(
                IniEvidence(
                    "Early.bsa",
                    "Skyrim.ini",
                    2
                ),
                IniEvidence(
                    "Late.bsa",
                    "Skyrim.ini",
                    7
                )
            );

        SkyrimRuntimeArchivePrecedenceDecision decision =
            resolver.Resolve(
                new[]
                {
                    early,
                    late
                }
            );

        Assert.Equal(
            SkyrimRuntimeArchivePrecedenceState
                .ResolvedByIniListingOrder,
            decision.State
        );

        Assert.Same(
            late,
            decision.WinningProvider
        );
    }

    [Fact]
    public void Resolve_SamePluginLoadPosition_IsAmbiguous()
    {
        var resolver =
            Resolver(
                PluginEvidence(
                    "Mod.bsa",
                    20
                ),
                PluginEvidence(
                    "Mod - Textures.bsa",
                    20
                )
            );

        SkyrimRuntimeArchivePrecedenceDecision decision =
            resolver.Resolve(
                new[]
                {
                    Provider(
                        "Mod.bsa"
                    ),
                    Provider(
                        "Mod - Textures.bsa"
                    )
                }
            );

        Assert.Equal(
            SkyrimRuntimeArchivePrecedenceState
                .AmbiguousSamePluginLoadOrderIndex,
            decision.State
        );

        Assert.Null(
            decision.WinningProvider
        );

        Assert.True(
            decision.IsAmbiguous
        );
    }

    [Fact]
    public void Resolve_DifferentIniFiles_IsAmbiguous()
    {
        var resolver =
            Resolver(
                IniEvidence(
                    "A.bsa",
                    "Skyrim.ini",
                    1
                ),
                IniEvidence(
                    "B.bsa",
                    "SkyrimCustom.ini",
                    1
                )
            );

        SkyrimRuntimeArchivePrecedenceDecision decision =
            resolver.Resolve(
                new[]
                {
                    Provider(
                        "A.bsa"
                    ),
                    Provider(
                        "B.bsa"
                    )
                }
            );

        Assert.Equal(
            SkyrimRuntimeArchivePrecedenceState
                .AmbiguousDifferentIniFiles,
            decision.State
        );

        Assert.Null(
            decision.WinningProvider
        );
    }

    [Fact]
    public void Resolve_DuplicateLogicalEntryWithinArchive_IsAmbiguous()
    {
        SkyrimArchiveAssetProvider first =
            Provider(
                "Mod.bsa",
                internalPath:
                    "meshes/a.nif"
            );

        SkyrimArchiveAssetProvider second =
            Provider(
                "Mod.bsa",
                internalPath:
                    "Meshes/A.nif"
            );

        var resolver =
            Resolver(
                PluginEvidence(
                    "Mod.bsa",
                    20
                )
            );

        SkyrimRuntimeArchivePrecedenceDecision decision =
            resolver.Resolve(
                new[]
                {
                    first,
                    second
                }
            );

        Assert.Equal(
            SkyrimRuntimeArchivePrecedenceState
                .AmbiguousDuplicateLogicalEntryWithinArchive,
            decision.State
        );

        Assert.Null(
            decision.WinningProvider
        );
    }

    private static SkyrimRuntimeArchivePrecedenceResolver
        Resolver(
            params SkyrimRuntimeArchiveEvidenceEntry[] archives)
    {
        string dataRoot =
            "/tmp/casecompat-precedence-tests/Data";

        var result =
            new SkyrimRuntimeArchiveEvidenceResult(
                DataRoot:
                    dataRoot,
                IniDirectory:
                    "/tmp/casecompat-precedence-tests/Ini",
                Archives:
                    archives,
                MissingIniArchives:
                    Array.Empty<
                        SkyrimRuntimeArchiveMissingIniArchive
                    >(),
                AssociationErrors:
                    Array.Empty<
                        SkyrimRuntimeArchiveAssociationError
                    >(),
                IniReadErrors:
                    Array.Empty<
                        SkyrimRuntimeArchiveIniReadError
                    >(),
                IniProvenanceErrors:
                    Array.Empty<
                        SkyrimRuntimeArchiveIniProvenanceError
                    >()
            );

        return new SkyrimRuntimeArchivePrecedenceResolver(
            result
        );
    }

    private static SkyrimArchiveAssetProvider Provider(
        string archiveName,
        string internalPath =
            "meshes/test.nif")
    {
        return new SkyrimArchiveAssetProvider(
            ArchiveName:
                archiveName,
            ArchivePath:
                ArchivePath(
                    archiveName
                ),
            InternalPath:
                internalPath,
            Size:
                123
        );
    }

    private static SkyrimRuntimeArchiveEvidenceEntry
        PluginEvidence(
            string archiveName,
            int loadOrderIndex)
    {
        string pluginBase =
            Path.GetFileNameWithoutExtension(
                archiveName
            );

        return new SkyrimRuntimeArchiveEvidenceEntry(
            ArchiveName:
                archiveName,
            ArchivePath:
                ArchivePath(
                    archiveName
                ),
            PluginAssociations:
                new[]
                {
                    new SkyrimRuntimeArchivePluginAssociation(
                        PluginName:
                            pluginBase + ".esp",
                        LoadOrderIndex:
                            loadOrderIndex
                    )
                },
            IniListings:
                Array.Empty<
                    SkyrimRuntimeArchiveIniListing
                >()
        );
    }

    private static SkyrimRuntimeArchiveEvidenceEntry
        IniEvidence(
            string archiveName,
            string iniName,
            int listingIndex)
    {
        string iniPath =
            Path.Combine(
                "/tmp/casecompat-precedence-tests/Ini",
                iniName
            );

        return new SkyrimRuntimeArchiveEvidenceEntry(
            ArchiveName:
                archiveName,
            ArchivePath:
                ArchivePath(
                    archiveName
                ),
            PluginAssociations:
                Array.Empty<
                    SkyrimRuntimeArchivePluginAssociation
                >(),
            IniListings:
                new[]
                {
                    new SkyrimRuntimeArchiveIniListing(
                        IniName:
                            iniName,
                        IniPath:
                            iniPath,
                        IniKey:
                            "sResourceArchiveList",
                        IndexWithinKey:
                            listingIndex,
                        ListingIndex:
                            listingIndex
                    )
                }
        );
    }

    private static string ArchivePath(
        string archiveName)
    {
        return Path.Combine(
            "/tmp/casecompat-precedence-tests/Data",
            archiveName
        );
    }
}
