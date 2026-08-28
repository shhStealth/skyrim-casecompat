using CaseCompat.Core.LoadOrder;
using Xunit;

namespace CaseCompat.Tests;

public sealed class SkyrimRuntimeLoadOrderReaderTests
{
    [Fact]
    public void Read_UsesPluginsForActivationAndLoadOrderForOrdering()
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

        File.WriteAllText(
            pluginsPath,
            "\uFEFF# plugins\r\n" +
            "Disabled.esp\r\n" +
            "*First.esp\r\n" +
            "*[FB] Bishop Armor.esp\r\n" +
            "*Last.esp\r\n"
        );

        File.WriteAllText(
            loadOrderPath,
            "\uFEFF# load order\r\n" +
            "Skyrim.esm\r\n" +
            "First.esp\r\n" +
            "Disabled.esp\r\n" +
            "[FB] Bishop Armor.esp\r\n" +
            "Last.esp\r\n"
        );

        SkyrimRuntimeLoadOrder result =
            SkyrimRuntimeLoadOrderReader.Read(
                pluginsPath,
                loadOrderPath
            );

        Assert.Equal(
            4,
            result.PluginsFileEntryCount
        );

        Assert.Equal(
            3,
            result.ExplicitlyActiveCount
        );

        Assert.Equal(
            5,
            result.LoadOrderEntryCount
        );

        Assert.True(
            result.IsConsistent
        );

        Assert.Empty(
            result.MissingActivePlugins
        );

        Assert.Empty(
            result.RelativeOrderFailures
        );

        Assert.Equal(
            new[]
            {
                "First.esp",
                "[FB] Bishop Armor.esp",
                "Last.esp"
            },
            result.OrderedExplicitlyActiveEntries
                .Select(entry =>
                    entry.PluginName
                )
                .ToArray()
        );

        SkyrimRuntimeLoadOrderEntry bishop =
            Assert.Single(
                result.LoadOrderEntries,
                entry =>
                    entry.PluginName ==
                    "[FB] Bishop Armor.esp"
            );

        Assert.True(
            bishop.ExplicitlyActive
        );

        Assert.Equal(
            3,
            bishop.LoadOrderIndex
        );
    }

    [Fact]
    public void Read_ReportsMissingAndRelativeOrderFailures()
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

        File.WriteAllText(
            pluginsPath,
            "*B.esp\n" +
            "*A.esp\n" +
            "*Missing.esp\n"
        );

        File.WriteAllText(
            loadOrderPath,
            "A.esp\n" +
            "B.esp\n"
        );

        SkyrimRuntimeLoadOrder result =
            SkyrimRuntimeLoadOrderReader.Read(
                pluginsPath,
                loadOrderPath
            );

        Assert.False(
            result.IsConsistent
        );

        Assert.Equal(
            "Missing.esp",
            Assert.Single(
                result.MissingActivePlugins
            )
        );

        SkyrimRuntimeLoadOrderOrderFailure failure =
            Assert.Single(
                result.RelativeOrderFailures
            );

        Assert.Equal(
            "A.esp",
            failure.PluginName
        );

        Assert.Equal(
            0,
            failure.LoadOrderIndex
        );

        Assert.Equal(
            1,
            failure.PreviousLoadOrderIndex
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
