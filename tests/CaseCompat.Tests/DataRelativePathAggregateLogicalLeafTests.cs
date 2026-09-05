using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class DataRelativePathAggregateLogicalLeafTests
{
    [Fact]
    public void Classify_OneRepresentation_IsUnique()
    {
        DataRelativePathAggregateLogicalLeaf leaf =
            DataRelativePathAggregateLogicalLeafClassifier
                .Classify(
                    "meshes/test/example.nif",
                    [
                        Snapshot(
                            "/tmp/Data/meshes/Test/example.nif",
                            10,
                            Hash('a'),
                            100
                        )
                    ]
                );

        Assert.Equal(
            DataRelativePathAggregateLogicalLeafState
                .UniqueRepresentation,
            leaf.State
        );

        Assert.Single(
            leaf.PhysicalRepresentations
        );
    }

    [Fact]
    public void
        Classify_MultipleRepresentationsWithEqualContent_AreEquivalent()
    {
        DataRelativePathAggregateLogicalLeaf leaf =
            DataRelativePathAggregateLogicalLeafClassifier
                .Classify(
                    "meshes/test/example.nif",
                    [
                        Snapshot(
                            "/tmp/Data/meshes/Test/example.nif",
                            10,
                            Hash('a'),
                            100
                        ),
                        Snapshot(
                            "/tmp/Data/meshes/test/example.nif",
                            10,
                            Hash('a'),
                            200
                        )
                    ]
                );

        Assert.Equal(
            DataRelativePathAggregateLogicalLeafState
                .EquivalentContentMultipleRepresentations,
            leaf.State
        );

        Assert.Equal(
            2,
            leaf.PhysicalRepresentations.Count
        );
    }

    [Fact]
    public void
        Classify_MultipleRepresentationsWithDifferentHashes_Conflict()
    {
        DataRelativePathAggregateLogicalLeaf leaf =
            DataRelativePathAggregateLogicalLeafClassifier
                .Classify(
                    "meshes/test/example.nif",
                    [
                        Snapshot(
                            "/tmp/Data/meshes/Test/example.nif",
                            10,
                            Hash('a'),
                            100
                        ),
                        Snapshot(
                            "/tmp/Data/meshes/test/example.nif",
                            10,
                            Hash('b'),
                            200
                        )
                    ]
                );

        Assert.Equal(
            DataRelativePathAggregateLogicalLeafState
                .ConflictingContentMultipleRepresentations,
            leaf.State
        );
    }

    [Fact]
    public void
        Classify_MultipleRepresentationsWithDifferentSizes_Conflict()
    {
        DataRelativePathAggregateLogicalLeaf leaf =
            DataRelativePathAggregateLogicalLeafClassifier
                .Classify(
                    "meshes/test/example.nif",
                    [
                        Snapshot(
                            "/tmp/Data/meshes/Test/example.nif",
                            10,
                            Hash('a'),
                            100
                        ),
                        Snapshot(
                            "/tmp/Data/meshes/test/example.nif",
                            11,
                            Hash('a'),
                            200
                        )
                    ]
                );

        Assert.Equal(
            DataRelativePathAggregateLogicalLeafState
                .ConflictingContentMultipleRepresentations,
            leaf.State
        );
    }

    [Fact]
    public void Classify_DoesNotSelectOrCollapsePhysicalIdentity()
    {
        DataRelativePathRepairSourceSnapshot first =
            Snapshot(
                "/tmp/Data/meshes/Test/example.nif",
                10,
                Hash('a'),
                100
            );

        DataRelativePathRepairSourceSnapshot second =
            Snapshot(
                "/tmp/Data/meshes/test/example.nif",
                10,
                Hash('a'),
                200
            );

        DataRelativePathAggregateLogicalLeaf leaf =
            DataRelativePathAggregateLogicalLeafClassifier
                .Classify(
                    "meshes/test/example.nif",
                    [
                        first,
                        second
                    ]
                );

        Assert.Equal(
            first,
            leaf.PhysicalRepresentations[0]
        );

        Assert.Equal(
            second,
            leaf.PhysicalRepresentations[1]
        );

        Assert.NotEqual(
            leaf.PhysicalRepresentations[0]
                .Identity.Inode,
            leaf.PhysicalRepresentations[1]
                .Identity.Inode
        );
    }

    [Fact]
    public void Classify_EmptyRepresentationSet_Throws()
    {
        Assert.Throws<ArgumentException>(
            () =>
                DataRelativePathAggregateLogicalLeafClassifier
                    .Classify(
                        "meshes/test/example.nif",
                        Array.Empty<
                            DataRelativePathRepairSourceSnapshot
                        >()
                    )
        );
    }

    private static DataRelativePathRepairSourceSnapshot Snapshot(
        string path,
        long size,
        string sha256,
        ulong inode)
    {
        return new(
            PhysicalPath:
                path,
            Size:
                size,
            Sha256:
                sha256,
            Identity:
                new LinuxFileIdentityResult(
                    FullPath:
                        path,
                    DeviceMajor:
                        8U,
                    DeviceMinor:
                        1U,
                    Inode:
                        inode,
                    LinkCount:
                        1U,
                    MountId:
                        1UL,
                    Error:
                        null
                )
        );
    }

    private static string Hash(char value) =>
        new(value, 64);
}
