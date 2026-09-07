using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.LoadOrder;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;

namespace CaseCompat.Tests;

public sealed class SkyrimWinningHeadPartInventoryTests
{
    [Fact]
    public void Inspect_UsesHighestPriorityOverrideForRecordProviderAndPartReference()
    {
        string dataRoot =
            CreateDataRoot();

        try
        {
            const string lowPluginName =
                "CaseCompatHeadPartLow.esp";

            const string highPluginName =
                "CaseCompatHeadPartHigh.esp";

            const string lowEditorId =
                "CaseCompatHeadPartLow";

            const string highEditorId =
                "CaseCompatHeadPartHigh";

            const string lowRequestedPath =
                "Meshes/Actors/Character/Character Assets/FaceParts/" +
                "MaleHeadbrows.tri";

            const string highRequestedPath =
                "Meshes/Actors/Character/Character Assets/FaceParts/" +
                "MaleHeadBrows.tri";

            string lowPluginPath =
                Path.Combine(
                    dataRoot,
                    lowPluginName
                );

            string highPluginPath =
                Path.Combine(
                    dataRoot,
                    highPluginName
                );

            var lowMod =
                new SkyrimMod(
                    ModKey.FromNameAndExtension(
                        lowPluginName
                    ),
                    SkyrimRelease.SkyrimSE
                );

            var lowRecord =
                new HeadPart(
                    lowMod,
                    lowEditorId
                );

            lowRecord.Parts.Add(
                new Part
                {
                    PartType =
                        Part.PartTypeEnum.Tri,
                    FileName =
                        lowRequestedPath
                }
            );

            FormKey targetFormKey =
                lowRecord.FormKey;

            lowMod.HeadParts.Set(
                lowRecord
            );

            lowMod.WriteToBinary(
                lowPluginPath
            );

            var highMod =
                new SkyrimMod(
                    ModKey.FromNameAndExtension(
                        highPluginName
                    ),
                    SkyrimRelease.SkyrimSE
                );

            var highRecord =
                new HeadPart(
                    targetFormKey,
                    SkyrimRelease.SkyrimSE
                )
                {
                    EditorID =
                        highEditorId
                };

            highRecord.Parts.Add(
                new Part
                {
                    PartType =
                        Part.PartTypeEnum.Tri,
                    FileName =
                        highRequestedPath
                }
            );

            highMod.HeadParts.Set(
                highRecord
            );

            highMod.WriteToBinary(
                highPluginPath
            );

            SkyrimRuntimePluginSet runtimePluginSet =
                RuntimePluginSet(
                    dataRoot,
                    (lowPluginName, 0),
                    (highPluginName, 1)
                );

            SkyrimWinningHeadPartInventoryResult result =
                SkyrimWinningHeadPartInventory.Inspect(
                    dataRoot,
                    runtimePluginSet
                );

            Assert.True(
                result.SearchComplete
            );

            Assert.Equal(
                2,
                result.RuntimeActivePluginCount
            );

            Assert.Equal(
                2,
                result.PluginsOpened
            );

            Assert.Empty(
                result.MissingPluginFiles
            );

            Assert.Empty(
                result.ReadErrors
            );

            Assert.Equal(
                1,
                result.WinningHeadPartCount
            );

            Assert.Equal(
                1,
                result.WinningPartReferenceCount
            );

            SkyrimWinningHeadPartRecord winner =
                Assert.Single(
                    result.Winners
                );

            Assert.Equal(
                targetFormKey.ToString(),
                winner.FormKey
            );

            Assert.Equal(
                highEditorId,
                winner.EditorId
            );

            Assert.Equal(
                highPluginName,
                winner.WinningPluginName
            );

            Assert.Equal(
                1,
                winner.WinningLoadOrderIndex
            );

            SkyrimHeadPartPartReference reference =
                Assert.Single(
                    winner.PartReferences
                );

            Assert.Equal(
                winner.FormKey,
                reference.FormKey
            );

            Assert.Equal(
                highEditorId,
                reference.EditorId
            );

            Assert.Equal(
                0,
                reference.PartIndex
            );

            Assert.Equal(
                Part.PartTypeEnum.Tri,
                reference.PartType
            );

            Assert.Equal(
                highRequestedPath,
                reference.GivenPath
            );

            Assert.Equal(
                highRequestedPath,
                reference.DataRelativePath
            );

            Assert.NotEqual(
                lowRequestedPath,
                reference.DataRelativePath
            );
        }
        finally
        {
            DeleteDataRoot(
                dataRoot
            );
        }
    }

