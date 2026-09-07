using CaseCompat.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;

namespace CaseCompat.Tests;

public sealed class SkyrimHeadPartPartReferenceExtractorTests
{
    [Fact]
    public void Extract_TriPart_PreservesExactConsumerSpellingAndProvenance()
    {
        const string requestedPath =
            "Meshes/Actors/Character/Character Assets/FaceParts/" +
            "MaleHeadBrows.tri";

        HeadPart headPart =
            CreateHeadPart(
                "MaleBrows"
            );

        headPart.Parts.Add(
            new Part
            {
                PartType =
                    Part.PartTypeEnum.Tri,
                FileName =
                    requestedPath
            }
        );

        SkyrimHeadPartPartReference reference =
            Assert.Single(
                SkyrimHeadPartPartReferenceExtractor.Extract(
                    headPart
                )
            );

        Assert.Equal(
            headPart.FormKey.ToString(),
            reference.FormKey
        );

        Assert.Equal(
            "MaleBrows",
            reference.EditorId
        );

        Assert.Equal(
            0,
            reference.PartIndex
        );

        Assert.Equal(
            Part.PartTypeEnum.Tri,
            reference.PartType
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
    public void Extract_AllThreePartTypes_AreRetainedInSourceOrder()
    {
        HeadPart headPart =
            CreateHeadPart(
                "AllPartTypes"
            );

        headPart.Parts.Add(
            new Part
            {
                PartType =
                    Part.PartTypeEnum.RaceMorph,
                FileName =
                    "Meshes/Actors/Character/Character Assets/FaceParts/" +
                    "MaleHead.tri"
            }
        );

        headPart.Parts.Add(
            new Part
            {
                PartType =
                    Part.PartTypeEnum.Tri,
                FileName =
                    "Meshes/Actors/Character/Character Assets/FaceParts/" +
                    "MaleHeadBrows.tri"
            }
        );

        headPart.Parts.Add(
            new Part
            {
                PartType =
                    Part.PartTypeEnum.ChargenMorph,
                FileName =
                    "Meshes/Actors/Character/Character Assets/FaceParts/" +
                    "MaleHeadCharGen.tri"
            }
        );

        IReadOnlyList<SkyrimHeadPartPartReference> references =
            SkyrimHeadPartPartReferenceExtractor.Extract(
                headPart
            );

        Assert.Equal(
            3,
            references.Count
        );

        Assert.Equal(
            new[]
            {
                Part.PartTypeEnum.RaceMorph,
                Part.PartTypeEnum.Tri,
                Part.PartTypeEnum.ChargenMorph
            },
            references
                .Select(
                    reference =>
                        reference.PartType!.Value
                )
                .ToArray()
        );

        Assert.Equal(
            new[]
            {
                0,
                1,
                2
            },
            references
                .Select(
                    reference =>
                        reference.PartIndex
                )
                .ToArray()
        );
    }

    [Fact]
    public void Extract_NullPartType_IsRetainedAsProvenance()
    {
        HeadPart headPart =
            CreateHeadPart(
                "NullPartType"
            );

        headPart.Parts.Add(
            new Part
            {
                PartType =
                    null,
                FileName =
                    "Meshes/Actors/Character/Character Assets/FaceParts/" +
                    "UnknownPart.tri"
            }
        );

        SkyrimHeadPartPartReference reference =
            Assert.Single(
                SkyrimHeadPartPartReferenceExtractor.Extract(
                    headPart
                )
            );

        Assert.Null(
            reference.PartType
        );

        Assert.Equal(
            0,
            reference.PartIndex
        );
    }

    [Fact]
    public void Extract_NullAssetLink_IsSkipped()
    {
        HeadPart headPart =
            CreateHeadPart(
                "NullLink"
            );

        headPart.Parts.Add(
            new Part
            {
                PartType =
                    Part.PartTypeEnum.Tri
            }
        );

        Assert.Empty(
            SkyrimHeadPartPartReferenceExtractor.Extract(
                headPart
            )
        );
    }

    [Fact]
    public void Extract_SkippedPart_DoesNotRenumberLaterPartIndex()
    {
        HeadPart headPart =
            CreateHeadPart(
                "StableIndex"
            );

        headPart.Parts.Add(
            new Part
            {
                PartType =
                    Part.PartTypeEnum.Tri
            }
        );

        headPart.Parts.Add(
            new Part
            {
                PartType =
                    Part.PartTypeEnum.ChargenMorph,
                FileName =
                    "Meshes/Actors/Character/Character Assets/FaceParts/" +
                    "FemaleHeadCharGen.tri"
            }
        );

        SkyrimHeadPartPartReference reference =
            Assert.Single(
                SkyrimHeadPartPartReferenceExtractor.Extract(
                    headPart
                )
            );

        Assert.Equal(
            1,
            reference.PartIndex
        );

        Assert.Equal(
            Part.PartTypeEnum.ChargenMorph,
            reference.PartType
        );
    }

    [Fact]
    public void Extract_EmptyParts_ProducesEmptyResult()
    {
        HeadPart headPart =
            CreateHeadPart(
                "Empty"
            );

        Assert.Empty(
            SkyrimHeadPartPartReferenceExtractor.Extract(
                headPart
            )
        );
    }

    [Fact]
    public void Extract_NullHeadPart_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                SkyrimHeadPartPartReferenceExtractor.Extract(
                    null!
                )
        );
    }

    private static HeadPart CreateHeadPart(
        string editorId)
    {
        return new HeadPart(
            default,
            SkyrimRelease.SkyrimSE
        )
        {
            EditorID =
                editorId
        };
    }
}
