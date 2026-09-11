using CaseCompat.Bethesda.Assets;

namespace CaseCompat.Tests;

// Regression coverage for the "one bad leaf doesn't abort the whole
// inventory" fix: a real install crashed an earlier version of this
// pipeline's mesh counterpart with an unhandled parser exception deep
// inside a third-party library. A corrupted/truncated material file must
// be tolerated the same way - zero references for that one file, not an
// inventory-wide failure.
public sealed class SkyrimWinningMaterialTextureInventoryTests
{
    [Fact]
    public void Inspect_CorruptedMaterialFile_DoesNotFailWholeInventory()
    {
        string tempDirectory =
            Directory.CreateTempSubdirectory(
                "casecompat-material-inventory-test-"
            ).FullName;

        try
        {
            string corruptedPath =
                Path.Combine(
                    tempDirectory,
                    "corrupted.bgsm"
                );

            File.WriteAllBytes(
                corruptedPath,
                [
                    0x42, 0x47, 0x53, 0x4D,
                    0x02, 0x00, 0x00, 0x00
                ]
            );

            SkyrimWinningMaterialTextureInventoryResult result =
                SkyrimWinningMaterialTextureInventory.Inspect(
                    tempDirectory,
                    [corruptedPath]
                );

            Assert.True(
                result.SearchComplete
            );

            Assert.Empty(
                result.ReadErrors
            );

            Assert.Empty(
                result.References
            );
        }
        finally
        {
            Directory.Delete(
                tempDirectory,
                recursive: true
            );
        }
    }

    [Fact]
    public void Inspect_MissingMaterialFile_RecordsReadError()
    {
        string tempDirectory =
            Directory.CreateTempSubdirectory(
                "casecompat-material-inventory-test-"
            ).FullName;

        try
        {
            string missingPath =
                Path.Combine(
                    tempDirectory,
                    "does-not-exist.bgsm"
                );

            SkyrimWinningMaterialTextureInventoryResult result =
                SkyrimWinningMaterialTextureInventory.Inspect(
                    tempDirectory,
                    [missingPath]
                );

            Assert.False(
                result.SearchComplete
            );

            Assert.Single(
                result.ReadErrors
            );

            Assert.Equal(
                missingPath,
                result.ReadErrors[0].MaterialPhysicalPath
            );
        }
        finally
        {
            Directory.Delete(
                tempDirectory,
                recursive: true
            );
        }
    }

    [Fact]
    public void Inspect_RealFixture_ExtractsExpectedReference()
    {
        string fixturePath =
            Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                "MaterialSamples",
                "Version_2_Default.bgsm"
            );

        SkyrimWinningMaterialTextureInventoryResult result =
            SkyrimWinningMaterialTextureInventory.Inspect(
                Path.GetDirectoryName(
                    fixturePath
                )!,
                [fixturePath]
            );

        Assert.True(
            result.SearchComplete
        );

        Assert.Contains(
            result.References,
            reference =>
                reference.SlotName == "DiffuseTexture" &&
                reference.GivenPath == "Shared/FlatGray01_d.dds"
        );
    }
}