    [Fact]
    public void Inspect_MissingActivePlugin_IsIncompleteAndProducesNoWinner()
    {
        string dataRoot =
            CreateDataRoot();

        try
        {
            const string missingPluginName =
                "CaseCompatMissingHeadPart.esp";

            SkyrimRuntimePluginSet runtimePluginSet =
                RuntimePluginSet(
                    dataRoot,
                    (missingPluginName, 0)
                );

            SkyrimWinningHeadPartInventoryResult result =
                SkyrimWinningHeadPartInventory.Inspect(
                    dataRoot,
                    runtimePluginSet
                );

            Assert.False(
                result.SearchComplete
            );

            Assert.Equal(
                1,
                result.RuntimeActivePluginCount
            );

            Assert.Equal(
                0,
                result.PluginsOpened
            );

            Assert.Equal(
                missingPluginName,
                Assert.Single(
                    result.MissingPluginFiles
                )
            );

            Assert.Empty(
                result.ReadErrors
            );

            Assert.Empty(
                result.Winners
            );

            Assert.Equal(
                0,
                result.WinningHeadPartCount
            );

            Assert.Equal(
                0,
                result.WinningPartReferenceCount
            );
        }
        finally
        {
            DeleteDataRoot(
                dataRoot
            );
        }
    }

    private static SkyrimRuntimePluginSet RuntimePluginSet(
        string dataRoot,
        params (string PluginName, int LoadOrderIndex)[] plugins)
    {
        SkyrimRuntimeLoadOrderEntry[] loadOrderEntries =
            plugins
                .Select(plugin =>
                    new SkyrimRuntimeLoadOrderEntry(
                        LoadOrderIndex:
                            plugin.LoadOrderIndex,
                        PluginName:
                            plugin.PluginName,
                        ExplicitlyActive:
                            true
                    )
                )
                .ToArray();

        string[] activePluginNames =
            plugins
                .Select(plugin =>
                    plugin.PluginName
                )
                .ToArray();

        var loadOrder =
            new SkyrimRuntimeLoadOrder(
                PluginsPath:
                    Path.Combine(
                        dataRoot,
                        "plugins.txt"
                    ),
                LoadOrderPath:
                    Path.Combine(
                        dataRoot,
                        "loadorder.txt"
                    ),
                PluginsFileEntryCount:
                    plugins.Length,
                ExplicitlyActivePluginNames:
                    activePluginNames,
                LoadOrderEntries:
                    loadOrderEntries,
                MissingActivePlugins:
                    Array.Empty<string>(),
                DuplicatePluginsFileEntries:
                    Array.Empty<string>(),
                DuplicateLoadOrderEntries:
                    Array.Empty<string>(),
                RelativeOrderFailures:
                    Array.Empty<
                        SkyrimRuntimeLoadOrderOrderFailure
                    >()
            );

        SkyrimRuntimePluginSetEntry[] runtimeEntries =
            plugins
                .Select(plugin =>
                    new SkyrimRuntimePluginSetEntry(
                        LoadOrderIndex:
                            plugin.LoadOrderIndex,
                        PluginName:
                            plugin.PluginName,
                        ActivationSources:
                            SkyrimRuntimePluginActivationSource
                                .ExplicitPluginsTxt
                    )
                )
                .ToArray();

        return new SkyrimRuntimePluginSet(
            SourceLoadOrder:
                loadOrder,
            SkyrimCccPath:
                Path.Combine(
                    dataRoot,
                    "Skyrim.ccc"
                ),
            SkyrimCccEntryCount:
                0,
            SkyrimCccPluginNames:
                Array.Empty<string>(),
            LoadOrderEntries:
                runtimeEntries,
            MissingCoreMasters:
                Array.Empty<string>(),
            MissingSkyrimCccPlugins:
                Array.Empty<string>(),
            DuplicateSkyrimCccEntries:
                Array.Empty<string>()
        );
    }

    private static string CreateDataRoot()
    {
        string dataRoot =
            Path.Combine(
                Path.GetTempPath(),
                "casecompat-winning-headpart-" +
                Guid.NewGuid().ToString("N")
            );

        Directory.CreateDirectory(
            dataRoot
        );

        return dataRoot;
    }

    private static void DeleteDataRoot(
        string dataRoot)
    {
        if (Directory.Exists(dataRoot))
        {
            Directory.Delete(
                dataRoot,
                recursive:
                    true
            );
        }
    }
}
