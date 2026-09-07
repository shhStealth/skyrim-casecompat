using CaseCompat.Core.Repair;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathAggregateConsumerPhysicalSpellingProjectorTests
{
    private const string BrowLogicalPath =
        "MESHES/ACTORS/CHARACTER/CHARACTER ASSETS/" +
        "FACEPARTS/MALEHEADBROWS.TRI";

    private const string BrowConsumerPath =
        "meshes/Actors/Character/Character Assets/" +
        "FaceParts/MaleHeadBrows.tri";

    [Fact]
    public void Project_MaleHeadBrowsPhysicalLeafWithConsumer_IsMismatch()
    {
        IReadOnlyList<
            DataRelativePathAggregateConsumerPhysicalSpellingEvidence
        > result =
            DataRelativePathAggregateConsumerPhysicalSpellingProjector
                .Project(
                    [
                        Physical(
                            BrowLogicalPath,
                            "meshes/actors/character/character assets/" +
                            "faceparts/MaleHeadbrows.tri"
                        )
                    ],
                    [
                        Consumer(
                            BrowLogicalPath,
                            BrowConsumerPath
                        )
                    ]
                );

        DataRelativePathAggregateConsumerPhysicalSpellingEvidence evidence =
            Assert.Single(
                result
            );

        Assert.Equal(
            DataRelativePathAggregateConsumerPhysicalSpellingState
                .ConsumerCaseMismatch,
            evidence.State
        );

        Assert.Equal(
            BrowConsumerPath,
            evidence.AuthoritativeRequestedPath
        );
    }

    [Fact]
    public void Project_PhysicalLeafWithoutConsumer_EmitsNoConsumerEvidence()
    {
        const string logicalPath =
            "MESHES/ACTORS/FIXTURE.NIF";

        IReadOnlyList<
            DataRelativePathAggregateConsumerPhysicalSpellingEvidence
        > result =
            DataRelativePathAggregateConsumerPhysicalSpellingProjector
                .Project(
                    [
                        Physical(
                            logicalPath,
                            "meshes/Actors/Fixture.nif"
                        )
                    ],
                    Array.Empty<
                        DataRelativePathAggregateConsumerSpellingEvidence
                    >()
                );

        DataRelativePathAggregateConsumerPhysicalSpellingEvidence evidence =
            Assert.Single(
                result
            );

        Assert.Equal(
            DataRelativePathAggregateConsumerPhysicalSpellingState
                .NoConsumerEvidence,
            evidence.State
        );

        Assert.Equal(
            DataRelativePathAggregateConsumerSpellingState
                .NoConsumerEvidence,
            evidence.ConsumerSpelling.State
        );

        Assert.Empty(
            evidence.ConsumerSpelling.DistinctRequestedPaths
        );

        Assert.Null(
            evidence.AuthoritativeRequestedPath
        );
    }

    [Fact]
    public void
        Project_ExactPhysicalSpellingAmongMultipleRepresentations_IsPresent()
    {
        IReadOnlyList<
            DataRelativePathAggregateConsumerPhysicalSpellingEvidence
        > result =
            DataRelativePathAggregateConsumerPhysicalSpellingProjector
                .Project(
                    [
                        Physical(
                            BrowLogicalPath,
                            "meshes/actors/character/character assets/" +
                            "faceparts/MaleHeadbrows.tri",
                            BrowConsumerPath
                        )
                    ],
                    [
                        Consumer(
                            BrowLogicalPath,
                            BrowConsumerPath
                        )
                    ]
                );

        DataRelativePathAggregateConsumerPhysicalSpellingEvidence evidence =
            Assert.Single(
                result
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
    public void Project_ConflictingConsumers_AreRetained()
    {
        const string first =
            "meshes/Actors/Character/Body.nif";

        const string second =
            "meshes/actors/character/Body.nif";

        const string logicalPath =
            "MESHES/ACTORS/CHARACTER/BODY.NIF";

        IReadOnlyList<
            DataRelativePathAggregateConsumerPhysicalSpellingEvidence
        > result =
            DataRelativePathAggregateConsumerPhysicalSpellingProjector
                .Project(
                    [
                        Physical(
                            logicalPath,
                            first
                        )
                    ],
                    [
                        Consumer(
                            logicalPath,
                            first,
                            second
                        )
                    ]
                );

        DataRelativePathAggregateConsumerPhysicalSpellingEvidence evidence =
            Assert.Single(
                result
            );

        Assert.Equal(
            DataRelativePathAggregateConsumerPhysicalSpellingState
                .ConflictingConsumerSpellings,
            evidence.State
        );

        Assert.Equal(
            DataRelativePathAggregateConsumerSpellingState
                .ConflictingConsumerSpellings,
            evidence.ConsumerSpelling.State
        );

        Assert.Null(
            evidence.AuthoritativeRequestedPath
        );
    }

    [Fact]
    public void Project_MultiplePhysicalLeaves_AreOrdinallyOrdered()
    {
        IReadOnlyList<
            DataRelativePathAggregateConsumerPhysicalSpellingEvidence
        > result =
            DataRelativePathAggregateConsumerPhysicalSpellingProjector
                .Project(
                    [
                        Physical(
                            "TEXTURES/Z.DDS",
                            "textures/Z.dds"
                        ),
                        Physical(
                            "MESHES/A.NIF",
                            "Meshes/A.nif"
                        )
                    ],
                    Array.Empty<
                        DataRelativePathAggregateConsumerSpellingEvidence
                    >()
                );

        Assert.Equal(
            new[]
            {
                "MESHES/A.NIF",
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
    public void Project_ConsumerOutsidePhysicalUniverse_IsNotProjected()
    {
        IReadOnlyList<
            DataRelativePathAggregateConsumerPhysicalSpellingEvidence
        > result =
            DataRelativePathAggregateConsumerPhysicalSpellingProjector
                .Project(
                    [
                        Physical(
                            "MESHES/A.NIF",
                            "Meshes/A.nif"
                        )
                    ],
                    [
                        Consumer(
                            "TEXTURES/B.DDS",
                            "Textures/B.dds"
                        )
                    ]
                );

        DataRelativePathAggregateConsumerPhysicalSpellingEvidence evidence =
            Assert.Single(
                result
            );

        Assert.Equal(
            "MESHES/A.NIF",
            evidence.WindowsLogicalPath
        );

        Assert.Equal(
            DataRelativePathAggregateConsumerPhysicalSpellingState
                .NoConsumerEvidence,
            evidence.State
        );
    }

    [Fact]
    public void Project_DuplicatePhysicalLogicalLeaf_IsRejected()
    {
        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () =>
                    DataRelativePathAggregateConsumerPhysicalSpellingProjector
                        .Project(
                            [
                                Physical(
                                    "MESHES/A.NIF",
                                    "Meshes/A.nif"
                                ),
                                Physical(
                                    "MESHES/A.NIF",
                                    "meshes/A.nif"
                                )
                            ],
                            Array.Empty<
                                DataRelativePathAggregateConsumerSpellingEvidence
                            >()
                        )
            );

        Assert.Contains(
            "occurs more than once",
            exception.Message
        );
    }

    [Fact]
    public void Project_DuplicateConsumerLogicalLeaf_IsRejected()
    {
        DataRelativePathAggregateConsumerSpellingEvidence first =
            Consumer(
                "MESHES/A.NIF",
                "Meshes/A.nif"
            );

        DataRelativePathAggregateConsumerSpellingEvidence second =
            Consumer(
                "MESHES/A.NIF",
                "meshes/A.nif"
            );

        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () =>
                    DataRelativePathAggregateConsumerPhysicalSpellingProjector
                        .Project(
                            [
                                Physical(
                                    "MESHES/A.NIF",
                                    "Meshes/A.nif"
                                )
                            ],
                            [
                                first,
                                second
                            ]
                        )
            );

        Assert.Contains(
            "occurs more than once",
            exception.Message
        );
    }

    [Fact]
    public void
        Project_NonCanonicalUnmatchedConsumerEvidence_IsRejected()
    {
        var nonCanonical =
            new DataRelativePathAggregateConsumerSpellingEvidence(
                WindowsLogicalPath:
                    "meshes/a.nif",
                DistinctRequestedPaths:
                    [
                        "Meshes/A.nif"
                    ],
                State:
                    DataRelativePathAggregateConsumerSpellingState
                        .UniqueConsumerSpelling
            );

        Assert.Throws<ArgumentException>(
            () =>
                DataRelativePathAggregateConsumerPhysicalSpellingProjector
                    .Project(
                        Array.Empty<
                            DataRelativePathAggregatePhysicalSpellingLeaf
                        >(),
                        [
                            nonCanonical
                        ]
                    )
        );
    }

    [Fact]
    public void Project_PhysicalPathOutsideDeclaredLeaf_IsRejected()
    {
        Assert.Throws<ArgumentException>(
            () =>
                DataRelativePathAggregateConsumerPhysicalSpellingProjector
                    .Project(
                        [
                            Physical(
                                "MESHES/A.NIF",
                                "Meshes/B.nif"
                            )
                        ],
                        Array.Empty<
                            DataRelativePathAggregateConsumerSpellingEvidence
                        >()
                    )
        );
    }

    [Fact]
    public void Project_EmptyPhysicalUniverse_ProducesEmptyResult()
    {
        IReadOnlyList<
            DataRelativePathAggregateConsumerPhysicalSpellingEvidence
        > result =
            DataRelativePathAggregateConsumerPhysicalSpellingProjector
                .Project(
                    Array.Empty<
                        DataRelativePathAggregatePhysicalSpellingLeaf
                    >(),
                    [
                        Consumer(
                            "MESHES/A.NIF",
                            "Meshes/A.nif"
                        )
                    ]
                );

        Assert.Empty(
            result
        );
    }

    private static DataRelativePathAggregatePhysicalSpellingLeaf Physical(
        string logicalPath,
        params string[] physicalRelativePaths)
    {
        return new DataRelativePathAggregatePhysicalSpellingLeaf(
            WindowsLogicalPath:
                logicalPath,
            PhysicalRelativePaths:
                physicalRelativePaths
        );
    }

    private static DataRelativePathAggregateConsumerSpellingEvidence Consumer(
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
