using CaseCompat.Core.Resolution;
using Xunit;

namespace CaseCompat.Tests;

public sealed class DataRelativePathCaseMismatchTopologyClassifierTests
{
    private static readonly string DataRoot =
        Path.GetFullPath(
            Path.Combine(
                Path.GetTempPath(),
                "casecompat-topology-tests",
                "Data"
            )
        );

    [Fact]
    public void DirectStrictCaseMismatch_IsClassified()
    {
        DataRelativePathResolution resolution =
            CreateDirectResolution();

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .DirectStrictCaseMismatch,
            DataRelativePathCaseMismatchTopologyClassifier
                .Classify(
                    resolution
                )
        );
    }

    [Fact]
    public void CandidateBranchesBeforeFailure_IsBlocked()
    {
        DataRelativePathResolution resolution =
            CreateResolution(
                requestedPath:
                    "Meshes/Actors/AtronachFlame/fixture.nif",
                failedComponentIndex:
                    2,
                steps:
                [
                    Step(
                        0,
                        "Meshes",
                        "",
                        true,
                        PathResolutionStepKind
                            .CasefoldEquivalent,
                        "meshes",
                        "meshes"
                    ),
                    Step(
                        1,
                        "Actors",
                        "meshes",
                        false,
                        PathResolutionStepKind
                            .ExactSpelling,
                        "Actors",
                        "Actors",
                        "actors"
                    ),
                    Step(
                        2,
                        "AtronachFlame",
                        "meshes/Actors",
                        false,
                        PathResolutionStepKind
                            .Missing,
                        null
                    )
                ],
                candidates:
                [
                    Candidate(
                        "meshes/actors/" +
                        "atronachflame/fixture.nif"
                    )
                ]
            );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .CandidateBranchesBeforeFailure,
            DataRelativePathCaseMismatchTopologyClassifier
                .Classify(
                    resolution
                )
        );
    }

    [Fact]
    public void PriorEquivalentHierarchySplit_IsBlocked()
    {
        DataRelativePathResolution resolution =
            CreateResolution(
                requestedPath:
                    "Meshes/Actors/Creature/fixture.nif",
                failedComponentIndex:
                    2,
                steps:
                [
                    Step(
                        0,
                        "Meshes",
                        "",
                        true,
                        PathResolutionStepKind
                            .CasefoldEquivalent,
                        "meshes",
                        "meshes"
                    ),
                    Step(
                        1,
                        "Actors",
                        "meshes",
                        false,
                        PathResolutionStepKind
                            .ExactSpelling,
                        "Actors",
                        "Actors",
                        "actors"
                    ),
                    Step(
                        2,
                        "Creature",
                        "meshes/Actors",
                        false,
                        PathResolutionStepKind
                            .Missing,
                        null,
                        "creature"
                    )
                ],
                candidates:
                [
                    Candidate(
                        "meshes/Actors/creature/fixture.nif"
                    )
                ]
            );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .PriorEquivalentHierarchySplit,
            DataRelativePathCaseMismatchTopologyClassifier
                .Classify(
                    resolution
                )
        );
    }

    [Fact]
    public void LinuxResolvable_TakesPriority()
    {
        DataRelativePathResolution resolution =
            new(
                DataRoot:
                    DataRoot,
                RequestedPath:
                    "Meshes/Test/fixture.nif",
                LinuxResolves:
                    true,
                ResolvedPhysicalPath:
                    Candidate(
                        "meshes/Test/fixture.nif"
                    ),
                FailedComponentIndex:
                    null,
                FailureReason:
                    null,
                Steps:
                    Array.Empty<
                        PathResolutionStep
                    >(),
                EquivalentPhysicalCandidates:
                [
                    Candidate(
                        "meshes/Test/fixture.nif"
                    )
                ],
                CandidateSearchErrors:
                    Array.Empty<string>()
            );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .LinuxResolvable,
            DataRelativePathCaseMismatchTopologyClassifier
                .Classify(
                    resolution
                )
        );
    }

    [Fact]
    public void IncompleteCandidateSearch_IsBlocked()
    {
        DataRelativePathResolution resolution =
            CreateResolution(
                requestedPath:
                    "Meshes/Test/fixture.nif",
                failedComponentIndex:
                    1,
                steps:
                [
                    Step(
                        0,
                        "Meshes",
                        "",
                        true,
                        PathResolutionStepKind
                            .CasefoldEquivalent,
                        "meshes",
                        "meshes"
                    ),
                    Step(
                        1,
                        "Test",
                        "meshes",
                        false,
                        PathResolutionStepKind
                            .Missing,
                        null,
                        "test"
                    )
                ],
                candidates:
                    Array.Empty<string>(),
                candidateSearchErrors:
                [
                    "fixture enumeration error"
                ]
            );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .IncompleteCandidateSearch,
            DataRelativePathCaseMismatchTopologyClassifier
                .Classify(
                    resolution
                )
        );
    }

    [Fact]
    public void NoEquivalentCandidate_IsClassified()
    {
        DataRelativePathResolution resolution =
            CreateResolution(
                requestedPath:
                    "Meshes/Test/fixture.nif",
                failedComponentIndex:
                    1,
                steps:
                [
                    Step(
                        0,
                        "Meshes",
                        "",
                        true,
                        PathResolutionStepKind
                            .CasefoldEquivalent,
                        "meshes",
                        "meshes"
                    ),
                    Step(
                        1,
                        "Test",
                        "meshes",
                        false,
                        PathResolutionStepKind
                            .Missing,
                        null
                    )
                ],
                candidates:
                    Array.Empty<string>()
            );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .NoEquivalentCandidate,
            DataRelativePathCaseMismatchTopologyClassifier
                .Classify(
                    resolution
                )
        );
    }

    [Fact]
    public void MultipleEquivalentCandidates_AreBlocked()
    {
        DataRelativePathResolution resolution =
            CreateResolution(
                requestedPath:
                    "Meshes/Test/fixture.nif",
                failedComponentIndex:
                    1,
                steps:
                [
                    Step(
                        0,
                        "Meshes",
                        "",
                        true,
                        PathResolutionStepKind
                            .CasefoldEquivalent,
                        "meshes",
                        "meshes"
                    ),
                    Step(
                        1,
                        "Test",
                        "meshes",
                        false,
                        PathResolutionStepKind
                            .Missing,
                        null,
                        "test",
                        "TEST"
                    )
                ],
                candidates:
                [
                    Candidate(
                        "meshes/test/fixture.nif"
                    ),
                    Candidate(
                        "meshes/TEST/fixture.nif"
                    )
                ]
            );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .MultipleEquivalentCandidates,
            DataRelativePathCaseMismatchTopologyClassifier
                .Classify(
                    resolution
                )
        );
    }

    [Fact]
    public void UnsupportedFailureShape_IsBlocked()
    {
        DataRelativePathResolution resolution =
            CreateResolution(
                requestedPath:
                    "Meshes/Test/fixture.nif",
                failedComponentIndex:
                    1,
                steps:
                [
                    Step(
                        0,
                        "Meshes",
                        "",
                        true,
                        PathResolutionStepKind
                            .CasefoldEquivalent,
                        "meshes",
                        "meshes"
                    ),
                    Step(
                        1,
                        "Test",
                        "meshes",
                        null,
                        PathResolutionStepKind
                            .CasefoldUnknown,
                        null,
                        "test"
                    )
                ],
                candidates:
                [
                    Candidate(
                        "meshes/test/fixture.nif"
                    )
                ]
            );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .UnsupportedFailureShape,
            DataRelativePathCaseMismatchTopologyClassifier
                .Classify(
                    resolution
                )
        );
    }

    [Fact]
    public void CandidateOutsideDataRoot_IsBlocked()
    {
        string outsideCandidate =
            Path.GetFullPath(
                Path.Combine(
                    DataRoot,
                    "..",
                    "Outside",
                    "meshes",
                    "test",
                    "fixture.nif"
                )
            );

        DataRelativePathResolution resolution =
            CreateResolution(
                requestedPath:
                    "Meshes/Test/fixture.nif",
                failedComponentIndex:
                    1,
                steps:
                [
                    Step(
                        0,
                        "Meshes",
                        "",
                        true,
                        PathResolutionStepKind
                            .CasefoldEquivalent,
                        "meshes",
                        "meshes"
                    ),
                    Step(
                        1,
                        "Test",
                        "meshes",
                        false,
                        PathResolutionStepKind
                            .Missing,
                        null,
                        "test"
                    )
                ],
                candidates:
                [
                    outsideCandidate
                ]
            );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .CandidateOutsideDataRoot,
            DataRelativePathCaseMismatchTopologyClassifier
                .Classify(
                    resolution
                )
        );
    }

    [Fact]
    public void MissingPriorTraversalStep_IsBlocked()
    {
        DataRelativePathResolution resolution =
            CreateResolution(
                requestedPath:
                    "Meshes/Actors/Test/fixture.nif",
                failedComponentIndex:
                    2,
                steps:
                [
                    Step(
                        0,
                        "Meshes",
                        "",
                        true,
                        PathResolutionStepKind
                            .CasefoldEquivalent,
                        "meshes",
                        "meshes"
                    ),
                    Step(
                        2,
                        "Test",
                        "meshes/Actors",
                        false,
                        PathResolutionStepKind
                            .Missing,
                        null,
                        "test"
                    )
                ],
                candidates:
                [
                    Candidate(
                        "meshes/Actors/test/fixture.nif"
                    )
                ]
            );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .PriorTraversalIncomplete,
            DataRelativePathCaseMismatchTopologyClassifier
                .Classify(
                    resolution
                )
        );
    }

    private static DataRelativePathResolution
        CreateDirectResolution()
    {
        return CreateResolution(
            requestedPath:
                "Meshes/00Taliesin/FreeHorse/imperialsaddle.nif",
            failedComponentIndex:
                2,
            steps:
            [
                Step(
                    0,
                    "Meshes",
                    "",
                    true,
                    PathResolutionStepKind
                        .CasefoldEquivalent,
                    "meshes",
                    "meshes"
                ),
                Step(
                    1,
                    "00Taliesin",
                    "meshes",
                    false,
                    PathResolutionStepKind
                        .ExactSpelling,
                    "00Taliesin",
                    "00Taliesin"
                ),
                Step(
                    2,
                    "FreeHorse",
                    "meshes/00Taliesin",
                    false,
                    PathResolutionStepKind
                        .Missing,
                    null,
                    "freehorse"
                )
            ],
            candidates:
            [
                Candidate(
                    "meshes/00Taliesin/" +
                    "freehorse/imperialsaddle.nif"
                )
            ]
        );
    }

    private static DataRelativePathResolution
        CreateResolution(
            string requestedPath,
            int? failedComponentIndex,
            IReadOnlyList<PathResolutionStep> steps,
            IReadOnlyList<string> candidates,
            IReadOnlyList<string>? candidateSearchErrors = null)
    {
        return new DataRelativePathResolution(
            DataRoot:
                DataRoot,
            RequestedPath:
                requestedPath,
            LinuxResolves:
                false,
            ResolvedPhysicalPath:
                null,
            FailedComponentIndex:
                failedComponentIndex,
            FailureReason:
                "fixture unresolved path",
            Steps:
                steps,
            EquivalentPhysicalCandidates:
                candidates,
            CandidateSearchErrors:
                candidateSearchErrors ??
                Array.Empty<string>()
        );
    }

    private static PathResolutionStep
        Step(
            int index,
            string requestedComponent,
            string parentRelativePath,
            bool? parentCasefoldEnabled,
            PathResolutionStepKind kind,
            string? selectedPhysicalName,
            params string[] equivalentPhysicalNames)
    {
        string parent =
            string.IsNullOrEmpty(
                parentRelativePath
            )
                ? DataRoot
                : Candidate(
                    parentRelativePath
                );

        return new PathResolutionStep(
            ComponentIndex:
                index,
            RequestedComponent:
                requestedComponent,
            ParentPhysicalPath:
                parent,
            ParentCasefoldEnabled:
                parentCasefoldEnabled,
            ParentCasefoldError:
                null,
            Kind:
                kind,
            SelectedPhysicalName:
                selectedPhysicalName,
            EquivalentPhysicalNames:
                equivalentPhysicalNames
        );
    }

    private static string Candidate(
        string relativePath)
    {
        return Path.GetFullPath(
            Path.Combine(
                DataRoot,
                relativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar
                )
            )
        );
    }
}
