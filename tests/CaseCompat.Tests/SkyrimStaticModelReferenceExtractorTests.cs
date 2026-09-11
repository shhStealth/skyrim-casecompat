using CaseCompat.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Assets;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Skyrim.Assets;

namespace CaseCompat.Tests;

public sealed class SkyrimStaticModelReferenceExtractorTests
{
    [Fact]
    public void Extract_PresentModel_PreservesExactConsumerSpellingAndProvenance()
    {
        const string requestedPath =
            "Meshes/Static/Common/Chair01.nif";

        Static staticRecord =
            CreateStatic(
                "CaseCompatChair"
            );

        staticRecord.Model =
            new Model
            {
                File =
                    new AssetLink<SkyrimModelAssetType>(
                        requestedPath
                    )
            };

        SkyrimStaticModelReference reference =
            Assert.Single(
                SkyrimStaticModelReferenceExtractor.Extract(
                    staticRecord
                )
            );

        Assert.Equal(
            staticRecord.FormKey.ToString(),
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
        Static staticRecord =
            CreateStatic(
                "NoModel"
            );

        Assert.Empty(
            SkyrimStaticModelReferenceExtractor.Extract(
                staticRecord
            )
        );
    }

    [Fact]
    public void Extract_NullAssetLink_IsSkipped()
    {
        Static staticRecord =
            CreateStatic(
                "NullLink"
            );

        staticRecord.Model =
            new Model();

        Assert.Empty(
            SkyrimStaticModelReferenceExtractor.Extract(
                staticRecord
            )
        );
    }

    [Fact]
    public void Extract_NullStatic_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                SkyrimStaticModelReferenceExtractor.Extract(
                    null!
                )
        );
    }

    private static Static CreateStatic(
        string editorId)
    {
        return new Static(
            default,
            SkyrimRelease.SkyrimSE
        )
        {
            EditorID =
                editorId
        };
    }
}
