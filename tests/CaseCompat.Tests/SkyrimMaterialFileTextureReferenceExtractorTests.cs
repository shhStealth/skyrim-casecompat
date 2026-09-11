using CaseCompat.Bethesda.Assets;

namespace CaseCompat.Tests;

// Fixture files under Fixtures/MaterialSamples/ are the "Default" .bgsm
// and .bgem test files from ousnius/Material-Editor's own MIT-licensed
// test suite (MaterialLib.Tests/Files), used here purely as small,
// authoritative real-format sample data - not as source code. They cover
// both the version-2 and version-21 field layouts, which is exactly the
// version-gating branch this extractor's header-skip logic depends on.
public sealed class SkyrimMaterialFileTextureReferenceExtractorTests
{
    private static string FixturePath(
        string fileName)
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "MaterialSamples",
            fileName
        );
    }

    [Theory]
    [InlineData(
        "Version_2_Default.bgsm",
        "DiffuseTexture",
        "Shared/FlatGray01_d.dds")]
    [InlineData(
        "Version_21_Default.bgsm",
        "DiffuseTexture",
        "Shared/FlatGray01_d.dds")]
    [InlineData(
        "Version_2_Default.bgsm",
        "NormalTexture",
        "Shared/FlatFlat_n.dds")]
    [InlineData(
        "Version_21_Default.bgsm",
        "NormalTexture",
        "Shared/FlatFlat_n.dds")]
    [InlineData(
        "Version_2_DefaultEffect.bgem",
        "BaseTexture",
        "Shared/FlatGray01_d.dds")]
    [InlineData(
        "Version_21_DefaultEffect.bgem",
        "BaseTexture",
        "Shared/FlatGray01_d.dds")]
    public void Extract_RealFixture_FindsExpectedTextureSlot(
        string fixtureFileName,
        string expectedSlot,
        string expectedPath)
    {
        string path =
            FixturePath(
                fixtureFileName
            );

        using FileStream stream =
            File.OpenRead(
                path
            );

        IReadOnlyList<SkyrimMaterialFileTextureReference> references =
            SkyrimMaterialFileTextureReferenceExtractor.Extract(
                stream,
                path
            );

        SkyrimMaterialFileTextureReference? match =
            references.FirstOrDefault(
                r =>
                    r.SlotName == expectedSlot
            );

        Assert.NotNull(
            match
        );

        Assert.Equal(
            expectedPath,
            match!.GivenPath
        );

        Assert.Equal(
            path,
            match.MaterialPhysicalPath
        );
    }

    [Fact]
    public void Extract_TruncatedFile_Throws()
    {
        // A truncated/corrupted material file is expected to throw here -
        // Extract has no defensive fallback of its own for a malformed
        // stream, matching the real BaseMaterialFile format it mirrors
        // (the common header is unconditional, regardless of signature).
        // Callers are responsible for the "one bad leaf" tolerance - see
        // SkyrimWinningMaterialTextureInventory.Inspect, which is what
        // actually needs to survive this.
        using var stream =
            new MemoryStream(
                [
                    0x42, 0x47, 0x53, 0x4D,
                    0x02, 0x00, 0x00, 0x00
                ]
            );

        Assert.ThrowsAny<Exception>(
            () =>
                SkyrimMaterialFileTextureReferenceExtractor.Extract(
                    stream,
                    "unused.bgsm"
                )
        );
    }
}
