using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Analysis;

namespace CaseCompat.Tests;

public sealed class SkyrimWinningArmorAddonSnapshotDiagnosticPipelineTests
{
    private const string DataRoot =
        "/fixture/Data";

    [Fact]
    public void Compose_EmptyEvidence_ComposesWholeChainAndRetainsExactInputs()
    {
        SkyrimWinningArmorAddonInventoryResult inventory =
            EmptyInventory();

        SkyrimArchiveCandidateIndexResult archiveIndex =
            ArchiveIndex(
                DataRoot
            );

        SkyrimRuntimeArchiveEvidenceResult runtimeEvidence =
            RuntimeEvidence(
                DataRoot
            );

        SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticResult result =
            SkyrimWinningArmorAddonSnapshotDiagnosticPipeline.Compose(
                inventory,
                Array.Empty<WindowsNamespaceAnalysis>(),
                archiveIndex,
                runtimeEvidence
            );

        Assert.Empty(
            result.Diagnostics
        );

        Assert.Equal(
            0,
            result.DiagnosticCount
        );

        Assert.Same(
            inventory,
            result
                .Projection
                .PathArchiveResult
                .Diagnostics
                .Projection
                .Scan
                .Inventory
        );

        Assert.Same(
            archiveIndex,
            result
                .Projection
                .PathArchiveResult
                .ArchiveIndex
        );

        Assert.Same(
            runtimeEvidence,
            result
                .Projection
                .PathArchiveResult
                .RuntimeArchiveEvidence
        );
    }

    [Fact]
    public void Compose_InvalidRequestedPath_FlowsToFinalIndeterminateEvidence()
    {
        SkyrimArmorAddonModelReference reference =
            new(
                FormKey:
                    "000800:Winner.esp",
                EditorId:
                    "Fixture",
                Field:
                    "WorldModel.Male",
                GivenPath:
                    "../bad.nif",
                DataRelativePath:
                    "../bad.nif"
            );

        SkyrimWinningArmorAddonInventoryResult inventory =
            InventoryWithReference(
                reference,
                winnerSearchComplete:
                    true
            );

        SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnostic diagnostic =
            Assert.Single(
                SkyrimWinningArmorAddonSnapshotDiagnosticPipeline
                    .Compose(
                        inventory,
                        Array.Empty<WindowsNamespaceAnalysis>(),
                        ArchiveIndex(
                            DataRoot
                        ),
                        RuntimeEvidence(
                            DataRoot
                        )
                    )
                    .Diagnostics
            );

        Assert.Equal(
            SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
                .IndeterminateEvidence,
            diagnostic.State
        );

        Assert.Same(
            reference,
            diagnostic.Consumer.Reference
        );

        Assert.Equal(
            "../bad.nif",
            diagnostic.PathInterpretation.Evidence.RequestedPath
        );

        Assert.True(
            diagnostic.PathInterpretation.EvidenceStructureValid
        );

        Assert.Null(
            diagnostic.PathInterpretation.InterpretationError
        );

        Assert.Equal(
            SkyrimArmorAddonSnapshotLookupEvidenceState
                .InvalidRequestedPath,
            diagnostic.PathInterpretation.Evidence.State
        );
    }

    [Fact]
    public void Compose_IncompleteWinnerSearch_DominatesInvalidPath()
    {
        SkyrimArmorAddonModelReference reference =
            new(
                FormKey:
                    "000800:Winner.esp",
                EditorId:
                    "Fixture",
                Field:
                    "WorldModel.Male",
                GivenPath:
                    "../bad.nif",
                DataRelativePath:
                    "../bad.nif"
            );

        SkyrimWinningArmorAddonInventoryResult inventory =
            InventoryWithReference(
                reference,
                winnerSearchComplete:
                    false
            );

        SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnostic diagnostic =
            Assert.Single(
                SkyrimWinningArmorAddonSnapshotDiagnosticPipeline
                    .Compose(
                        inventory,
                        Array.Empty<WindowsNamespaceAnalysis>(),
                        ArchiveIndex(
                            DataRoot
                        ),
                        RuntimeEvidence(
                            DataRoot
                        )
                    )
                    .Diagnostics
            );

        Assert.Equal(
            SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
                .IncompleteWinnerSearch,
            diagnostic.State
        );

        Assert.Same(
            reference,
            diagnostic.Consumer.Reference
        );
    }

    [Fact]
    public void Compose_ArchiveDataRootMismatch_IsRejectedByApprovedComposer()
    {
        Assert.Throws<ArgumentException>(
            () =>
                SkyrimWinningArmorAddonSnapshotDiagnosticPipeline.Compose(
                    EmptyInventory(),
                    Array.Empty<WindowsNamespaceAnalysis>(),
                    ArchiveIndex(
                        "/fixture/OtherData"
                    ),
                    RuntimeEvidence(
                        DataRoot
                    )
                )
        );
    }

    [Fact]
    public void Compose_RuntimeDataRootMismatch_IsRejectedByApprovedComposer()
    {
        Assert.Throws<ArgumentException>(
            () =>
                SkyrimWinningArmorAddonSnapshotDiagnosticPipeline.Compose(
                    EmptyInventory(),
                    Array.Empty<WindowsNamespaceAnalysis>(),
                    ArchiveIndex(
                        DataRoot
                    ),
                    RuntimeEvidence(
                        "/fixture/OtherData"
                    )
                )
        );
    }

