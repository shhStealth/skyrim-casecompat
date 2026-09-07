using CaseCompat.Core.Repair;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathAggregateConsumerPhysicalSpellingClassifierTests
{
    private const string BrowLogicalPath =
        "MESHES/ACTORS/CHARACTER/CHARACTER ASSETS/" +
        "FACEPARTS/MALEHEADBROWS.TRI";

    private const string BrowConsumerPath =
        "meshes/Actors/Character/Character Assets/" +
        "FaceParts/MaleHeadBrows.tri";

    [Fact]
    public void
        Classify_MaleHeadBrowsConsumerAgainstMaleHeadbrowsPhysical_IsMismatch()
    {
        DataRelativePathAggregateConsumerSpellingEvidence consumer =
            Consumer(
                BrowConsumerPath
            );

        DataRelativePathAggregateConsumerPhysicalSpellingEvidence evidence =
            DataRelativePathAggregateConsumerPhysicalSpellingClassifier
                .Classify(
                    consumer,
                    [
                        "meshes/actors/character/character assets/" +
                        "faceparts/MaleHeadbrows.tri"
                    ]
                );

        Assert.Equal(
            DataRelativePathAggregateConsumerPhysicalSpellingState
                .ConsumerCaseMismatch,
            evidence.State
        );

        Assert.Equal(
            BrowLogicalPath,
            evidence.WindowsLogicalPath
        );

        Assert.Equal(
            BrowConsumerPath,
            evidence.AuthoritativeRequestedPath
        );

        Assert.False(
            evidence.ExactPhysicalSpellingPresent
        );

        Assert.Equal(
            new[]
            {
                "meshes/actors/character/character assets/" +
                "faceparts/MaleHeadbrows.tri"
            },
            evidence.PhysicalRelativePaths
        );
    }

    [Fact]
    public void
        Classify_ExactPhysicalSpellingAmongMultipleRepresentations_IsPresent()
    {
        DataRelativePathAggregateConsumerSpellingEvidence consumer =
            Consumer(
                BrowConsumerPath
            );

        DataRelativePathAggregateConsumerPhysicalSpellingEvidence evidence =
            DataRelativePathAggregateConsumerPhysicalSpellingClassifier
                .Classify(
                    consumer,
                    [
                        "meshes/actors/character/character assets/" +
                        "faceparts/MaleHeadbrows.tri",

                        BrowConsumerPath
                    ]
                );

        Assert.Equal(
            DataRelativePathAggregateConsumerPhysicalSpellingState
                .ExactPhysicalSpellingPresent,
            evidence.State
        );

        Assert.True(
            evidence.ExactPhysicalSpellingPresent
        );

        Assert.Equal(
            2,
            evidence.PhysicalRelativePaths.Count
        );
    }

    [Fact]
    public void Classify_UniqueConsumerWithNoExactPhysicalSpelling_IsMismatch()
    {
        DataRelativePathAggregateConsumerSpellingEvidence consumer =
            DataRelativePathAggregateConsumerSpellingClassifier
                .Classify(
                    "MESHES/ACTORS/FIXTURE.NIF",
                    [
                        "meshes/Actors/Fixture.nif"
                    ]
                );

        DataRelativePathAggregateConsumerPhysicalSpellingEvidence evidence =
            DataRelativePathAggregateConsumerPhysicalSpellingClassifier
                .Classify(
                    consumer,
                    [
                        "meshes/actors/Fixture.nif",
                        "MESHES/ACTORS/FIXTURE.NIF"
                    ]
                );

        Assert.Equal(
            DataRelativePathAggregateConsumerPhysicalSpellingState
                .ConsumerCaseMismatch,
            evidence.State
        );

        Assert.Equal(
            2,
            evidence.PhysicalRelativePaths.Count
        );
    }

    [Fact]
    public void Classify_NoConsumerEvidence_IsRetained()
    {
        DataRelativePathAggregateConsumerSpellingEvidence consumer =
            DataRelativePathAggregateConsumerSpellingClassifier
                .Classify(
                    "MESHES/ACTORS/FIXTURE.NIF",
                    Array.Empty<string>()
                );

        DataRelativePathAggregateConsumerPhysicalSpellingEvidence evidence =
            DataRelativePathAggregateConsumerPhysicalSpellingClassifier
                .Classify(
                    consumer,
                    [
                        "meshes/actors/Fixture.nif"
                    ]
                );

        Assert.Equal(
            DataRelativePathAggregateConsumerPhysicalSpellingState
                .NoConsumerEvidence,
            evidence.State
        );

        Assert.Null(
            evidence.AuthoritativeRequestedPath
        );
    }

    [Fact]
    public void Classify_ConflictingConsumerSpellings_AreRetained()
    {
        DataRelativePathAggregateConsumerSpellingEvidence consumer =
            DataRelativePathAggregateConsumerSpellingClassifier
                .Classify(
                    "MESHES/ACTORS/FIXTURE.NIF",
                    [
                        "meshes/Actors/Fixture.nif",
                        "meshes/actors/Fixture.nif"
                    ]
                );

        DataRelativePathAggregateConsumerPhysicalSpellingEvidence evidence =
            DataRelativePathAggregateConsumerPhysicalSpellingClassifier
                .Classify(
                    consumer,
                    [
                        "meshes/actors/Fixture.nif"
                    ]
                );

        Assert.Equal(
            DataRelativePathAggregateConsumerPhysicalSpellingState
                .ConflictingConsumerSpellings,
            evidence.State
        );

        Assert.Null(
            evidence.AuthoritativeRequestedPath
        );
    }

    [Fact]
    public void Classify_PhysicalSeparatorVariantsCollapseWithoutChangingCase()
    {
        DataRelativePathAggregateConsumerSpellingEvidence consumer =
            DataRelativePathAggregateConsumerSpellingClassifier
                .Classify(
                    "MESHES/ACTORS/FIXTURE.NIF",
                    [
                        "meshes/Actors/Fixture.nif"
                    ]
                );

        DataRelativePathAggregateConsumerPhysicalSpellingEvidence evidence =
            DataRelativePathAggregateConsumerPhysicalSpellingClassifier
                .Classify(
                    consumer,
                    [
                        @"meshes\actors\Fixture.nif",
                        "meshes/actors/Fixture.nif"
                    ]
                );

        Assert.Single(
            evidence.PhysicalRelativePaths
        );

        Assert.Equal(
            "meshes/actors/Fixture.nif",
            evidence.PhysicalRelativePaths[0]
        );
    }

    [Fact]
    public void Classify_DifferentPhysicalLogicalLeaf_IsRejected()
    {
        DataRelativePathAggregateConsumerSpellingEvidence consumer =
            DataRelativePathAggregateConsumerSpellingClassifier
                .Classify(
                    "MESHES/ACTORS/FIXTURE.NIF",
                    [
                        "meshes/Actors/Fixture.nif"
                    ]
                );

        Assert.Throws<ArgumentException>(
            () =>
                DataRelativePathAggregateConsumerPhysicalSpellingClassifier
                    .Classify(
                        consumer,
                        [
                            "textures/actors/Fixture.dds"
                        ]
                    )
        );
    }

    [Fact]
    public void Classify_EmptyPhysicalRepresentationSet_IsRejected()
    {
        DataRelativePathAggregateConsumerSpellingEvidence consumer =
            DataRelativePathAggregateConsumerSpellingClassifier
                .Classify(
                    "MESHES/ACTORS/FIXTURE.NIF",
                    [
                        "meshes/Actors/Fixture.nif"
                    ]
                );

        Assert.Throws<ArgumentException>(
            () =>
                DataRelativePathAggregateConsumerPhysicalSpellingClassifier
                    .Classify(
                        consumer,
                        Array.Empty<string>()
                    )
        );
    }

    [Fact]
    public void Classify_NonCanonicalConsumerEvidence_IsRejected()
    {
        var consumer =
            new DataRelativePathAggregateConsumerSpellingEvidence(
                WindowsLogicalPath:
                    "MESHES/ACTORS/FIXTURE.NIF",
                DistinctRequestedPaths:
                [
                    "meshes/actors/Fixture.nif",
                    "meshes/Actors/Fixture.nif"
                ],
                State:
                    DataRelativePathAggregateConsumerSpellingState
                        .ConflictingConsumerSpellings
            );

        Assert.Throws<ArgumentException>(
            () =>
                DataRelativePathAggregateConsumerPhysicalSpellingClassifier
                    .Classify(
                        consumer,
                        [
                            "meshes/actors/Fixture.nif"
                        ]
                    )
        );
    }

    private static DataRelativePathAggregateConsumerSpellingEvidence Consumer(
        string requestedPath)
    {
        return
            DataRelativePathAggregateConsumerSpellingClassifier
                .Classify(
                    BrowLogicalPath,
                    [
                        requestedPath
                    ]
                );
    }
}
