using CaseCompat.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Assets;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Skyrim.Assets;

namespace CaseCompat.Tests;

public sealed class SkyrimTreeModelReferenceExtractorTests
{
    [Fact]
    public void Extract_PresentModel_PreservesExactConsumerSpellingAndProvenance()
    {
        const string requestedPath =
            "Meshes/Tree/Common/Chair01.nif";

        Tree tree =
            CreateTree(
                "CaseCompatChair"
            );

        tree.Model =
            new Model
            {
                File =
                    new AssetLink<SkyrimModelAssetType>(
                        requestedPath
                    )
            };

        SkyrimTreeModelReference reference =
            Assert.Single(
                SkyrimTreeModelReferenceExtractor.Extract(
                    tree
                )
            );

        Assert.Equal(
            tree.FormKey.ToString(),
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
        Tree tree =
            CreateTree(
                "NoModel"
            );

        Assert.Empty(
            SkyrimTreeModelReferenceExtractor.Extract(
                tree
            )
        );
    }

    [Fact]
    public void Extract_NullAssetLink_IsSkipped()
    {
        Tree tree =
            CreateTree(
                "NullLink"
            );

        tree.Model =
            new Model();

        Assert.Empty(
            SkyrimTreeModelReferenceExtractor.Extract(
                tree
            )
        );
    }

    [Fact]
    public void Extract_NullTree_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                SkyrimTreeModelReferenceExtractor.Extract(
                    null!
                )
        );
    }

    private static Tree CreateTree(
        string editorId)
    {
        return new Tree(
            default,
            SkyrimRelease.SkyrimSE
        )
        {
            EditorID =
                editorId
        };
    }
}
