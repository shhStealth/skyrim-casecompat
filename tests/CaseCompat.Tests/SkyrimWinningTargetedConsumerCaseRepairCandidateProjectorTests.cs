using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Repair;

namespace CaseCompat.Tests;

public sealed class
    SkyrimWinningTargetedConsumerCaseRepairCandidateProjectorTests
{
    [Fact]
    public void
        Project_IncompleteWinnerSearchDoesNotDereferenceEvidenceOrDataRoot()
    {
        var composition =
            new SkyrimWinningConsumerSpellingEvidenceCompositionResult(
                ArmorAddonProjection:
                    null!,
                HeadPartProjection:
                    null!,
                State:
                    SkyrimWinningConsumerSpellingEvidenceCompositionState
                        .IncompleteWinnerSearch,
                Evidence:
                    null!,
                Error:
                    null
            );

        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult result =
            SkyrimWinningTargetedConsumerCaseRepairCandidateProjector.Project(
                composition
            );

        Assert.Equal(
            SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
                .IncompleteWinnerSearch,
            result.State
        );

        Assert.Null(
            result.DataRoot
        );

        Assert.Empty(
            result.Leaves
        );

        Assert.Empty(
            result.Candidates
        );
    }

    [Fact]
    public void
        Project_IndeterminateConsumerEvidenceDoesNotDereferenceEvidenceOrDataRoot()
    {
        var composition =
            new SkyrimWinningConsumerSpellingEvidenceCompositionResult(
                ArmorAddonProjection:
                    null!,
                HeadPartProjection:
                    null!,
                State:
                    SkyrimWinningConsumerSpellingEvidenceCompositionState
                        .IndeterminateConsumerPathEvidence,
                Evidence:
                    null!,
                Error:
                    "consumer authority unavailable"
            );

        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult result =
            SkyrimWinningTargetedConsumerCaseRepairCandidateProjector.Project(
                composition
            );

        Assert.Equal(
            SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
                .IndeterminateConsumerPathEvidence,
            result.State
        );

        Assert.Null(
            result.DataRoot
        );

        Assert.Empty(
            result.Candidates
        );
    }

    [Fact]
    public void Project_MalformedCompleteCompositionFailsBeforeFilesystem()
    {
        string dataRoot =
            UnusedDataRoot(
                "malformed-composition"
            );

        SkyrimWinningConsumerSpellingEvidenceCompositionResult canonical =
            Complete(
                dataRoot,
                Consumer(
                    "MESHES/A/FILE.NIF",
                    "Meshes/A/File.nif"
                )
            );

        SkyrimWinningConsumerSpellingEvidenceCompositionResult malformed =
            canonical with
            {
                Evidence =
                    new[]
                    {
                        Consumer(
                            "MESHES/B/FILE.NIF",
                            "Meshes/B/File.nif"
                        )
                    }
            };

        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult result =
            SkyrimWinningTargetedConsumerCaseRepairCandidateProjector.Project(
                malformed
            );

        Assert.Equal(
            SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
                .IndeterminateConsumerPathEvidence,
            result.State
        );

        Assert.Empty(
            result.Candidates
        );

        Assert.False(
            Directory.Exists(
                dataRoot
            )
        );
    }

    [Fact]
    public void Project_MismatchedSourceDataRootsFailBeforeFilesystem()
    {
        string armorRoot =
            UnusedDataRoot(
                "armor-root"
            );

        string headPartRoot =
            UnusedDataRoot(
                "head-root"
            );

        SkyrimWinningConsumerSpellingEvidenceCompositionResult composition =
            SkyrimWinningConsumerSpellingEvidenceComposer.Compose(
                CompleteArmorAddon(
                    armorRoot,
                    Consumer(
                        "MESHES/A/FILE.NIF",
                        "Meshes/A/File.nif"
                    )
                ),
                CompleteHeadPart(
                    headPartRoot
                )
            );

        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult result =
            SkyrimWinningTargetedConsumerCaseRepairCandidateProjector.Project(
                composition
            );

        Assert.Equal(
            SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
                .IndeterminateDataRootEvidence,
            result.State
        );

        Assert.Null(
            result.DataRoot
        );

        Assert.Empty(
            result.Candidates
        );
    }

    [Fact]
    public void Project_ConflictingConsumerSpellingsDoNotOpenDataRoot()
    {
        string dataRoot =
            UnusedDataRoot(
                "conflicting-consumer"
            );

        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult result =
            SkyrimWinningTargetedConsumerCaseRepairCandidateProjector.Project(
                Complete(
                    dataRoot,
                    Consumer(
                        "MESHES/BLOCKED/FILE.NIF",
                        "Meshes/BLOCKED/File.nif",
                        "meshes/blocked/File.nif"
                    )
                )
            );

        Assert.Equal(
            SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
                .Complete,
            result.State
        );

        Assert.Equal(
            dataRoot,
            result.DataRoot
        );

        SkyrimWinningTargetedConsumerCaseRepairLeafProjection leaf =
            Assert.Single(
                result.Leaves
            );

        Assert.Equal(
            SkyrimWinningTargetedConsumerCaseRepairLeafState
                .ConflictingConsumerSpellings,
            leaf.State
        );

        Assert.Null(
            leaf.TargetedProjection
        );

        Assert.Empty(
            result.Candidates
        );

        Assert.False(
            Directory.Exists(
                dataRoot
            )
        );
    }

    [Fact]
    public void Project_NoConsumerEvidenceDoesNotOpenDataRoot()
    {
        string dataRoot =
            UnusedDataRoot(
                "no-consumer"
            );

        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult result =
            SkyrimWinningTargetedConsumerCaseRepairCandidateProjector.Project(
                Complete(
                    dataRoot,
                    Consumer(
                        "MESHES/GHOST/FILE.NIF"
                    )
                )
            );

        Assert.Equal(
            SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
                .Complete,
            result.State
        );

        SkyrimWinningTargetedConsumerCaseRepairLeafProjection leaf =
            Assert.Single(
                result.Leaves
            );

        Assert.Equal(
            SkyrimWinningTargetedConsumerCaseRepairLeafState
                .NoConsumerEvidence,
            leaf.State
        );

        Assert.Null(
            leaf.TargetedProjection
        );

        Assert.Empty(
            result.Candidates
        );

        Assert.False(
            Directory.Exists(
                dataRoot
            )
        );
    }

    [Fact]
    public void
        Project_UniqueMismatchPublishesTargetedCandidateAndPreservesRequestedCase()
    {
        string dataRoot =
            CreateDataRoot(
                "unique-mismatch"
            );

        string physicalRelativePath =
            "meshes/actors/file.nif";

        CreateFile(
            dataRoot,
            physicalRelativePath,
            "candidate bytes"
        );

        string requestedPath =
            "Meshes/Actors/File.NIF";

        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult result =
            SkyrimWinningTargetedConsumerCaseRepairCandidateProjector.Project(
                Complete(
                    dataRoot,
                    Consumer(
                        "MESHES/ACTORS/FILE.NIF",
                        requestedPath
                    )
                )
            );

        Assert.Equal(
            SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
                .Complete,
            result.State
        );

        Assert.True(
            result.CandidateEvidenceComplete
        );

        DataRelativePathTargetedConsumerCaseRepairCandidate candidate =
            Assert.Single(
                result.Candidates
            );

        Assert.Equal(
            requestedPath,
            candidate.AuthoritativeRequestedPath
        );

        Assert.Equal(
            physicalRelativePath,
            candidate.SourceRepresentation.RelativePath
        );

        SkyrimWinningTargetedConsumerCaseRepairLeafProjection leaf =
            Assert.Single(
                result.Leaves
            );

        Assert.Equal(
            SkyrimWinningTargetedConsumerCaseRepairLeafState
                .Complete,
            leaf.State
        );

        Assert.Equal(
            "MESHES",
            leaf
                .TargetedProjection!
                .PhysicalProjection
                .CurrentLeafAnalysis
                .RootWindowsLogicalPath
        );

        Assert.Equal(
            requestedPath,
            leaf
                .TargetedProjection!
                .Candidate!
                .AuthoritativeRequestedPath
        );
    }

    [Fact]
    public void Project_ExactPhysicalSpellingPublishesNoCandidate()
    {
        string dataRoot =
            CreateDataRoot(
                "exact-physical"
            );

        string requestedPath =
            "Meshes/Actors/File.nif";

        CreateFile(
            dataRoot,
            requestedPath,
            "exact bytes"
        );

        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult result =
            SkyrimWinningTargetedConsumerCaseRepairCandidateProjector.Project(
                Complete(
                    dataRoot,
                    Consumer(
                        "MESHES/ACTORS/FILE.NIF",
                        requestedPath
                    )
                )
            );

        Assert.Equal(
            SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
                .Complete,
            result.State
        );

        Assert.Empty(
            result.Candidates
        );

        SkyrimWinningTargetedConsumerCaseRepairLeafProjection leaf =
            Assert.Single(
                result.Leaves
            );

        Assert.Equal(
            DataRelativePathAggregateConsumerCaseRepairPolicyState
                .ExactPhysicalSpellingPresent,
            leaf.TargetedProjection!.PolicyState
        );
    }

    [Fact]
    public void
        Project_UniqueConsumerWithNoPhysicalRepresentationIsCompleteWithoutCandidate()
    {
        string dataRoot =
            CreateDataRoot(
                "no-physical"
            );

        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult result =
            SkyrimWinningTargetedConsumerCaseRepairCandidateProjector.Project(
                Complete(
                    dataRoot,
                    Consumer(
                        "MESHES/ACTORS/MISSING.NIF",
                        "Meshes/Actors/Missing.nif"
                    )
                )
            );

        Assert.Equal(
            SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
                .Complete,
            result.State
        );

        Assert.Empty(
            result.Candidates
        );

        SkyrimWinningTargetedConsumerCaseRepairLeafProjection leaf =
            Assert.Single(
                result.Leaves
            );

        Assert.Equal(
            SkyrimWinningTargetedConsumerCaseRepairLeafState
                .NoPhysicalRepresentation,
            leaf.State
        );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairCandidateProjectionState
                .NoPhysicalRepresentation,
            leaf.TargetedProjection!.State
        );

        Assert.Equal(
            DataRelativePathAggregateConsumerSpellingState
                .UniqueConsumerSpelling,
            leaf.ConsumerSpelling.State
        );
    }

    [Fact]
    public void
        Project_TargetedFailurePublishesZeroCandidatesGloballyAfterEarlierSuccess()
    {
        string dataRoot =
            CreateDataRoot(
                "global-failure"
            );

        CreateFile(
            dataRoot,
            "meshes/a/file.nif",
            "first candidate"
        );

        string outside =
            Path.Combine(
                dataRoot,
                "outside"
            );

        Directory.CreateDirectory(
            outside
        );

        string symlink =
            Path.Combine(
                dataRoot,
                "meshes",
                "z"
            );

        Directory.CreateSymbolicLink(
            symlink,
            outside
        );

        SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult result =
            SkyrimWinningTargetedConsumerCaseRepairCandidateProjector.Project(
                Complete(
                    dataRoot,
                    Consumer(
                        "MESHES/A/FILE.NIF",
                        "Meshes/A/File.nif"
                    ),
                    Consumer(
                        "MESHES/Z/FILE.NIF",
                        "Meshes/Z/File.nif"
                    )
                )
            );

        Assert.Equal(
            SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionState
                .IndeterminatePhysicalEvidence,
            result.State
        );

        Assert.False(
            result.CandidateEvidenceComplete
        );

        Assert.Empty(
            result.Candidates
        );

        Assert.Equal(
            2,
            result.Leaves.Count
        );

        Assert.NotNull(
            result.Leaves[0].Candidate
        );

        Assert.Equal(
            SkyrimWinningTargetedConsumerCaseRepairLeafState
                .IndeterminatePhysicalEvidence,
            result.Leaves[1].State
        );

        Assert.Null(
            result.Leaves[1].Candidate
        );
    }

    private static
        SkyrimWinningConsumerSpellingEvidenceCompositionResult
        Complete(
            string dataRoot,
            params DataRelativePathAggregateConsumerSpellingEvidence[] evidence)
    {
        return SkyrimWinningConsumerSpellingEvidenceComposer.Compose(
            CompleteArmorAddon(
                dataRoot,
                evidence
            ),
            CompleteHeadPart(
                dataRoot
            )
        );
    }

    private static
        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult
        CompleteArmorAddon(
            string dataRoot,
            params DataRelativePathAggregateConsumerSpellingEvidence[] evidence)
    {
        return ArmorAddon(
            dataRoot,
            winnerSearchComplete:
                true,
            state:
                SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionState
                    .Complete,
            evidence:
                evidence
        );
    }

    private static
        SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult
        CompleteHeadPart(
            string dataRoot,
            params DataRelativePathAggregateConsumerSpellingEvidence[] evidence)
    {
        return HeadPart(
            dataRoot,
            winnerSearchComplete:
                true,
            state:
                SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionState
                    .Complete,
            evidence:
                evidence
        );
    }

    private static
        SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult
        ArmorAddon(
            string dataRoot,
            bool winnerSearchComplete,
            SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionState
                state,
            IReadOnlyList<DataRelativePathAggregateConsumerSpellingEvidence>
                evidence,
            string? error = null)
    {
        SkyrimWinningArmorAddonInventoryResult inventory =
            new(
                DataRoot:
                    dataRoot,
                RuntimeActivePluginCount:
                    winnerSearchComplete
                        ? 1
                        : 2,
                PluginsOpened:
                    1,
                MissingPluginFiles:
                    winnerSearchComplete
                        ? Array.Empty<string>()
                        : new[]
                        {
                            "MissingArmorAddon.esp"
                        },
                ReadErrors:
                    Array.Empty<
                        SkyrimPluginReadError
                    >(),
                Winners:
                    Array.Empty<
                        SkyrimWinningArmorAddonRecord
                    >()
            );

        var scan =
            new SkyrimWinningArmorAddonSnapshotEvidenceScanResult(
                Inventory:
                    inventory,
                Paths:
                    Array.Empty<
                        SkyrimWinningArmorAddonSnapshotPathEvidence
                    >()
            );

        return new
            SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult(
                Scan:
                    scan,
                State:
                    state,
                Evidence:
                    evidence,
                Error:
                    error
            );
    }

    private static
        SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult
        HeadPart(
            string dataRoot,
            bool winnerSearchComplete,
            SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionState
                state,
            IReadOnlyList<DataRelativePathAggregateConsumerSpellingEvidence>
                evidence,
            string? error = null)
    {
        SkyrimWinningHeadPartInventoryResult inventory =
            new(
                DataRoot:
                    dataRoot,
                RuntimeActivePluginCount:
                    winnerSearchComplete
                        ? 1
                        : 2,
                PluginsOpened:
                    1,
                MissingPluginFiles:
                    winnerSearchComplete
                        ? Array.Empty<string>()
                        : new[]
                        {
                            "MissingHeadPart.esp"
                        },
                ReadErrors:
                    Array.Empty<
                        SkyrimPluginReadError
                    >(),
                Winners:
                    Array.Empty<
                        SkyrimWinningHeadPartRecord
                    >()
            );

        return new
            SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult(
                Inventory:
                    inventory,
                State:
                    state,
                Evidence:
                    evidence,
                Error:
                    error
            );
    }

    private static DataRelativePathAggregateConsumerSpellingEvidence Consumer(
        string logicalPath,
        params string[] requestedPaths)
    {
        return DataRelativePathAggregateConsumerSpellingClassifier.Classify(
            logicalPath,
            requestedPaths
        );
    }

    private static string CreateDataRoot(
        string name)
    {
        string dataRoot =
            UnusedDataRoot(
                name
            );

        Directory.CreateDirectory(
            dataRoot
        );

        return dataRoot;
    }

    private static string UnusedDataRoot(
        string name)
    {
        return Path.GetFullPath(
            Path.Combine(
                Path.GetTempPath(),
                "casecompat-c4e-4c-tests",
                Guid.NewGuid().ToString("N"),
                name,
                "Data"
            )
        );
    }

    private static void CreateFile(
        string dataRoot,
        string relativePath,
        string contents)
    {
        string fullPath =
            Path.GetFullPath(
                Path.Combine(
                    dataRoot,
                    relativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar
                    )
                )
            );

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                fullPath
            )!
        );

        File.WriteAllText(
            fullPath,
            contents
        );
    }
}
