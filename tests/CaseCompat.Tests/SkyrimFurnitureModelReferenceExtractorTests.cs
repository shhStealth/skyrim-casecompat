using CaseCompat.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Assets;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Skyrim.Assets;

namespace CaseCompat.Tests;

public sealed class SkyrimFurnitureModelReferenceExtractorTests
{
    [Fact]
    public void Extract_PresentModel_PreservesExactConsumerSpellingAndProvenance()
    {
        const string requestedPath =
            "Meshes/Furniture/Common/Chair01.nif";

        Furniture furniture =
            CreateFurniture(
                "CaseCompatChair"
            );

        furniture.Model =
            new Model
            {
                File =
                    new AssetLink<SkyrimModelAssetType>(
                        requestedPath
                    )
            };

        SkyrimFurnitureModelReference reference =
            Assert.Single(
                SkyrimFurnitureModelReferenceExtractor.Extract(
                    furniture
                )
            );

        Assert.Equal(
            furniture.FormKey.ToString(),
            reference.FormKey
        );

        Assert.Equal(
            "CaseCompatChair",
            reference.EditorId
        );

        Assert.Equal(
            "Model",
            reference.Field
        );

        Assert.Equal(
            requestedPath,
            reference.GivenPath
        );

        Assert.Equal(
            requestedPath,
            reference.DataRelativePath
        );
    }

    [Fact]
    public void Extract_NullModel_ProducesEmptyResult()
    {
        Furniture furniture =
            CreateFurniture(
                "NoModel"
            );

        Assert.Empty(
            SkyrimFurnitureModelReferenceExtractor.Extract(
                furniture
            )
        );
    }

    [Fact]
    public void Extract_NullAssetLink_IsSkipped()
    {
        Furniture furniture =
            CreateFurniture(
                "NullLink"
            );

        furniture.Model =
            new Model();

        Assert.Empty(
            SkyrimFurnitureModelReferenceExtractor.Extract(
                furniture
            )
        );
    }

    [Fact]
    public void Extract_NullFurniture_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                SkyrimFurnitureModelReferenceExtractor.Extract(
                    null!
                )
        );
    }

    private static Furniture CreateFurniture(
        string editorId)
    {
        return new Furniture(
            default,
            SkyrimRelease.SkyrimSE
        )
        {
            EditorID =
                editorId
        };
    }
}
