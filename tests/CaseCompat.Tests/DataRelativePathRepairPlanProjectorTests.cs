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
    public void ProjectAggregateAlternateBranchBatchCandidate_OneMissingParent_ProjectsDirectoryThenFile()
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

        string requestedParent =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "Actors",
                    "Character",
                    "Character Assets"
                )
            ).FullName;

        string alternateParent =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes",
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
            "aggregate-alternate-faceparts-fixture"
        );

        string destinationDirectory =
            Path.Combine(
                requestedParent,
                "FaceParts"
            );

        string destinationFile =
            Path.Combine(
                destinationDirectory,
                "MaleHeadbrows.tri"
            );

        Assert.False(
            Directory.Exists(
                destinationDirectory
            )
        );

        Assert.False(
            File.Exists(
                destinationFile
            )
        );

        DataRelativePathResolution resolution =
            Resolve(
                dataRoot,
                "meshes/Actors/Character/Character Assets/" +
                "FaceParts/MaleHeadbrows.tri"
            );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .CandidateBranchesBeforeFailure,
            DataRelativePathCaseMismatchTopologyClassifier
                .Classify(
                    resolution
                )
        );

        Assert.Equal(
            4,
            resolution.FailedComponentIndex
        );

        Assert.Single(
            resolution.EquivalentPhysicalCandidates
        );

        Assert.Equal(
            Path.GetFullPath(
                sourceFile
            ),
            Path.GetFullPath(
                resolution
                    .EquivalentPhysicalCandidates[0]
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

        Assert.Equal(
            DataRelativePathRepairPlanProjectionState
                .Projected,
            projection.State
        );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .CandidateBranchesBeforeFailure,
            projection.TopologyState
        );

        DataRelativePathRepairSourceSnapshot sourceSnapshot =
            Assert.IsType<
                DataRelativePathRepairSourceSnapshot
            >(
                projection.SourceSnapshot
            );

        Assert.Equal(
            Path.GetFullPath(
                sourceFile
            ),
            Path.GetFullPath(
                sourceSnapshot.PhysicalPath
            )
        );

        DataRelativePathRepairDestinationParentSnapshot
            destinationParentSnapshot =
                Assert.IsType<
                    DataRelativePathRepairDestinationParentSnapshot
                >(
                    projection.DestinationParentSnapshot
                );

        Assert.Equal(
            Path.GetFullPath(
                requestedParent
            ),
            Path.GetFullPath(
                destinationParentSnapshot.PhysicalPath
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
            Path.GetFullPath(
                destinationDirectory
            ),
            Path.GetFullPath(
                createDirectory.DestinationPath
            )
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
            Path.GetFullPath(
                destinationFile
            ),
            Path.GetFullPath(
                createFile.DestinationPath
            )
        );

        Assert.Equal(
            Path.GetFullPath(
                sourceFile
            ),
            Path.GetFullPath(
                createFile.SourcePath!
            )
        );

        Assert.Null(
            projection.Error
        );

        /*
         * The ordinary resolver-derived manifest factory must remain
         * leaf-only for CandidateBranchesBeforeFailure.  Missing-parent
         * aggregate topology has a separately named schema-v4 factory.
         */
        DataRelativePathRepairPlanManifestCreation legacyCreation =
            DataRelativePathRepairPlanManifest.CreateFromResolution(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                resolution,
                sourceSnapshot,
                destinationParentSnapshot,
                projection.Operations
            );

        Assert.False(
            legacyCreation.Success
        );

        Assert.Contains(
            "final requested file component",
            legacyCreation.Error ??
                string.Empty
        );

        DataRelativePathRepairPlanManifestCreation schema4Creation =
            DataRelativePathRepairPlanManifest
                .CreateAggregateAlternateBranchFromResolution(
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow,
                    resolution,
                    sourceSnapshot,
                    destinationParentSnapshot,
                    projection.Operations
                );

        Assert.True(
            schema4Creation.Success,
            schema4Creation.Error ??
                schema4Creation.State.ToString()
        );

        DataRelativePathRepairPlanManifestRecord schema4Manifest =
            Assert.IsType<
                DataRelativePathRepairPlanManifestRecord
            >(
                schema4Creation.Manifest
            );

        Assert.Equal(
            DataRelativePathRepairPlanManifestRecord
                .SchemaVersion4,
            schema4Manifest.SchemaVersion
        );

        /*
         * Current/default manifest meaning remains schema-v2.
         */
        Assert.Equal(
            DataRelativePathRepairPlanManifestRecord
                .SchemaVersion2,
            DataRelativePathRepairPlanManifestRecord
                .CurrentSchemaVersion
        );

        Assert.NotNull(
            schema4Manifest.ResolvedPrefixSteps
        );

        Assert.Equal(
            4,
            schema4Manifest.ResolvedPrefixSteps!.Count
        );

        Assert.Equal(
            2,
            schema4Manifest.Operations.Count
        );

        Assert.Equal(
            DataRelativePathRepairPlanOperationKind
                .CreateDirectory,
            schema4Manifest.Operations[0]
                .Operation.Kind
        );

        Assert.Equal(
            DataRelativePathRepairPlanOperationKind
                .CreateFile,
            schema4Manifest.Operations[1]
                .Operation.Kind
        );

        Assert.Null(
            DataRelativePathRepairPlanManifest.Validate(
                schema4Manifest
            )
        );

        /*
         * Fail closed if the durable prefix no longer proves the first
         * physical source/destination branch divergence.
         */
        string schema4SourceRelative =
            Path.GetRelativePath(
                dataRoot,
                sourceFile
            )
            .Replace(
                Path.DirectorySeparatorChar,
                '/'
            );

        string[] schema4SourceComponents =
            schema4SourceRelative.Split('/');

        DataRelativePathRepairPlanResolvedPrefixStep[]
            schema4BranchEvidence =
                schema4Manifest.ResolvedPrefixSteps!
                    .ToArray();

        int schema4BranchIndex =
            Array.FindIndex(
                schema4BranchEvidence,
                step =>
                    step.ComponentIndex <
                        schema4SourceComponents.Length &&
                    !string.Equals(
                        schema4SourceComponents[
                            step.ComponentIndex
                        ],
                        step.SelectedPhysicalName,
                        StringComparison.Ordinal
                    )
            );

        Assert.InRange(
            schema4BranchIndex,
            0,
            schema4BranchEvidence.Length - 1
        );

        DataRelativePathRepairPlanResolvedPrefixStep
            schema4BranchStep =
                schema4BranchEvidence[
                    schema4BranchIndex
                ];

        Assert.Contains(
            schema4SourceComponents[
                schema4BranchStep.ComponentIndex
            ],
            schema4BranchStep.EquivalentPhysicalNames
        );

        schema4BranchEvidence[
            schema4BranchIndex
        ] =
            schema4BranchStep with
            {
                EquivalentPhysicalNames =
                [
                    schema4BranchStep.SelectedPhysicalName
                ]
            };

        DataRelativePathRepairPlanManifestRecord
            missingSchema4BranchEvidence =
                schema4Manifest with
                {
                    ResolvedPrefixSteps =
                        schema4BranchEvidence
                };

        string? missingSchema4BranchError =
            DataRelativePathRepairPlanManifest.Validate(
                missingSchema4BranchEvidence
            );

        Assert.NotNull(
            missingSchema4BranchError
        );

        Assert.Contains(
            "branch divergence",
            missingSchema4BranchError ??
                string.Empty
        );

        /*
         * Projection is metadata only. The future aggregate caller still
         * has no durable persistence or execution authority here.
         */
        Assert.False(
            Directory.Exists(
                destinationDirectory
            )
        );

        Assert.False(
            File.Exists(
                destinationFile
            )
        );

        Assert.True(
            File.Exists(
                sourceFile
            )
        );
    }

    [Fact]
    public void ProjectAggregateAlternateBranchBatchCandidate_MissingParent_DoesNotBroadenExistingEntryPoints()
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

        string requestedParent =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "Actors",
                    "Character",
                    "Character Assets"
                )
            ).FullName;

        string alternateParent =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes",
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
            "aggregate-entry-point-isolation-fixture"
        );

        string destinationDirectory =
            Path.Combine(
                requestedParent,
                "FaceParts"
            );

        DataRelativePathResolution resolution =
            Resolve(
                dataRoot,
                "meshes/Actors/Character/Character Assets/" +
                "FaceParts/MaleHeadbrows.tri"
            );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .CandidateBranchesBeforeFailure,
            DataRelativePathCaseMismatchTopologyClassifier
                .Classify(
                    resolution
                )
        );

        Assert.Equal(
            4,
            resolution.FailedComponentIndex
        );

        DataRelativePathRepairPlanProjection standalone =
            DataRelativePathRepairPlanProjector
                .Project(
                    resolution
                );

        Assert.Equal(
            DataRelativePathRepairPlanProjectionState
                .NotDirectStrictCaseMismatch,
            standalone.State
        );

        Assert.False(
            standalone.HasPlan
        );

        Assert.Empty(
            standalone.Operations
        );

        DataRelativePathRepairPlanProjection existingBatch =
            DataRelativePathRepairPlanProjector
                .ProjectBatchCandidate(
                    resolution
                );

        Assert.Equal(
            DataRelativePathRepairPlanProjectionState
                .NotDirectStrictCaseMismatch,
            existingBatch.State
        );

        Assert.False(
            existingBatch.HasPlan
        );

        Assert.Empty(
            existingBatch.Operations
        );

        DataRelativePathRepairPlanProjection aggregate =
            DataRelativePathRepairPlanProjector
                .ProjectAggregateAlternateBranchBatchCandidate(
                    resolution
                );

        Assert.True(
            aggregate.HasPlan,
            aggregate.Error
        );

        Assert.Equal(
            2,
            aggregate.Operations.Count
        );

        Assert.Equal(
            DataRelativePathRepairPlanOperationKind
                .CreateDirectory,
            aggregate.Operations[0].Kind
        );

        Assert.Equal(
            DataRelativePathRepairPlanOperationKind
                .CreateFile,
            aggregate.Operations[1].Kind
        );

        /*
         * Merely projecting through the dormant aggregate entry point must
         * not publish the requested parallel namespace.
         */
        Assert.False(
            Directory.Exists(
                destinationDirectory
            )
        );

        Assert.True(
            File.Exists(
                sourceFile
            )
        );
    }

    [Fact]
    public void Project_LeafOnlyAlternatePhysicalHierarchy_ProjectsSingleCreateFile()
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

        string requestedParent =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "Actors",
                    "Character",
                    "Character Assets"
                )
            ).FullName;

        string alternateParent =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "actors",
                    "character",
                    "character assets"
                )
            ).FullName;

        string sourceFile =
            Path.Combine(
                alternateParent,
                "MaleHead.tri"
            );

        File.WriteAllText(
            sourceFile,
            "facegen-leaf-alternate-branch-fixture"
        );

        string destinationFile =
            Path.Combine(
                requestedParent,
                "MaleHead.tri"
            );

        Assert.False(
            File.Exists(
                destinationFile
            )
        );

        DataRelativePathResolution resolution =
            Resolve(
                dataRoot,
                "Meshes/Actors/Character/Character Assets/" +
                "MaleHead.tri"
            );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .CandidateBranchesBeforeFailure,
            DataRelativePathCaseMismatchTopologyClassifier
                .Classify(
                    resolution
                )
        );

        Assert.Equal(
            4,
            resolution.FailedComponentIndex
        );

        Assert.Single(
            resolution.EquivalentPhysicalCandidates
        );

        Assert.Equal(
            Path.GetFullPath(
                sourceFile
            ),
            Path.GetFullPath(
                resolution
                    .EquivalentPhysicalCandidates[0]
            )
        );

        DataRelativePathRepairPlanProjection projection =
            DataRelativePathRepairPlanProjector.Project(
                resolution
            );

        /*
         * This is intentionally narrower than general
         * CandidateBranchesBeforeFailure support.
         *
         * The exact requested destination parent already exists.
         * Only the final file component is missing, so the safe repair
         * shape requires no parallel directory hierarchy at all:
         *
         *     existing requested parent
         *         + one CreateFile from the unique equivalent source.
         */
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
                .CandidateBranchesBeforeFailure,
            projection.TopologyState
        );

        DataRelativePathRepairSourceSnapshot sourceSnapshot =
            Assert.IsType<
                DataRelativePathRepairSourceSnapshot
            >(
                projection.SourceSnapshot
            );

        Assert.Equal(
            Path.GetFullPath(
                sourceFile
            ),
            sourceSnapshot.PhysicalPath
        );

        DataRelativePathRepairDestinationParentSnapshot
            destinationParentSnapshot =
                Assert.IsType<
                    DataRelativePathRepairDestinationParentSnapshot
                >(
                    projection.DestinationParentSnapshot
                );

        Assert.Equal(
            Path.GetFullPath(
                requestedParent
            ),
            destinationParentSnapshot.PhysicalPath
        );

        Assert.False(
            destinationParentSnapshot.CasefoldEnabled
        );

        DataRelativePathRepairPlanOperation operation =
            Assert.Single(
                projection.Operations
            );

        Assert.Equal(
            DataRelativePathRepairPlanOperationKind
                .CreateFile,
            operation.Kind
        );

        Assert.Equal(
            Path.GetFullPath(
                destinationFile
            ),
            operation.DestinationPath
        );

        Assert.Equal(
            Path.GetFullPath(
                sourceFile
            ),
            operation.SourcePath
        );

        /*
         * FaceGen alternate-branch projection must be durably representable
         * before it can ever be allowed to reach repair-apply.
         *
         * Schema v2 deliberately describes direct strict-case mismatch.
         * This alternate-source / existing-destination-parent shape requires
         * a distinct durable schema while retaining the same immutable source
         * snapshot, destination-parent snapshot, and CreateFile operation.
         */
        DataRelativePathRepairPlanManifestCreation creation =
            DataRelativePathRepairPlanManifest.CreateFromResolution(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                resolution,
                sourceSnapshot,
                destinationParentSnapshot,
                projection.Operations
            );

        Assert.True(
            creation.Success,
            creation.Error ??
            creation.State.ToString()
        );

        DataRelativePathRepairPlanManifestRecord manifest =
            Assert.IsType<
                DataRelativePathRepairPlanManifestRecord
            >(
                creation.Manifest
            );

        Assert.Equal(
            3,
            manifest.SchemaVersion
        );

        Assert.NotNull(
            manifest.ResolvedPrefixSteps
        );

        Assert.Equal(
            4,
            manifest.ResolvedPrefixSteps!.Count
        );

        Assert.Equal(
            Path.GetFullPath(
                requestedParent
            ),
            manifest.InitialDestinationParentSnapshot
                .PhysicalPath
        );

        Assert.Equal(
            Path.GetFullPath(
                sourceFile
            ),
            manifest.SourceSnapshot
                .PhysicalPath
        );

        DataRelativePathRepairPlanManifestOperation
            persistedOperation =
                Assert.Single(
                    manifest.Operations
                );

        Assert.Equal(
            DataRelativePathRepairPlanOperationKind
                .CreateFile,
            persistedOperation.Operation.Kind
        );

        Assert.Equal(
            Path.GetFullPath(
                destinationFile
            ),
            persistedOperation.Operation
                .DestinationPath
        );

        /*
         * ===== SCHEMA-V3 NEGATIVE / TAMPER COVERAGE =====
         *
         * A valid v3 record is not enough.  Persisted evidence must fail
         * closed if its alternate source, branch proof, destination prefix,
         * or operation cardinality is changed.
         */

        // ------------------------------------------------------------
        // A. The source must remain Windows-logically equivalent to
        //    the requested path.
        // ------------------------------------------------------------

        string nonEquivalentSource =
            Path.Combine(
                alternateParent,
                "DefinitelyNotMaleHead.tri"
            );

        File.WriteAllText(
            nonEquivalentSource,
            "schema-v3-non-equivalent-source"
        );

        LinuxFileIdentityResult nonEquivalentIdentity =
            LinuxFileIdentity.Inspect(
                nonEquivalentSource
            );

        Assert.True(
            nonEquivalentIdentity.Success,
            nonEquivalentIdentity.Error
        );

        DataRelativePathRepairPlanManifestOperation
            nonEquivalentOperation =
                persistedOperation with
                {
                    Operation =
                        persistedOperation.Operation with
                        {
                            SourcePath =
                                nonEquivalentSource
                        }
                };

        DataRelativePathRepairPlanManifestRecord
            nonEquivalentSourceManifest =
                manifest with
                {
                    SourceSnapshot =
                        manifest.SourceSnapshot with
                        {
                            PhysicalPath =
                                nonEquivalentSource,
                            Identity =
                                nonEquivalentIdentity
                        },
                    Operations =
                    [
                        nonEquivalentOperation
                    ]
                };

        string? nonEquivalentSourceError =
            DataRelativePathRepairPlanManifest.Validate(
                nonEquivalentSourceManifest
            );

        Assert.NotNull(
            nonEquivalentSourceError
        );

        Assert.Contains(
            "not Windows-logically equivalent",
            nonEquivalentSourceError ??
            string.Empty
        );

        // ------------------------------------------------------------
        // B. Merely differing at the file leaf is insufficient.
        //    Schema v3 requires the source to have branched from the
        //    persisted destination hierarchy BEFORE the final component.
        // ------------------------------------------------------------

        string unbranchedSource =
            Path.Combine(
                requestedParent,
                "malehead.tri"
            );

        File.WriteAllText(
            unbranchedSource,
            "schema-v3-unbranched-source"
        );

        LinuxFileIdentityResult unbranchedIdentity =
            LinuxFileIdentity.Inspect(
                unbranchedSource
            );

        Assert.True(
            unbranchedIdentity.Success,
            unbranchedIdentity.Error
        );

        DataRelativePathRepairPlanManifestOperation
            unbranchedOperation =
                persistedOperation with
                {
                    Operation =
                        persistedOperation.Operation with
                        {
                            SourcePath =
                                unbranchedSource
                        }
                };

        DataRelativePathRepairPlanManifestRecord
            unbranchedSourceManifest =
                manifest with
                {
                    SourceSnapshot =
                        manifest.SourceSnapshot with
                        {
                            PhysicalPath =
                                unbranchedSource,
                            Identity =
                                unbranchedIdentity
                        },
                    Operations =
                    [
                        unbranchedOperation
                    ]
                };

        string? unbranchedSourceError =
            DataRelativePathRepairPlanManifest.Validate(
                unbranchedSourceManifest
            );

        Assert.NotNull(
            unbranchedSourceError
        );

        Assert.Contains(
            "must branch",
            unbranchedSourceError ??
            string.Empty
        );

        // ------------------------------------------------------------
        // C. The first physical branch divergence must be supported by
        //    the resolver's recorded Windows-equivalent-name evidence.
        // ------------------------------------------------------------

        string sourceRelative =
            Path.GetRelativePath(
                dataRoot,
                sourceFile
            )
            .Replace(
                Path.DirectorySeparatorChar,
                '/'
            );

        string[] sourceComponents =
            sourceRelative.Split('/');

        DataRelativePathRepairPlanResolvedPrefixStep[]
            branchEvidence =
                manifest.ResolvedPrefixSteps!
                    .ToArray();

        int branchStepIndex =
            Array.FindIndex(
                branchEvidence,
                step =>
                    step.ComponentIndex <
                        sourceComponents.Length &&
                    !string.Equals(
                        sourceComponents[
                            step.ComponentIndex
                        ],
                        step.SelectedPhysicalName,
                        StringComparison.Ordinal
                    )
            );

        Assert.InRange(
            branchStepIndex,
            0,
            branchEvidence.Length - 1
        );

        DataRelativePathRepairPlanResolvedPrefixStep
            branchStep =
                branchEvidence[
                    branchStepIndex
                ];

        Assert.Contains(
            sourceComponents[
                branchStep.ComponentIndex
            ],
            branchStep.EquivalentPhysicalNames
        );

        branchEvidence[
            branchStepIndex
        ] =
            branchStep with
            {
                EquivalentPhysicalNames =
                [
                    branchStep.SelectedPhysicalName
                ]
            };

        DataRelativePathRepairPlanManifestRecord
            missingBranchEvidenceManifest =
                manifest with
                {
                    ResolvedPrefixSteps =
                        branchEvidence
                };

        string? missingBranchEvidenceError =
            DataRelativePathRepairPlanManifest.Validate(
                missingBranchEvidenceManifest
            );

        Assert.NotNull(
            missingBranchEvidenceError
        );

        Assert.Contains(
            "branch divergence",
            missingBranchEvidenceError ??
            string.Empty
        );

        // ------------------------------------------------------------
        // D. The persisted destination prefix must be complete through
        //    the already-existing parent immediately above the file.
        // ------------------------------------------------------------

        DataRelativePathRepairPlanManifestRecord
            truncatedPrefixManifest =
                manifest with
                {
                    ResolvedPrefixSteps =
                        manifest.ResolvedPrefixSteps!
                            .Take(
                                manifest.ResolvedPrefixSteps.Count - 1
                            )
                            .ToArray()
                };

        string? truncatedPrefixError =
            DataRelativePathRepairPlanManifest.Validate(
                truncatedPrefixManifest
            );

        Assert.NotNull(
            truncatedPrefixError
        );

        Assert.Contains(
            "complete existing destination",
            truncatedPrefixError ??
            string.Empty
        );

        // ------------------------------------------------------------
        // E. Creation itself must reject operation cardinality greater
        //    than the single permitted CreateFile.
        // ------------------------------------------------------------

        DataRelativePathRepairPlanManifestCreation
            multipleOperationCreation =
                DataRelativePathRepairPlanManifest
                    .CreateFromResolution(
                        Guid.NewGuid(),
                        DateTimeOffset.UtcNow,
                        resolution,
                        sourceSnapshot,
                        destinationParentSnapshot,
                        [
                            operation,
                            operation
                        ]
                    );

        Assert.False(
            multipleOperationCreation.Success
        );

        Assert.Contains(
            "exactly one CreateFile",
            multipleOperationCreation.Error ??
            string.Empty
        );

        // ------------------------------------------------------------
        // F. Batch projection remains deliberately excluded.  Schema v3
        //    is still a standalone single-plan capability only.
        // ------------------------------------------------------------

        DataRelativePathRepairPlanProjection
            batchProjection =
                DataRelativePathRepairPlanProjector
                    .ProjectBatchCandidate(
                        resolution
                    );

        Assert.Equal(
            DataRelativePathRepairPlanProjectionState
                .NotDirectStrictCaseMismatch,
            batchProjection.State
        );

        Assert.Equal(
            DataRelativePathCaseMismatchTopologyState
                .CandidateBranchesBeforeFailure,
            batchProjection.TopologyState
        );

        Assert.False(
            batchProjection.HasPlan
        );

        // Projection itself must remain read-only.
        Assert.False(
            File.Exists(
                destinationFile
            )
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
