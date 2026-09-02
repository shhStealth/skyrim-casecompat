using CaseCompat.Core.Repair;
using CaseCompat.Core.Resolution;
using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace CaseCompat.Tests;

public sealed class DataRelativePathRepairPlanProjectorTests
{
    [Fact]
    public void Project_DirectStrictMismatch_ProducesCreateOnlyPlanWithoutWriting()
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
                    "00Taliesin",
                    "freehorse"
                )
            ).FullName;

        string physicalFile =
            Path.Combine(
                physicalDirectory,
                "imperialsaddle.nif"
            );

        const string content =
            "projector-fixture";

        File.WriteAllText(
            physicalFile,
            content
        );

        DataRelativePathResolution resolution =
            Resolve(
                dataRoot,
                "Meshes/00Taliesin/FreeHorse/" +
                "imperialsaddle.nif"
            );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .DirectStrictCaseMismatch,
            DataRelativePathCaseMismatchTopologyClassifier
                .Classify(
                    resolution
                )
        );

        string requestedDirectory =
            Path.Combine(
                dataRoot,
                "meshes",
                "00Taliesin",
                "FreeHorse"
            );

        string requestedFile =
            Path.Combine(
                requestedDirectory,
                "imperialsaddle.nif"
            );

        Assert.False(
            Directory.Exists(
                requestedDirectory
            )
        );

        Assert.False(
            File.Exists(
                requestedFile
            )
        );

        DataRelativePathRepairPlanProjection projection =
            DataRelativePathRepairPlanProjector.Project(
                resolution
            );

        Assert.Equal(
            DataRelativePathRepairPlanProjectionState
                .Projected,
            projection.State
        );

        Assert.True(
            projection.HasPlan
        );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .DirectStrictCaseMismatch,
            projection.TopologyState
        );

        DataRelativePathRepairSourceSnapshot snapshot =
            Assert.IsType<
                DataRelativePathRepairSourceSnapshot
            >(
                projection.SourceSnapshot
            );

        Assert.Equal(
            Path.GetFullPath(
                physicalFile
            ),
            snapshot.PhysicalPath
        );

        Assert.Equal(
            Encoding.UTF8.GetByteCount(
                content
            ),
            snapshot.Size
        );

        string expectedHash =
            Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        content
                    )
                )
            );

        Assert.Equal(
            expectedHash,
            snapshot.Sha256
        );

        Assert.True(
            snapshot.Identity.Success
        );

        Assert.Equal(
            Path.GetFullPath(
                physicalFile
            ),
            snapshot.Identity.FullPath
        );

        string expectedDestinationParent =
            Path.Combine(
                dataRoot,
                "meshes",
                "00Taliesin"
            );

        DataRelativePathRepairDestinationParentSnapshot
            parentSnapshot =
                Assert.IsType<
                    DataRelativePathRepairDestinationParentSnapshot
                >(
                    projection.DestinationParentSnapshot
                );

        Assert.Equal(
            Path.GetFullPath(
                expectedDestinationParent
            ),
            parentSnapshot.PhysicalPath
        );

        Assert.False(
            parentSnapshot.CasefoldEnabled
        );

        Assert.Equal(
            0L,
            parentSnapshot.RawFlags &
            LinuxDirectoryFlags.FsCasefoldFlag
        );

        LinuxFileIdentityResult
            destinationParentIdentity =
                LinuxFileIdentity.Inspect(
                    expectedDestinationParent
                );

        Assert.True(
            destinationParentIdentity.Success
        );

        Assert.True(
            parentSnapshot.Identity.SameObjectAs(
                destinationParentIdentity
            )
        );

        Assert.Equal(
            2,
            projection.Operations.Count
        );

        DataRelativePathRepairPlanOperation createDirectory =
            projection.Operations[0];

        Assert.Equal(
            DataRelativePathRepairPlanOperationKind
                .CreateDirectory,
            createDirectory.Kind
        );

        Assert.Equal(
            requestedDirectory,
            createDirectory.DestinationPath
        );

        Assert.Null(
            createDirectory.SourcePath
        );

        DataRelativePathRepairPlanOperation createFile =
            projection.Operations[1];

        Assert.Equal(
            DataRelativePathRepairPlanOperationKind
                .CreateFile,
            createFile.Kind
        );

        Assert.Equal(
            requestedFile,
            createFile.DestinationPath
        );

        Assert.Equal(
            Path.GetFullPath(
                physicalFile
            ),
            createFile.SourcePath
        );

        Assert.Null(
            projection.Error
        );

        /*
         * A projected plan must also be representable by the durable
         * manifest when an already-resolved prefix differs only because
         * traversal occurred beneath a casefold-enabled parent.
         *
         * Here the Skyrim request begins with "Meshes", while the
         * physical existing directory beneath casefold-enabled Data is
         * "meshes". The strict mismatch being repaired occurs later at
         * FreeHorse/freehorse.
         */
        DataRelativePathRepairPlanManifestCreation manifestCreation =
            DataRelativePathRepairPlanManifest.CreateFromResolution(
                Guid.NewGuid(),
                DateTimeOffset.UnixEpoch,
                resolution,
                snapshot,
                parentSnapshot,
                projection.Operations
            );

        Assert.True(
            manifestCreation.Success,
            manifestCreation.Error
        );

        DataRelativePathRepairPlanManifestRecord manifest =
            Assert.IsType<
                DataRelativePathRepairPlanManifestRecord
            >(
                manifestCreation.Manifest
            );

        Assert.Equal(
            DataRelativePathRepairPlanManifestRecord
                .SchemaVersion2,
            manifest.SchemaVersion
        );

        Assert.NotNull(
            manifest.ResolvedPrefixSteps
        );

        Assert.Equal(
            2,
            manifest.ResolvedPrefixSteps!.Count
        );

        Assert.Equal(
            DataRelativePathRepairPlanResolvedPrefixStepKind
                .CasefoldEquivalent,
            manifest.ResolvedPrefixSteps[0].Kind
        );

        Assert.Equal(
            "Meshes",
            manifest.ResolvedPrefixSteps[0].RequestedComponent
        );

        Assert.Equal(
            "meshes",
            manifest.ResolvedPrefixSteps[0].SelectedPhysicalName
        );

        Assert.True(
            manifest.ResolvedPrefixSteps[0].ParentCasefoldEnabled
        );

        Assert.Equal(
            DataRelativePathRepairPlanResolvedPrefixStepKind
                .ExactSpelling,
            manifest.ResolvedPrefixSteps[1].Kind
        );

        Assert.Equal(
            "00Taliesin",
            manifest.ResolvedPrefixSteps[1].RequestedComponent
        );

        Assert.Equal(
            "00Taliesin",
            manifest.ResolvedPrefixSteps[1].SelectedPhysicalName
        );

        Assert.False(
            manifest.ResolvedPrefixSteps[1].ParentCasefoldEnabled
        );

        Assert.Equal(
            resolution.RequestedPath,
            manifest.RequestedPath
        );

        Assert.Equal(
            createFile.DestinationPath,
            manifest.Operations[^1].Operation.DestinationPath
        );

        /*
         * Schema-v2 prefix evidence is durable plan evidence, not merely
         * an in-memory creation aid. Prove that the exact resolver-derived
         * evidence survives the manifest JSON wire format and still
         * validates after deserialization.
         */
        byte[] manifestJson =
            DataRelativePathRepairPlanManifestJson.Serialize(
                manifest
            );

        DataRelativePathRepairPlanManifestRecord restoredManifest =
            Assert.IsType<
                DataRelativePathRepairPlanManifestRecord
            >(
                DataRelativePathRepairPlanManifestJson.Deserialize(
                    manifestJson
                )
            );

        Assert.Equal(
            DataRelativePathRepairPlanManifestRecord
                .SchemaVersion2,
            restoredManifest.SchemaVersion
        );

        Assert.NotNull(
            restoredManifest.ResolvedPrefixSteps
        );

        Assert.Equal(
            manifest.ResolvedPrefixSteps!.Count,
            restoredManifest.ResolvedPrefixSteps!.Count
        );

        for (
            int index = 0;
            index < manifest.ResolvedPrefixSteps.Count;
            index++)
        {
            DataRelativePathRepairPlanResolvedPrefixStep expectedStep =
                manifest.ResolvedPrefixSteps[index];

            DataRelativePathRepairPlanResolvedPrefixStep actualStep =
                restoredManifest.ResolvedPrefixSteps[index];

            Assert.Equal(
                expectedStep.ComponentIndex,
                actualStep.ComponentIndex
            );

            Assert.Equal(
                expectedStep.RequestedComponent,
                actualStep.RequestedComponent
            );

            Assert.Equal(
                expectedStep.ParentPhysicalPath,
                actualStep.ParentPhysicalPath
            );

            Assert.Equal(
                expectedStep.ParentCasefoldEnabled,
                actualStep.ParentCasefoldEnabled
            );

            Assert.Equal(
                expectedStep.Kind,
                actualStep.Kind
            );

            Assert.Equal(
                expectedStep.SelectedPhysicalName,
                actualStep.SelectedPhysicalName
            );

            Assert.Equal(
                expectedStep.EquivalentPhysicalNames.ToArray(),
                actualStep.EquivalentPhysicalNames.ToArray()
            );
        }

        Assert.Null(
            DataRelativePathRepairPlanManifest.Validate(
                restoredManifest
            )
        );

        /*
         * Only the component traversed beneath the casefold-enabled Data
         * root may differ in case. "00Taliesin" is beneath strict "meshes",
         * so changing its spelling must not be accepted merely because it
         * lies above the repair-created suffix.
         */
        DataRelativePathRepairPlanManifestRecord changedStrictPrefix =
            manifest with
            {
                RequestedPath =
                    "Meshes/00TALIESIN/FreeHorse/" +
                    "imperialsaddle.nif"
            };

        string? strictPrefixError =
            DataRelativePathRepairPlanManifest.Validate(
                changedStrictPrefix
            );

        Assert.NotNull(
            strictPrefixError
        );

        Assert.Contains(
            "final CreateFile destination",
            strictPrefixError,
            StringComparison.OrdinalIgnoreCase
        );

        // Projection is strictly read-only.
        Assert.False(
            Directory.Exists(
                requestedDirectory
            )
        );

        Assert.False(
            File.Exists(
                requestedFile
            )
        );

        Assert.True(
            File.Exists(
                physicalFile
            )
        );
    }

    [Fact]
    public void Project_CaseVariantDirectoryWithUntargetedContent_IsBlocked()
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

        string physicalArmorDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "1_soldier_replacer",
                    "guards",
                    "dawnstar",
                    "armor"
                )
            ).FullName;

        string physicalSource =
            Path.Combine(
                physicalArmorDirectory,
                "boots_f.nif"
            );

        string untargetedSibling =
            Path.Combine(
                physicalArmorDirectory,
                "armorf_0.nif"
            );

        File.WriteAllText(
            physicalSource,
            "targeted-boots-fixture"
        );

        File.WriteAllText(
            untargetedSibling,
            "untargeted-armor-fixture"
        );

        DataRelativePathResolution resolution =
            Resolve(
                dataRoot,
                "Meshes/1_Soldier_Replacer/Guards/" +
                "Dawnstar/Armor/Boots_F.nif"
            );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .DirectStrictCaseMismatch,
            DataRelativePathCaseMismatchTopologyClassifier
                .Classify(
                    resolution
                )
        );

        string requestedDirectory =
            Path.Combine(
                dataRoot,
                "meshes",
                "1_Soldier_Replacer"
            );

        Assert.False(
            Directory.Exists(
                requestedDirectory
            )
        );

        DataRelativePathRepairPlanProjection projection =
            DataRelativePathRepairPlanProjector.Project(
                resolution
            );

        /*
         * Creating a sparse parallel 1_Soldier_Replacer tree would
         * strand armorf_0.nif in the existing 1_soldier_replacer
         * hierarchy. Skyrim can then resolve through the new sparse
         * tree and report the untargeted armour mesh as missing.
         *
         * This is the real-world guard regression reproduced by the
         * full 1,103-plan acceptance test.
         */
        Assert.Equal(
            DataRelativePathRepairPlanProjectionState
                .DestinationConflict,
            projection.State
        );

        Assert.False(
            projection.HasPlan
        );

        Assert.Empty(
            projection.Operations
        );

        /*
         * The same resolver evidence is still technically projectable for
         * a future batch-wide coverage decision. This candidate is NOT
         * standalone authority: only an aggregate batch authorizer may
         * promote it after proving complete and consistent namespace
         * coverage.
         */
        DataRelativePathRepairPlanProjection batchCandidate =
            DataRelativePathRepairPlanProjector
                .ProjectBatchCandidate(
                    resolution
                );

        Assert.Equal(
            DataRelativePathRepairPlanProjectionState
                .Projected,
            batchCandidate.State
        );

        Assert.True(
            batchCandidate.HasPlan,
            batchCandidate.Error
        );

        Assert.NotEmpty(
            batchCandidate.Operations
        );

        Assert.Equal(
            physicalSource,
            batchCandidate.SourceSnapshot!.PhysicalPath
        );

        // Both projection modes themselves remain read-only.
        Assert.True(
            File.Exists(
                physicalSource
            )
        );

        Assert.True(
            File.Exists(
                untargetedSibling
            )
        );

        Assert.False(
            Directory.Exists(
                requestedDirectory
            )
        );
    }

    [Fact]
    public void Project_DestinationAppearsAfterResolution_IsBlocked()
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
                    "00Taliesin",
                    "freehorse"
                )
            ).FullName;

        string physicalFile =
            Path.Combine(
                physicalDirectory,
                "imperialsaddle.nif"
            );

        File.WriteAllText(
            physicalFile,
            "destination-conflict-fixture"
        );

        DataRelativePathResolution resolution =
            Resolve(
                dataRoot,
                "Meshes/00Taliesin/FreeHorse/" +
                "imperialsaddle.nif"
            );

        string requestedDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "00Taliesin",
                    "FreeHorse"
                )
            ).FullName;

        string requestedFile =
            Path.Combine(
                requestedDirectory,
                "imperialsaddle.nif"
            );

        DataRelativePathRepairPlanProjection projection =
            DataRelativePathRepairPlanProjector.Project(
                resolution
            );

        Assert.Equal(
            DataRelativePathRepairPlanProjectionState
                .DestinationConflict,
            projection.State
        );

        Assert.False(
            projection.HasPlan
        );

        Assert.Empty(
            projection.Operations
        );

        Assert.False(
            File.Exists(
                requestedFile
            )
        );
    }

    [Fact]
    public void Project_SourceDisappearsAfterResolution_IsBlocked()
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
                    "00Taliesin",
                    "freehorse"
                )
            ).FullName;

        string physicalFile =
            Path.Combine(
                physicalDirectory,
                "imperialsaddle.nif"
            );

        File.WriteAllText(
            physicalFile,
            "source-disappears-fixture"
        );

        DataRelativePathResolution resolution =
            Resolve(
                dataRoot,
                "Meshes/00Taliesin/FreeHorse/" +
                "imperialsaddle.nif"
            );

        File.Delete(
            physicalFile
        );

        DataRelativePathRepairPlanProjection projection =
            DataRelativePathRepairPlanProjector.Project(
                resolution
            );

        Assert.Equal(
            DataRelativePathRepairPlanProjectionState
                .SourceUnavailable,
            projection.State
        );

        Assert.False(
            projection.HasPlan
        );

        Assert.Null(
            projection.SourceSnapshot
        );

        Assert.Empty(
            projection.Operations
        );
    }

    [Fact]
    public void Project_AlternatePhysicalHierarchy_IsNotProjected()
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

        Directory.CreateDirectory(
            Path.Combine(
                dataRoot,
                "meshes",
                "Actors"
            )
        );

        string alternateDirectory =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "actors",
                    "atronachflame"
                )
            ).FullName;

        string physicalFile =
            Path.Combine(
                alternateDirectory,
                "fixture.nif"
            );

        File.WriteAllText(
            physicalFile,
            "alternate-hierarchy-fixture"
        );

        DataRelativePathResolution resolution =
            Resolve(
                dataRoot,
                "Meshes/Actors/AtronachFlame/" +
                "fixture.nif"
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
            DataRelativePathRepairPlanProjector.Project(
                resolution
            );

        Assert.Equal(
            DataRelativePathRepairPlanProjectionState
                .NotDirectStrictCaseMismatch,
            projection.State
        );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .CandidateBranchesBeforeFailure,
            projection.TopologyState
        );

        Assert.False(
            projection.HasPlan
        );

        Assert.Null(
            projection.SourceSnapshot
        );

        Assert.Empty(
            projection.Operations
        );
    }

    [Fact]
    public void Project_StrictMismatchAtFirstComponent_SnapshotsDataRoot()
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
                    "meshes"
                )
            ).FullName;

        string physicalFile =
            Path.Combine(
                physicalDirectory,
                "fixture.nif"
            );

        File.WriteAllText(
            physicalFile,
            "root-parent-fixture"
        );

        DataRelativePathResolution resolution =
            DataRelativePathResolver.ResolveFile(
                dataRoot,
                "Meshes/fixture.nif",
                path =>
                {
                    string fullPath =
                        Path.GetFullPath(
                            path
                        );

                    return new DirectoryCasefoldResult(
                        FullPath:
                            fullPath,
                        Exists:
                            Directory.Exists(
                                fullPath
                            ),
                        CasefoldEnabled:
                            false,
                        RawFlags:
                            0L,
                        Error:
                            null
                    );
                }
            );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .DirectStrictCaseMismatch,
            DataRelativePathCaseMismatchTopologyClassifier
                .Classify(
                    resolution
                )
        );

        Assert.Equal(
            0,
            resolution.FailedComponentIndex
        );

        DataRelativePathRepairPlanProjection projection =
            DataRelativePathRepairPlanProjector.Project(
                resolution
            );

        Assert.Equal(
            DataRelativePathRepairPlanProjectionState
                .Projected,
            projection.State
        );

        Assert.True(
            projection.HasPlan
        );

        DataRelativePathRepairDestinationParentSnapshot
            parentSnapshot =
                Assert.IsType<
                    DataRelativePathRepairDestinationParentSnapshot
                >(
                    projection.DestinationParentSnapshot
                );

        Assert.Equal(
            Path.GetFullPath(
                dataRoot
            ),
            parentSnapshot.PhysicalPath
        );

        Assert.False(
            parentSnapshot.CasefoldEnabled
        );

        LinuxFileIdentityResult dataIdentity =
            LinuxFileIdentity.Inspect(
                dataRoot
            );

        Assert.True(
            parentSnapshot.Identity.SameObjectAs(
                dataIdentity
            )
        );

        string requestedDirectory =
            Path.Combine(
                dataRoot,
                "Meshes"
            );

        string requestedFile =
            Path.Combine(
                requestedDirectory,
                "fixture.nif"
            );

        Assert.Equal(
            2,
            projection.Operations.Count
        );

        Assert.Equal(
            requestedDirectory,
            projection.Operations[0]
                .DestinationPath
        );

        Assert.Equal(
            requestedFile,
            projection.Operations[1]
                .DestinationPath
        );

        // Projection remains read-only.
        Assert.False(
            Directory.Exists(
                requestedDirectory
            )
        );
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
                    "casecompat-repair-plan-tests",
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
