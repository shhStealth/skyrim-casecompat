using CaseCompat.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Assets;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Skyrim.Assets;

namespace CaseCompat.Tests;

public sealed class SkyrimContainerModelReferenceExtractorTests
{
    [Fact]
    public void Extract_PresentModel_PreservesExactConsumerSpellingAndProvenance()
    {
        const string requestedPath =
            "Meshes/Container/Common/Chair01.nif";

        Container container =
            CreateContainer(
                "CaseCompatChair"
            );

        container.Model =
            new Model
            {
                File =
                    new AssetLink<SkyrimModelAssetType>(
                        requestedPath
                    )
            };

        SkyrimContainerModelReference reference =
            Assert.Single(
                SkyrimContainerModelReferenceExtractor.Extract(
                    container
                )
            );

        Assert.Equal(
            container.FormKey.ToString(),
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
        Container container =
            CreateContainer(
                "NoModel"
            );

        Assert.Empty(
            SkyrimContainerModelReferenceExtractor.Extract(
                container
            )
        );
    }

    [Fact]
    public void Extract_NullAssetLink_IsSkipped()
    {
        Container container =
            CreateContainer(
                "NullLink"
            );

        container.Model =
            new Model();

        Assert.Empty(
            SkyrimContainerModelReferenceExtractor.Extract(
                container
            )
        );
    }

    [Fact]
    public void Extract_NullContainer_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                SkyrimContainerModelReferenceExtractor.Extract(
                    null!
                )
        );
    }

    private static Container CreateContainer(
        string editorId)
    {
        return new Container(
            default,
            SkyrimRelease.SkyrimSE
        )
        {
            EditorID =
                editorId
        };
    }
}