    [Fact]
    public void Compose_NullInventory_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                SkyrimWinningArmorAddonSnapshotDiagnosticPipeline.Compose(
                    null!,
                    Array.Empty<WindowsNamespaceAnalysis>(),
                    ArchiveIndex(
                        DataRoot
                    ),
                    RuntimeEvidence(
                        DataRoot
                    )
                )
        );
    }

    [Fact]
    public void Compose_NullAnalyses_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                SkyrimWinningArmorAddonSnapshotDiagnosticPipeline.Compose(
                    EmptyInventory(),
                    null!,
                    ArchiveIndex(
                        DataRoot
                    ),
                    RuntimeEvidence(
                        DataRoot
                    )
                )
        );
    }

    [Fact]
    public void Compose_NullArchiveIndex_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                SkyrimWinningArmorAddonSnapshotDiagnosticPipeline.Compose(
                    EmptyInventory(),
                    Array.Empty<WindowsNamespaceAnalysis>(),
                    null!,
                    RuntimeEvidence(
                        DataRoot
                    )
                )
        );
    }

    [Fact]
    public void Compose_NullRuntimeArchiveEvidence_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                SkyrimWinningArmorAddonSnapshotDiagnosticPipeline.Compose(
                    EmptyInventory(),
                    Array.Empty<WindowsNamespaceAnalysis>(),
                    ArchiveIndex(
                        DataRoot
                    ),
                    null!
                )
        );
    }

    [Fact]
    public void Compose_NullAnalysisEntry_IsRejectedByApproved10CScanner()
    {
        IReadOnlyList<WindowsNamespaceAnalysis> analyses =
            new WindowsNamespaceAnalysis[]
            {
                null!
            };

        Assert.Throws<ArgumentException>(
            () =>
                SkyrimWinningArmorAddonSnapshotDiagnosticPipeline.Compose(
                    EmptyInventory(),
                    analyses,
                    ArchiveIndex(
                        DataRoot
                    ),
                    RuntimeEvidence(
                        DataRoot
                    )
                )
        );
    }

    private static SkyrimWinningArmorAddonInventoryResult EmptyInventory()
    {
        return new SkyrimWinningArmorAddonInventoryResult(
            DataRoot:
                DataRoot,
            RuntimeActivePluginCount:
                0,
            PluginsOpened:
                0,
            MissingPluginFiles:
                Array.Empty<string>(),
            ReadErrors:
                Array.Empty<
                    SkyrimPluginReadError
                >(),
            Winners:
                Array.Empty<
                    SkyrimWinningArmorAddonRecord
                >()
        );
    }

    private static SkyrimWinningArmorAddonInventoryResult
        InventoryWithReference(
            SkyrimArmorAddonModelReference reference,
            bool winnerSearchComplete)
    {
        SkyrimWinningArmorAddonRecord winner =
            new(
                FormKey:
                    "000800:Winner.esp",
                EditorId:
                    "Fixture",
                WinningPluginName:
                    "Winner.esp",
                WinningLoadOrderIndex:
                    10,
                ModelReferences:
                    new[]
                    {
                        reference
                    }
            );

        return new SkyrimWinningArmorAddonInventoryResult(
            DataRoot:
                DataRoot,
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
                        "Missing.esp"
                    },
            ReadErrors:
                Array.Empty<
                    SkyrimPluginReadError
                >(),
            Winners:
                new[]
                {
                    winner
                }
        );
    }

    private static SkyrimArchiveCandidateIndexResult ArchiveIndex(
        string dataRoot)
    {
        return new SkyrimArchiveCandidateIndexResult(
            DataRoot:
                dataRoot,
            ArchivesDiscovered:
                0,
            ArchivesRead:
                0,
            TotalFileEntries:
                0,
            DuplicateLogicalEntriesWithinArchive:
                0,
            ReadErrors:
                Array.Empty<
                    SkyrimArchiveReadError
                >(),
            Assets:
                new Dictionary<
                    WindowsLogicalPath,
                    IReadOnlyList<
                        SkyrimArchiveAssetProvider
                    >
                >()
        );
    }

    private static SkyrimRuntimeArchiveEvidenceResult RuntimeEvidence(
        string dataRoot)
    {
        return new SkyrimRuntimeArchiveEvidenceResult(
            DataRoot:
                dataRoot,
            IniDirectory:
                "/fixture",
            Archives:
                Array.Empty<
                    SkyrimRuntimeArchiveEvidenceEntry
                >(),
            MissingIniArchives:
                Array.Empty<
                    SkyrimRuntimeArchiveMissingIniArchive
                >(),
            AssociationErrors:
                Array.Empty<
                    SkyrimRuntimeArchiveAssociationError
                >(),
            IniReadErrors:
                Array.Empty<
                    SkyrimRuntimeArchiveIniReadError
                >(),
            IniProvenanceErrors:
                Array.Empty<
                    SkyrimRuntimeArchiveIniProvenanceError
                >()
        );
    }
}
