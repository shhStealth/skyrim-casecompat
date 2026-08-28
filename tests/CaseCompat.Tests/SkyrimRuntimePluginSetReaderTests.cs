using CaseCompat.Core.LoadOrder;
using Xunit;

namespace CaseCompat.Tests;

public sealed class SkyrimRuntimePluginSetReaderTests
{
    [Fact]
    public void Read_CombinesCoreCccAndExplicitActivation()
    {
        using var temp =
            new TemporaryDirectory();

        string pluginsPath =
            Path.Combine(
                temp.RootPath,
                "Plugins.txt"
            );

        string loadOrderPath =
            Path.Combine(
                temp.RootPath,
                "loadorder.txt"
            );

        string cccPath =
            Path.Combine(
                temp.RootPath,
                "Skyrim.ccc"
            );

        File.WriteAllText(
            pluginsPath,
            "Disabled.esp\n" +
            "*ccExample.esl\n" +
            "*Explicit.esp\n"
        );

        File.WriteAllText(
            loadOrderPath,
            "Skyrim.esm\n" +
            "Update.esm\n" +
            "Dawnguard.esm\n" +
            "HearthFires.esm\n" +
            "Dragonborn.esm\n" +
            "ccExample.esl\n" +
            "Explicit.esp\n" +
            "Disabled.esp\n"
        );

        File.WriteAllText(
            cccPath,
            "\uFEFF# Creation content\n" +
            "ccExample.esl"
        );

        SkyrimRuntimeLoadOrder loadOrder =
            SkyrimRuntimeLoadOrderReader.Read(
                pluginsPath,
                loadOrderPath
            );

        SkyrimRuntimePluginSet result =
            SkyrimRuntimePluginSetReader.Read(
                loadOrder,
                cccPath
            );

        Assert.True(
            result.IsConsistent
        );

        Assert.Equal(
            7,
            result.RuntimeActiveCount
        );

        Assert.Equal(
            1,
            result.LoadOrderOnlyCount
        );

        SkyrimRuntimePluginSetEntry ccc =
            Assert.Single(
                result.LoadOrderEntries,
                entry =>
                    entry.PluginName ==
                    "ccExample.esl"
            );

        Assert.True(
            ccc.IsActivatedBy(
                SkyrimRuntimePluginActivationSource
                    .SkyrimCcc
            )
        );

        Assert.True(
            ccc.IsActivatedBy(
                SkyrimRuntimePluginActivationSource
                    .ExplicitPluginsTxt
            )
        );

        SkyrimRuntimePluginSetEntry skyrim =
            Assert.Single(
                result.LoadOrderEntries,
                entry =>
                    entry.PluginName ==
                    "Skyrim.esm"
            );

        Assert.True(
            skyrim.IsActivatedBy(
                SkyrimRuntimePluginActivationSource
                    .CoreMaster
            )
        );

        Assert.Equal(
            "Disabled.esp",
            Assert.Single(
                result.LoadOrderOnlyEntries
            ).PluginName
        );
    }

    [Fact]
    public void Read_ReportsMissingCoreCccAndDuplicateCccEntries()
    {
        using var temp =
            new TemporaryDirectory();

        string pluginsPath =
            Path.Combine(
                temp.RootPath,
                "Plugins.txt"
            );

        string loadOrderPath =
            Path.Combine(
                temp.RootPath,
                "loadorder.txt"
            );

        string cccPath =
            Path.Combine(
                temp.RootPath,
                "Skyrim.ccc"
            );

        File.WriteAllText(
            pluginsPath,
            "*Explicit.esp\n"
        );

        File.WriteAllText(
            loadOrderPath,
            "Skyrim.esm\n" +
            "Update.esm\n" +
            "Dawnguard.esm\n" +
            "HearthFires.esm\n" +
            "Explicit.esp\n"
        );

        File.WriteAllText(
            cccPath,
            "ccMissing.esl\n" +
            "ccMissing.esl\n"
        );

        SkyrimRuntimeLoadOrder loadOrder =
            SkyrimRuntimeLoadOrderReader.Read(
                pluginsPath,
                loadOrderPath
            );

        SkyrimRuntimePluginSet result =
            SkyrimRuntimePluginSetReader.Read(
                loadOrder,
                cccPath
            );

        Assert.False(
            result.IsConsistent
        );

        Assert.Equal(
            "Dragonborn.esm",
            Assert.Single(
                result.MissingCoreMasters
            )
        );

        Assert.Equal(
            "ccMissing.esl",
            Assert.Single(
                result.MissingSkyrimCccPlugins
            )
        );

        Assert.Equal(
            "ccMissing.esl",
            Assert.Single(
                result.DuplicateSkyrimCccEntries
            )
        );
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
