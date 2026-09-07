using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.LoadOrder;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Assets;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Skyrim.Assets;

namespace CaseCompat.Tests;

public sealed class SkyrimWinningArmorAddonInventoryTests
{
    [Fact]
    public void Inspect_UsesHighestPriorityOverrideForRecordProviderAndReference()
    {
        string dataRoot =
            Path.Combine(
                Path.GetTempPath(),
                "casecompat-winning-armoraddon-" +
                Guid.NewGuid().ToString("N")
            );

        Directory.CreateDirectory(
            dataRoot
        );

        try
        {
            const string lowPluginName =
                "CaseCompatLow.esp";

            const string highPluginName =
                "CaseCompatHigh.esp";

            const string lowEditorId =
                "CaseCompatLowArmorAddon";

            const string highEditorId =
                "CaseCompatHighArmorAddon";

            const string lowRequestedPath =
                "Meshes/CaseCompat/LowWinner.nif";

            const string highRequestedPath =
                "Meshes/CaseCompat/HighWinner.nif";

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

            ArmorAddon lowRecord =
                lowMod.ArmorAddons.AddNew(
                    lowEditorId
                );

            lowRecord.WorldModel =
                new GenderedItem<Model?>(
                    new Model
                    {
                        File =
                            new AssetLink<
                                SkyrimModelAssetType
                            >(
                                lowRequestedPath
                            )
                    },
                    null
                );

            FormKey targetFormKey =
                lowRecord.FormKey;

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
                new ArmorAddon(
                    targetFormKey,
                    SkyrimRelease.SkyrimSE
                )
                {
                    EditorID =
                        highEditorId,
                    WorldModel =
                        new GenderedItem<Model?>(
                            new Model
                            {
                                File =
                                    new AssetLink<
                                        SkyrimModelAssetType
                                    >(
                                        highRequestedPath
                                    )
                            },
                            null
                        )
                };

            highMod.ArmorAddons.Set(
                highRecord
            );

            highMod.WriteToBinary(
                highPluginPath
            );

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
                        2,
                    ExplicitlyActivePluginNames:
                        new[]
                        {
                            lowPluginName,
                            highPluginName
                        },
                    LoadOrderEntries:
                        new[]
                        {
                            new SkyrimRuntimeLoadOrderEntry(
                                LoadOrderIndex:
                                    0,
                                PluginName:
                                    lowPluginName,
                                ExplicitlyActive:
                                    true
                            ),
                            new SkyrimRuntimeLoadOrderEntry(
                                LoadOrderIndex:
                                    1,
                                PluginName:
                                    highPluginName,
                                ExplicitlyActive:
                                    true
                            )
                        },
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

            var runtimePluginSet =
                new SkyrimRuntimePluginSet(
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
                        new[]
                        {
                            new SkyrimRuntimePluginSetEntry(
                                LoadOrderIndex:
                                    0,
                                PluginName:
                                    lowPluginName,
                                ActivationSources:
                                    SkyrimRuntimePluginActivationSource
                                        .ExplicitPluginsTxt
                            ),
                            new SkyrimRuntimePluginSetEntry(
                                LoadOrderIndex:
                                    1,
                                PluginName:
                                    highPluginName,
                                ActivationSources:
                                    SkyrimRuntimePluginActivationSource
                                        .ExplicitPluginsTxt
                            )
                        },
                    MissingCoreMasters:
                        Array.Empty<string>(),
                    MissingSkyrimCccPlugins:
                        Array.Empty<string>(),
                    DuplicateSkyrimCccEntries:
                        Array.Empty<string>()
                );

            SkyrimWinningArmorAddonInventoryResult result =
                SkyrimWinningArmorAddonInventory.Inspect(
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

            SkyrimWinningArmorAddonRecord winner =
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

            SkyrimArmorAddonModelReference reference =
                Assert.Single(
                    winner.ModelReferences
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
                "WorldModel.Male",
                reference.Field
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
}
