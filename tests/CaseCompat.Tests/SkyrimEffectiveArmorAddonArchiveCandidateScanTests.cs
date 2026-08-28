using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Analysis;
using CaseCompat.Core.Findings;
using CaseCompat.Core.Resolution;
using Xunit;

namespace CaseCompat.Tests;

public sealed class SkyrimEffectiveArmorAddonArchiveCandidateScanTests
{
    private const string RequestedPath =
        "Meshes/Test/Fixture.nif";

    [Fact]
    public void Inspect_RuntimeEvidencedPhysicalProvider_AppearsInBothLists()
    {
        string dataRoot =
            CreateDataRoot(
                "runtime-provider"
            );

        SkyrimArchiveAssetProvider provider =
            CreateProvider(
                dataRoot,
                "Runtime.bsa"
            );

        SkyrimEffectiveArmorAddonArchiveCandidateScanResult result =
            SkyrimEffectiveArmorAddonArchiveCandidateScan.Inspect(
                CreateEffectiveScan(
                    dataRoot
                ),
                CreateArchiveIndex(
                    dataRoot,
                    provider
                ),
                CreateRuntimeEvidence(
                    dataRoot,
                    CreateRuntimeArchive(
                        provider,
                        hasRuntimeEvidence: true
                    )
                )
            );

        SkyrimEffectiveArmorAddonArchiveCandidateFinding finding =
            Assert.Single(
                result.Findings
            );

        Assert.Single(
            finding.ArchiveCandidates
        );

        Assert.Single(
            finding.RuntimeEvidencedArchiveCandidates
        );

        Assert.True(
            finding.HasArchiveCandidates
        );

        Assert.True(
            finding.HasRuntimeEvidencedArchiveCandidates
        );

        Assert.Equal(
            1,
            result.FindingsWithArchiveCandidates
        );

        Assert.Equal(
            1,
            result.FindingsWithRuntimeEvidencedArchiveCandidates
        );

        Assert.True(
            result.Complete
        );
    }

    [Fact]
    public void Inspect_PhysicalProviderWithoutRuntimeEvidence_StaysPhysicalOnly()
    {
        string dataRoot =
            CreateDataRoot(
                "physical-only"
            );

        SkyrimArchiveAssetProvider provider =
            CreateProvider(
                dataRoot,
                "PhysicalOnly.bsa"
            );

        SkyrimEffectiveArmorAddonArchiveCandidateScanResult result =
            SkyrimEffectiveArmorAddonArchiveCandidateScan.Inspect(
                CreateEffectiveScan(
                    dataRoot
                ),
                CreateArchiveIndex(
                    dataRoot,
                    provider
                ),
                CreateRuntimeEvidence(
                    dataRoot,
                    CreateRuntimeArchive(
                        provider,
                        hasRuntimeEvidence: false
                    )
                )
            );

        SkyrimEffectiveArmorAddonArchiveCandidateFinding finding =
            Assert.Single(
                result.Findings
            );

        Assert.Single(
            finding.ArchiveCandidates
        );

        Assert.Empty(
            finding.RuntimeEvidencedArchiveCandidates
        );

        Assert.True(
            finding.HasArchiveCandidates
        );

        Assert.False(
            finding.HasRuntimeEvidencedArchiveCandidates
        );

        Assert.Equal(
            1,
            result.FindingsWithArchiveCandidates
        );

        Assert.Equal(
            0,
            result.FindingsWithRuntimeEvidencedArchiveCandidates
        );

        Assert.Equal(
            1,
            result.UniqueRequestedPathsWithArchiveCandidates
        );

        Assert.Equal(
            0,
            result.UniqueRequestedPathsWithRuntimeEvidencedArchiveCandidates
        );
    }

