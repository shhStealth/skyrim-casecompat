using CaseCompat.Core.Analysis;
using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class DataRelativePathTargetedPhysicalLeafProjectorTests
{
    private const string HashA =
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    private const string HashB =
        "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB" +
        "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

    [Fact]
    public void Project_UniqueRepresentationPreservesSnapshotAndGeneration()
    {
        string dataRoot =
            DataRoot();

        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis analysis =
            Analysis(
                "meshes/Test/File.nif",
                Representation(
                    "meshes/Test/File.nif",
                    HashA,
                    inode:
                        100,
                    generation:
                        7
                )
            );

        DataRelativePathTargetedPhysicalLeafProjection result =
            DataRelativePathTargetedPhysicalLeafProjector.Project(
                dataRoot,
                analysis
            );

        Assert.Equal(
            DataRelativePathTargetedPhysicalLeafProjectionState.Projected,
            result.State
        );

        Assert.True(
            result.EvidenceComplete
        );

        Assert.Same(
            analysis,
            result.CurrentLeafAnalysis
        );

        Assert.Equal(
            DataRelativePathAggregateLogicalLeafState.UniqueRepresentation,
            result.PhysicalState
        );

        DataRelativePathTargetedPhysicalFileRepresentation projected =
            Assert.Single(
                result.PhysicalRepresentations
            );

        Assert.Equal(
            "meshes/Test/File.nif",
            projected.RelativePath
        );

        Assert.Equal(
            PhysicalPath(
                dataRoot,
                "meshes/Test/File.nif"
            ),
            projected.Snapshot.PhysicalPath
        );

        Assert.Equal(
            100UL,
            projected.Snapshot.Identity.Inode
        );

        Assert.Equal(
            50UL,
            projected.Snapshot.Identity.MountId
        );

        Assert.Equal(
            HashA,
            projected.Snapshot.Sha256
        );

        Assert.Equal(
            7u,
            projected.InodeGeneration
        );
    }

    [Fact]
    public void Project_EquivalentMultipleRepresentationsAreClassified()
    {
        DataRelativePathTargetedPhysicalLeafProjection result =
            DataRelativePathTargetedPhysicalLeafProjector.Project(
                DataRoot(),
                Analysis(
                    "meshes/Actors/File.nif",
                    Representation(
                        "meshes/Actors/File.nif",
                        HashA,
                        inode:
                            101,
                        generation:
                            8
                    ),
                    Representation(
                        "meshes/actors/file.NIF",
                        HashA,
                        inode:
                            102,
                        generation:
                            9
                    )
                )
            );

        Assert.Equal(
            DataRelativePathAggregateLogicalLeafState
                .EquivalentContentMultipleRepresentations,
            result.PhysicalState
        );

        Assert.Equal(
            new[]
            {
                "meshes/Actors/File.nif",
                "meshes/actors/file.NIF"
            },
            result.PhysicalRepresentations
                .Select(
                    representation =>
                        representation.RelativePath
                )
                .ToArray()
        );
    }

    [Fact]
    public void Project_ConflictingMultipleRepresentationsAreClassified()
    {
        DataRelativePathTargetedPhysicalLeafProjection result =
            DataRelativePathTargetedPhysicalLeafProjector.Project(
                DataRoot(),
                Analysis(
                    "meshes/Actors/File.nif",
                    Representation(
                        "meshes/Actors/File.nif",
                        HashA,
                        inode:
                            103,
                        generation:
                            10
                    ),
                    Representation(
                        "meshes/actors/file.NIF",
                        HashB,
                        inode:
                            104,
                        generation:
                            11
                    )
                )
            );

        Assert.Equal(
            DataRelativePathAggregateLogicalLeafState
                .ConflictingContentMultipleRepresentations,
            result.PhysicalState
        );
    }

    [Fact]
    public void Project_NoPhysicalRepresentationIsCompleteEvidence()
    {
        DataRelativePathTargetedPhysicalLeafProjection result =
            DataRelativePathTargetedPhysicalLeafProjector.Project(
                DataRoot(),
                Analysis(
                    "meshes/Test/Missing.nif"
                )
            );

        Assert.Equal(
            DataRelativePathTargetedPhysicalLeafProjectionState
                .NoPhysicalRepresentation,
            result.State
        );

        Assert.True(
            result.EvidenceComplete
        );

        Assert.Empty(
            result.PhysicalRepresentations
        );

        Assert.Null(
            result.LogicalLeaf
        );

        Assert.Null(
            result.PhysicalState
        );
    }

    [Fact]
    public void
        Project_FailedCurrentLeafAnalysisDoesNotInspectMalformedRepresentations()
    {
        var failed =
            new DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis(
                State:
                    DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysisState
                        .HierarchyRevalidationFailed,
                RootWindowsLogicalPath:
                    "MESHES",
                LogicalPath:
                    WindowsLogicalPath.FromRelativePath(
                        "meshes/Test/File.nif"
                    ),
                PhysicalRootNames:
                    null!,
                Representations:
                    null!,
                Error:
                    "current evidence changed"
            );

        DataRelativePathTargetedPhysicalLeafProjection result =
            DataRelativePathTargetedPhysicalLeafProjector.Project(
                DataRoot(),
                failed
            );

        Assert.Equal(
            DataRelativePathTargetedPhysicalLeafProjectionState
                .CurrentLeafAnalysisFailed,
            result.State
        );

        Assert.False(
            result.EvidenceComplete
        );

        Assert.Empty(
            result.PhysicalRepresentations
        );

        Assert.Equal(
            "current evidence changed",
            result.Error
        );
    }

    [Fact]
    public void Project_MalformedPhysicalRelativePathFailsClosed()
    {
        DataRelativePathTargetedPhysicalLeafProjection result =
            DataRelativePathTargetedPhysicalLeafProjector.Project(
                DataRoot(),
                Analysis(
                    "meshes/Test/File.nif",
                    Representation(
                        "meshes/Test/../File.nif",
                        HashA,
                        inode:
                            105,
                        generation:
                            12
                    )
                )
            );

        Assert.Equal(
            DataRelativePathTargetedPhysicalLeafProjectionState.InvalidEvidence,
            result.State
        );

        Assert.False(
            result.EvidenceComplete
        );

        Assert.Empty(
            result.PhysicalRepresentations
        );

        Assert.NotNull(
            result.Error
        );
    }

    [Fact]
    public void Project_PhysicalLogicalPathMismatchFailsClosed()
    {
        DataRelativePathTargetedPhysicalLeafProjection result =
            DataRelativePathTargetedPhysicalLeafProjector.Project(
                DataRoot(),
                Analysis(
                    "meshes/Test/File.nif",
                    Representation(
                        "meshes/Other/File.nif",
                        HashA,
                        inode:
                            106,
                        generation:
                            13
                    )
                )
            );

        Assert.Equal(
            DataRelativePathTargetedPhysicalLeafProjectionState.InvalidEvidence,
            result.State
        );

        Assert.False(
            result.EvidenceComplete
        );

        Assert.Contains(
            "does not belong",
            result.Error!,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Project_NullAndBlankInputsAreRejected()
    {
        DataRelativePathRepairAggregateNamespaceCurrentLeafAnalysis analysis =
            Analysis(
                "meshes/Test/File.nif"
            );

        Assert.Throws<ArgumentNullException>(
            () =>
                DataRelativePathTargetedPhysicalLeafProjector.Project(
                    DataRoot(),
                    null!
                )
        );

        Assert.Throws<ArgumentException>(
            () =>
                DataRelativePathTargetedPhysicalLeafProjector.Project(
                    " ",
                    analysis
                )
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
                    "meshes"
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
                "casecompat-targeted-physical-leaf-tests"
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
