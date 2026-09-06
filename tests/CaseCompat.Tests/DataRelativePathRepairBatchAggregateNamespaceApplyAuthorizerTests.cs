using CaseCompat.Core.Analysis;
using CaseCompat.Core.Repair;
using CaseCompat.Core.Resolution;
using CaseCompat.Filesystem.Linux;
using System.Reflection;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizerTests
{
    [Fact]
    public void Authorize_ExactSchemaV4PolicyV3UniqueSource_Authorizes()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespaceApplyAuthorization result =
            fixture.Authorize();

        Assert.True(
            result.AllAuthorized,
            result.Error
        );

        Assert.Single(
            result.Decisions
        );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                .Authorized,
            result.Decisions[0].State
        );
    }

    [Fact]
    public void Authorize_MultipleUniqueChildren_PreservesBatchOrder()
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

        DataRelativePathRepairBatchAggregateNamespaceApplyAuthorization result =
            fixture.Authorize();

        Assert.True(
            result.AllAuthorized,
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

        Assert.Equal(
            fixture.Batch.Children
                .Select(
                    child =>
                        child.ChildName
                ),
            result.Decisions
                .Select(
                    decision =>
                        decision.ChildName
                )
        );
    }

    [Fact]
    public void Authorize_NullDataRoot_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizer
                    .Authorize(
                        null!,
                        null!,
                        null!,
                        null!
                    )
        );
    }

    [Fact]
    public void Authorize_NullBatchManifest_Throws()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        Assert.Throws<ArgumentNullException>(
            () =>
                DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizer
                    .Authorize(
                        fixture.DataRootHandle,
                        null!,
                        fixture.Children,
                        fixture.NamespaceEvidence
                    )
        );
    }

    [Fact]
    public void Authorize_NullChildManifests_Throws()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        Assert.Throws<ArgumentNullException>(
            () =>
                DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizer
                    .Authorize(
                        fixture.DataRootHandle,
                        fixture.Batch,
                        null!,
                        fixture.NamespaceEvidence
                    )
        );
    }

    [Fact]
    public void Authorize_NullNamespaceEvidence_Throws()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        Assert.Throws<ArgumentNullException>(
            () =>
                DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizer
                    .Authorize(
                        fixture.DataRootHandle,
                        fixture.Batch,
                        fixture.Children,
                        null!
                    )
        );
    }

    [Fact]
    public void Authorize_UnsupportedSchemaPolicyPair_Rejects()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchManifestRecord invalid =
            fixture.Batch with
            {
                SchemaVersion =
                    DataRelativePathRepairBatchManifestRecord
                        .SchemaVersion2,
                CoveragePolicyVersion =
                    DataRelativePathRepairBatchManifestRecord
                        .CoveragePolicyVersion1
            };

        Assert.False(
            fixture.Authorize(
                batch:
                    invalid
            ).AllAuthorized
        );
    }

    [Fact]
    public void Authorize_NonZeroSafeRejectionCount_Rejects()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        Assert.False(
            fixture.Authorize(
                batch:
                    fixture.Batch with
                    {
                        SafeRejectionCount =
                            1
                    }
            ).AllAuthorized
        );
    }

    [Fact]
    public void Authorize_ChildCountMismatch_Rejects()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizationState
                .InvalidChildManifestSet,
            fixture.Authorize(
                children:
                    []
            ).State
        );
    }

    [Fact]
    public void Authorize_ChildSchemaNotV4_Rejects()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairPlanManifestRecord invalid =
            fixture.Children[0] with
            {
                SchemaVersion =
                    DataRelativePathRepairPlanManifestRecord
                        .SchemaVersion3
            };

        Assert.False(
            fixture.Authorize(
                children:
                    [invalid]
            ).AllAuthorized
        );
    }

    [Fact]
    public void Authorize_ChildPlanIdMismatch_Rejects()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchManifestChild[] children =
            fixture.Batch.Children
                .ToArray();

        children[0] =
            children[0] with
            {
                PlanId =
                    Guid.NewGuid()
            };

        DataRelativePathRepairBatchManifestRecord invalid =
            fixture.Batch with
            {
                Children =
                    children
            };

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                .ChildManifestBindingMismatch,
            fixture.Authorize(
                batch:
                    invalid
            ).Decisions[0].State
        );
    }

    [Fact]
    public void Authorize_ChildManifestShaMismatch_Rejects()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchManifestChild[] children =
            fixture.Batch.Children
                .ToArray();

        children[0] =
            children[0] with
            {
                ManifestSha256 =
                    new string(
                        'B',
                        64
                    )
            };

        DataRelativePathRepairBatchManifestRecord invalid =
            fixture.Batch with
            {
                Children =
                    children
            };

        Assert.False(
            fixture.Authorize(
                batch:
                    invalid
            ).AllAuthorized
        );
    }

    [Fact]
    public void Authorize_SidecarReadFailure_Rejects()
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
            DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizationState
                .InvalidNamespaceEvidence,
            fixture.Authorize(
                evidence:
                    failed
            ).State
        );
    }

    [Fact]
    public void Authorize_SidecarShaMismatch_Rejects()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        DataRelativePathAggregateNamespaceManifestReaderResult wrong =
            fixture.NamespaceEvidence with
            {
                ManifestSha256 =
                    new string(
                        'B',
                        64
                    )
            };

        Assert.False(
            fixture.Authorize(
                evidence:
                    wrong
            ).AllAuthorized
        );
    }

    [Fact]
    public void Authorize_SidecarDataRootMismatch_Rejects()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        DataRelativePathAggregateNamespaceManifestRecord wrongManifest =
            fixture.NamespaceEvidence.Manifest! with
            {
                DataRoot =
                    fixture.DataRoot + "-wrong"
            };

        DataRelativePathAggregateNamespaceManifestReaderResult wrong =
            fixture.NamespaceEvidence with
            {
                Manifest =
                    wrongManifest
            };

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizationState
                .InvalidNamespaceEvidence,
            fixture.Authorize(
                evidence:
                    wrong
            ).State
        );
    }

    [Fact]
    public void Authorize_SidecarRootMismatch_Rejects()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespaceEvidenceReference
            reference =
                fixture.Batch.AggregateNamespaceEvidence![0];

        DataRelativePathRepairBatchManifestRecord wrong =
            fixture.Batch with
            {
                AggregateNamespaceEvidence =
                    [
                        reference with
                        {
                            RootWindowsLogicalPath =
                                "TEXTURES"
                        }
                    ]
            };

        Assert.False(
            fixture.Authorize(
                batch:
                    wrong
            ).AllAuthorized
        );
    }

    [Fact]
    public void Authorize_SourceGenerationOrIdentityReplacement_Rejects()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        string backup =
            fixture.SourcePaths[0] +
            ".old";

        File.Move(
            fixture.SourcePaths[0],
            backup
        );

        File.WriteAllText(
            fixture.SourcePaths[0],
            fixture.SourceContents[0]
        );

        DataRelativePathRepairBatchAggregateNamespaceApplyAuthorization result =
            fixture.Authorize();

        Assert.False(
            result.AllAuthorized
        );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                .SourceGenerationBindingFailed,
            result.Decisions[0].State
        );
    }

    [Fact]
    public void Authorize_SourceContentChange_Rejects()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        File.WriteAllText(
            fixture.SourcePaths[0],
            "changed-current-source-content"
        );

        Assert.False(
            fixture.Authorize()
                .AllAuthorized
        );
    }

    [Fact]
    public void Authorize_NewEquivalentRepresentation_Rejects()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        File.WriteAllText(
            Path.Combine(
                fixture.AlternateParent,
                "maleheadbrows.tri"
            ),
            fixture.SourceContents[0]
        );

        DataRelativePathRepairBatchAggregateNamespaceApplyAuthorization result =
            fixture.Authorize();

        Assert.False(
            result.AllAuthorized
        );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                .EquivalentContentMultipleRepresentations,
            result.Decisions[0].State
        );
    }

    [Fact]
    public void Authorize_NewConflictingRepresentation_Rejects()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        File.WriteAllText(
            Path.Combine(
                fixture.AlternateParent,
                "maleheadbrows.tri"
            ),
            "conflicting-current-content"
        );

        DataRelativePathRepairBatchAggregateNamespaceApplyAuthorization result =
            fixture.Authorize();

        Assert.False(
            result.AllAuthorized
        );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                .ConflictingContentMultipleRepresentations,
            result.Decisions[0].State
        );
    }

    [Fact]
    public void Authorize_NewWindowsEquivalentRoot_Rejects()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        Directory.CreateDirectory(
            Path.Combine(
                fixture.DataRoot,
                "MESHES"
            )
        );

        DataRelativePathRepairBatchAggregateNamespaceApplyAuthorization result =
            fixture.Authorize();

        Assert.False(
            result.AllAuthorized
        );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                .CurrentRootSetMismatch,
            result.Decisions[0].State
        );
    }

    [Fact]
    public void Authorize_MissingCurrentLogicalLeaf_Rejects()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespaceApplyAuthorization result =
            fixture.AuthorizeCore(
                afterHistoricalCoverage:
                    () =>
                        File.Delete(
                            fixture.SourcePaths[0]
                        )
            );

        Assert.False(
            result.AllAuthorized
        );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                .MissingCurrentLogicalLeaf,
            result.Decisions[0].State
        );
    }

    [Fact]
    public void
        AuthorizeCurrentChild_PostApplySiblingEquivalentRepresentation_SelectedSiblingAuthorizes()
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

        string appliedSiblingDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    fixture.DataRoot,
                    "meshes",
                    "Actors",
                    "Character",
                    "Character Assets",
                    "FaceParts"
                )
            ).FullName;

        File.WriteAllText(
            Path.Combine(
                appliedSiblingDirectory,
                Path.GetFileName(
                    fixture.SourcePaths[0]
                )
            ),
            fixture.SourceContents[0]
        );

        /*
         * Whole-batch initial-authority semantics deliberately remain strict:
         * the post-apply sibling now has two equivalent representations.
         */
        DataRelativePathRepairBatchAggregateNamespaceApplyAuthorization
            wholeBatch =
                fixture.Authorize();

        Assert.False(
            wholeBatch.AllAuthorized
        );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                .EquivalentContentMultipleRepresentations,
            wholeBatch.Decisions[0].State
        );

        /*
         * The later unstarted child is still pristine. Its fresh boundary is
         * candidate-scoped and must not reinterpret the already-applied
         * sibling as pre-apply source authority.
         */
        DataRelativePathRepairBatchAggregateNamespaceApplyAuthorization
            targeted =
                DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizer
                    .AuthorizeCurrentChild(
                        fixture.DataRootHandle,
                        fixture.Batch,
                        fixture.Children,
                        fixture.NamespaceEvidence,
                        candidateIndex:
                            1
                    );

        Assert.True(
            targeted.AllAuthorized,
            targeted.Error
        );

        DataRelativePathRepairBatchAggregateNamespaceApplyDecision decision =
            Assert.Single(
                targeted.Decisions
            );

        Assert.Equal(
            1,
            decision.CandidateIndex
        );

        Assert.Equal(
            fixture.Batch.Children[1].ChildName,
            decision.ChildName
        );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                .Authorized,
            decision.State
        );
    }

    [Fact]
    public void
        AuthorizeCurrentChild_PostApplySibling_SelectedSourceReplacementRejectsRealCandidate()
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

        string appliedSiblingDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    fixture.DataRoot,
                    "meshes",
                    "Actors",
                    "Character",
                    "Character Assets",
                    "FaceParts"
                )
            ).FullName;

        File.WriteAllText(
            Path.Combine(
                appliedSiblingDirectory,
                Path.GetFileName(
                    fixture.SourcePaths[0]
                )
            ),
            fixture.SourceContents[0]
        );

        string backup =
            fixture.SourcePaths[1] +
            ".old";

        File.Move(
            fixture.SourcePaths[1],
            backup
        );

        File.WriteAllText(
            fixture.SourcePaths[1],
            fixture.SourceContents[1]
        );

        DataRelativePathRepairBatchAggregateNamespaceApplyAuthorization result =
            DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizer
                .AuthorizeCurrentChild(
                    fixture.DataRootHandle,
                    fixture.Batch,
                    fixture.Children,
                    fixture.NamespaceEvidence,
                    candidateIndex:
                        1
                );

        Assert.False(
            result.AllAuthorized
        );

        DataRelativePathRepairBatchAggregateNamespaceApplyDecision decision =
            Assert.Single(
                result.Decisions
            );

        Assert.Equal(
            1,
            decision.CandidateIndex
        );

        Assert.Equal(
            fixture.Batch.Children[1].ChildName,
            decision.ChildName
        );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceApplyDecisionState
                .SourceGenerationBindingFailed,
            decision.State
        );
    }

    private sealed class Fixture :
        IDisposable
    {
        private const string RequestedPrefix =
            "meshes/Actors/Character/Character Assets/" +
            "FaceParts/";

        private Fixture(
            int fileCount)
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-c4a-apply-authorizer-tests",
                    Guid.NewGuid().ToString("N")
                );

            DataRoot =
                Directory.CreateDirectory(
                    Path.Combine(
                        RootPath,
                        "Data"
                    )
                ).FullName;

            _ =
                Directory.CreateDirectory(
                    Path.Combine(
                        DataRoot,
                        "meshes",
                        "Actors",
                        "Character",
                        "Character Assets"
                    )
                );

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

            SourceContents =
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

                string content =
                    $"c4a-authorizer-fixture-{index}";

                string source =
                    Path.Combine(
                        AlternateParent,
                        fileName
                    );

                File.WriteAllText(
                    source,
                    content
                );

                SourcePaths[index] =
                    source;

                SourceContents[index] =
                    content;
            }

            Candidates =
                Enumerable.Range(
                        0,
                        fileCount
                    )
                    .Select(
                        BuildCandidate
                    )
                    .ToArray();

            DataRelativePathAggregateNamespaceManifestRecord sidecar =
                BuildSidecar();

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

            LinuxNoFollowPathOpenResult opened =
                LinuxNoFollowPath.OpenRootReadOnly(
                    DataRoot
                );

            Assert.True(
                opened.Success,
                opened.Error
            );

            DataRootHandle =
                Assert.IsType<
                    LinuxNoFollowPathHandle
                >(
                    opened.OpenedPath
                );

            DataRelativePathRepairBatchAggregateNamespacePlanResult plan =
                DataRelativePathRepairBatchAggregateNamespacePlanner.Plan(
                    DataRootHandle,
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow,
                    "repair-plan.json",
                    Candidates,
                    NamespaceEvidence
                );

            Assert.True(
                plan.Success,
                plan.Error
            );

            Batch =
                Assert.IsType<
                    DataRelativePathRepairBatchManifestRecord
                >(
                    plan.BatchManifest
                );

            Children =
                plan.PlannedChildren
                    .Select(
                        child =>
                            child.Manifest
                    )
                    .ToArray();
        }

        public string RootPath { get; }

        public string DataRoot { get; }

        public string AlternateParent { get; }

        public string[] SourcePaths { get; }

        public string[] SourceContents { get; }

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

        public DataRelativePathRepairBatchManifestRecord Batch
        {
            get;
        }

        public DataRelativePathRepairPlanManifestRecord[] Children
        {
            get;
        }

        public static Fixture Create(
            int fileCount = 1)
        {
            return new(
                fileCount
            );
        }

        public DataRelativePathRepairBatchAggregateNamespaceApplyAuthorization
            Authorize(
                DataRelativePathRepairBatchManifestRecord? batch = null,
                IReadOnlyList<
                    DataRelativePathRepairPlanManifestRecord
                >? children = null,
                DataRelativePathAggregateNamespaceManifestReaderResult?
                    evidence = null)
        {
            return
                DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizer
                    .Authorize(
                        DataRootHandle,
                        batch ??
                            Batch,
                        children ??
                            Children,
                        evidence ??
                            NamespaceEvidence
                    );
        }

        public DataRelativePathRepairBatchAggregateNamespaceApplyAuthorization
            AuthorizeCore(
                Action? afterHistoricalCoverage)
        {
            MethodInfo method =
                Assert.IsType<MethodInfo>(
                    typeof(
                        DataRelativePathRepairBatchAggregateNamespaceApplyAuthorizer
                    )
                    .GetMethod(
                        "AuthorizeCore",
                        BindingFlags.Static |
                        BindingFlags.NonPublic
                    ),
                    exactMatch: false
                );

            object? result =
                method.Invoke(
                    null,
                    new object?[]
                    {
                        DataRootHandle,
                        Batch,
                        Children,
                        NamespaceEvidence,
                        afterHistoricalCoverage
                    }
                );

            return Assert.IsType<
                DataRelativePathRepairBatchAggregateNamespaceApplyAuthorization
            >(
                result
            );
        }

        private DataRelativePathRepairBatchAggregateNamespacePlanningCandidate
            BuildCandidate(
                int index)
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
                        ? LinuxDirectoryFlags
                            .FsCasefoldFlag
                        : 0L,
                Error:
                    null
            );
        }

        public void Dispose()
        {
            DataRootHandle.Dispose();

            if (
                Directory.Exists(
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