    [Fact]
    public void Inspect_MixedProviders_FiltersRuntimeCandidateSubset()
    {
        string dataRoot =
            CreateDataRoot(
                "mixed-providers"
            );

        SkyrimArchiveAssetProvider physicalOnly =
            CreateProvider(
                dataRoot,
                "PhysicalOnly.bsa"
            );

        SkyrimArchiveAssetProvider runtimeProvider =
            CreateProvider(
                dataRoot,
                "Runtime.bsa"
            );

        SkyrimEffectiveArmorAddonArchiveCandidateScanResult result =
            SkyrimEffectiveArmorAddonArchiveCandidateScan.Inspect(
                CreateEffectiveScan(
                    dataRoot
                ),
                CreateArchiveIndex(
                    dataRoot,
                    physicalOnly,
                    runtimeProvider
                ),
                CreateRuntimeEvidence(
                    dataRoot,
                    CreateRuntimeArchive(
                        physicalOnly,
                        hasRuntimeEvidence: false
                    ),
                    CreateRuntimeArchive(
                        runtimeProvider,
                        hasRuntimeEvidence: true
                    )
                )
            );

        SkyrimEffectiveArmorAddonArchiveCandidateFinding finding =
            Assert.Single(
                result.Findings
            );

        Assert.Equal(
            2,
            finding.ArchiveCandidateCount
        );

        Assert.Equal(
            1,
            finding.RuntimeEvidencedArchiveCandidateCount
        );

        SkyrimArchiveAssetProvider runtimeCandidate =
            Assert.Single(
                finding.RuntimeEvidencedArchiveCandidates
            );

        Assert.Equal(
            "Runtime.bsa",
            runtimeCandidate.ArchiveName
        );
    }

