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

    // Proves the cache is actually consulted (not merely that a rename
    // still parses correctly on its own): after the first Inspect call
    // populates the cache and the file is renamed, the file's bytes at
    // its new name are corrupted in place (same inode, truncated
    // content) before the second Inspect call. A second call that
    // genuinely re-parsed would see the corrupted bytes and yield zero
    // references (matching Inspect_CorruptedMaterialFile_DoesNotFailWholeInventory
    // above); a cache hit still returns the original real reference,
    // re-stamped to the new path - which is what this test requires.
    [Fact]
    public void Inspect_SameCacheAcrossRename_ReusesCachedExtractionByInode()
    {
        string tempDirectory =
            Directory.CreateTempSubdirectory(
                "casecompat-material-cache-test-"
            ).FullName;

        try
        {
            string fixturePath =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "Fixtures",
                    "MaterialSamples",
                    "Version_2_Default.bgsm"
                );

            string firstPath =
                Path.Combine(
                    tempDirectory,
                    "material1.bgsm"
                );

            File.Copy(
                fixturePath,
                firstPath
            );

            var cache =
                new SkyrimMaterialTextureExtractionCache();

            SkyrimWinningMaterialTextureInventoryResult firstResult =
                SkyrimWinningMaterialTextureInventory.Inspect(
                    tempDirectory,
                    [firstPath],
                    cache
                );

            Assert.Contains(
                firstResult.References,
                reference =>
                    reference.SlotName == "DiffuseTexture" &&
                    reference.GivenPath == "Shared/FlatGray01_d.dds"
            );

            string secondPath =
                Path.Combine(
                    tempDirectory,
                    "material2.bgsm"
                );

            File.Move(
                firstPath,
                secondPath
            );

            // Truncate the same inode's content in place - a real
            // re-parse of this path would now fail/yield nothing.
            using (var truncate =
                new FileStream(
                    secondPath,
                    FileMode.Truncate,
                    FileAccess.Write))
            {
                truncate.Write(
                    [0x00, 0x00, 0x00, 0x00]
                );
            }

            SkyrimWinningMaterialTextureInventoryResult secondResult =
                SkyrimWinningMaterialTextureInventory.Inspect(
                    tempDirectory,
                    [secondPath],
                    cache
                );

            Assert.True(
                secondResult.SearchComplete
            );

            SkyrimMaterialFileTextureReference match =
                Assert.Single(
                    secondResult.References,
                    reference =>
                        reference.SlotName == "DiffuseTexture"
                );

            Assert.Equal(
                "Shared/FlatGray01_d.dds",
                match.GivenPath
            );

            Assert.Equal(
                secondPath,
                match.MaterialPhysicalPath
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
}
