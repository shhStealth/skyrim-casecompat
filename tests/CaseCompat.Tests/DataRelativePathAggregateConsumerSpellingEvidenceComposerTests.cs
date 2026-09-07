using CaseCompat.Core.Repair;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathAggregateConsumerSpellingEvidenceComposerTests
{
    [Fact]
    public void Compose_EmptySourceCollectionProducesEmptyEvidence()
    {
        IReadOnlyList<
            DataRelativePathAggregateConsumerSpellingEvidence
        > result =
            DataRelativePathAggregateConsumerSpellingEvidenceComposer
                .Compose(
                    Array.Empty<
                        IReadOnlyList<
                            DataRelativePathAggregateConsumerSpellingEvidence
                        >
                    >()
                );

        Assert.Empty(result);
    }

    [Fact]
    public void Compose_SingleCanonicalSourcePreservesEvidenceSemantics()
    {
        DataRelativePathAggregateConsumerSpellingEvidence sourceEvidence =
            Consumer(
                "MESHES/ACTORS/HEAD.TRI",
                "Meshes/Actors/Head.tri"
            );

        IReadOnlyList<
            DataRelativePathAggregateConsumerSpellingEvidence
        > result =
            Compose(
                new[]
                {
                    sourceEvidence
                }
            );

        DataRelativePathAggregateConsumerSpellingEvidence composed =
            Assert.Single(
                result
            );

        Assert.Equal(
            sourceEvidence.WindowsLogicalPath,
            composed.WindowsLogicalPath
        );

        Assert.Equal(
            sourceEvidence.State,
            composed.State
        );

        Assert.Equal(
            sourceEvidence.DistinctRequestedPaths,
            composed.DistinctRequestedPaths
        );

        Assert.Equal(
            "Meshes/Actors/Head.tri",
            composed.AuthoritativeRequestedPath
        );
    }

    [Fact]
    public void Compose_CrossSourceSameExactSpellingRemainsUnique()
    {
        const string requestedPath =
            "Meshes/Actors/Character/Character Assets/FaceParts/" +
            "MaleHeadBrows.tri";

        IReadOnlyList<
            DataRelativePathAggregateConsumerSpellingEvidence
        > result =
            Compose(
                new[]
                {
                    Consumer(
                        "MESHES/ACTORS/CHARACTER/CHARACTER ASSETS/FACEPARTS/" +
                        "MALEHEADBROWS.TRI",
                        requestedPath
                    )
                },
                new[]
                {
                    Consumer(
                        "MESHES/ACTORS/CHARACTER/CHARACTER ASSETS/FACEPARTS/" +
                        "MALEHEADBROWS.TRI",
                        requestedPath
                    )
                }
            );

        DataRelativePathAggregateConsumerSpellingEvidence composed =
            Assert.Single(
                result
            );

        Assert.Equal(
            DataRelativePathAggregateConsumerSpellingState
                .UniqueConsumerSpelling,
            composed.State
        );

        Assert.Equal(
            new[]
            {
                requestedPath
            },
            composed.DistinctRequestedPaths
        );

        Assert.Equal(
            requestedPath,
            composed.AuthoritativeRequestedPath
        );
    }

    [Fact]
    public void Compose_CrossSourceCaseDistinctUniqueSpellingsConflict()
    {
        const string logicalPath =
            "MESHES/ACTORS/CHARACTER/CHARACTER ASSETS/FACEPARTS/" +
            "MALEHEADBROWS.TRI";

        const string armorAddonSpelling =
            "Meshes/Actors/Character/Character Assets/FaceParts/" +
            "MaleHeadbrows.tri";

        const string headPartSpelling =
            "Meshes/Actors/Character/Character Assets/FaceParts/" +
            "MaleHeadBrows.tri";

        IReadOnlyList<
            DataRelativePathAggregateConsumerSpellingEvidence
        > result =
            Compose(
                new[]
                {
                    Consumer(
                        logicalPath,
                        armorAddonSpelling
                    )
                },
                new[]
                {
                    Consumer(
                        logicalPath,
                        headPartSpelling
                    )
                }
            );

        DataRelativePathAggregateConsumerSpellingEvidence composed =
            Assert.Single(
                result
            );

        Assert.Equal(
            DataRelativePathAggregateConsumerSpellingState
                .ConflictingConsumerSpellings,
            composed.State
        );

        Assert.Null(
            composed.AuthoritativeRequestedPath
        );

        Assert.Equal(
            new[]
            {
                headPartSpelling,
                armorAddonSpelling
            }
                .OrderBy(
                    path =>
                        path,
                    StringComparer.Ordinal
                )
                .ToArray(),
            composed.DistinctRequestedPaths
        );
    }

    [Fact]
    public void Compose_ExplicitNoConsumerPlusUniqueBecomesUnique()
    {
        const string logicalPath =
            "MESHES/ACTORS/HEAD.TRI";

        DataRelativePathAggregateConsumerSpellingEvidence noConsumer =
            Consumer(
                logicalPath
            );

        DataRelativePathAggregateConsumerSpellingEvidence unique =
            Consumer(
                logicalPath,
                "Meshes/Actors/Head.tri"
            );

        DataRelativePathAggregateConsumerSpellingEvidence composed =
            Assert.Single(
                Compose(
                    new[]
                    {
                        noConsumer
                    },
                    new[]
                    {
                        unique
                    }
                )
            );

        Assert.Equal(
            DataRelativePathAggregateConsumerSpellingState
                .UniqueConsumerSpelling,
            composed.State
        );

        Assert.Equal(
            "Meshes/Actors/Head.tri",
            composed.AuthoritativeRequestedPath
        );
    }

    [Fact]
    public void Compose_MultipleLogicalLeavesAreOrdinallyOrdered()
    {
        IReadOnlyList<
            DataRelativePathAggregateConsumerSpellingEvidence
        > result =
            Compose(
                new[]
                {
                    Consumer(
                        "TEXTURES/Z.DDS",
                        "Textures/Z.dds"
                    )
                },
                new[]
                {
                    Consumer(
                        "MESHES/A.TRI",
                        "Meshes/A.tri"
                    )
                }
            );

        Assert.Equal(
            new[]
            {
                "MESHES/A.TRI",
                "TEXTURES/Z.DDS"
            },
            result
                .Select(
                    evidence =>
                        evidence.WindowsLogicalPath
                )
                .ToArray()
        );
    }

    [Fact]
    public void Compose_NonCanonicalEvidenceIsRejectedBeforeUnion()
    {
        var malformed =
            new DataRelativePathAggregateConsumerSpellingEvidence(
                WindowsLogicalPath:
                    "MESHES/A.TRI",
                DistinctRequestedPaths:
                    new[]
                    {
                        "Meshes/A.tri"
                    },
                State:
                    DataRelativePathAggregateConsumerSpellingState
                        .ConflictingConsumerSpellings
            );

        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () =>
                    Compose(
                        new[]
                        {
                            malformed
                        }
                    )
            );

        Assert.Contains(
            "canonical form",
            exception.Message
        );
    }

    [Fact]
    public void Compose_NullEvidenceEntryIsRejected()
    {
        var source =
            new DataRelativePathAggregateConsumerSpellingEvidence[]
            {
                null!
            };

        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () =>
                    Compose(
                        source
                    )
            );

        Assert.Contains(
            "null evidence entry",
            exception.Message
        );
    }

    private static IReadOnlyList<
        DataRelativePathAggregateConsumerSpellingEvidence
    > Compose(
        params IReadOnlyList<
            DataRelativePathAggregateConsumerSpellingEvidence
        >[] sources)
    {
        return
            DataRelativePathAggregateConsumerSpellingEvidenceComposer
                .Compose(
                    sources
                );
    }

    private static
        DataRelativePathAggregateConsumerSpellingEvidence
        Consumer(
            string logicalPath,
            params string[] requestedPaths)
    {
        return DataRelativePathAggregateConsumerSpellingClassifier
            .Classify(
                logicalPath,
                requestedPaths
            );
    }
}
