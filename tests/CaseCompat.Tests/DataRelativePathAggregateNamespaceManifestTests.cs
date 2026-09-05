using CaseCompat.Core.Analysis;
using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using System.Text;

namespace CaseCompat.Tests;

public sealed partial class DataRelativePathAggregateNamespaceManifestTests
{
    [Fact]
    public void Validate_CompleteSchemaVersion1Evidence_IsAccepted()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        Assert.Null(
            DataRelativePathAggregateNamespaceManifest.Validate(
                manifest
            )
        );
    }

    [Fact]
    public void Json_RoundTripsSchemaVersion1_WithStringLeafState()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        byte[] bytes =
            DataRelativePathAggregateNamespaceManifestJson.Serialize(
                manifest
            );

        string json =
            Encoding.UTF8.GetString(
                bytes
            );

        Assert.Contains(
            "\"SchemaVersion\": 1",
            json
        );

        Assert.Contains(
            "\"State\": \"EquivalentContentMultipleRepresentations\"",
            json
        );

        Assert.DoesNotContain(
            "\"schemaVersion\"",
            json
        );

        DataRelativePathAggregateNamespaceManifestRecord? restored =
            DataRelativePathAggregateNamespaceManifestJson.Deserialize(
                bytes
            );

        Assert.NotNull(restored);

        Assert.Null(
            DataRelativePathAggregateNamespaceManifest.Validate(
                restored
            )
        );

        Assert.Equal(
            manifest.RootWindowsLogicalPath,
            restored.RootWindowsLogicalPath
        );

        Assert.Equal(
            2,
            restored.LogicalLeaves[1]
                .PhysicalRepresentations.Count
        );
    }

    [Fact]
    public void Validate_UnsupportedSchemaVersion_IsRejected()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest() with
            {
                SchemaVersion =
                    DataRelativePathAggregateNamespaceManifestRecord
                        .SchemaVersion1 + 1
            };

        string? error =
            DataRelativePathAggregateNamespaceManifest.Validate(
                manifest
            );

        Assert.NotNull(error);
        Assert.Contains(
            "Unsupported",
            error
        );
    }

    [Fact]
    public void Validate_MissingEquivalentRootDirectoryEvidence_IsRejected()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        manifest =
            manifest with
            {
                DirectoryLookupObservations =
                    manifest.DirectoryLookupObservations
                        .Where(observation =>
                            observation.RelativePath !=
                            "meshes")
                        .ToArray(),
                DirectoryIncarnationObservations =
                    manifest.DirectoryIncarnationObservations
                        .Where(observation =>
                            observation.RelativePath !=
                            "meshes")
                        .ToArray()
            };

        string? error =
            DataRelativePathAggregateNamespaceManifest.Validate(
                manifest
            );

        Assert.NotNull(error);
        Assert.Contains(
            "meshes",
            error
        );
    }

    [Fact]
    public void Validate_DirectoryObservationSetMismatch_IsRejected()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        manifest =
            manifest with
            {
                DirectoryIncarnationObservations =
                    manifest.DirectoryIncarnationObservations
                        .Where(observation =>
                            observation.RelativePath !=
                            "Meshes/Actors")
                        .ToArray()
            };

        string? error =
            DataRelativePathAggregateNamespaceManifest.Validate(
                manifest
            );

        Assert.NotNull(error);
        Assert.Contains(
            "same physical directory set",
            error
        );
    }

    [Fact]
    public void Validate_LeafStateMismatch_IsRejected()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        DataRelativePathAggregateNamespaceManifestLogicalLeaf[] leaves =
            manifest.LogicalLeaves.ToArray();

        leaves[1] =
            leaves[1] with
            {
                State =
                    DataRelativePathAggregateLogicalLeafState
                        .ConflictingContentMultipleRepresentations
            };

        manifest =
            manifest with
            {
                LogicalLeaves =
                    leaves
            };

        string? error =
            DataRelativePathAggregateNamespaceManifest.Validate(
                manifest
            );

        Assert.NotNull(error);
        Assert.Contains(
            "classification",
            error
        );
    }

    [Fact]
    public void Validate_DuplicateExactPhysicalPathWithinLeaf_IsRejected()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        DataRelativePathAggregateNamespaceManifestLogicalLeaf[] leaves =
            manifest.LogicalLeaves.ToArray();

        DataRelativePathAggregateNamespaceManifestLogicalLeaf leaf =
            leaves[1];

        DataRelativePathAggregateNamespaceManifestFileRepresentation
            duplicate =
                leaf.PhysicalRepresentations[0];

        leaves[1] =
            leaf with
            {
                PhysicalRepresentations =
                    new[]
                    {
                        duplicate,
                        duplicate
                    }
            };

        manifest =
            manifest with
            {
                LogicalLeaves =
                    leaves
            };

        string? error =
            DataRelativePathAggregateNamespaceManifest.Validate(
                manifest
            );

        Assert.NotNull(error);
        Assert.Contains(
            "occurs more than once",
            error
        );
    }

    [Fact]
    public void Validate_IncompleteSnapshotPhysicalIdentity_IsRejected()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        DataRelativePathAggregateNamespaceManifestLogicalLeaf[] leaves =
            manifest.LogicalLeaves.ToArray();

        DataRelativePathAggregateNamespaceManifestFileRepresentation
            representation =
                leaves[0].PhysicalRepresentations[0];

        DataRelativePathRepairSourceSnapshot snapshot =
            representation.Snapshot;

        leaves[0] =
            leaves[0] with
            {
                PhysicalRepresentations =
                    new[]
                    {
                        representation with
                        {
                            Snapshot =
                                snapshot with
                                {
                                    Identity =
                                        snapshot.Identity with
                                        {
                                            LinkCount =
                                                null
                                        }
                                }
                        }
                    }
            };

        manifest =
            manifest with
            {
                LogicalLeaves =
                    leaves
            };

        string? error =
            DataRelativePathAggregateNamespaceManifest.Validate(
                manifest
            );

        Assert.NotNull(error);
        Assert.Contains(
            "identity is incomplete",
            error
        );
    }

    [Fact]
    public void Validate_DistinctHardlinkedPhysicalPaths_AreAccepted()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        DataRelativePathAggregateNamespaceManifestLogicalLeaf leaf =
            manifest.LogicalLeaves[1];

        DataRelativePathAggregateNamespaceManifestFileRepresentation[] reps =
            leaf.PhysicalRepresentations.ToArray();

        LinuxFileIdentityResult firstIdentity =
            reps[0].Snapshot.Identity;

        LinuxFileIdentityResult secondIdentity =
            reps[1].Snapshot.Identity;

        reps[1] =
            reps[1] with
            {
                Snapshot =
                    reps[1].Snapshot with
                    {
                        Identity =
                            secondIdentity with
                            {
                                DeviceMajor =
                                    firstIdentity.DeviceMajor,
                                DeviceMinor =
                                    firstIdentity.DeviceMinor,
                                Inode =
                                    firstIdentity.Inode,
                                MountId =
                                    firstIdentity.MountId
                            }
                    }
            };

        DataRelativePathAggregateNamespaceManifestLogicalLeaf[] leaves =
            manifest.LogicalLeaves.ToArray();

        leaves[1] =
            leaf with
            {
                PhysicalRepresentations =
                    reps
            };

        manifest =
            manifest with
            {
                LogicalLeaves =
                    leaves
            };

        Assert.Null(
            DataRelativePathAggregateNamespaceManifest.Validate(
                manifest
            )
        );
    }

    [Fact]
    public void Validate_PhysicalRepresentationOutsideRoot_IsRejected()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        DataRelativePathAggregateNamespaceManifestLogicalLeaf[] leaves =
            manifest.LogicalLeaves.ToArray();

        DataRelativePathAggregateNamespaceManifestFileRepresentation
            representation =
                leaves[0].PhysicalRepresentations[0];

        leaves[0] =
            leaves[0] with
            {
                PhysicalRepresentations =
                    new[]
                    {
                        representation with
                        {
                            RelativePath =
                                "Textures/Actors/Body.tri"
                        }
                    }
            };

        manifest =
            manifest with
            {
                LogicalLeaves =
                    leaves
            };

        string? error =
            DataRelativePathAggregateNamespaceManifest.Validate(
                manifest
            );

        Assert.NotNull(error);
        Assert.Contains(
            "does not map",
            error
        );
    }

    [Fact]
    public void Validate_RepresentationPhysicalPathBindingMismatch_IsRejected()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        DataRelativePathAggregateNamespaceManifestLogicalLeaf[] leaves =
            manifest.LogicalLeaves.ToArray();

        DataRelativePathAggregateNamespaceManifestFileRepresentation
            representation =
                leaves[0].PhysicalRepresentations[0];

        string mismatchedPhysicalPath =
            $"{manifest.DataRoot}/Meshes/Actors/Other.tri";

        leaves[0] =
            leaves[0] with
            {
                PhysicalRepresentations =
                    new[]
                    {
                        representation with
                        {
                            Snapshot =
                                representation.Snapshot with
                                {
                                    PhysicalPath =
                                        mismatchedPhysicalPath,
                                    Identity =
                                        representation.Snapshot.Identity with
                                        {
                                            FullPath =
                                                mismatchedPhysicalPath
                                        }
                                }
                        }
                    }
            };

        manifest =
            manifest with
            {
                LogicalLeaves =
                    leaves
            };

        string? error =
            DataRelativePathAggregateNamespaceManifest.Validate(
                manifest
            );

        Assert.NotNull(error);
        Assert.Contains(
            "does not match its physical relative path",
            error
        );
    }

    [Fact]
    public void Validate_DirectoryFullPathBindingMismatch_IsRejected()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        WindowsNamespaceDirectoryLookupObservation[] lookups =
            manifest.DirectoryLookupObservations.ToArray();

        lookups[2] =
            lookups[2] with
            {
                FullPath =
                    $"{manifest.DataRoot}/Meshes/Wrong"
            };

        manifest =
            manifest with
            {
                DirectoryLookupObservations =
                    lookups
            };

        string? error =
            DataRelativePathAggregateNamespaceManifest.Validate(
                manifest
            );

        Assert.NotNull(error);
        Assert.Contains(
            "FullPath does not match",
            error
        );
    }

    [Fact]
    public void Validate_DataRootChildNames_NotStrictOrdinalOrder_IsRejected()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        manifest =
            manifest with
            {
                DataRootChildNames =
                    new[]
                    {
                        "meshes",
                        "Meshes",
                        "Textures"
                    }
            };

        string? error =
            DataRelativePathAggregateNamespaceManifest.Validate(
                manifest
            );

        Assert.NotNull(error);
        Assert.Contains(
            "strict ordinal order",
            error
        );
    }

    [Fact]
    public void Validate_DirectoryObservations_NotStrictOrdinalOrder_IsRejected()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        WindowsNamespaceDirectoryLookupObservation[] lookups =
            manifest.DirectoryLookupObservations.ToArray();

        (lookups[1], lookups[2]) =
            (lookups[2], lookups[1]);

        manifest =
            manifest with
            {
                DirectoryLookupObservations =
                    lookups
            };

        string? error =
            DataRelativePathAggregateNamespaceManifest.Validate(
                manifest
            );

        Assert.NotNull(error);
        Assert.Contains(
            "strict ordinal relative-path order",
            error
        );
    }

    [Fact]
    public void Validate_NonCanonicalDataRoot_IsRejected()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest() with
            {
                DataRoot =
                    "/game/./Data"
            };

        string? error =
            DataRelativePathAggregateNamespaceManifest.Validate(
                manifest
            );

        Assert.NotNull(error);
        Assert.Contains(
            "canonical absolute form",
            error
        );
    }

    [Fact]
    public void Validate_UnrelatedLinuxBackslashRootChild_IsAccepted()
    {
        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        manifest =
            manifest with
            {
                DataRootChildNames =
                    manifest.DataRootChildNames
                        .Append(
                            "odd\\physical-name"
                        )
                        .OrderBy(
                            name =>
                                name,
                            StringComparer.Ordinal
                        )
                        .ToArray()
            };

        Assert.Null(
            DataRelativePathAggregateNamespaceManifest.Validate(
                manifest
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