    [Fact]
    public void Inspect_DifferentDataRoots_Throws()
    {
        string effectiveDataRoot =
            CreateDataRoot(
                "effective-root"
            );

        string archiveDataRoot =
            CreateDataRoot(
                "archive-root"
            );

        SkyrimArchiveAssetProvider provider =
            CreateProvider(
                archiveDataRoot,
                "Runtime.bsa"
            );

        ArgumentException exception =
            Assert.Throws<ArgumentException>(() =>
                SkyrimEffectiveArmorAddonArchiveCandidateScan.Inspect(
                    CreateEffectiveScan(
                        effectiveDataRoot
                    ),
                    CreateArchiveIndex(
                        archiveDataRoot,
                        provider
                    ),
                    CreateRuntimeEvidence(
                        effectiveDataRoot
                    )
                )
            );

        Assert.Contains(
            "same Data root",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Inspect_IncompleteRuntimeArchiveEvidence_ResultIsIncomplete()
    {
        string dataRoot =
            CreateDataRoot(
                "incomplete-runtime"
            );

        SkyrimArchiveAssetProvider provider =
            CreateProvider(
                dataRoot,
                "Runtime.bsa"
            );

        SkyrimRuntimeArchiveEvidenceResult runtimeEvidence =
            new(
                DataRoot:
                    dataRoot,
                IniDirectory:
                    Path.Combine(
                        dataRoot,
                        "ini"
                    ),
                Archives:
                    new[]
                    {
                        CreateRuntimeArchive(
                            provider,
                            hasRuntimeEvidence: true
                        )
                    },
                MissingIniArchives:
                    Array.Empty<
                        SkyrimRuntimeArchiveMissingIniArchive
                    >(),
                AssociationErrors:
                    Array.Empty<
                        SkyrimRuntimeArchiveAssociationError
                    >(),
                IniReadErrors:
                    new[]
                    {
                        new SkyrimRuntimeArchiveIniReadError(
                            IniName:
                                "Skyrim.ini",
                            IniPath:
                                Path.Combine(
                                    dataRoot,
                                    "ini",
                                    "Skyrim.ini"
                                ),
                            Error:
                                "fixture INI error"
                        )
                    },
                IniProvenanceErrors:
                    Array.Empty<
                        SkyrimRuntimeArchiveIniProvenanceError
                    >()
            );

        SkyrimEffectiveArmorAddonArchiveCandidateScanResult result =
            SkyrimEffectiveArmorAddonArchiveCandidateScan.Inspect(
                CreateEffectiveScan(
                    dataRoot
                ),
                CreateArchiveIndex(
                    dataRoot,
                    provider
                ),
                runtimeEvidence
            );

        Assert.False(
            runtimeEvidence.SearchComplete
        );

        Assert.False(
            result.Complete
        );

        Assert.Single(
            Assert.Single(
                result.Findings
            ).RuntimeEvidencedArchiveCandidates
        );
    }

    private static SkyrimWinningArmorAddonEffectiveScanResult
        CreateEffectiveScan(
            string dataRoot)
    {
        DataRelativePathResolution resolution =
            new(
                DataRoot:
                    dataRoot,
                RequestedPath:
                    RequestedPath,
                LinuxResolves:
                    false,
                ResolvedPhysicalPath:
                    null,
                FailedComponentIndex:
                    1,
                FailureReason:
                    "fixture unresolved path",
                Steps:
                    Array.Empty<
                        PathResolutionStep
                    >(),
                EquivalentPhysicalCandidates:
                    Array.Empty<string>(),
                CandidateSearchErrors:
                    Array.Empty<string>()
            );

        EffectiveAssetReferenceFinding finding =
            new(
                ConsumerKind:
                    "ArmorAddon",
                ConsumerFormKey:
                    "000001:Fixture.esp",
                ConsumerEditorId:
                    "FixtureAA",
                WinningPluginName:
                    "Fixture.esp",
                WinningLoadOrderIndex:
                    100,
                WinnerSearchComplete:
                    true,
                ReferenceField:
                    "WorldModel.Female",
                RawPath:
                    @"Test\Fixture.nif",
                RequestedPath:
                    RequestedPath,
                Resolution:
                    resolution
            );

        SkyrimWinningArmorAddonInventoryResult inventory =
            new(
                DataRoot:
                    dataRoot,
                RuntimeActivePluginCount:
                    1,
                PluginsOpened:
                    1,
                MissingPluginFiles:
                    Array.Empty<string>(),
                ReadErrors:
                    Array.Empty<
                        SkyrimPluginReadError
                    >(),
                Winners:
                    Array.Empty<
                        SkyrimWinningArmorAddonRecord
                    >()
            );

        return new SkyrimWinningArmorAddonEffectiveScanResult(
            Inventory:
                inventory,
            UniqueRequestedPathCount:
                1,
            ResolutionErrors:
                Array.Empty<
                    SkyrimAssetPathResolutionError
                >(),
            Findings:
                new[]
                {
                    finding
                }
        );
    }

    private static SkyrimArchiveCandidateIndexResult
        CreateArchiveIndex(
            string dataRoot,
            params SkyrimArchiveAssetProvider[] providers)
    {
        var assets =
            new Dictionary<
                WindowsLogicalPath,
                IReadOnlyList<SkyrimArchiveAssetProvider>
            >
            {
                [
                    WindowsLogicalPath.FromRelativePath(
                        RequestedPath
                    )
                ] =
                    providers
            };

        int archiveCount =
            providers
                .Select(provider =>
                    provider.ArchivePath
                )
                .Distinct(
                    StringComparer.Ordinal
                )
                .Count();

        return new SkyrimArchiveCandidateIndexResult(
            DataRoot:
                dataRoot,
            ArchivesDiscovered:
                archiveCount,
            ArchivesRead:
                archiveCount,
            TotalFileEntries:
                providers.Length,
            DuplicateLogicalEntriesWithinArchive:
                0,
            ReadErrors:
                Array.Empty<
                    SkyrimArchiveReadError
                >(),
            Assets:
                assets
        );
    }

    private static SkyrimRuntimeArchiveEvidenceResult
        CreateRuntimeEvidence(
            string dataRoot,
            params SkyrimRuntimeArchiveEvidenceEntry[] archives)
    {
        return new SkyrimRuntimeArchiveEvidenceResult(
            DataRoot:
                dataRoot,
            IniDirectory:
                Path.Combine(
                    dataRoot,
                    "ini"
                ),
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
    }

    private static SkyrimRuntimeArchiveEvidenceEntry
        CreateRuntimeArchive(
            SkyrimArchiveAssetProvider provider,
            bool hasRuntimeEvidence)
    {
        IReadOnlyList<
            SkyrimRuntimeArchivePluginAssociation
        > associations =
            hasRuntimeEvidence
                ? new[]
                {
                    new SkyrimRuntimeArchivePluginAssociation(
                        PluginName:
                            "Fixture.esp",
                        LoadOrderIndex:
                            100
                    )
                }
                : Array.Empty<
                    SkyrimRuntimeArchivePluginAssociation
                >();

        return new SkyrimRuntimeArchiveEvidenceEntry(
            ArchiveName:
                provider.ArchiveName,
            ArchivePath:
                provider.ArchivePath,
            PluginAssociations:
                associations,
            IniListings:
                Array.Empty<
                    SkyrimRuntimeArchiveIniListing
                >()
        );
    }

    private static SkyrimArchiveAssetProvider
        CreateProvider(
            string dataRoot,
            string archiveName)
    {
        return new SkyrimArchiveAssetProvider(
            ArchiveName:
                archiveName,
            ArchivePath:
                Path.Combine(
                    dataRoot,
                    archiveName
                ),
            InternalPath:
                "meshes/test/fixture.nif",
            Size:
                123
        );
    }

    private static string CreateDataRoot(
        string suffix)
    {
        return Path.GetFullPath(
            Path.Combine(
                Path.GetTempPath(),
                "casecompat-tests",
                "archive-correlation",
                suffix
            )
        );
    }
}
