using CaseCompat.Core.Analysis;

namespace CaseCompat.Tests;

public sealed class WindowsDataRelativePathParserTests
{
    [Fact]
    public void TryParse_PreservesComponentSpelling()
    {
        bool success =
            WindowsDataRelativePathParser.TryParse(
                "Meshes/Armor/Foo.NIF",
                out string[] components,
                out string? error
            );

        Assert.True(success);
        Assert.Null(error);

        Assert.Equal(
            new[]
            {
                "Meshes",
                "Armor",
                "Foo.NIF"
            },
            components
        );
    }

    [Fact]
    public void TryParse_BackslashSeparatorsPreserveComponents()
    {
        bool success =
            WindowsDataRelativePathParser.TryParse(
                @"Meshes\Armor\Foo.NIF",
                out string[] components,
                out string? error
            );

        Assert.True(success);
        Assert.Null(error);

        Assert.Equal(
            new[]
            {
                "Meshes",
                "Armor",
                "Foo.NIF"
            },
            components
        );
    }

    [Fact]
    public void TryParse_RejectsUnsafeOrMalformedPaths()
    {
        string?[] invalid =
        {
            null,
            "",
            " ",
            "\0",
            "/Meshes/Foo.nif",
            "Meshes/Foo.nif/",
            "Meshes//Foo.nif",
            @"Meshes\\Foo.nif",
            "Meshes/./Foo.nif",
            "Meshes/../Foo.nif"
        };

        foreach (string? requestedPath in invalid)
        {
            bool success =
                WindowsDataRelativePathParser.TryParse(
                    requestedPath,
                    out string[] components,
                    out string? error
                );

            Assert.False(success);
            Assert.Empty(components);
            Assert.False(
                string.IsNullOrWhiteSpace(error)
            );
        }
    }
}
