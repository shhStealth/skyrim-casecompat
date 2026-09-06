using CaseCompat.Core.Analysis;
using CaseCompat.Core.Repair;
using CaseCompat.Core.Resolution;
using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairBatchAggregateNamespacePlannerTests
{
    [Fact]
    public void
        Plan_ExactSingleUniqueCandidate_CreatesAuthorizedSchemaV4Batch()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespacePlanResult result =
            fixture.Plan();

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            DataRelativePathRepairBatchManifestRecord.SchemaVersion4,
            result.BatchManifest!.SchemaVersion
        );

        Assert.Equal(
            DataRelativePathRepairBatchManifestRecord
                .CoveragePolicyVersion3,
            result.BatchManifest.CoveragePolicyVersion
        );

        Assert.Single(
            result.Decisions
        );

        Assert.True(
            result.Decisions[0].Authorized
        );
    }

    [Fact]
    public void Plan_MultipleCandidates_PreservesInputDecisionOrder()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create(
                fileCount:
                    2
            );

        DataRelativePathRepairBatchAggregateNamespacePlanResult result =
            fixture.Plan();

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            new[]
            {
                0,
                1
            },
            result.Decisions
                .Select(
                    decision =>
                        decision.CandidateIndex
                )
                .ToArray()
        );
    }

    [Fact]
    public void Plan_NullDataRoot_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                DataRelativePathRepairBatchAggregateNamespacePlanner.Plan(
                    null!,
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow,
                    "repair-plan.json",
                    [],
                    null!
                )
        );
    }

    [Fact]
    public void Plan_NullCandidates_Throws()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        Assert.Throws<ArgumentNullException>(
            () =>
                DataRelativePathRepairBatchAggregateNamespacePlanner.Plan(
                    fixture.DataRootHandle,
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow,
                    "repair-plan.json",
                    null!,
                    fixture.NamespaceEvidence
                )
        );
    }

    [Fact]
    public void Plan_NullNamespaceEvidence_Throws()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        Assert.Throws<ArgumentNullException>(
            () =>
                DataRelativePathRepairBatchAggregateNamespacePlanner.Plan(
                    fixture.DataRootHandle,
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow,
                    "repair-plan.json",
                    fixture.Candidates,
                    null!
                )
        );
    }

    [Fact]
    public void Plan_EmptyCandidateSet_IsInvalidInput()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespacePlanState.InvalidInput,
            fixture.Plan(
                candidates:
                    Array.Empty<
                        DataRelativePathRepairBatchAggregateNamespacePlanningCandidate>()
            ).State
        );
    }

    [Fact]
    public void Plan_NullCandidate_IsInvalidCandidate()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        var candidates =
            new DataRelativePathRepairBatchAggregateNamespacePlanningCandidate[]
            {
                null!
            };

        DataRelativePathRepairBatchAggregateNamespacePlanResult result =
            fixture.Plan(
                candidates:
                    candidates
            );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespacePlanningDecisionState
                .InvalidCandidate,
            result.Decisions[0].State
        );
    }

    [Fact]
    public void Plan_InvalidChildName_IsInvalidChildName()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        var candidate =
            fixture.Candidates[0] with
            {
                ChildName =
                    "bad/name"
            };

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespacePlanningDecisionState
                .InvalidChildName,
            fixture.Plan(
                candidates:
                    [
                        candidate
                    ]
            ).Decisions[0].State
        );
    }

    [Fact]
    public void Plan_DuplicateChildName_IsInvalidInput()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create(
                fileCount:
                    2
            );

        var candidates =
            new[]
            {
                fixture.Candidates[0],
                fixture.Candidates[1] with
                {
                    ChildName =
                        fixture.Candidates[0].ChildName
                }
            };

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespacePlanState.InvalidInput,
            fixture.Plan(
                candidates:
                    candidates
            ).State
        );
    }

    [Fact]
    public void Plan_DuplicatePlanId_IsInvalidInput()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Guid sharedPlanId =
            Guid.NewGuid();

        using Fixture fixture =
            Fixture.Create(
                fileCount:
                    2,
                candidatePlanId:
                    sharedPlanId
            );

        Assert.All(
            fixture.Candidates,
            candidate =>
            {
                Assert.Equal(
                    sharedPlanId,
                    candidate.Manifest.PlanId
                );

                Assert.Null(
                    DataRelativePathRepairPlanManifest.Validate(
                        candidate.Manifest
                    )
                );
            }
        );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespacePlanState.InvalidInput,
            fixture.Plan().State
        );
    }

    [Fact]
    public void Plan_InvalidPlanManifest_IsInvalidPlanManifest()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        var candidate =
            fixture.Candidates[0] with
            {
                Manifest =
                    fixture.Candidates[0].Manifest with
                    {
                        PlanId =
                            Guid.Empty
                    }
            };

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespacePlanningDecisionState
                .InvalidPlanManifest,
            fixture.Plan(
                candidates:
                [
                    candidate
                ]
            ).Decisions[0].State
        );
    }

    [Fact]
    public void Plan_NonSchemaV4Plan_IsInvalidPlanManifest()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        var candidate =
            fixture.Candidates[0] with
            {
                Manifest =
                    fixture.Candidates[0].Manifest with
                    {
                        SchemaVersion =
                            DataRelativePathRepairPlanManifestRecord
                                .SchemaVersion2,
                        ResolvedPrefixSteps =
                            null
                    }
            };

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespacePlanningDecisionState
                .InvalidPlanManifest,
            fixture.Plan(
                candidates:
                [
                    candidate
                ]
            ).Decisions[0].State
        );
    }

    [Fact]
    public void Plan_CandidateDataRootMismatch_IsInvalidPlanManifest()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        var candidate =
            fixture.Candidates[0] with
            {
                Manifest =
                    fixture.Candidates[0].Manifest with
                    {
                        DataRoot =
                            fixture.DataRoot + "-wrong"
                    }
            };

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespacePlanningDecisionState
                .InvalidPlanManifest,
            fixture.Plan(
                candidates:
                [
                    candidate
                ]
            ).Decisions[0].State
        );
    }

    [Fact]
    public void Plan_FailedNamespaceReader_IsInvalidNamespaceEvidence()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        DataRelativePathAggregateNamespaceManifestReaderResult failed =
            fixture.NamespaceEvidence with
            {
                State =
                    DataRelativePathAggregateNamespaceManifestReadState
                        .ManifestUnavailable
            };

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespacePlanState
                .InvalidNamespaceEvidence,
            fixture.Plan(
                namespaceEvidence:
                    failed
            ).State
        );
    }

    [Fact]
    public void Plan_InvalidNamespaceManifest_IsInvalidNamespaceEvidence()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        DataRelativePathAggregateNamespaceManifestRecord invalid =
            fixture.NamespaceEvidence.Manifest! with
            {
                RootWindowsLogicalPath =
                    "meshes"
            };

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespacePlanState
                .InvalidNamespaceEvidence,
            fixture.Plan(
                namespaceEvidence:
                    fixture.NamespaceEvidence with
                    {
                        Manifest =
                            invalid
                    }
            ).State
        );
    }

    [Fact]
    public void Plan_NamespaceDataRootMismatch_IsInvalidNamespaceEvidence()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        DataRelativePathAggregateNamespaceManifestRecord invalid =
            fixture.NamespaceEvidence.Manifest! with
            {
                DataRoot =
                    fixture.DataRoot + "-wrong"
            };

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespacePlanState
                .InvalidNamespaceEvidence,
            fixture.Plan(
                namespaceEvidence:
                    fixture.NamespaceEvidence with
                    {
                        Manifest =
                            invalid
                    }
            ).State
        );
    }

    [Fact]
    public void Plan_DerivesExactNamespaceReferenceFromReaderResult()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespacePlanResult result =
            fixture.Plan();

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            fixture.NamespaceEvidence.Manifest!.SchemaVersion,
            result.NamespaceEvidenceReference!.ManifestSchemaVersion
        );

        Assert.Equal(
            fixture.NamespaceEvidence.Manifest.RootWindowsLogicalPath,
            result.NamespaceEvidenceReference.RootWindowsLogicalPath
        );

        Assert.Equal(
            fixture.NamespaceEvidence.ManifestSha256,
            result.NamespaceEvidenceReference.ManifestSha256
        );
    }

    [Fact]
    public void Plan_CanonicalChildSerialization_BindsExactManifestSha()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespacePlanResult result =
            fixture.Plan();

        byte[] expectedBytes =
            DataRelativePathRepairPlanManifestJson.Serialize(
                fixture.Candidates[0].Manifest
            );

        string expected =
            Convert.ToHexString(
                SHA256.HashData(
                    expectedBytes
                )
            );

        Assert.Equal(
            expected,
            result.PlannedChildren[0].ManifestSha256
        );

        Assert.Equal(
            expected,
            result.BatchManifest!.Children[0].ManifestSha256
        );
    }

    [Fact]
    public void Plan_SourceGenerationBindingFailure_BlocksCoverageAuthorization()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        File.Delete(
            fixture.SourcePaths[0]
        );

        DataRelativePathRepairBatchAggregateNamespacePlanResult result =
            fixture.Plan();

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespacePlanState
                .CandidatePreparationFailed,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespacePlanningDecisionState
                .SourceGenerationBindingFailed,
            result.Decisions[0].State
        );

        Assert.Null(
            result.CoverageAuthorization
        );
    }

    [Fact]
    public void Plan_ExactSourceGeneration_IsPassedToPolicyV3Authorizer()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespacePlanResult result =
            fixture.Plan();

        Assert.True(
            result.Success,
            result.Error
        );

        DataRelativePathAggregateNamespaceManifestFileRepresentation
            representation =
                fixture.FindRepresentation(
                    fixture.Candidates[0].Manifest
                );

        Assert.Equal(
            representation.InodeGeneration,
            result.PlannedChildren[0].SourceInodeGeneration
        );

        Assert.True(
            result.CoverageAuthorization!.AllAuthorized
        );
    }

    [Fact]
    public void Plan_EquivalentMultipleRepresentations_BlocksBatchCompletion()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create(
                extraRepresentation:
                    ExtraRepresentation.Equivalent
            );

        DataRelativePathRepairBatchAggregateNamespacePlanResult result =
            fixture.Plan();

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespacePlanState
                .CoverageRejected,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .EquivalentContentMultipleRepresentations,
            result.Decisions[0].CoverageDecisionState
        );
    }

    [Fact]
    public void Plan_ConflictingMultipleRepresentations_BlocksBatchCompletion()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create(
                extraRepresentation:
                    ExtraRepresentation.Conflicting
            );

        DataRelativePathRepairBatchAggregateNamespacePlanResult result =
            fixture.Plan();

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespacePlanState
                .CoverageRejected,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .ConflictingContentMultipleRepresentations,
            result.Decisions[0].CoverageDecisionState
        );
    }

    [Fact]
    public void Plan_SourceRepresentationMismatch_BlocksBatchCompletion()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create(
                staleNamespaceEvidence:
                    true
            );

        DataRelativePathRepairBatchAggregateNamespacePlanResult result =
            fixture.Plan();

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespacePlanState
                .CoverageRejected,
            result.State
        );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .SourceRepresentationMismatch,
            result.Decisions[0].CoverageDecisionState
        );
    }

    [Fact]
    public void Plan_PreservesExactPolicyV3CoverageDecisionForEveryInput()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create(
                fileCount:
                    2
            );

        DataRelativePathRepairBatchAggregateNamespacePlanResult result =
            fixture.Plan();

        Assert.Equal(
            result.CoverageAuthorization!.Decisions
                .Select(
                    decision =>
                        decision.State
                ),
            result.Decisions
                .Select(
                    decision =>
                        decision.CoverageDecisionState!.Value
                )
        );
    }

    [Fact]
    public void Plan_SuccessfulBatchUsesInputCountAndZeroSafeRejections()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create(
                fileCount:
                    2
            );

        DataRelativePathRepairBatchAggregateNamespacePlanResult result =
            fixture.Plan();

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            2,
            result.BatchManifest!.InputPathCount
        );

        Assert.Equal(
            0,
            result.BatchManifest.SafeRejectionCount
        );
    }

    [Fact]
    public void Plan_SuccessfulPlanning_PerformsNoPublicationOrExecution()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        string[] before =
            Directory.GetFileSystemEntries(
                fixture.RootPath,
                "*",
                SearchOption.AllDirectories
            )
            .OrderBy(
                path =>
                    path,
                StringComparer.Ordinal
            )
            .ToArray();

        DataRelativePathRepairBatchAggregateNamespacePlanResult result =
            fixture.Plan();

        string[] after =
            Directory.GetFileSystemEntries(
                fixture.RootPath,
                "*",
                SearchOption.AllDirectories
            )
            .OrderBy(
                path =>
                    path,
                StringComparer.Ordinal
            )
            .ToArray();

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            before,
            after
        );
    }

    private enum ExtraRepresentation
    {
        None,
        Equivalent,
        Conflicting
    }

    private sealed class Fixture
        : IDisposable
    {
        private const string RequestedPrefix =
            "meshes/Actors/Character/Character Assets/" +
            "FaceParts/";

        private Fixture(
            int fileCount,
            ExtraRepresentation extraRepresentation,
            bool staleNamespaceEvidence,
            Guid? candidatePlanId)
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-aggregate-namespace-planner-tests",
                    Guid.NewGuid().ToString("N")
                );

            DataRoot =
                Directory.CreateDirectory(
                    Path.Combine(
                        RootPath,
                        "Data"
                    )
                ).FullName;

            string requestedParent =
                Directory.CreateDirectory(
                    Path.Combine(
                        DataRoot,
                        "meshes",
                        "Actors",
                        "Character",
                        "Character Assets"
                    )
                ).FullName;

            AlternateParent =
                Directory.CreateDirectory(
                    Path.Combine(
                        DataRoot,
                        "meshes",
                        "actors",
                        "character",
                        "character assets",
                        "faceparts"
                    )
                ).FullName;

            SourcePaths =
                new string[fileCount];

            for (
                int index = 0;
                index < fileCount;
                index++)
            {
                string fileName =
                    index == 0
                        ? "MaleHeadbrows.tri"
                        : $"Fixture{index}.tri";

                string source =
                    Path.Combine(
                        AlternateParent,
                        fileName
                    );

                File.WriteAllText(
                    source,
                    $"planner-fixture-{index}"
                );

                SourcePaths[index] =
                    source;
            }

            /*
             * Build the schema-v4 plans BEFORE optional duplicate physical
             * representations are introduced. That preserves one exact
             * aggregate alternate source candidate for plan construction.
             */
            DataRelativePathRepairBatchAggregateNamespacePlanningCandidate[]
                initialCandidates =
                    Enumerable.Range(
                            0,
                            fileCount
                        )
                        .Select(
                            index =>
                                BuildCandidate(
                                    index,
                                    candidatePlanId
                                )
                        )
                        .ToArray();

            DataRelativePathAggregateNamespaceManifestRecord initialSidecar =
                BuildSidecar();

            if (
                extraRepresentation !=
                    ExtraRepresentation.None)
            {
                string duplicate =
                    Path.Combine(
                        AlternateParent,
                        "maleheadbrows.tri"
                    );

                File.WriteAllText(
                    duplicate,
                    extraRepresentation ==
                        ExtraRepresentation.Equivalent
                        ? File.ReadAllText(
                            SourcePaths[0]
                        )
                        : "conflicting-content"
                );
            }

            DataRelativePathAggregateNamespaceManifestRecord sidecar =
                extraRepresentation ==
                    ExtraRepresentation.None
                    ? initialSidecar
                    : BuildSidecar();

            if (staleNamespaceEvidence)
            {
                /*
                 * Preserve the already-built sidecar, then change the source
                 * and rebuild the plan against current source bytes. Binder
                 * succeeds, while policy-v3 must reject the stale sidecar's
                 * source representation.
                 */
                File.WriteAllText(
                    SourcePaths[0],
                    "new-current-source-content"
                );

                initialCandidates[0] =
                    BuildCandidate(
                        0
                    );
            }

            Candidates =
                initialCandidates;

            NamespaceEvidence =
                new(
                    State:
                        DataRelativePathAggregateNamespaceManifestReadState
                            .Read,
                    ManifestChildName:
                        "aggregate-namespace-manifest.json",
                    Manifest:
                        sidecar,
                    ManifestIncarnation:
                        null,
                    Length:
                        1,
                    ManifestSha256:
                        new string(
                            'A',
                            64
                        ),
                    Error:
                        null
                );

            LinuxNoFollowPathOpenResult rootOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    DataRoot
                );

            Assert.True(
                rootOpen.Success,
                rootOpen.Error
            );

            DataRootHandle =
                Assert.IsType<
                    LinuxNoFollowPathHandle>(
                        rootOpen.OpenedPath
                    );

            Assert.Null(
                DataRelativePathAggregateNamespaceManifest.Validate(
                    sidecar
                )
            );

            Assert.All(
                Candidates,
                candidate =>
                    Assert.Null(
                        DataRelativePathRepairPlanManifest.Validate(
                            candidate.Manifest
                        )
                    )
            );

            _ =
                requestedParent;
        }

        public string RootPath { get; }

        public string DataRoot { get; }

        public string AlternateParent { get; }

        public string[] SourcePaths { get; }

        public LinuxNoFollowPathHandle DataRootHandle { get; }

        public DataRelativePathRepairBatchAggregateNamespacePlanningCandidate[]
            Candidates
        {
            get;
        }

        public DataRelativePathAggregateNamespaceManifestReaderResult
            NamespaceEvidence
        {
            get;
        }

        public static Fixture Create(
            int fileCount = 1,
            ExtraRepresentation extraRepresentation =
                ExtraRepresentation.None,
            bool staleNamespaceEvidence = false,
            Guid? candidatePlanId = null)
        {
            return new(
                fileCount,
                extraRepresentation,
                staleNamespaceEvidence,
                candidatePlanId
            );
        }

        public DataRelativePathRepairBatchAggregateNamespacePlanResult Plan(
            IReadOnlyList<
                DataRelativePathRepairBatchAggregateNamespacePlanningCandidate
            >? candidates = null,
            DataRelativePathAggregateNamespaceManifestReaderResult?
                namespaceEvidence = null)
        {
            return DataRelativePathRepairBatchAggregateNamespacePlanner.Plan(
                DataRootHandle,
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                "repair-plan.json",
                candidates ??
                    Candidates,
                namespaceEvidence ??
                    NamespaceEvidence
            );
        }

        public DataRelativePathAggregateNamespaceManifestFileRepresentation
            FindRepresentation(
                DataRelativePathRepairPlanManifestRecord manifest)
        {
            string logical =
                WindowsLogicalPath
                    .FromRelativePath(
                        manifest.RequestedPath
                    )
                    .Value;

            DataRelativePathAggregateNamespaceManifestLogicalLeaf leaf =
                Assert.Single(
                    NamespaceEvidence.Manifest!.LogicalLeaves,
                    item =>
                        string.Equals(
                            item.WindowsLogicalPath,
                            logical,
                            StringComparison.Ordinal
                        )
                );

            return Assert.Single(
                leaf.PhysicalRepresentations
            );
        }

        private DataRelativePathRepairBatchAggregateNamespacePlanningCandidate
            BuildCandidate(
                int index,
                Guid? planId = null)
        {
            string fileName =
                Path.GetFileName(
                    SourcePaths[index]
                );

            DataRelativePathResolution resolution =
                DataRelativePathResolver.ResolveFile(
                    DataRoot,
                    RequestedPrefix +
                        fileName,
                    InspectFixtureCasefold
                );

            Assert.Equal(
                DataRelativePathCaseMismatchTopologyState
                    .CandidateBranchesBeforeFailure,
                DataRelativePathCaseMismatchTopologyClassifier
                    .Classify(
                        resolution
                    )
            );

            DataRelativePathRepairPlanProjection projection =
                DataRelativePathRepairPlanProjector
                    .ProjectAggregateAlternateBranchBatchCandidate(
                        resolution
                    );

            Assert.True(
                projection.HasPlan,
                projection.Error
            );

            DataRelativePathRepairPlanManifestCreation creation =
                DataRelativePathRepairPlanManifest
                    .CreateAggregateAlternateBranchFromResolution(
                        planId ??
                            Guid.NewGuid(),
                        DateTimeOffset.UtcNow,
                        resolution,
                        projection.SourceSnapshot!,
                        projection.DestinationParentSnapshot!,
                        projection.Operations
                    );

            Assert.True(
                creation.Success,
                creation.Error
            );

            return new(
                ChildName:
                    $"plan-{index + 1:000000}",
                Manifest:
                    creation.Manifest!
            );
        }

        private DataRelativePathAggregateNamespaceManifestRecord BuildSidecar()
        {
            WindowsNamespaceAnalysis analysis =
                WindowsNamespaceAnalyzer.Analyze(
                    DataRoot,
                    "meshes"
                );

            Assert.True(
                analysis.Complete,
                string.Join(
                    Environment.NewLine,
                    analysis.Errors
                )
            );

            WindowsNamespaceRegularFileContentAnalysis content =
                WindowsNamespaceRegularFileContentAnalyzer.Analyze(
                    analysis
                );

            Assert.True(
                content.Complete,
                string.Join(
                    Environment.NewLine,
                    content.Errors
                )
            );

            DataRelativePathAggregateNamespaceManifestRecord sidecar =
                WindowsNamespaceAggregateManifestProjector.Project(
                    analysis,
                    content,
                    DateTimeOffset.UtcNow
                );

            Assert.Null(
                DataRelativePathAggregateNamespaceManifest.Validate(
                    sidecar
                )
            );

            return sidecar;
        }

        private DirectoryCasefoldResult InspectFixtureCasefold(
            string path)
        {
            string fullPath =
                Path.GetFullPath(
                    path
                );

            bool isDataRoot =
                string.Equals(
                    fullPath,
                    Path.GetFullPath(
                        DataRoot
                    ),
                    StringComparison.Ordinal
                );

            return new(
                FullPath:
                    fullPath,
                Exists:
                    Directory.Exists(
                        fullPath
                    ),
                CasefoldEnabled:
                    isDataRoot,
                RawFlags:
                    isDataRoot
                        ? LinuxDirectoryFlags.FsCasefoldFlag
                        : 0L,
                Error:
                    null
            );
        }

        public void Dispose()
        {
            DataRootHandle.Dispose();

            if (Directory.Exists(
                    RootPath))
            {
                Directory.Delete(
                    RootPath,
                    recursive:
                        true
                );
            }
        }
    }
}
