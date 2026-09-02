using CaseCompat.Core.Repair;
using CaseCompat.Core.Resolution;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairBatchCoverageAuthorizerTests
{
    [Fact]
    public void
        Authorize_CompleteCoverageWithConsistentRequestedSpelling_AuthorizesBoth()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            CreateDataRoot(
                temp
            );

        string physicalDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "alpha"
                )
            ).FullName;

        File.WriteAllText(
            Path.Combine(
                physicalDirectory,
                "Thing1.nif"
            ),
            "complete-coverage-1"
        );

        File.WriteAllText(
            Path.Combine(
                physicalDirectory,
                "Thing2.nif"
            ),
            "complete-coverage-2"
        );

        DataRelativePathRepairPlanProjection[] candidates =
        [
            Candidate(
                dataRoot,
                "Meshes/Alpha/Thing1.nif"
            ),
            Candidate(
                dataRoot,
                "Meshes/Alpha/Thing2.nif"
            )
        ];

        DataRelativePathRepairBatchCoverageAuthorization result =
            DataRelativePathRepairBatchCoverageAuthorizer
                .Authorize(
                    candidates
                );

        Assert.True(
            result.AllAuthorized
        );

        Assert.Equal(
            2,
            result.AuthorizedCount
        );

        Assert.Equal(
            0,
            result.RejectedCount
        );

        Assert.All(
            result.Decisions,
            decision =>
            {
                Assert.Equal(
                    DataRelativePathRepairBatchCoverageDecisionState
                        .Authorized,
                    decision.State
                );

                Assert.Null(
                    decision.Error
                );
            }
        );

        Assert.False(
            Directory.Exists(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "Alpha"
                )
            )
        );
    }

    [Fact]
    public void
        Authorize_PartialCoverageWithConsistentRequestedSpelling_RejectsBranch()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            CreateDataRoot(
                temp
            );

        string physicalDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "alpha"
                )
            ).FullName;

        File.WriteAllText(
            Path.Combine(
                physicalDirectory,
                "Thing1.nif"
            ),
            "partial-coverage-1"
        );

        File.WriteAllText(
            Path.Combine(
                physicalDirectory,
                "Thing2.nif"
            ),
            "partial-coverage-2"
        );

        string untargeted =
            Path.Combine(
                physicalDirectory,
                "Untargeted.nif"
            );

        File.WriteAllText(
            untargeted,
            "partial-coverage-untargeted"
        );

        DataRelativePathRepairPlanProjection[] candidates =
        [
            Candidate(
                dataRoot,
                "Meshes/Alpha/Thing1.nif"
            ),
            Candidate(
                dataRoot,
                "Meshes/Alpha/Thing2.nif"
            )
        ];

        DataRelativePathRepairBatchCoverageAuthorization result =
            DataRelativePathRepairBatchCoverageAuthorizer
                .Authorize(
                    candidates
                );

        Assert.False(
            result.AllAuthorized
        );

        Assert.Equal(
            0,
            result.AuthorizedCount
        );

        Assert.Equal(
            2,
            result.RejectedCount
        );

        Assert.All(
            result.Decisions,
            decision =>
                Assert.Equal(
                    DataRelativePathRepairBatchCoverageDecisionState
                        .IncompletePhysicalCoverage,
                    decision.State
                )
        );

        Assert.True(
            File.Exists(
                untargeted
            )
        );

        Assert.False(
            Directory.Exists(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "Alpha"
                )
            )
        );
    }

    [Fact]
    public void
        Authorize_CompleteCoverageWithConflictingRequestedSpelling_RejectsBranch()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            CreateDataRoot(
                temp
            );

        string physicalDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "alpha"
                )
            ).FullName;

        File.WriteAllText(
            Path.Combine(
                physicalDirectory,
                "Thing1.nif"
            ),
            "conflicting-spelling-1"
        );

        File.WriteAllText(
            Path.Combine(
                physicalDirectory,
                "Thing2.nif"
            ),
            "conflicting-spelling-2"
        );

        DataRelativePathRepairPlanProjection[] candidates =
        [
            Candidate(
                dataRoot,
                "Meshes/Alpha/Thing1.nif"
            ),
            Candidate(
                dataRoot,
                "Meshes/ALPHA/Thing2.nif"
            )
        ];

        DataRelativePathRepairBatchCoverageAuthorization result =
            DataRelativePathRepairBatchCoverageAuthorizer
                .Authorize(
                    candidates
                );

        Assert.False(
            result.AllAuthorized
        );

        Assert.Equal(
            0,
            result.AuthorizedCount
        );

        Assert.Equal(
            2,
            result.RejectedCount
        );

        Assert.All(
            result.Decisions,
            decision =>
                Assert.Equal(
                    DataRelativePathRepairBatchCoverageDecisionState
                        .ConflictingRequestedNamespace,
                    decision.State
                )
        );

        Assert.False(
            Directory.Exists(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "Alpha"
                )
            )
        );

        Assert.False(
            Directory.Exists(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "ALPHA"
                )
            )
        );
    }

    [Fact]
    public void
        Authorize_TwoFileOnlyCandidatesForSamePhysicalSource_RejectsDuplicateCoverage()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            CreateDataRoot(
                temp
            );

        string meshes =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes"
                )
            ).FullName;

        string physicalSource =
            Path.Combine(
                meshes,
                "thing.nif"
            );

        File.WriteAllText(
            physicalSource,
            "duplicate-file-only-source"
        );

        /*
         * Both requested paths fail only at the final file component and
         * therefore require no aggregate directory-branch coverage.
         *
         * They nevertheless describe two requested destinations backed by
         * the exact same physical source file. The batch authorizer must
         * reject both rather than independently authorizing two copies of
         * one source under competing spellings.
         */
        DataRelativePathRepairPlanProjection[] candidates =
        [
            Candidate(
                dataRoot,
                "meshes/Thing.nif"
            ),
            Candidate(
                dataRoot,
                "meshes/THING.nif"
            )
        ];

        Assert.All(
            candidates,
            candidate =>
                Assert.Equal(
                    candidate.Resolution
                            .EquivalentPhysicalCandidates
                            .Single(),
                    physicalSource
                )
        );

        Assert.All(
            candidates,
            candidate =>
                Assert.Equal(
                    candidate.Resolution
                            .RequestedPath
                            .Split('/')
                            .Length - 1,
                    candidate.Resolution
                        .FailedComponentIndex
                )
        );

        DataRelativePathRepairBatchCoverageAuthorization result =
            DataRelativePathRepairBatchCoverageAuthorizer
                .Authorize(
                    candidates
                );

        Assert.False(
            result.AllAuthorized
        );

        Assert.Equal(
            0,
            result.AuthorizedCount
        );

        Assert.Equal(
            2,
            result.RejectedCount
        );

        Assert.All(
            result.Decisions,
            decision =>
                Assert.Equal(
                    DataRelativePathRepairBatchCoverageDecisionState
                        .DuplicateSourceCoverage,
                    decision.State
                )
        );

        Assert.True(
            File.Exists(
                physicalSource
            )
        );

        Assert.False(
            File.Exists(
                Path.Combine(
                    meshes,
                    "Thing.nif"
                )
            )
        );

        Assert.False(
            File.Exists(
                Path.Combine(
                    meshes,
                    "THING.nif"
                )
            )
        );
    }

    [Fact]
    public void
        Authorize_CompleteNestedPhysicalSubtree_AuthorizesEveryLeaf()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            CreateDataRoot(
                temp
            );

        string physicalDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "alpha"
                )
            ).FullName;

        File.WriteAllText(
            Path.Combine(
                physicalDirectory,
                "Root.nif"
            ),
            "nested-root"
        );

        string nestedDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    physicalDirectory,
                    "nested"
                )
            ).FullName;

        File.WriteAllText(
            Path.Combine(
                nestedDirectory,
                "Thing1.nif"
            ),
            "nested-1"
        );

        File.WriteAllText(
            Path.Combine(
                nestedDirectory,
                "Thing2.nif"
            ),
            "nested-2"
        );

        DataRelativePathRepairPlanProjection[] candidates =
        [
            Candidate(
                dataRoot,
                "Meshes/Alpha/Root.nif"
            ),
            Candidate(
                dataRoot,
                "Meshes/Alpha/nested/Thing1.nif"
            ),
            Candidate(
                dataRoot,
                "Meshes/Alpha/nested/Thing2.nif"
            )
        ];

        DataRelativePathRepairBatchCoverageAuthorization result =
            DataRelativePathRepairBatchCoverageAuthorizer
                .Authorize(
                    candidates
                );

        Assert.True(
            result.AllAuthorized
        );

        Assert.Equal(
            3,
            result.AuthorizedCount
        );
    }

    [Fact]
    public void
        Authorize_OverlappingPhysicalBranchRootsAtDifferentFailedDepths_RejectsBoth()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            CreateDataRoot(
                temp
            );

        string nested =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "alpha",
                    "bar"
                )
            ).FullName;

        File.WriteAllText(
            Path.Combine(
                nested,
                "A.nif"
            ),
            "overlap-a"
        );

        File.WriteAllText(
            Path.Combine(
                nested,
                "B.nif"
            ),
            "overlap-b"
        );

        DataRelativePathRepairPlanProjection outer =
            Candidate(
                dataRoot,
                "Meshes/Alpha/bar/A.nif"
            );

        DataRelativePathRepairPlanProjection inner =
            Candidate(
                dataRoot,
                "meshes/alpha/Bar/B.nif"
            );

        Assert.Equal(
            1,
            outer.Resolution.FailedComponentIndex
        );

        Assert.Equal(
            2,
            inner.Resolution.FailedComponentIndex
        );

        DataRelativePathRepairBatchCoverageAuthorization result =
            DataRelativePathRepairBatchCoverageAuthorizer
                .Authorize(
                    [
                        outer,
                        inner
                    ]
                );

        Assert.False(
            result.AllAuthorized
        );

        Assert.Equal(
            0,
            result.AuthorizedCount
        );

        Assert.Equal(
            2,
            result.RejectedCount
        );

        Assert.All(
            result.Decisions,
            decision =>
                Assert.Equal(
                    DataRelativePathRepairBatchCoverageDecisionState
                        .IncompletePhysicalCoverage,
                    decision.State
                )
        );

        Assert.False(
            Directory.Exists(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "Alpha"
                )
            )
        );

        Assert.False(
            Directory.Exists(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "alpha",
                    "Bar"
                )
            )
        );
    }

    [Fact]
    public void
        AuthorizePersistedManifests_CompleteCoverage_AuthorizesEveryManifest()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            CreateDataRoot(
                temp
            );

        string physicalDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "alpha"
                )
            ).FullName;

        File.WriteAllText(
            Path.Combine(
                physicalDirectory,
                "Thing1.nif"
            ),
            "persisted-coverage-1"
        );

        File.WriteAllText(
            Path.Combine(
                physicalDirectory,
                "Thing2.nif"
            ),
            "persisted-coverage-2"
        );

        DataRelativePathRepairPlanProjection first =
            Candidate(
                dataRoot,
                "Meshes/Alpha/Thing1.nif"
            );

        DataRelativePathRepairPlanProjection second =
            Candidate(
                dataRoot,
                "Meshes/Alpha/Thing2.nif"
            );

        DataRelativePathRepairPlanManifestRecord[] manifests =
        [
            PersistedManifest(
                first
            ),
            PersistedManifest(
                second
            )
        ];

        Assert.All(
            manifests,
            manifest =>
            {
                Assert.Equal(
                    DataRelativePathRepairPlanManifestRecord
                        .SchemaVersion2,
                    manifest.SchemaVersion
                );

                Assert.Null(
                    DataRelativePathRepairPlanManifest.Validate(
                        manifest
                    )
                );
            }
        );

        DataRelativePathRepairBatchCoverageAuthorization result =
            DataRelativePathRepairBatchCoverageAuthorizer
                .AuthorizePersistedManifests(
                    manifests
                );

        Assert.True(
            result.AllAuthorized
        );

        Assert.Equal(
            2,
            result.AuthorizedCount
        );

        Assert.Equal(
            0,
            result.RejectedCount
        );

        Assert.All(
            result.Decisions,
            decision =>
                Assert.Equal(
                    DataRelativePathRepairBatchCoverageDecisionState
                        .Authorized,
                    decision.State
                )
        );
    }

    [Fact]
    public void
        AuthorizePersistedManifests_LegacySchemaV1_RejectsCandidate()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            CreateDataRoot(
                temp
            );

        string physicalDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "alpha"
                )
            ).FullName;

        File.WriteAllText(
            Path.Combine(
                physicalDirectory,
                "Thing1.nif"
            ),
            "legacy-v1-source"
        );

        DataRelativePathRepairPlanProjection candidate =
            Candidate(
                dataRoot,
                "meshes/Alpha/Thing1.nif"
            );

        DataRelativePathRepairPlanManifestCreation creation =
            DataRelativePathRepairPlanManifest.Create(
                Guid.NewGuid(),
                DateTimeOffset.UnixEpoch,
                candidate.Resolution.DataRoot,
                candidate.Resolution.RequestedPath,
                candidate.SourceSnapshot!,
                candidate.DestinationParentSnapshot!,
                candidate.Operations
            );

        Assert.True(
            creation.Success,
            creation.Error
        );

        DataRelativePathRepairPlanManifestRecord manifest =
            Assert.IsType<
                DataRelativePathRepairPlanManifestRecord
            >(
                creation.Manifest
            );

        Assert.Equal(
            DataRelativePathRepairPlanManifestRecord
                .SchemaVersion1,
            manifest.SchemaVersion
        );

        Assert.Null(
            DataRelativePathRepairPlanManifest.Validate(
                manifest
            )
        );

        DataRelativePathRepairBatchCoverageAuthorization result =
            DataRelativePathRepairBatchCoverageAuthorizer
                .AuthorizePersistedManifests(
                    [
                        manifest
                    ]
                );

        Assert.False(
            result.AllAuthorized
        );

        Assert.Equal(
            0,
            result.AuthorizedCount
        );

        Assert.Equal(
            1,
            result.RejectedCount
        );

        Assert.Equal(
            DataRelativePathRepairBatchCoverageDecisionState
                .InvalidCandidate,
            Assert.Single(
                result.Decisions
            ).State
        );
    }

    [Fact]
    public void
        AuthorizePersistedManifests_ValidManifestWithDifferentLogicalSourceSuffix_RejectsCandidate()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            CreateDataRoot(
                temp
            );

        string physicalDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "alpha"
                )
            ).FullName;

        File.WriteAllText(
            Path.Combine(
                physicalDirectory,
                "Thing1.nif"
            ),
            "logical-source-mismatch"
        );

        DataRelativePathRepairPlanProjection candidate =
            Candidate(
                dataRoot,
                "meshes/Alpha/Thing1.nif"
            );

        DataRelativePathRepairPlanManifestRecord original =
            PersistedManifest(
                candidate
            );

        DataRelativePathRepairPlanManifestOperation[]
            alteredOperations =
                original.Operations.ToArray();

        DataRelativePathRepairPlanManifestOperation
            finalEntry =
                alteredOperations[^1];

        string differentDestination =
            Path.Combine(
                dataRoot,
                "meshes",
                "Alpha",
                "Other.nif"
            );

        alteredOperations[^1] =
            finalEntry with
            {
                Operation =
                    finalEntry.Operation with
                    {
                        DestinationPath =
                            differentDestination
                    }
            };

        DataRelativePathRepairPlanManifestRecord altered =
            original with
            {
                RequestedPath =
                    "meshes/Alpha/Other.nif",
                Operations =
                    alteredOperations
            };

        /*
         * This is deliberately still a structurally valid schema-v2
         * manifest. Its source snapshot and final CreateFile source remain
         * mutually bound, and RequestedPath remains bound to the altered
         * destination operation.
         *
         * What it no longer proves is that source and destination describe
         * the same Windows-logical asset below the strict mismatch.
         */
        Assert.Null(
            DataRelativePathRepairPlanManifest.Validate(
                altered
            )
        );

        DataRelativePathRepairBatchCoverageAuthorization result =
            DataRelativePathRepairBatchCoverageAuthorizer
                .AuthorizePersistedManifests(
                    [
                        altered
                    ]
                );

        Assert.False(
            result.AllAuthorized
        );

        Assert.Equal(
            0,
            result.AuthorizedCount
        );

        Assert.Equal(
            1,
            result.RejectedCount
        );

        DataRelativePathRepairBatchCoverageDecision decision =
            Assert.Single(
                result.Decisions
            );

        Assert.Equal(
            DataRelativePathRepairBatchCoverageDecisionState
                .InvalidCandidateShape,
            decision.State
        );

        Assert.Contains(
            "not Windows-logically equivalent",
            decision.Error,
            StringComparison.Ordinal
        );
    }

    private static DataRelativePathRepairPlanManifestRecord
        PersistedManifest(
            DataRelativePathRepairPlanProjection candidate)
    {
        Assert.True(
            candidate.HasPlan,
            candidate.Error
        );

        DataRelativePathRepairPlanManifestCreation creation =
            DataRelativePathRepairPlanManifest.CreateFromResolution(
                Guid.NewGuid(),
                DateTimeOffset.UnixEpoch,
                candidate.Resolution,
                candidate.SourceSnapshot!,
                candidate.DestinationParentSnapshot!,
                candidate.Operations
            );

        Assert.True(
            creation.Success,
            creation.Error
        );

        return Assert.IsType<
            DataRelativePathRepairPlanManifestRecord
        >(
            creation.Manifest
        );
    }

    private static
        DataRelativePathRepairPlanProjection
        Candidate(
            string dataRoot,
            string requestedPath)
    {
        DataRelativePathResolution resolution =
            Resolve(
                dataRoot,
                requestedPath
            );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .DirectStrictCaseMismatch,
            DataRelativePathCaseMismatchTopologyClassifier
                .Classify(
                    resolution
                )
        );

        DataRelativePathRepairPlanProjection candidate =
            DataRelativePathRepairPlanProjector
                .ProjectBatchCandidate(
                    resolution
                );

        Assert.True(
            candidate.HasPlan,
            candidate.Error
        );

        return candidate;
    }

    private static string CreateDataRoot(
        TemporaryDirectory temp)
    {
        return Directory.CreateDirectory(
            Path.Combine(
                temp.RootPath,
                "Data"
            )
        ).FullName;
    }

    private static DataRelativePathResolution Resolve(
        string dataRoot,
        string requestedPath)
    {
        return DataRelativePathResolver.ResolveFile(
            dataRoot,
            requestedPath,
            path =>
                InspectFixtureCasefold(
                    path,
                    dataRoot
                )
        );
    }

    private static DirectoryCasefoldResult
        InspectFixtureCasefold(
            string path,
            string dataRoot)
    {
        string fullPath =
            Path.GetFullPath(
                path
            );

        bool casefoldEnabled =
            string.Equals(
                fullPath,
                Path.GetFullPath(
                    dataRoot
                ),
                StringComparison.Ordinal
            );

        return new DirectoryCasefoldResult(
            FullPath:
                fullPath,
            Exists:
                Directory.Exists(
                    fullPath
                ),
            CasefoldEnabled:
                casefoldEnabled,
            RawFlags:
                casefoldEnabled
                    ? LinuxDirectoryFlags
                        .FsCasefoldFlag
                    : 0L,
            Error:
                null
        );
    }

    private sealed class TemporaryDirectory
        : IDisposable
    {
        public TemporaryDirectory()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-batch-coverage-tests",
                    Guid.NewGuid()
                        .ToString(
                            "N"
                        )
                );

            Directory.CreateDirectory(
                RootPath
            );
        }

        public string RootPath { get; }

        public void Dispose()
        {
            if (
                Directory.Exists(
                    RootPath
                ))
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
