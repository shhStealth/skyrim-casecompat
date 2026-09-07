using CaseCompat.Core.Analysis;
using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathAggregateNamespaceManifestPhysicalSpellingProjectorTests
{
    [Fact]
    public void Project_ValidManifestPreservesLogicalLeafAndSpellingOrder()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        IReadOnlyList<DataRelativePathAggregatePhysicalSpellingLeaf> result =
            DataRelativePathAggregateNamespaceManifestPhysicalSpellingProjector
                .Project(
                    manifest
                );

        Assert.Equal(
            new[]
            {
                "MESHES/ACTORS/BODY.TRI",
                "MESHES/ACTORS/HEAD.TRI"
            },
            result
                .Select(
                    leaf =>
                        leaf.WindowsLogicalPath
                )
                .ToArray()
        );

        Assert.Equal(
            new[]
            {
                "Meshes/Actors/Body.tri"
            },
            result[0].PhysicalRelativePaths.ToArray()
        );

        Assert.Equal(
            new[]
            {
                "Meshes/Actors/Head.tri",
                "meshes/actors/head.tri"
            },
            result[1].PhysicalRelativePaths.ToArray()
        );
    }

    [Fact]
    public void Project_PreservesExactPhysicalCasingWithoutContentState()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        IReadOnlyList<DataRelativePathAggregatePhysicalSpellingLeaf> result =
            DataRelativePathAggregateNamespaceManifestPhysicalSpellingProjector
                .Project(
                    manifest
                );

        DataRelativePathAggregatePhysicalSpellingLeaf equivalent =
            Assert.Single(
                result,
                leaf =>
                    leaf.WindowsLogicalPath ==
                    "MESHES/ACTORS/HEAD.TRI"
            );

        Assert.Equal(
            2,
            equivalent.PhysicalRelativePaths.Count
        );

        Assert.Equal(
            "Meshes/Actors/Head.tri",
            equivalent.PhysicalRelativePaths[0]
        );

        Assert.Equal(
            "meshes/actors/head.tri",
            equivalent.PhysicalRelativePaths[1]
        );
    }

    [Fact]
    public void Project_InvalidManifestFailsClosedBeforeProjection()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest() with
            {
                SchemaVersion =
                    DataRelativePathAggregateNamespaceManifestRecord
                        .SchemaVersion1 + 1
            };

        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () =>
                    DataRelativePathAggregateNamespaceManifestPhysicalSpellingProjector
                        .Project(
                            manifest
                        )
            );

        Assert.Equal(
            "manifest",
            exception.ParamName
        );

        Assert.Contains(
            "Unsupported",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Project_NullManifestIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                DataRelativePathAggregateNamespaceManifestPhysicalSpellingProjector
                    .Project(
                        null!
                    )
        );
    }

    private static DataRelativePathAggregateNamespaceManifestRecord
        CreateValidManifest()
    {
        const string dataRoot =
            "/game/Data";

        const string hashA =
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

        const string hashB =
            "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB" +
            "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

        var lookupObservations =
            new[]
            {
                Lookup(
                    dataRoot,
                    ".",
                    casefold:
                        true
                ),
                Lookup(
                    $"{dataRoot}/Meshes",
                    "Meshes",
                    casefold:
                        false
                ),
                Lookup(
                    $"{dataRoot}/Meshes/Actors",
                    "Meshes/Actors",
                    casefold:
                        false
                ),
                Lookup(
                    $"{dataRoot}/meshes",
                    "meshes",
                    casefold:
                        false
                ),
                Lookup(
                    $"{dataRoot}/meshes/actors",
                    "meshes/actors",
                    casefold:
                        false
                )
            };

        var incarnationObservations =
            new[]
            {
                DirectoryIncarnation(
                    dataRoot,
                    ".",
                    inode:
                        100,
                    generation:
                        1
                ),
                DirectoryIncarnation(
                    $"{dataRoot}/Meshes",
                    "Meshes",
                    inode:
                        200,
                    generation:
                        2
                ),
                DirectoryIncarnation(
                    $"{dataRoot}/Meshes/Actors",
                    "Meshes/Actors",
                    inode:
                        300,
                    generation:
                        4
                ),
                DirectoryIncarnation(
                    $"{dataRoot}/meshes",
                    "meshes",
                    inode:
                        201,
                    generation:
                        3
                ),
                DirectoryIncarnation(
                    $"{dataRoot}/meshes/actors",
                    "meshes/actors",
                    inode:
                        301,
                    generation:
                        5
                )
            };

        var uniqueLeaf =
            new DataRelativePathAggregateNamespaceManifestLogicalLeaf(
                WindowsLogicalPath:
                    "MESHES/ACTORS/BODY.TRI",
                State:
                    DataRelativePathAggregateLogicalLeafState
                        .UniqueRepresentation,
                PhysicalRepresentations:
                    new[]
                    {
                        Representation(
                            relativePath:
                                "Meshes/Actors/Body.tri",
                            physicalPath:
                                $"{dataRoot}/Meshes/Actors/Body.tri",
                            size:
                                10,
                            sha256:
                                hashA,
                            inode:
                                400,
                            generation:
                                10
                        )
                    }
            );

        var equivalentLeaf =
            new DataRelativePathAggregateNamespaceManifestLogicalLeaf(
                WindowsLogicalPath:
                    "MESHES/ACTORS/HEAD.TRI",
                State:
                    DataRelativePathAggregateLogicalLeafState
                        .EquivalentContentMultipleRepresentations,
                PhysicalRepresentations:
                    new[]
                    {
                        Representation(
                            relativePath:
                                "Meshes/Actors/Head.tri",
                            physicalPath:
                                $"{dataRoot}/Meshes/Actors/Head.tri",
                            size:
                                20,
                            sha256:
                                hashB,
                            inode:
                                401,
                            generation:
                                11
                        ),
                        Representation(
                            relativePath:
                                "meshes/actors/head.tri",
                            physicalPath:
                                $"{dataRoot}/meshes/actors/head.tri",
                            size:
                                20,
                            sha256:
                                hashB,
                            inode:
                                402,
                            generation:
                                12
                        )
                    }
            );

        return new(
            SchemaVersion:
                DataRelativePathAggregateNamespaceManifestRecord
                    .SchemaVersion1,
            CreatedUtc:
                new DateTimeOffset(
                    2026,
                    9,
                    5,
                    0,
                    0,
                    0,
                    TimeSpan.Zero
                ),
            DataRoot:
                dataRoot,
            RootWindowsLogicalPath:
                "MESHES",
            DataRootChildNames:
                new[]
                {
                    "Meshes",
                    "Textures",
                    "meshes"
                },
            DirectoryLookupObservations:
                lookupObservations,
            DirectoryIncarnationObservations:
                incarnationObservations,
            LogicalLeaves:
                new[]
                {
                    uniqueLeaf,
                    equivalentLeaf
                }
        );
    }

    private static WindowsNamespaceDirectoryLookupObservation Lookup(
        string fullPath,
        string relativePath,
        bool casefold)
    {
        return new(
            FullPath:
                fullPath,
            RelativePath:
                relativePath,
            CasefoldEnabled:
                casefold,
            RawFlags:
                casefold
                    ? 0x40000000L
                    : 0L,
            Error:
                null
        );
    }

    private static WindowsNamespaceDirectoryIncarnationObservation
        DirectoryIncarnation(
            string fullPath,
            string relativePath,
            ulong inode,
            uint generation)
    {
        return new(
            FullPath:
                fullPath,
            RelativePath:
                relativePath,
            DeviceMajor:
                8,
            DeviceMinor:
                1,
            Inode:
                inode,
            MountId:
                50,
            InodeGeneration:
                generation,
            Error:
                null
        );
    }

    private static
        DataRelativePathAggregateNamespaceManifestFileRepresentation
        Representation(
            string relativePath,
            string physicalPath,
            long size,
            string sha256,
            ulong inode,
            uint generation)
    {
        return new(
            RelativePath:
                relativePath,
            Snapshot:
                new DataRelativePathRepairSourceSnapshot(
                    PhysicalPath:
                        physicalPath,
                    Size:
                        size,
                    Sha256:
                        sha256,
                    Identity:
                        new LinuxFileIdentityResult(
                            FullPath:
                                physicalPath,
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
                            Error:
                                null
                        )
                ),
            InodeGeneration:
                generation
        );
    }
}
