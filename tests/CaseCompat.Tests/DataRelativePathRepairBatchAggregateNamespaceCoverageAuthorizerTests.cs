using CaseCompat.Core.Analysis;
using CaseCompat.Core.Repair;
using CaseCompat.Core.Resolution;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorizerTests
{
    [Fact]
    public void Authorize_ExactUniqueRepresentation_Authorizes()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespaceCoverageDecision decision =
            SingleDecision(
                fixture
            );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .Authorized,
            decision.State
        );

        Assert.True(
            decision.Authorized
        );
    }

    [Fact]
    public void Authorize_MultipleCandidates_PreservesInputDecisionOrder()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate missing =
            WithRequestedLeaf(
                fixture.Candidate,
                "OtherBrow.tri"
            );

        DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorization
            result =
                Authorize(
                    fixture,
                    candidates:
                        [
                            fixture.Candidate,
                            missing
                        ]
                );

        Assert.Equal(
            2,
            result.Decisions.Count
        );

        Assert.Equal(
            0,
            result.Decisions[0].CandidateIndex
        );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .Authorized,
            result.Decisions[0].State
        );

        Assert.Equal(
            1,
            result.Decisions[1].CandidateIndex
        );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .MissingLogicalLeaf,
            result.Decisions[1].State
        );
    }

    [Fact]
    public void Authorize_NullBatchManifest_Throws()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        Assert.Throws<ArgumentNullException>(
            () =>
                DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorizer
                    .Authorize(
                        null!,
                        [fixture.Candidate],
                        [fixture.Evidence]
                    )
        );
    }

    [Fact]
    public void Authorize_NullCandidates_Throws()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        Assert.Throws<ArgumentNullException>(
            () =>
                DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorizer
                    .Authorize(
                        fixture.Batch,
                        null!,
                        [fixture.Evidence]
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

        Fixture fixture =
            Fixture.Create();

        Assert.Throws<ArgumentNullException>(
            () =>
                DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorizer
                    .Authorize(
                        fixture.Batch,
                        [fixture.Candidate],
                        null!
                    )
        );
    }

    [Fact]
    public void Authorize_InvalidBatchManifest_RejectsEveryCandidate()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchManifestRecord invalid =
            fixture.Batch with
            {
                BatchId =
                    Guid.Empty
            };

        DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorization
            result =
                Authorize(
                    fixture,
                    batch:
                        invalid,
                    candidates:
                        [
                            fixture.Candidate,
                            fixture.Candidate
                        ]
                );

        Assert.Equal(
            2,
            result.Decisions.Count
        );

        Assert.All(
            result.Decisions,
            decision =>
                Assert.Equal(
                    DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                        .InvalidBatchManifest,
                    decision.State
                )
        );
    }

    [Fact]
    public void Authorize_NonSchemaV4Batch_RejectsEveryCandidate()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespaceCoverageDecision decision =
            SingleDecision(
                fixture,
                batch:
                    fixture.LegacyBatch
            );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .InvalidBatchManifest,
            decision.State
        );
    }

    [Fact]
    public void Authorize_NonPolicyV3Batch_RejectsEveryCandidate()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchManifestRecord changed =
            fixture.Batch with
            {
                CoveragePolicyVersion =
                    DataRelativePathRepairBatchManifestRecord
                        .CoveragePolicyVersion2
            };

        DataRelativePathRepairBatchAggregateNamespaceCoverageDecision decision =
            SingleDecision(
                fixture,
                batch:
                    changed
            );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .InvalidBatchManifest,
            decision.State
        );
    }

    [Fact]
    public void Authorize_NullCandidate_IsInvalidCandidate()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorization
            result =
                DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorizer
                    .Authorize(
                        fixture.Batch,
                        [
                            null!
                        ],
                        [
                            fixture.Evidence
                        ]
                    );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .InvalidCandidate,
            Assert.Single(
                result.Decisions
            ).State
        );
    }

    [Fact]
    public void Authorize_InvalidPlanManifest_IsInvalidCandidate()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate changed =
            fixture.Candidate with
            {
                Manifest =
                    fixture.Candidate.Manifest with
                    {
                        PlanId =
                            Guid.Empty
                    }
            };

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .InvalidCandidate,
            SingleDecision(
                fixture,
                candidate:
                    changed
            ).State
        );
    }

    [Fact]
    public void Authorize_NonSchemaV4Plan_IsInvalidCandidateShape()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate changed =
            new(
                Manifest:
                    fixture.LegacyPlan,
                SourceInodeGeneration:
                    fixture.Candidate.SourceInodeGeneration
            );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .InvalidCandidateShape,
            SingleDecision(
                fixture,
                candidate:
                    changed
            ).State
        );
    }

    [Fact]
    public void Authorize_CandidateDataRootMismatch_IsInvalidCandidateShape()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture first =
            Fixture.Create();

        Fixture second =
            Fixture.Create();

        Assert.NotEqual(
            first.DataRoot,
            second.DataRoot
        );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .InvalidCandidateShape,
            SingleDecision(
                first,
                candidate:
                    second.Candidate
            ).State
        );
    }

    [Fact]
    public void Authorize_UnreferencedSuppliedRoot_InvalidatesEvidenceSet()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespaceCoverageEvidence
            unreferenced =
                fixture.Evidence with
                {
                    RootWindowsLogicalPath =
                        "TEXTURES"
                };

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .InvalidNamespaceEvidenceSet,
            SingleDecision(
                fixture,
                evidence:
                    [
                        fixture.Evidence,
                        unreferenced
                    ]
            ).State
        );
    }

    [Fact]
    public void Authorize_MissingSuppliedRoot_IsMissingNamespaceEvidence()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .MissingNamespaceEvidence,
            SingleDecision(
                fixture,
                evidence:
                    Array.Empty<
                        DataRelativePathRepairBatchAggregateNamespaceCoverageEvidence
                    >()
            ).State
        );
    }

    [Fact]
    public void Authorize_DuplicateSuppliedRoot_IsAmbiguousNamespaceEvidence()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .AmbiguousNamespaceEvidence,
            SingleDecision(
                fixture,
                evidence:
                    [
                        fixture.Evidence,
                        fixture.Evidence
                    ]
            ).State
        );
    }

    [Fact]
    public void Authorize_FailedReaderResult_IsNamespaceEvidenceReadFailed()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathAggregateNamespaceManifestReaderResult failed =
            fixture.Reader with
            {
                State =
                    DataRelativePathAggregateNamespaceManifestReadState
                        .ManifestInvalid,
                Error =
                    "fixture reader failure"
            };

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .NamespaceEvidenceReadFailed,
            SingleDecision(
                fixture,
                evidence:
                    [
                        fixture.Evidence with
                        {
                            ReadResult =
                                failed
                        }
                    ]
            ).State
        );
    }

    [Fact]
    public void Authorize_SidecarSchemaMismatch_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathAggregateNamespaceManifestRecord changed =
            fixture.Sidecar with
            {
                SchemaVersion =
                    DataRelativePathAggregateNamespaceManifestRecord
                        .SchemaVersion1 + 1
            };

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .NamespaceEvidenceSchemaMismatch,
            SingleDecision(
                fixture,
                evidence:
                    [
                        EvidenceWithManifest(
                            fixture,
                            changed
                        )
                    ]
            ).State
        );
    }

    [Fact]
    public void Authorize_SidecarShaMismatch_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespaceEvidenceReference
            reference =
                Assert.Single(
                    fixture.Batch.AggregateNamespaceEvidence!
                );

        DataRelativePathRepairBatchManifestRecord changed =
            fixture.Batch with
            {
                AggregateNamespaceEvidence =
                    [
                        reference with
                        {
                            ManifestSha256 =
                                Hash(
                                    'b'
                                )
                        }
                    ]
            };

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .NamespaceEvidenceShaMismatch,
            SingleDecision(
                fixture,
                batch:
                    changed
            ).State
        );
    }

    [Fact]
    public void Authorize_SidecarShaHexCaseDifference_IsAccepted()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        string lower =
            Hash(
                'a'
            );

        string upper =
            lower.ToUpperInvariant();

        DataRelativePathRepairBatchAggregateNamespaceEvidenceReference
            reference =
                Assert.Single(
                    fixture.Batch.AggregateNamespaceEvidence!
                );

        DataRelativePathRepairBatchManifestRecord batch =
            fixture.Batch with
            {
                AggregateNamespaceEvidence =
                    [
                        reference with
                        {
                            ManifestSha256 =
                                upper
                        }
                    ]
            };

        DataRelativePathRepairBatchAggregateNamespaceCoverageEvidence evidence =
            fixture.Evidence with
            {
                ReadResult =
                    fixture.Reader with
                    {
                        ManifestSha256 =
                            lower
                    }
            };

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .Authorized,
            SingleDecision(
                fixture,
                batch:
                    batch,
                evidence:
                    [
                        evidence
                    ]
            ).State
        );
    }

    [Fact]
    public void Authorize_SidecarRootMismatch_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathAggregateNamespaceManifestRecord changed =
            fixture.Sidecar with
            {
                RootWindowsLogicalPath =
                    "TEXTURES"
            };

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .NamespaceEvidenceRootMismatch,
            SingleDecision(
                fixture,
                evidence:
                    [
                        EvidenceWithManifest(
                            fixture,
                            changed
                        )
                    ]
            ).State
        );
    }

    [Fact]
    public void Authorize_SidecarDataRootMismatch_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathAggregateNamespaceManifestRecord changed =
            fixture.Sidecar with
            {
                DataRoot =
                    Path.GetFullPath(
                        Path.Combine(
                            Path.GetTempPath(),
                            "casecompat-other-data-root"
                        )
                    )
            };

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .NamespaceEvidenceDataRootMismatch,
            SingleDecision(
                fixture,
                evidence:
                    [
                        EvidenceWithManifest(
                            fixture,
                            changed
                        )
                    ]
            ).State
        );
    }

    [Fact]
    public void Authorize_InvalidSidecarManifest_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathAggregateNamespaceManifestRecord changed =
            fixture.Sidecar with
            {
                CreatedUtc =
                    default
            };

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .InvalidNamespaceEvidence,
            SingleDecision(
                fixture,
                evidence:
                    [
                        EvidenceWithManifest(
                            fixture,
                            changed
                        )
                    ]
            ).State
        );
    }

    [Fact]
    public void Authorize_NoBatchReferenceForCandidateRoot_IsMissingNamespaceReference()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespaceEvidenceReference
            reference =
                Assert.Single(
                    fixture.Batch.AggregateNamespaceEvidence!
                );

        DataRelativePathRepairBatchManifestRecord changedBatch =
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

        DataRelativePathRepairBatchAggregateNamespaceCoverageEvidence
            changedEvidence =
                fixture.Evidence with
                {
                    RootWindowsLogicalPath =
                        "TEXTURES"
                };

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .MissingNamespaceReference,
            SingleDecision(
                fixture,
                batch:
                    changedBatch,
                evidence:
                    [
                        changedEvidence
                    ]
            ).State
        );
    }

    [Fact]
    public void Authorize_MissingLogicalLeaf_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate changed =
            WithRequestedLeaf(
                fixture.Candidate,
                "OtherBrow.tri"
            );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .MissingLogicalLeaf,
            SingleDecision(
                fixture,
                candidate:
                    changed
            ).State
        );
    }

    [Fact]
    public void Authorize_EquivalentContentMultipleRepresentations_IsCoveredButNotAuthorized()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespaceCoverageDecision decision =
            SingleDecision(
                fixture,
                evidence:
                    [
                        EquivalentEvidence(
                            fixture
                        )
                    ]
            );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .EquivalentContentMultipleRepresentations,
            decision.State
        );

        Assert.False(
            decision.Authorized
        );
    }

    [Fact]
    public void Authorize_EquivalentContentMatchToOneRepresentation_StillDoesNotSelectProvider()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorization
            result =
                Authorize(
                    fixture,
                    evidence:
                        [
                            EquivalentEvidence(
                                fixture
                            )
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
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .EquivalentContentMultipleRepresentations,
            Assert.Single(
                result.Decisions
            ).State
        );
    }

    [Fact]
    public void Authorize_ConflictingContentMultipleRepresentations_IsCoveredButNotAuthorized()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespaceCoverageDecision decision =
            SingleDecision(
                fixture,
                evidence:
                    [
                        ConflictingEvidence(
                            fixture
                        )
                    ]
            );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .ConflictingContentMultipleRepresentations,
            decision.State
        );

        Assert.False(
            decision.Authorized
        );
    }

    [Fact]
    public void Authorize_UniqueStateWithInvalidRepresentationCardinality_FailsClosed()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathAggregateNamespaceManifestFileRepresentation second =
            AlternateRepresentation(
                fixture,
                conflicting:
                    false
            );

        DataRelativePathAggregateNamespaceManifestLogicalLeaf leaf =
            fixture.Leaf with
            {
                State =
                    DataRelativePathAggregateLogicalLeafState
                        .UniqueRepresentation,
                PhysicalRepresentations =
                    [
                        fixture.Representation,
                        second
                    ]
            };

        DataRelativePathAggregateNamespaceManifestRecord changed =
            fixture.Sidecar with
            {
                LogicalLeaves =
                    [
                        leaf
                    ]
            };

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .InvalidNamespaceEvidence,
            SingleDecision(
                fixture,
                evidence:
                    [
                        EvidenceWithManifest(
                            fixture,
                            changed
                        )
                    ]
            ).State
        );
    }

    [Fact]
    public void Authorize_SourceRelativePathMismatch_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate changed =
            WithSourceLeafCase(
                fixture.Candidate,
                "maleheadbrows.tri"
            );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .SourceRepresentationMismatch,
            SingleDecision(
                fixture,
                candidate:
                    changed
            ).State
        );
    }

    [Fact]
    public void Authorize_SourcePhysicalPathMismatch_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate changed =
            WithSourceLeafCase(
                fixture.Candidate,
                "MALEHEADBROWS.tri"
            );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .SourceRepresentationMismatch,
            SingleDecision(
                fixture,
                candidate:
                    changed
            ).State
        );
    }

    [Fact]
    public void Authorize_SourceDeviceMajorMismatch_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate changed =
            WithIdentity(
                fixture.Candidate,
                identity =>
                    identity with
                    {
                        DeviceMajor =
                            (identity.DeviceMajor ?? 0U) +
                            1U
                    }
            );

        AssertSourceMismatch(
            fixture,
            changed
        );
    }

    [Fact]
    public void Authorize_SourceDeviceMinorMismatch_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate changed =
            WithIdentity(
                fixture.Candidate,
                identity =>
                    identity with
                    {
                        DeviceMinor =
                            (identity.DeviceMinor ?? 0U) +
                            1U
                    }
            );

        AssertSourceMismatch(
            fixture,
            changed
        );
    }

    [Fact]
    public void Authorize_SourceInodeMismatch_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate changed =
            WithIdentity(
                fixture.Candidate,
                identity =>
                    identity with
                    {
                        Inode =
                            (identity.Inode ?? 0UL) +
                            1UL
                    }
            );

        AssertSourceMismatch(
            fixture,
            changed
        );
    }

    [Fact]
    public void Authorize_SourceMountIdMismatch_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate changed =
            WithIdentity(
                fixture.Candidate,
                identity =>
                    identity with
                    {
                        MountId =
                            (identity.MountId ?? 0UL) +
                            1UL
                    }
            );

        AssertSourceMismatch(
            fixture,
            changed
        );
    }

    [Fact]
    public void Authorize_SourceInodeGenerationMismatch_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate changed =
            fixture.Candidate with
            {
                SourceInodeGeneration =
                    fixture.Candidate.SourceInodeGeneration +
                    1U
            };

        AssertSourceMismatch(
            fixture,
            changed
        );
    }

    [Fact]
    public void Authorize_SourceSizeMismatch_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate changed =
            WithSnapshot(
                fixture.Candidate,
                snapshot =>
                    snapshot with
                    {
                        Size =
                            snapshot.Size +
                            1
                    }
            );

        AssertSourceMismatch(
            fixture,
            changed
        );
    }

    [Fact]
    public void Authorize_SourceShaMismatch_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate changed =
            WithSnapshot(
                fixture.Candidate,
                snapshot =>
                    snapshot with
                    {
                        Sha256 =
                            Hash(
                                'f'
                            )
                    }
            );

        AssertSourceMismatch(
            fixture,
            changed
        );
    }

    [Fact]
    public void Authorize_SourceShaHexCaseDifference_IsAccepted()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate candidate =
            WithSnapshot(
                fixture.Candidate,
                snapshot =>
                    snapshot with
                    {
                        Sha256 =
                            Hash(
                                'a'
                            )
                    }
            );

        DataRelativePathRepairSourceSnapshot persistedSnapshot =
            fixture.Representation.Snapshot with
            {
                Sha256 =
                    Hash(
                        'A'
                    )
            };

        DataRelativePathAggregateNamespaceManifestFileRepresentation
            representation =
                fixture.Representation with
                {
                    Snapshot =
                        persistedSnapshot
                };

        DataRelativePathAggregateNamespaceManifestLogicalLeaf leaf =
            fixture.Leaf with
            {
                PhysicalRepresentations =
                    [
                        representation
                    ]
            };

        DataRelativePathAggregateNamespaceManifestRecord sidecar =
            fixture.Sidecar with
            {
                LogicalLeaves =
                    [
                        leaf
                    ]
            };

        Assert.Null(
            DataRelativePathAggregateNamespaceManifest.Validate(
                sidecar
            )
        );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .Authorized,
            SingleDecision(
                fixture,
                candidate:
                    candidate,
                evidence:
                    [
                        EvidenceWithManifest(
                            fixture,
                            sidecar
                        )
                    ]
            ).State
        );
    }

    [Fact]
    public void Authorize_DoesNotRequireFilesystemAccess()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Fixture fixture =
            Fixture.Create();

        Assert.False(
            Directory.Exists(
                fixture.FixtureRoot
            )
        );

        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .Authorized,
            SingleDecision(
                fixture
            ).State
        );
    }

    [Fact]
    public void PolicyV2AuthorizerSource_RemainsFrozenByPatchScope()
    {
        Assert.NotEqual(
            typeof(
                DataRelativePathRepairBatchCoverageAuthorizer
            ),
            typeof(
                DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorizer
            )
        );

        Assert.NotNull(
            typeof(
                DataRelativePathRepairBatchCoverageAuthorizer
            )
                .GetMethod(
                    "AuthorizeAggregateAlternateBranchPersistedManifests"
                )
        );
    }

    private static void AssertSourceMismatch(
        Fixture fixture,
        DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate
            candidate)
    {
        Assert.Equal(
            DataRelativePathRepairBatchAggregateNamespaceCoverageDecisionState
                .SourceRepresentationMismatch,
            SingleDecision(
                fixture,
                candidate:
                    candidate
            ).State
        );
    }

    private static
        DataRelativePathRepairBatchAggregateNamespaceCoverageDecision
        SingleDecision(
            Fixture fixture,
            DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate?
                candidate = null,
            DataRelativePathRepairBatchManifestRecord? batch = null,
            IReadOnlyList<
                DataRelativePathRepairBatchAggregateNamespaceCoverageEvidence
            >? evidence = null)
    {
        return Assert.Single(
            Authorize(
                fixture,
                candidates:
                    [
                        candidate ??
                        fixture.Candidate
                    ],
                batch:
                    batch,
                evidence:
                    evidence
            )
                .Decisions
        );
    }

    private static
        DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorization
        Authorize(
            Fixture fixture,
            IReadOnlyList<
                DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate
            >? candidates = null,
            DataRelativePathRepairBatchManifestRecord? batch = null,
            IReadOnlyList<
                DataRelativePathRepairBatchAggregateNamespaceCoverageEvidence
            >? evidence = null)
    {
        return
            DataRelativePathRepairBatchAggregateNamespaceCoverageAuthorizer
                .Authorize(
                    batch ??
                    fixture.Batch,
                    candidates ??
                    [
                        fixture.Candidate
                    ],
                    evidence ??
                    [
                        fixture.Evidence
                    ]
                );
    }

    private static
        DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate
        WithIdentity(
            DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate
                candidate,
            Func<
                LinuxFileIdentityResult,
                LinuxFileIdentityResult
            > transform)
    {
        return WithSnapshot(
            candidate,
            snapshot =>
                snapshot with
                {
                    Identity =
                        transform(
                            snapshot.Identity
                        )
                }
        );
    }

    private static
        DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate
        WithSnapshot(
            DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate
                candidate,
            Func<
                DataRelativePathRepairSourceSnapshot,
                DataRelativePathRepairSourceSnapshot
            > transform)
    {
        DataRelativePathRepairPlanManifestRecord changed =
            candidate.Manifest with
            {
                SourceSnapshot =
                    transform(
                        candidate.Manifest.SourceSnapshot
                    )
            };

        Assert.Null(
            DataRelativePathRepairPlanManifest.Validate(
                changed
            )
        );

        return candidate with
        {
            Manifest =
                changed
        };
    }

    private static
        DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate
        WithSourceLeafCase(
            DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate
                candidate,
            string sourceLeafName)
    {
        DataRelativePathRepairPlanManifestRecord manifest =
            candidate.Manifest;

        string sourcePath =
            ReplaceFileName(
                manifest.SourceSnapshot.PhysicalPath,
                sourceLeafName
            );

        LinuxFileIdentityResult sourceIdentity =
            manifest.SourceSnapshot.Identity with
            {
                FullPath =
                    sourcePath
            };

        DataRelativePathRepairSourceSnapshot sourceSnapshot =
            manifest.SourceSnapshot with
            {
                PhysicalPath =
                    sourcePath,
                Identity =
                    sourceIdentity
            };

        DataRelativePathRepairPlanManifestOperation[] operations =
            manifest.Operations.ToArray();

        int finalIndex =
            operations.Length - 1;

        operations[finalIndex] =
            operations[finalIndex] with
            {
                Operation =
                    operations[finalIndex].Operation with
                    {
                        SourcePath =
                            sourcePath
                    }
            };

        DataRelativePathRepairPlanManifestRecord changed =
            manifest with
            {
                SourceSnapshot =
                    sourceSnapshot,
                Operations =
                    operations
            };

        Assert.Null(
            DataRelativePathRepairPlanManifest.Validate(
                changed
            )
        );

        return candidate with
        {
            Manifest =
                changed
        };
    }

    private static
        DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate
        WithRequestedLeaf(
            DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate
                candidate,
            string leafName)
    {
        DataRelativePathRepairPlanManifestRecord manifest =
            candidate.Manifest;

        string requestedPath =
            ReplaceRelativeLeaf(
                manifest.RequestedPath,
                leafName
            );

        string sourcePath =
            ReplaceFileName(
                manifest.SourceSnapshot.PhysicalPath,
                leafName
            );

        LinuxFileIdentityResult sourceIdentity =
            manifest.SourceSnapshot.Identity with
            {
                FullPath =
                    sourcePath
            };

        DataRelativePathRepairSourceSnapshot sourceSnapshot =
            manifest.SourceSnapshot with
            {
                PhysicalPath =
                    sourcePath,
                Identity =
                    sourceIdentity
            };

        DataRelativePathRepairPlanManifestOperation[] operations =
            manifest.Operations.ToArray();

        int finalIndex =
            operations.Length - 1;

        operations[finalIndex] =
            operations[finalIndex] with
            {
                Operation =
                    operations[finalIndex].Operation with
                    {
                        DestinationPath =
                            ReplaceFileName(
                                operations[finalIndex]
                                    .Operation
                                    .DestinationPath,
                                leafName
                            ),
                        SourcePath =
                            sourcePath
                    }
            };

        DataRelativePathRepairPlanManifestRecord changed =
            manifest with
            {
                RequestedPath =
                    requestedPath,
                SourceSnapshot =
                    sourceSnapshot,
                Operations =
                    operations
            };

        Assert.Null(
            DataRelativePathRepairPlanManifest.Validate(
                changed
            )
        );

        return candidate with
        {
            Manifest =
                changed
        };
    }

    private static
        DataRelativePathRepairBatchAggregateNamespaceCoverageEvidence
        EquivalentEvidence(
            Fixture fixture)
    {
        DataRelativePathAggregateNamespaceManifestFileRepresentation second =
            AlternateRepresentation(
                fixture,
                conflicting:
                    false
            );

        DataRelativePathAggregateNamespaceManifestLogicalLeaf leaf =
            fixture.Leaf with
            {
                State =
                    DataRelativePathAggregateLogicalLeafState
                        .EquivalentContentMultipleRepresentations,
                PhysicalRepresentations =
                    [
                        fixture.Representation,
                        second
                    ]
            };

        DataRelativePathAggregateNamespaceManifestRecord manifest =
            fixture.Sidecar with
            {
                LogicalLeaves =
                    [
                        leaf
                    ]
            };

        Assert.Null(
            DataRelativePathAggregateNamespaceManifest.Validate(
                manifest
            )
        );

        return EvidenceWithManifest(
            fixture,
            manifest
        );
    }

    private static
        DataRelativePathRepairBatchAggregateNamespaceCoverageEvidence
        ConflictingEvidence(
            Fixture fixture)
    {
        DataRelativePathAggregateNamespaceManifestFileRepresentation second =
            AlternateRepresentation(
                fixture,
                conflicting:
                    true
            );

        DataRelativePathAggregateNamespaceManifestLogicalLeaf leaf =
            fixture.Leaf with
            {
                State =
                    DataRelativePathAggregateLogicalLeafState
                        .ConflictingContentMultipleRepresentations,
                PhysicalRepresentations =
                    [
                        fixture.Representation,
                        second
                    ]
            };

        DataRelativePathAggregateNamespaceManifestRecord manifest =
            fixture.Sidecar with
            {
                LogicalLeaves =
                    [
                        leaf
                    ]
            };

        Assert.Null(
            DataRelativePathAggregateNamespaceManifest.Validate(
                manifest
            )
        );

        return EvidenceWithManifest(
            fixture,
            manifest
        );
    }

    private static
        DataRelativePathAggregateNamespaceManifestFileRepresentation
        AlternateRepresentation(
            Fixture fixture,
            bool conflicting)
    {
        DataRelativePathAggregateNamespaceManifestFileRepresentation original =
            fixture.Representation;

        string relativePath =
            ReplaceRelativeLeaf(
                original.RelativePath,
                "maleheadbrows.tri"
            );

        string physicalPath =
            Path.GetFullPath(
                Path.Combine(
                    fixture.DataRoot,
                    relativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar
                    )
                )
            );

        LinuxFileIdentityResult identity =
            original.Snapshot.Identity with
            {
                FullPath =
                    physicalPath,
                Inode =
                    (original.Snapshot.Identity.Inode ?? 1UL) +
                    100UL
            };

        DataRelativePathRepairSourceSnapshot snapshot =
            original.Snapshot with
            {
                PhysicalPath =
                    physicalPath,
                Size =
                    conflicting
                        ? original.Snapshot.Size + 1
                        : original.Snapshot.Size,
                Identity =
                    identity
            };

        return new(
            RelativePath:
                relativePath,
            Snapshot:
                snapshot,
            InodeGeneration:
                original.InodeGeneration +
                1U
        );
    }

    private static
        DataRelativePathRepairBatchAggregateNamespaceCoverageEvidence
        EvidenceWithManifest(
            Fixture fixture,
            DataRelativePathAggregateNamespaceManifestRecord manifest)
    {
        return fixture.Evidence with
        {
            ReadResult =
                fixture.Reader with
                {
                    Manifest =
                        manifest
                }
        };
    }

    private static string ReplaceRelativeLeaf(
        string path,
        string leaf)
    {
        int separator =
            path.LastIndexOf(
                '/'
            );

        Assert.True(
            separator >= 0
        );

        return
            path[..(separator + 1)] +
            leaf;
    }

    private static string ReplaceFileName(
        string path,
        string leaf)
    {
        string? parent =
            Path.GetDirectoryName(
                path
            );

        Assert.False(
            string.IsNullOrEmpty(
                parent
            )
        );

        return Path.GetFullPath(
            Path.Combine(
                parent!,
                leaf
            )
        );
    }

    private static string Hash(
        char value) =>
            new(
                value,
                64
            );

    private sealed record Fixture(
        string FixtureRoot,
        string DataRoot,
        DataRelativePathRepairBatchManifestRecord Batch,
        DataRelativePathRepairBatchManifestRecord LegacyBatch,
        DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate
            Candidate,
        DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate
            OtherRootCandidate,
        DataRelativePathRepairPlanManifestRecord LegacyPlan,
        DataRelativePathRepairBatchAggregateNamespaceCoverageEvidence Evidence,
        DataRelativePathAggregateNamespaceManifestReaderResult Reader,
        DataRelativePathAggregateNamespaceManifestRecord Sidecar,
        DataRelativePathAggregateNamespaceManifestLogicalLeaf Leaf,
        DataRelativePathAggregateNamespaceManifestFileRepresentation
            Representation)
    {
        public static Fixture Create()
        {
            string fixtureRoot =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-policy-v3-" +
                    Guid.NewGuid()
                        .ToString(
                            "N"
                        )
                );

            Directory.CreateDirectory(
                fixtureRoot
            );

            try
            {
                string dataRoot =
                    Directory.CreateDirectory(
                        Path.Combine(
                            fixtureRoot,
                            "Data"
                        )
                    ).FullName;

                DataRelativePathRepairPlanManifestRecord meshesPlan =
                    BuildSchema4Plan(
                        dataRoot,
                        "meshes",
                        "mesh-source"
                    );

                DataRelativePathRepairPlanManifestRecord texturesPlan =
                    BuildSchema4Plan(
                        dataRoot,
                        "textures",
                        "texture-source"
                    );

                WindowsNamespaceAnalysis namespaceAnalysis =
                    WindowsNamespaceAnalyzer.Analyze(
                        dataRoot,
                        "meshes"
                    );

                Assert.True(
                    namespaceAnalysis.Complete,
                    string.Join(
                        Environment.NewLine,
                        namespaceAnalysis.Errors
                    )
                );

                WindowsNamespaceRegularFileContentAnalysis contentAnalysis =
                    WindowsNamespaceRegularFileContentAnalyzer.Analyze(
                        namespaceAnalysis
                    );

                Assert.True(
                    contentAnalysis.Complete,
                    string.Join(
                        Environment.NewLine,
                        contentAnalysis.Errors
                    )
                );

                DataRelativePathAggregateNamespaceManifestRecord sidecar =
                    WindowsNamespaceAggregateManifestProjector.Project(
                        namespaceAnalysis,
                        contentAnalysis,
                        new DateTimeOffset(
                            2026,
                            9,
                            5,
                            0,
                            0,
                            0,
                            TimeSpan.Zero
                        )
                    );

                Assert.Null(
                    DataRelativePathAggregateNamespaceManifest.Validate(
                        sidecar
                    )
                );

                string logicalPath =
                    WindowsLogicalPath
                        .FromRelativePath(
                            meshesPlan.RequestedPath
                        )
                        .Value;

                DataRelativePathAggregateNamespaceManifestLogicalLeaf leaf =
                    Assert.Single(
                        sidecar.LogicalLeaves,
                        item =>
                            string.Equals(
                                item.WindowsLogicalPath,
                                logicalPath,
                                StringComparison.Ordinal
                            )
                    );

                DataRelativePathAggregateNamespaceManifestFileRepresentation
                    representation =
                        Assert.Single(
                            leaf.PhysicalRepresentations
                        );

                Assert.Equal(
                    meshesPlan.SourceSnapshot.PhysicalPath,
                    representation.Snapshot.PhysicalPath
                );

                var candidate =
                    new DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate(
                        Manifest:
                            meshesPlan,
                        SourceInodeGeneration:
                            representation.InodeGeneration
                    );

                var otherRootCandidate =
                    new DataRelativePathRepairBatchAggregateNamespaceCoverageCandidate(
                        Manifest:
                            texturesPlan,
                        SourceInodeGeneration:
                            1U
                    );

                DataRelativePathRepairPlanManifestCreation legacyPlanCreation =
                    DataRelativePathRepairPlanManifest.Create(
                        Guid.NewGuid(),
                        DateTimeOffset.UtcNow,
                        meshesPlan.DataRoot,
                        meshesPlan.RequestedPath,
                        meshesPlan.SourceSnapshot,
                        meshesPlan.InitialDestinationParentSnapshot,
                        meshesPlan.Operations
                            .Select(
                                entry =>
                                    entry.Operation
                            )
                            .ToArray()
                    );

                Assert.True(
                    legacyPlanCreation.Success,
                    legacyPlanCreation.Error
                );

                DataRelativePathRepairPlanManifestRecord legacyPlan =
                    Assert.IsType<
                        DataRelativePathRepairPlanManifestRecord
                    >(
                        legacyPlanCreation.Manifest
                    );

                Assert.Null(
                    DataRelativePathRepairPlanManifest.Validate(
                        legacyPlan
                    )
                );

                string sidecarSha =
                    Hash(
                        'a'
                    );

                var reader =
                    new DataRelativePathAggregateNamespaceManifestReaderResult(
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
                            sidecarSha,
                        Error:
                            null
                    );

                Assert.True(
                    reader.Success
                );

                var evidence =
                    new DataRelativePathRepairBatchAggregateNamespaceCoverageEvidence(
                        RootWindowsLogicalPath:
                            sidecar.RootWindowsLogicalPath,
                        ReadResult:
                            reader
                    );

                var child =
                    new DataRelativePathRepairBatchManifestChild(
                        ChildName:
                            "plan-000001",
                        PlanId:
                            meshesPlan.PlanId,
                        ManifestSha256:
                            Hash(
                                'b'
                            )
                    );

                var batch =
                    new DataRelativePathRepairBatchManifestRecord(
                        SchemaVersion:
                            DataRelativePathRepairBatchManifestRecord
                                .SchemaVersion4,
                        BatchId:
                            Guid.NewGuid(),
                        CreatedUtc:
                            DateTimeOffset.UtcNow,
                        DataRoot:
                            dataRoot,
                        ChildManifestName:
                            "repair-plan.json",
                        InputPathCount:
                            1,
                        SafeRejectionCount:
                            0,
                        Children:
                            [
                                child
                            ]
                    )
                    {
                        CoveragePolicyVersion =
                            DataRelativePathRepairBatchManifestRecord
                                .CoveragePolicyVersion3,
                        AggregateNamespaceEvidence =
                            [
                                new(
                                    ManifestSchemaVersion:
                                        sidecar.SchemaVersion,
                                    RootWindowsLogicalPath:
                                        sidecar.RootWindowsLogicalPath,
                                    ManifestSha256:
                                        sidecarSha
                                )
                            ]
                    };

                Assert.Null(
                    DataRelativePathRepairBatchManifest.Validate(
                        batch
                    )
                );

                DataRelativePathRepairBatchManifestCreation
                    legacyBatchCreation =
                        DataRelativePathRepairBatchManifest.Create(
                            Guid.NewGuid(),
                            DateTimeOffset.UtcNow,
                            dataRoot,
                            "repair-plan.json",
                            1,
                            0,
                            [
                                child
                            ]
                        );

                Assert.True(
                    legacyBatchCreation.Success,
                    legacyBatchCreation.Error
                );

                DataRelativePathRepairBatchManifestRecord legacyBatch =
                    Assert.IsType<
                        DataRelativePathRepairBatchManifestRecord
                    >(
                        legacyBatchCreation.Manifest
                    );

                Assert.Null(
                    DataRelativePathRepairBatchManifest.Validate(
                        legacyBatch
                    )
                );

                return new(
                    FixtureRoot:
                        fixtureRoot,
                    DataRoot:
                        dataRoot,
                    Batch:
                        batch,
                    LegacyBatch:
                        legacyBatch,
                    Candidate:
                        candidate,
                    OtherRootCandidate:
                        otherRootCandidate,
                    LegacyPlan:
                        legacyPlan,
                    Evidence:
                        evidence,
                    Reader:
                        reader,
                    Sidecar:
                        sidecar,
                    Leaf:
                        leaf,
                    Representation:
                        representation
                );
            }
            finally
            {
                if (
                    Directory.Exists(
                        fixtureRoot
                    ))
                {
                    Directory.Delete(
                        fixtureRoot,
                        recursive:
                            true
                    );
                }
            }
        }

        private static DataRelativePathRepairPlanManifestRecord
            BuildSchema4Plan(
                string dataRoot,
                string rootName,
                string content)
        {
            string requestedParent =
                Directory.CreateDirectory(
                    Path.Combine(
                        dataRoot,
                        rootName,
                        "Actors",
                        "Character",
                        "Character Assets"
                    )
                ).FullName;

            string alternateParent =
                Directory.CreateDirectory(
                    Path.Combine(
                        dataRoot,
                        rootName,
                        "actors",
                        "character",
                        "character assets",
                        "faceparts"
                    )
                ).FullName;

            string sourceFile =
                Path.Combine(
                    alternateParent,
                    "MaleHeadbrows.tri"
                );

            File.WriteAllText(
                sourceFile,
                content
            );

            string requestedPath =
                $"{rootName}/Actors/Character/Character Assets/" +
                "FaceParts/MaleHeadbrows.tri";

            DataRelativePathResolution resolution =
                DataRelativePathResolver.ResolveFile(
                    dataRoot,
                    requestedPath,
                    path =>
                        InspectFixtureCasefold(
                            path,
                            dataRoot
                        )
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
                        Assert.IsType<
                            DataRelativePathRepairSourceSnapshot
                        >(
                            projection.SourceSnapshot
                        ),
                        Assert.IsType<
                            DataRelativePathRepairDestinationParentSnapshot
                        >(
                            projection.DestinationParentSnapshot
                        ),
                        projection.Operations
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
                    .SchemaVersion4,
                manifest.SchemaVersion
            );

            Assert.Null(
                DataRelativePathRepairPlanManifest.Validate(
                    manifest
                )
            );

            Assert.Equal(
                Path.GetFullPath(
                    requestedParent
                ),
                manifest.InitialDestinationParentSnapshot.PhysicalPath
            );

            return manifest;
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

            return new(
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
    }
}
