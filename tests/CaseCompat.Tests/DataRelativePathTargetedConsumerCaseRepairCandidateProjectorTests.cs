using CaseCompat.Core.Analysis;
using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathTargetedConsumerCaseRepairCandidateProjectorTests
{
    private const string HashA =
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    private const string HashB =
        "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB" +
        "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

    [Fact]
    public void Project_UniqueMismatchPublishesSourceBoundCandidate()
    {
        string dataRoot =
            DataRoot();

        DataRelativePathTargetedConsumerCaseRepairCandidateProjection result =
            DataRelativePathTargetedConsumerCaseRepairCandidateProjector.Project(
                dataRoot,
                Consumer(
                    "meshes/Actors/File.nif",
                    "meshes/Actors/File.nif"
                ),
                Analysis(
                    "meshes/Actors/File.nif",
                    Representation(
                        "meshes/actors/file.nif",
                        HashA,
                        inode:
                            100,
                        generation:
                            7
                    )
                )
            );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                .Complete,
            result.State
        );

        Assert.True(
            result.CandidateEvidenceComplete
        );

        Assert.True(
            result.HasCandidate
        );

        Assert.Equal(
            DataRelativePathAggregateConsumerCaseRepairPolicyState
                .UniqueRepresentationConsumerCaseMismatchCandidate,
            result.PolicyState
        );

        DataRelativePathTargetedConsumerCaseRepairCandidate candidate =
            Assert.IsType<
                DataRelativePathTargetedConsumerCaseRepairCandidate
            >(
                result.Candidate
            );

        Assert.Equal(
            "meshes/Actors/File.nif",
            candidate.AuthoritativeRequestedPath
        );

        Assert.Equal(
            "meshes/actors/file.nif",
            candidate.SourceRepresentation.RelativePath
        );

        Assert.Equal(
            PhysicalPath(
                dataRoot,
                "meshes/actors/file.nif"
            ),
            candidate.SourceSnapshot.PhysicalPath
        );

        Assert.Equal(
            HashA,
            candidate.SourceSnapshot.Sha256
        );

        Assert.Equal(
            7u,
            candidate.SourceInodeGeneration
        );
    }

    [Fact]
    public void Project_ExactPhysicalSpellingIsCompleteWithoutCandidate()
    {
        DataRelativePathTargetedConsumerCaseRepairCandidateProjection result =
            Project(
                consumerPath:
                    "meshes/Actors/File.nif",
                physical:
                    Representation(
                        "meshes/Actors/File.nif",
                        HashA,
                        inode:
                            101,
                        generation:
                            8
                    )
            );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                .Complete,
            result.State
        );

        Assert.Equal(
            DataRelativePathAggregateConsumerCaseRepairPolicyState
                .ExactPhysicalSpellingPresent,
            result.PolicyState
        );

        Assert.False(
            result.HasCandidate
        );
    }

    [Fact]
    public void Project_EquivalentContentMismatchRemainsBlocked()
    {
        DataRelativePathTargetedConsumerCaseRepairCandidateProjection result =
            Project(
                consumerPath:
                    "meshes/Actors/File.nif",
                physical:
                    new[]
                    {
                        Representation(
                            "meshes/actors/File.nif",
                            HashA,
                            inode:
                                102,
                            generation:
                                9
                        ),
                        Representation(
                            "meshes/ACTORS/file.NIF",
                            HashA,
                            inode:
                                103,
                            generation:
                                10
                        )
                    }
            );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                .Complete,
            result.State
        );

        Assert.Equal(
            DataRelativePathAggregateConsumerCaseRepairPolicyState
                .EquivalentContentMultipleRepresentationsSourcePolicyRequired,
            result.PolicyState
        );

        Assert.False(
            result.HasCandidate
        );
    }

    [Fact]
    public void Project_ConflictingContentMismatchRemainsRejected()
    {
        DataRelativePathTargetedConsumerCaseRepairCandidateProjection result =
            Project(
                consumerPath:
                    "meshes/Actors/File.nif",
                physical:
                    new[]
                    {
                        Representation(
                            "meshes/actors/File.nif",
                            HashA,
                            inode:
                                104,
                            generation:
                                11
                        ),
                        Representation(
                            "meshes/ACTORS/file.NIF",
                            HashB,
                            inode:
                                105,
                            generation:
                                12
                        )
                    }
            );

        Assert.Equal(
            DataRelativePathAggregateConsumerCaseRepairPolicyState
                .ConflictingContentMultipleRepresentationsRejected,
            result.PolicyState
        );

        Assert.False(
            result.HasCandidate
        );
    }

    [Fact]
    public void Project_ConflictingConsumerSpellingsRemainNonCandidate()
    {
        DataRelativePathAggregateConsumerSpellingEvidence consumer =
            Consumer(
                "meshes/Actors/File.nif",
                "meshes/Actors/File.nif",
                "meshes/actors/File.nif"
            );

        DataRelativePathTargetedConsumerCaseRepairCandidateProjection result =
            DataRelativePathTargetedConsumerCaseRepairCandidateProjector.Project(
                DataRoot(),
                consumer,
                Analysis(
                    "meshes/Actors/File.nif",
                    Representation(
                        "meshes/ACTORS/File.nif",
                        HashA,
                        inode:
                            106,
                        generation:
                            13
                    )
                )
            );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                .Complete,
            result.State
        );

        Assert.Equal(
            DataRelativePathAggregateConsumerCaseRepairPolicyState
                .ConflictingConsumerSpellings,
            result.PolicyState
        );

        Assert.False(
            result.HasCandidate
        );
    }

    [Fact]
    public void Project_NoConsumerEvidenceRemainsNonCandidate()
    {
        DataRelativePathAggregateConsumerSpellingEvidence consumer =
            Consumer(
                "meshes/Actors/File.nif"
            );

        DataRelativePathTargetedConsumerCaseRepairCandidateProjection result =
            DataRelativePathTargetedConsumerCaseRepairCandidateProjector.Project(
                DataRoot(),
                consumer,
                Analysis(
                    "meshes/Actors/File.nif",
                    Representation(
                        "meshes/actors/File.nif",
                        HashA,
                        inode:
                            107,
                        generation:
                            14
                    )
                )
            );

        Assert.Equal(
            DataRelativePathAggregateConsumerCaseRepairPolicyState
                .NoConsumerEvidence,
            result.PolicyState
        );

        Assert.False(
            result.HasCandidate
        );
    }

    [Fact]
    public void
        Project_NoPhysicalRepresentationPreservesConsumerAuthorityExplicitly()
    {
        DataRelativePathAggregateConsumerSpellingEvidence consumer =
            Consumer(
                "meshes/Actors/Missing.nif",
                "meshes/Actors/Missing.nif"
            );

        DataRelativePathTargetedConsumerCaseRepairCandidateProjection result =
            DataRelativePathTargetedConsumerCaseRepairCandidateProjector.Project(
                DataRoot(),
                consumer,
                Analysis(
                    "meshes/Actors/Missing.nif"
                )
            );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                .NoPhysicalRepresentation,
            result.State
        );

        Assert.True(
            result.CandidateEvidenceComplete
        );

        Assert.Equal(
            consumer.WindowsLogicalPath,
            result.ConsumerSpelling.WindowsLogicalPath
        );

        Assert.Equal(
            DataRelativePathAggregateConsumerSpellingState
                .UniqueConsumerSpelling,
            result.ConsumerSpelling.State
        );

        Assert.Equal(
            consumer.AuthoritativeRequestedPath,
            result.ConsumerSpelling.AuthoritativeRequestedPath
        );

        Assert.Equal(
            consumer.DistinctRequestedPaths,
            result.ConsumerSpelling.DistinctRequestedPaths
        );

        Assert.Null(
            result.SpellingEvidence
        );

        Assert.Null(
            result.PolicyState
        );

        Assert.False(
            result.HasCandidate
        );
    }

    [Fact]
    public void Project_FailedCurrentLeafAnalysisIsIndeterminatePhysicalEvidence()
    {
        DataRelativePathAggregateConsumerSpellingEvidence consumer =
            Consumer(
                "meshes/Actors/File.nif",
                "meshes/Actors/File.nif"
            );

        var failed =
            new DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis(
                State:
                    DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                        .HierarchyRevalidationFailed,
                RootWindowsLogicalPath:
                    "MESHES",
                LogicalPath:
                    WindowsLogicalPath.FromRelativePath(
                        "meshes/Actors/File.nif"
                    ),
                PhysicalRootNames:
                    null!,
                Representations:
                    null!,
                Error:
                    "current evidence changed"
            );

        DataRelativePathTargetedConsumerCaseRepairCandidateProjection result =
            DataRelativePathTargetedConsumerCaseRepairCandidateProjector.Project(
                DataRoot(),
                consumer,
                failed
            );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                .IndeterminatePhysicalEvidence,
            result.State
        );

        Assert.False(
            result.CandidateEvidenceComplete
        );

        Assert.False(
            result.HasCandidate
        );
    }

    [Fact]
    public void Project_LogicalLeafMismatchFailsClosed()
    {
        DataRelativePathTargetedConsumerCaseRepairCandidateProjection result =
            DataRelativePathTargetedConsumerCaseRepairCandidateProjector.Project(
                DataRoot(),
                Consumer(
                    "meshes/Actors/File.nif",
                    "meshes/Actors/File.nif"
                ),
                Analysis(
                    "textures/Actors/File.dds",
                    Representation(
                        "textures/actors/file.dds",
                        HashA,
                        inode:
                            108,
                        generation:
                            15
                    )
                )
            );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                .IndeterminateEvidence,
            result.State
        );

        Assert.False(
            result.CandidateEvidenceComplete
        );

        Assert.Contains(
            "does not match",
            result.Error!,
            StringComparison.OrdinalIgnoreCase
        );
    }

    private static
        DataRelativePathTargetedConsumerCaseRepairCandidateProjection
        Project(
            string consumerPath,
            params
                DataRelativePathRepairAggregateNamespaceCurrentFileRepresentation[]
                physical)
    {
        return
            DataRelativePathTargetedConsumerCaseRepairCandidateProjector.Project(
                DataRoot(),
                Consumer(
                    consumerPath,
                    consumerPath
                ),
                Analysis(
                    consumerPath,
                    physical
                )
            );
    }

    private static DataRelativePathAggregateConsumerSpellingEvidence Consumer(
        string logicalPath,
        params string[] requestedPaths)
    {
        return
            DataRelativePathAggregateConsumerSpellingClassifier.Classify(
                WindowsLogicalPath
                    .FromRelativePath(
                        logicalPath
                    )
                    .Value,
                requestedPaths
            );
    }

    private static
        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis
        Analysis(
            string requestedPath,
            params
                DataRelativePathRepairAggregateNamespaceCurrentFileRepresentation[]
                representations)
    {
        return new(
            State:
                DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                    .Analyzed,
            RootWindowsLogicalPath:
                "MESHES",
            LogicalPath:
                WindowsLogicalPath.FromRelativePath(
                    requestedPath
                ),
            PhysicalRootNames:
                new[]
                {
                    requestedPath
                        .Split(
                            '/',
                            StringSplitOptions.RemoveEmptyEntries
                        )[0]
                },
            Representations:
                representations,
            Error:
                null
        );
    }

    private static
        DataRelativePathRepairAggregateNamespaceCurrentFileRepresentation
        Representation(
            string relativePath,
            string sha256,
            ulong inode,
            uint generation)
    {
        return new(
            RelativePath:
                relativePath,
            IncarnationIdentity:
                new LinuxFileIncarnationIdentity(
                    PhysicalIdentity:
                        new LinuxOpenedFileIdentityResult(
                            State:
                                LinuxOpenedFileIdentityState.Captured,
                            DeviceMajor:
                                8,
                            DeviceMinor:
                                1,
                            Inode:
                                inode,
                            LinkCount:
                                1,
                            MountId:
                                50,
                            Errno:
                                null,
                            Error:
                                null
                        ),
                    InodeGeneration:
                        generation
                ),
            Size:
                10,
            Sha256:
                sha256
        );
    }

    private static string DataRoot()
    {
        return Path.GetFullPath(
            Path.Combine(
                Path.GetTempPath(),
                "casecompat-targeted-consumer-candidate-tests"
            )
        );
    }

    private static string PhysicalPath(
        string dataRoot,
        string relativePath)
    {
        return Path.GetFullPath(
            Path.Combine(
                dataRoot,
                relativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar
                )
            )
        );
    }
}
