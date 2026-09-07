using CaseCompat.Core.Repair;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathAggregateConsumerSpellingClassifierTests
{
    [Fact]
    public void Classify_NoRequestedPaths_HasNoConsumerEvidence()
    {
        DataRelativePathAggregateConsumerSpellingEvidence evidence =
            DataRelativePathAggregateConsumerSpellingClassifier
                .Classify(
                    "MESHES/ACTORS/FIXTURE.NIF",
                    Array.Empty<string>()
                );

        Assert.Equal(
            DataRelativePathAggregateConsumerSpellingState
                .NoConsumerEvidence,
            evidence.State
        );

        Assert.Empty(
            evidence.DistinctRequestedPaths
        );

        Assert.Null(
            evidence.AuthoritativeRequestedPath
        );
    }

    [Fact]
    public void Classify_RepeatedExactSpelling_IsUnique()
    {
        DataRelativePathAggregateConsumerSpellingEvidence evidence =
            DataRelativePathAggregateConsumerSpellingClassifier
                .Classify(
                    "MESHES/ACTORS/FIXTURE.NIF",
                    [
                        "Meshes/Actors/Fixture.nif",
                        "Meshes/Actors/Fixture.nif"
                    ]
                );

        Assert.Equal(
            DataRelativePathAggregateConsumerSpellingState
                .UniqueConsumerSpelling,
            evidence.State
        );

        Assert.Equal(
            new[]
            {
                "Meshes/Actors/Fixture.nif"
            },
            evidence.DistinctRequestedPaths
        );

        Assert.Equal(
            "Meshes/Actors/Fixture.nif",
            evidence.AuthoritativeRequestedPath
        );
    }

    [Fact]
    public void Classify_SeparatorVariantsCollapseWithoutChangingCase()
    {
        DataRelativePathAggregateConsumerSpellingEvidence evidence =
            DataRelativePathAggregateConsumerSpellingClassifier
                .Classify(
                    "MESHES/ACTORS/FIXTURE.NIF",
                    [
                        @"Meshes\Actors\Fixture.nif",
                        "Meshes/Actors/Fixture.nif"
                    ]
                );

        Assert.Equal(
            DataRelativePathAggregateConsumerSpellingState
                .UniqueConsumerSpelling,
            evidence.State
        );

        Assert.Equal(
            "Meshes/Actors/Fixture.nif",
            evidence.AuthoritativeRequestedPath
        );
    }

    [Fact]
    public void Classify_CaseDistinctSpellings_Conflict()
    {
        DataRelativePathAggregateConsumerSpellingEvidence evidence =
            DataRelativePathAggregateConsumerSpellingClassifier
                .Classify(
                    "MESHES/ACTORS/FIXTURE.NIF",
                    [
                        "Meshes/Actors/Fixture.nif",
                        "meshes/actors/fixture.nif"
                    ]
                );

        Assert.Equal(
            DataRelativePathAggregateConsumerSpellingState
                .ConflictingConsumerSpellings,
            evidence.State
        );

        Assert.Equal(
            new[]
            {
                "Meshes/Actors/Fixture.nif",
                "meshes/actors/fixture.nif"
            },
            evidence.DistinctRequestedPaths
        );

        Assert.Null(
            evidence.AuthoritativeRequestedPath
        );
    }

    [Fact]
    public void Classify_DifferentLogicalLeaf_IsRejected()
    {
        Assert.Throws<ArgumentException>(
            () =>
                DataRelativePathAggregateConsumerSpellingClassifier
                    .Classify(
                        "MESHES/ACTORS/FIXTURE.NIF",
                        [
                            "Textures/Actors/Fixture.dds"
                        ]
                    )
        );
    }

    [Fact]
    public void Classify_InvalidRequestedPath_IsRejected()
    {
        Assert.Throws<ArgumentException>(
            () =>
                DataRelativePathAggregateConsumerSpellingClassifier
                    .Classify(
                        "MESHES/ACTORS/FIXTURE.NIF",
                        [
                            "Meshes/../Fixture.nif"
                        ]
                    )
        );
    }

    [Fact]
    public void Classify_NonCanonicalLogicalPath_IsRejected()
    {
        Assert.Throws<ArgumentException>(
            () =>
                DataRelativePathAggregateConsumerSpellingClassifier
                    .Classify(
                        "meshes/actors/fixture.nif",
                        [
                            "Meshes/Actors/Fixture.nif"
                        ]
                    )
        );
    }
}
