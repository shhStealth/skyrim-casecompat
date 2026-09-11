using CaseCompat.Bethesda.Assets;

namespace CaseCompat.Tests;

// Covers TryGetUsablePath's normalization/filtering rules directly - each
// case here reproduces a real degenerate value found on a real install's
// meshes (via a full real-install scan), not a hypothetical edge case.
public sealed class SkyrimNifTextureReferenceExtractorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\n")]
    [InlineData("textures/")]
    [InlineData("textures\\")]
    [InlineData("nor")]
    [InlineData("NOR")]
    public void TryGetUsablePath_DegenerateValue_ReturnsFalse(
        string? content)
    {
        bool usable =
            SkyrimNifTextureReferenceExtractor.TryGetUsablePath(
                content,
                out string normalized
            );

        Assert.False(
            usable
        );

        Assert.Equal(
            string.Empty,
            normalized
        );
    }

    [Theory]
    [InlineData(
        "textures/architecture/whiterun/wrwalls01.dds",
        "textures/architecture/whiterun/wrwalls01.dds")]
    [InlineData(
        @"textures\architecture\whiterun\wrwalls01.dds",
        "textures/architecture/whiterun/wrwalls01.dds")]
    [InlineData(
        "textures//chaosdragons//effects//spiritworm.dds",
        "textures/chaosdragons/effects/spiritworm.dds")]
    [InlineData(
        "textures/chaosdragons/effects//dragonwind.dds",
        "textures/chaosdragons/effects/dragonwind.dds")]
    [InlineData(
        "/textures/aaaamv/furniture/barset/metal_e.dds",
        "textures/aaaamv/furniture/barset/metal_e.dds")]
    [InlineData(
        "textures//nimDD/pulcharmsolis/colovianprince/glovesboots_n.dds",
        "textures/nimDD/pulcharmsolis/colovianprince/glovesboots_n.dds")]
    public void TryGetUsablePath_RealWorldValue_NormalizesCorrectly(
        string content,
        string expectedNormalized)
    {
        bool usable =
            SkyrimNifTextureReferenceExtractor.TryGetUsablePath(
                content,
                out string normalized
            );

        Assert.True(
            usable
        );

        Assert.Equal(
            expectedNormalized,
            normalized
        );
    }
}
