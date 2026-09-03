using CaseCompat.Core.Analysis;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class WindowsNamespaceMultipleFileContentInterpreterTests
{
    private const string HashA =
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    private const string HashB =
        "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB" +
        "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

    [Fact]
    public void Interpret_EmptyAnalysis_IsComplete()
    {
        WindowsNamespaceMultipleFileContentAnalysis content =
            new(
                Nodes:
                    Array.Empty<
                        WindowsNamespaceMultipleFileContentNodeAnalysis
                    >(),
                Errors:
                    Array.Empty<string>()
            );

        WindowsNamespaceMultipleFileContentInterpretation result =
            WindowsNamespaceMultipleFileContentInterpreter.Interpret(
                content
            );

        Assert.True(
            result.Complete
        );

        Assert.Empty(
            result.Nodes
        );

        Assert.Equal(
            0,
            result.IdenticalContentNodes
        );

        Assert.Equal(
            0,
            result.DivergentContentNodes
        );

        Assert.Equal(
            0,
            result.IndeterminateEvidenceNodes
        );
    }

    [Fact]
    public void Interpret_EqualStableSizeAndHash_IsIdenticalContent()
    {
        WindowsNamespaceMultipleFileContentAnalysis content =
            ContentAnalysis(
                Node(
                    "Meshes/Foo/Sword.nif",
                    StableFile(
                        "Meshes/Foo/Sword.nif",
                        size:
                            4,
                        sha256:
                            HashA,
                        inode:
                            100,
                        generation:
                            10
                    ),
                    StableFile(
                        "Meshes/Foo/sword.NIF",
                        size:
                            4,
                        sha256:
                            HashA,
                        inode:
                            101,
                        generation:
                            11
                    )
                )
            );

        WindowsNamespaceMultipleFileContentInterpretation result =
            WindowsNamespaceMultipleFileContentInterpreter.Interpret(
                content
            );

        WindowsNamespaceMultipleFileContentNodeInterpretation node =
            Assert.Single(
                result.Nodes
            );

        Assert.True(
            result.Complete
        );

        Assert.True(
            node.Determinate
        );

        Assert.Equal(
            WindowsNamespaceMultipleFileContentInterpretationState
                .IdenticalContent,
            node.State
        );

        Assert.Null(
            node.Error
        );

        Assert.Equal(
            1,
            result.IdenticalContentNodes
        );

        Assert.Equal(
            0,
            result.DivergentContentNodes
        );

        Assert.Equal(
            0,
            result.IndeterminateEvidenceNodes
        );
    }

    [Fact]
    public void Interpret_DifferentStableSize_IsDivergentContent()
    {
        WindowsNamespaceMultipleFileContentAnalysis content =
            ContentAnalysis(
                Node(
                    "Meshes/Foo/Sword.nif",
                    StableFile(
                        "Meshes/Foo/Sword.nif",
                        size:
                            4,
                        sha256:
                            HashA,
                        inode:
                            100,
                        generation:
                            10
                    ),
                    StableFile(
                        "Meshes/Foo/sword.NIF",
                        size:
                            8,
                        sha256:
                            HashA,
                        inode:
                            101,
                        generation:
                            11
                    )
                )
            );

        WindowsNamespaceMultipleFileContentInterpretation result =
            WindowsNamespaceMultipleFileContentInterpreter.Interpret(
                content
            );

        WindowsNamespaceMultipleFileContentNodeInterpretation node =
            Assert.Single(
                result.Nodes
            );

        Assert.True(
            result.Complete
        );

        Assert.Equal(
            WindowsNamespaceMultipleFileContentInterpretationState
                .DivergentContent,
            node.State
        );

        Assert.Equal(
            1,
            result.DivergentContentNodes
        );
    }

    [Fact]
    public void Interpret_SameStableSizeDifferentHash_IsDivergentContent()
    {
        WindowsNamespaceMultipleFileContentAnalysis content =
            ContentAnalysis(
                Node(
                    "Meshes/Foo/Sword.nif",
                    StableFile(
                        "Meshes/Foo/Sword.nif",
                        size:
                            4,
                        sha256:
                            HashA,
                        inode:
                            100,
                        generation:
                            10
                    ),
                    StableFile(
                        "Meshes/Foo/sword.NIF",
                        size:
                            4,
                        sha256:
                            HashB,
                        inode:
                            101,
                        generation:
                            11
                    )
                )
            );

        WindowsNamespaceMultipleFileContentInterpretation result =
            WindowsNamespaceMultipleFileContentInterpreter.Interpret(
                content
            );

        WindowsNamespaceMultipleFileContentNodeInterpretation node =
            Assert.Single(
                result.Nodes
            );

        Assert.True(
            result.Complete
        );

        Assert.Equal(
            WindowsNamespaceMultipleFileContentInterpretationState
                .DivergentContent,
            node.State
        );

        Assert.Equal(
            1,
            result.DivergentContentNodes
        );
    }

    [Fact]
    public void Interpret_FailedParticipantEvidence_IsIndeterminate()
    {
        WindowsNamespacePhysicalFileContentEvidence stable =
            StableFile(
                "Meshes/Foo/Sword.nif",
                size:
                    4,
                sha256:
                    HashA,
                inode:
                    100,
                generation:
                    10
            );

        WindowsNamespacePhysicalFileContentEvidence failed =
            StableFile(
                "Meshes/Foo/sword.NIF",
                size:
                    4,
                sha256:
                    HashA,
                inode:
                    101,
                generation:
                    11
            ) with
            {
                State =
                    WindowsNamespacePhysicalFileContentEvidenceState
                        .InitialReacquisitionFailed,
                InitialReacquisitionState =
                    WindowsNamespacePhysicalFileReacquisitionState
                        .FileIncarnationChanged,
                ContentObservation =
                    null,
                PostObservationReacquisitionState =
                    null,
                PostObservationIncarnation =
                    null,
                FailedComponent =
                    "sword.NIF",
                Error =
                    "Synthetic initial reacquisition failure."
            };

        WindowsNamespaceMultipleFileContentAnalysis content =
            ContentAnalysis(
                errors:
                    new[]
                    {
                        "Synthetic participant evidence failure."
                    },
                Node(
                    "Meshes/Foo/Sword.nif",
                    stable,
                    failed
                )
            );

        Assert.False(
            failed.Success
        );

        Assert.Null(
            failed.Size
        );

        Assert.Null(
            failed.Sha256
        );

        WindowsNamespaceMultipleFileContentInterpretation result =
            WindowsNamespaceMultipleFileContentInterpreter.Interpret(
                content
            );

        WindowsNamespaceMultipleFileContentNodeInterpretation node =
            Assert.Single(
                result.Nodes
            );

        Assert.False(
            result.Complete
        );

        Assert.False(
            node.Determinate
        );

        Assert.Equal(
            WindowsNamespaceMultipleFileContentInterpretationState
                .IndeterminateEvidence,
            node.State
        );

        Assert.NotNull(
            node.Error
        );

        Assert.Equal(
            1,
            result.IndeterminateEvidenceNodes
        );
    }

    [Fact]
    public void Interpret_PostObservationFailureWithInternalHash_IsIndeterminate()
    {
        WindowsNamespacePhysicalFileContentEvidence stable =
            StableFile(
                "Meshes/Foo/Sword.nif",
                size:
                    4,
                sha256:
                    HashA,
                inode:
                    100,
                generation:
                    10
            );

        WindowsNamespacePhysicalFileContentEvidence failedAfterHash =
            StableFile(
                "Meshes/Foo/sword.NIF",
                size:
                    4,
                sha256:
                    HashA,
                inode:
                    101,
                generation:
                    11
            ) with
            {
                State =
                    WindowsNamespacePhysicalFileContentEvidenceState
                        .PostObservationReacquisitionFailed,
                PostObservationReacquisitionState =
                    WindowsNamespacePhysicalFileReacquisitionState
                        .ExactFileSpellingUnavailable,
                PostObservationIncarnation =
                    null,
                FailedComponent =
                    "sword.NIF",
                Error =
                    "Synthetic post-observation spelling failure."
            };

        Assert.NotNull(
            failedAfterHash.ContentObservation
        );

        Assert.True(
            failedAfterHash.ContentObservation!.Success
        );

        Assert.Equal(
            HashA,
            failedAfterHash.ContentObservation.Sha256
        );

        /*
         * Checkpoint 9A deliberately withholds this internally computed
         * digest after the namespace proof fails.
         */
        Assert.False(
            failedAfterHash.Success
        );

        Assert.Null(
            failedAfterHash.Size
        );

        Assert.Null(
            failedAfterHash.Sha256
        );

        WindowsNamespaceMultipleFileContentAnalysis content =
            ContentAnalysis(
                errors:
                    new[]
                    {
                        "Synthetic post-observation failure."
                    },
                Node(
                    "Meshes/Foo/Sword.nif",
                    stable,
                    failedAfterHash
                )
            );

        WindowsNamespaceMultipleFileContentInterpretation result =
            WindowsNamespaceMultipleFileContentInterpreter.Interpret(
                content
            );

        WindowsNamespaceMultipleFileContentNodeInterpretation node =
            Assert.Single(
                result.Nodes
            );

        Assert.False(
            result.Complete
        );

        Assert.Equal(
            WindowsNamespaceMultipleFileContentInterpretationState
                .IndeterminateEvidence,
            node.State
        );

        Assert.Equal(
            1,
            result.IndeterminateEvidenceNodes
        );

        Assert.Equal(
            0,
            result.IdenticalContentNodes
        );

        Assert.Equal(
            0,
            result.DivergentContentNodes
        );
    }

    [Fact]
    public void Interpret_MixedNodes_PreservesLocalStatesButOverallIncomplete()
    {
        WindowsNamespaceMultipleFileContentAnalysis content =
            ContentAnalysis(
                errors:
                    new[]
                    {
                        "Synthetic failure belonging to one logical node."
                    },
                Node(
                    "Meshes/A/Thing.nif",
                    StableFile(
                        "Meshes/A/Thing.nif",
                        size:
                            4,
                        sha256:
                            HashA,
                        inode:
                            100,
                        generation:
                            10
                    ),
                    StableFile(
                        "Meshes/A/THING.NIF",
                        size:
                            4,
                        sha256:
                            HashA,
                        inode:
                            101,
                        generation:
                            11
                    )
                ),
                Node(
                    "Meshes/B/Thing.nif",
                    StableFile(
                        "Meshes/B/Thing.nif",
                        size:
                            4,
                        sha256:
                            HashA,
                        inode:
                            102,
                        generation:
                            12
                    ),
                    StableFile(
                        "Meshes/B/THING.NIF",
                        size:
                            4,
                        sha256:
                            HashB,
                        inode:
                            103,
                        generation:
                            13
                    )
                ),
                Node(
                    "Meshes/C/Thing.nif",
                    StableFile(
                        "Meshes/C/Thing.nif",
                        size:
                            4,
                        sha256:
                            HashA,
                        inode:
                            104,
                        generation:
                            14
                    ),
                    FailedAfterObservation(
                        "Meshes/C/THING.NIF",
                        inode:
                            105,
                        generation:
                            15
                    )
                )
            );

        WindowsNamespaceMultipleFileContentInterpretation result =
            WindowsNamespaceMultipleFileContentInterpreter.Interpret(
                content
            );

        Assert.False(
            result.Complete
        );

        Assert.Equal(
            3,
            result.Nodes.Count
        );

        Assert.Equal(
            1,
            result.IdenticalContentNodes
        );

        Assert.Equal(
            1,
            result.DivergentContentNodes
        );

        Assert.Equal(
            1,
            result.IndeterminateEvidenceNodes
        );

        Assert.Equal(
            WindowsNamespaceMultipleFileContentInterpretationState
                .IdenticalContent,
            result.Nodes[0].State
        );

        Assert.Equal(
            WindowsNamespaceMultipleFileContentInterpretationState
                .DivergentContent,
            result.Nodes[1].State
        );

        Assert.Equal(
            WindowsNamespaceMultipleFileContentInterpretationState
                .IndeterminateEvidence,
            result.Nodes[2].State
        );
    }

    [Fact]
    public void Interpret_NullParticipantEvidence_IsIndeterminate()
    {
        WindowsNamespaceMultipleFileContentAnalysis content =
            ContentAnalysis(
                errors:
                    new[]
                    {
                        "Synthetic malformed participant evidence."
                    },
                Node(
                    "Meshes/Foo/Sword.nif",
                    StableFile(
                        "Meshes/Foo/Sword.nif",
                        size:
                            4,
                        sha256:
                            HashA,
                        inode:
                            100,
                        generation:
                            10
                    ),
                    null!
                )
            );

        WindowsNamespaceMultipleFileContentInterpretation result =
            WindowsNamespaceMultipleFileContentInterpreter.Interpret(
                content
            );

        WindowsNamespaceMultipleFileContentNodeInterpretation node =
            Assert.Single(
                result.Nodes
            );

        Assert.False(
            result.Complete
        );

        Assert.Equal(
            WindowsNamespaceMultipleFileContentInterpretationState
                .IndeterminateEvidence,
            node.State
        );

        Assert.Contains(
            "null",
            node.Error!,
            StringComparison.OrdinalIgnoreCase
        );

        Assert.Empty(
            result.Errors
        );
    }

    [Fact]
    public void Interpret_MalformedSha256_IsIndeterminate()
    {
        WindowsNamespaceMultipleFileContentAnalysis content =
            ContentAnalysis(
                Node(
                    "Meshes/Foo/Sword.nif",
                    StableFile(
                        "Meshes/Foo/Sword.nif",
                        size:
                            4,
                        sha256:
                            HashA,
                        inode:
                            100,
                        generation:
                            10
                    ),
                    StableFile(
                        "Meshes/Foo/sword.NIF",
                        size:
                            4,
                        sha256:
                            new string(
                                'G',
                                64
                            ),
                        inode:
                            101,
                        generation:
                            11
                    )
                )
            );

        /*
         * Synthetic checkpoint-9A evidence can be constructed in memory with
         * an invalid digest even though its structural Success property is
         * true. 9B must refuse to interpret that fabricated digest.
         */
        Assert.True(
            content.Nodes[0].Files[1].Success
        );

        WindowsNamespaceMultipleFileContentInterpretation result =
            WindowsNamespaceMultipleFileContentInterpreter.Interpret(
                content
            );

        WindowsNamespaceMultipleFileContentNodeInterpretation node =
            Assert.Single(
                result.Nodes
            );

        Assert.False(
            result.Complete
        );

        Assert.Equal(
            WindowsNamespaceMultipleFileContentInterpretationState
                .IndeterminateEvidence,
            node.State
        );

        Assert.Equal(
            1,
            result.IndeterminateEvidenceNodes
        );

        Assert.Equal(
            0,
            result.IdenticalContentNodes
        );

        Assert.Equal(
            0,
            result.DivergentContentNodes
        );
    }

    [Fact]
    public void Interpret_NullNodeMember_FailsClosedWithoutThrowing()
    {
        WindowsNamespaceMultipleFileContentNodeAnalysis valid =
            Node(
                "Meshes/A/Thing.nif",
                StableFile(
                    "Meshes/A/Thing.nif",
                    size:
                        4,
                    sha256:
                        HashA,
                    inode:
                        100,
                    generation:
                        10
                ),
                StableFile(
                    "Meshes/A/THING.NIF",
                    size:
                        4,
                    sha256:
                        HashA,
                    inode:
                        101,
                    generation:
                        11
                )
            );

        WindowsNamespaceMultipleFileContentAnalysis content =
            ContentAnalysis(
                valid,
                null!
            );

        WindowsNamespaceMultipleFileContentInterpretation result =
            WindowsNamespaceMultipleFileContentInterpreter.Interpret(
                content
            );

        Assert.False(
            result.Complete
        );

        WindowsNamespaceMultipleFileContentNodeInterpretation interpreted =
            Assert.Single(
                result.Nodes
            );

        Assert.Equal(
            WindowsNamespaceMultipleFileContentInterpretationState
                .IdenticalContent,
            interpreted.State
        );

        Assert.Single(
            result.Errors
        );

        Assert.Contains(
            "index 1",
            result.Errors[0],
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Interpret_NullSourceCollections_FailClosedWithoutThrowing()
    {
        WindowsNamespaceMultipleFileContentAnalysis nullNodes =
            new(
                Nodes:
                    null!,
                Errors:
                    Array.Empty<string>()
            );

        WindowsNamespaceMultipleFileContentInterpretation nodeResult =
            WindowsNamespaceMultipleFileContentInterpreter.Interpret(
                nullNodes
            );

        Assert.False(
            nodeResult.Complete
        );

        Assert.Empty(
            nodeResult.Nodes
        );

        Assert.Single(
            nodeResult.Errors
        );

        WindowsNamespaceMultipleFileContentAnalysis nullErrors =
            new(
                Nodes:
                    Array.Empty<
                        WindowsNamespaceMultipleFileContentNodeAnalysis
                    >(),
                Errors:
                    null!
            );

        WindowsNamespaceMultipleFileContentInterpretation errorResult =
            WindowsNamespaceMultipleFileContentInterpreter.Interpret(
                nullErrors
            );

        Assert.False(
            errorResult.Complete
        );

        Assert.Empty(
            errorResult.Nodes
        );

        Assert.Single(
            errorResult.Errors
        );
    }

    private static WindowsNamespaceMultipleFileContentAnalysis
        ContentAnalysis(
            params WindowsNamespaceMultipleFileContentNodeAnalysis[] nodes)
    {
        return ContentAnalysis(
            Array.Empty<string>(),
            nodes
        );
    }

    private static WindowsNamespaceMultipleFileContentAnalysis
        ContentAnalysis(
            IReadOnlyList<string> errors,
            params WindowsNamespaceMultipleFileContentNodeAnalysis[] nodes)
    {
        return new WindowsNamespaceMultipleFileContentAnalysis(
            Nodes:
                nodes,
            Errors:
                errors
        );
    }

    private static WindowsNamespaceMultipleFileContentNodeAnalysis Node(
        string relativePath,
        params WindowsNamespacePhysicalFileContentEvidence[] files)
    {
        return new WindowsNamespaceMultipleFileContentNodeAnalysis(
            LogicalPath:
                WindowsLogicalPath.FromRelativePath(
                    relativePath
                ),
            Files:
                files
        );
    }

    private static WindowsNamespacePhysicalFileContentEvidence
        FailedAfterObservation(
            string relativePath,
            ulong inode,
            uint generation)
    {
        WindowsNamespacePhysicalFileContentEvidence stable =
            StableFile(
                relativePath,
                size:
                    4,
                sha256:
                    HashA,
                inode:
                    inode,
                generation:
                    generation
            );

        return stable with
        {
            State =
                WindowsNamespacePhysicalFileContentEvidenceState
                    .PostObservationReacquisitionFailed,
            PostObservationReacquisitionState =
                WindowsNamespacePhysicalFileReacquisitionState
                    .ExactFileSpellingUnavailable,
            PostObservationIncarnation =
                null,
            FailedComponent =
                stable.Participant.Name,
            Error =
                "Synthetic post-observation failure."
        };
    }

    private static WindowsNamespacePhysicalFileContentEvidence StableFile(
        string relativePath,
        long size,
        string sha256,
        ulong inode,
        uint generation)
    {
        string fullPath =
            "/fixture/Data/" +
            relativePath;

        string name =
            relativePath
                .Split('/')
                [^1];

        WindowsNamespacePhysicalParticipant participant =
            new(
                FullPath:
                    fullPath,
                RelativePath:
                    relativePath,
                Name:
                    name,
                Kind:
                    WindowsNamespacePhysicalObjectKind.File,
                DeviceMajor:
                    8,
                DeviceMinor:
                    1,
                Inode:
                    inode,
                MountId:
                    42,
                IdentityError:
                    null
            );

        WindowsNamespaceFileIncarnationObservation expected =
            new(
                FullPath:
                    fullPath,
                RelativePath:
                    relativePath,
                InodeGeneration:
                    generation,
                Error:
                    null
            );

        LinuxOpenedFileIdentityResult openedIdentity =
            new(
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
                    42,
                Errno:
                    null,
                Error:
                    null
            );

        LinuxOpenedInodeGenerationResult generationCapture =
            new(
                State:
                    LinuxOpenedInodeGenerationState.Captured,
                Generation:
                    generation,
                Errno:
                    null,
                Error:
                    null
            );

        LinuxFileIncarnationIdentity incarnationIdentity =
            new(
                PhysicalIdentity:
                    openedIdentity,
                InodeGeneration:
                    generation
            );

        LinuxOpenedFileIncarnationResult incarnation =
            new(
                State:
                    LinuxOpenedFileIncarnationState.Captured,
                PhysicalIdentity:
                    openedIdentity,
                GenerationCapture:
                    generationCapture,
                Identity:
                    incarnationIdentity,
                Error:
                    null
            );

        LinuxOpenedFileObservationStampResult stamp =
            new(
                State:
                    LinuxOpenedFileObservationStampState.Captured,
                Identity:
                    openedIdentity,
                Size:
                    size,
                ChangeTimeSeconds:
                    100,
                ChangeTimeNanoseconds:
                    0,
                ModificationTimeSeconds:
                    100,
                ModificationTimeNanoseconds:
                    0,
                Errno:
                    null,
                Error:
                    null
            );

        LinuxFileIdentityResult snapshotIdentity =
            new(
                FullPath:
                    fullPath,
                DeviceMajor:
                    8,
                DeviceMinor:
                    1,
                Inode:
                    inode,
                LinkCount:
                    1,
                MountId:
                    42,
                Error:
                    null
            );

        LinuxOpenedFileSnapshotResult snapshot =
            new(
                State:
                    LinuxOpenedFileSnapshotState.Captured,
                FullPath:
                    fullPath,
                Identity:
                    snapshotIdentity,
                Size:
                    size,
                Sha256:
                    sha256,
                Errno:
                    null,
                Error:
                    null
            );

        LinuxOpenedFileContentObservationResult content =
            new(
                State:
                    LinuxOpenedFileContentObservationState
                        .StableContentEvidence,
                DisplayPath:
                    fullPath,
                Before:
                    stamp,
                Snapshot:
                    snapshot,
                After:
                    stamp,
                Size:
                    size,
                Sha256:
                    sha256,
                Error:
                    null
            );

        return new WindowsNamespacePhysicalFileContentEvidence(
            Participant:
                participant,
            State:
                WindowsNamespacePhysicalFileContentEvidenceState
                    .StableContentEvidence,
            ExpectedIncarnationObservation:
                expected,
            InitialReacquisitionState:
                WindowsNamespacePhysicalFileReacquisitionState
                    .Reacquired,
            InitialIncarnation:
                incarnation,
            ContentObservation:
                content,
            PostObservationReacquisitionState:
                WindowsNamespacePhysicalFileReacquisitionState
                    .Reacquired,
            PostObservationIncarnation:
                incarnation,
            FailedComponent:
                null,
            Error:
                null
        );
    }
}
