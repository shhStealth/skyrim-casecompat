using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Analysis;

namespace CaseCompat.Tests;

public sealed class
    SkyrimWinningArmorAddonSnapshotConsumerArchiveProjectorTests
{
    [Fact]
    public void Project_PreservesEveryDiagnosticAndInputOrder()
    {
        SkyrimArmorAddonSnapshotLoosePathInterpretation firstInterpretation =
            Interpretation(
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .LooseResolved,
                "meshes/first.nif"
            );

        SkyrimArmorAddonSnapshotLoosePathInterpretation secondInterpretation =
            Interpretation(
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .LooseUnresolved,
                "meshes/second.nif"
            );

        SkyrimWinningArmorAddonSnapshotConsumerDiagnosticResult diagnostics =
            Diagnostics(
                "/fixture/Data",
                winnerSearchComplete:
                    true,
                firstInterpretation,
                secondInterpretation
            );

        SkyrimWinningArmorAddonSnapshotPathArchiveEvidence secondEvidence =
            ArchiveEvidence(
                secondInterpretation
            );

        SkyrimWinningArmorAddonSnapshotConsumerArchiveProjectionResult result =
            SkyrimWinningArmorAddonSnapshotConsumerArchiveProjector
                .Project(
                    PathArchiveResult(
                        diagnostics,
                        secondEvidence
                    )
                );

        Assert.Equal(
            2,
            result.ConsumerCount
        );

        Assert.Same(
            diagnostics.Diagnostics[0],
            result.Consumers[0].Diagnostic
        );

        Assert.Same(
            diagnostics.Diagnostics[1],
            result.Consumers[1].Diagnostic
        );

        Assert.Null(
            result.Consumers[0].PathArchiveEvidence
        );

        Assert.Same(
            secondEvidence,
            result.Consumers[1].PathArchiveEvidence
        );
    }

    [Fact]
    public void Project_LooseUnresolved_AttachesExactSharedArchiveEvidence()
    {
        SkyrimArmorAddonSnapshotLoosePathInterpretation interpretation =
            Interpretation(
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .LooseUnresolved,
                "meshes/test.nif"
            );

        SkyrimWinningArmorAddonSnapshotConsumerDiagnosticResult diagnostics =
            Diagnostics(
                "/fixture/Data",
                winnerSearchComplete:
                    true,
                interpretation
            );

        SkyrimWinningArmorAddonSnapshotPathArchiveEvidence evidence =
            ArchiveEvidence(
                interpretation
            );

        SkyrimWinningArmorAddonSnapshotConsumerArchiveProjection projected =
            Assert.Single(
                SkyrimWinningArmorAddonSnapshotConsumerArchiveProjector
                    .Project(
                        PathArchiveResult(
                            diagnostics,
                            evidence
                        )
                    )
                    .Consumers
            );

        Assert.Same(
            diagnostics.Diagnostics[0],
            projected.Diagnostic
        );

        Assert.Same(
            diagnostics.Diagnostics[0].Consumer,
            projected.Consumer
        );

        Assert.Same(
            interpretation,
            projected.PathInterpretation
        );

        Assert.Same(
            evidence,
            projected.PathArchiveEvidence
        );

        Assert.True(
            projected.HasArchiveEvidence
        );
    }

    [Fact]
    public void Project_TwoConsumersSharingInterpretation_ShareArchiveEvidence()
    {
        SkyrimArmorAddonSnapshotLoosePathInterpretation interpretation =
            Interpretation(
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .LooseUnresolved,
                "meshes/shared.nif"
            );

        SkyrimWinningArmorAddonSnapshotConsumerProjection firstConsumer =
            Consumer(
                interpretation,
                "First.esp",
                10
            );

        SkyrimWinningArmorAddonSnapshotConsumerProjection secondConsumer =
            Consumer(
                interpretation,
                "Second.esp",
                20
            );

        SkyrimWinningArmorAddonSnapshotConsumerDiagnosticResult diagnostics =
            SkyrimWinningArmorAddonSnapshotConsumerDiagnosticClassifier
                .Classify(
                    Projection(
                        "/fixture/Data",
                        winnerSearchComplete:
                            true,
                        new[]
                        {
                            interpretation
                        },
                        new[]
                        {
                            firstConsumer,
                            secondConsumer
                        }
                    )
                );

        SkyrimWinningArmorAddonSnapshotPathArchiveEvidence evidence =
            ArchiveEvidence(
                interpretation
            );

        SkyrimWinningArmorAddonSnapshotConsumerArchiveProjectionResult result =
            SkyrimWinningArmorAddonSnapshotConsumerArchiveProjector
                .Project(
                    PathArchiveResult(
                        diagnostics,
                        evidence
                    )
                );

        Assert.Equal(
            2,
            result.ConsumerCount
        );

        Assert.Same(
            evidence,
            result.Consumers[0].PathArchiveEvidence
        );

        Assert.Same(
            evidence,
            result.Consumers[1].PathArchiveEvidence
        );

        Assert.Same(
            result.Consumers[0].PathArchiveEvidence,
            result.Consumers[1].PathArchiveEvidence
        );
    }

    [Fact]
    public void Project_IncompleteWinnerSearch_RetainsConsumerWithoutArchiveEvidence()
    {
        SkyrimArmorAddonSnapshotLoosePathInterpretation interpretation =
            Interpretation(
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .LooseUnresolved,
                "meshes/test.nif"
            );

        SkyrimWinningArmorAddonSnapshotConsumerArchiveProjection projected =
            Assert.Single(
                SkyrimWinningArmorAddonSnapshotConsumerArchiveProjector
                    .Project(
                        PathArchiveResult(
                            Diagnostics(
                                "/fixture/Data",
                                winnerSearchComplete:
                                    false,
                                interpretation
                            )
                        )
                    )
                    .Consumers
            );

        Assert.Equal(
            SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                .IncompleteWinnerSearch,
            projected.DiagnosticState
        );

        Assert.Null(
            projected.PathArchiveEvidence
        );

        Assert.False(
            projected.HasArchiveEvidence
        );
    }

    [Fact]
    public void Project_LooseResolved_RetainsConsumerWithoutArchiveEvidence()
    {
        SkyrimArmorAddonSnapshotLoosePathInterpretation interpretation =
            Interpretation(
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .LooseResolved,
                "meshes/resolved.nif"
            );

        SkyrimWinningArmorAddonSnapshotConsumerArchiveProjection projected =
            Assert.Single(
                SkyrimWinningArmorAddonSnapshotConsumerArchiveProjector
                    .Project(
                        PathArchiveResult(
                            Diagnostics(
                                "/fixture/Data",
                                winnerSearchComplete:
                                    true,
                                interpretation
                            )
                        )
                    )
                    .Consumers
            );

        Assert.Equal(
            SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                .LooseResolved,
            projected.DiagnosticState
        );

        Assert.Null(
            projected.PathArchiveEvidence
        );
    }

    [Fact]
    public void Project_IndeterminateEvidence_RetainsConsumerWithoutArchiveEvidence()
    {
        SkyrimArmorAddonSnapshotLoosePathInterpretation interpretation =
            Interpretation(
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .IndeterminateEvidence,
                "meshes/unknown.nif"
            );

        SkyrimWinningArmorAddonSnapshotConsumerArchiveProjection projected =
            Assert.Single(
                SkyrimWinningArmorAddonSnapshotConsumerArchiveProjector
                    .Project(
                        PathArchiveResult(
                            Diagnostics(
                                "/fixture/Data",
                                winnerSearchComplete:
                                    true,
                                interpretation
                            )
                        )
                    )
                    .Consumers
            );

        Assert.Equal(
            SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                .IndeterminateEvidence,
            projected.DiagnosticState
        );

        Assert.Null(
            projected.PathArchiveEvidence
        );
    }

    [Fact]
    public void Project_DuplicatePathEvidenceForSameInterpretation_IsRejected()
    {
        SkyrimArmorAddonSnapshotLoosePathInterpretation interpretation =
            Interpretation(
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .LooseUnresolved,
                "meshes/test.nif"
            );

        SkyrimWinningArmorAddonSnapshotConsumerDiagnosticResult diagnostics =
            Diagnostics(
                "/fixture/Data",
                winnerSearchComplete:
                    true,
                interpretation
            );

        SkyrimWinningArmorAddonSnapshotPathArchiveEvidence evidence =
            ArchiveEvidence(
                interpretation
            );

        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () =>
                    SkyrimWinningArmorAddonSnapshotConsumerArchiveProjector
                        .Project(
                            PathArchiveResult(
                                diagnostics,
                                evidence,
                                evidence
                            )
                        )
            );

        Assert.Contains(
            "same checkpoint-10D",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Project_UnmatchedPathEvidenceReference_IsRejected()
    {
        SkyrimArmorAddonSnapshotLoosePathInterpretation retained =
            Interpretation(
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .LooseUnresolved,
                "meshes/test.nif"
            );

        /*
         * Same requested path text and same state, but deliberately a
         * different interpretation object.
         */
        SkyrimArmorAddonSnapshotLoosePathInterpretation unretained =
            Interpretation(
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .LooseUnresolved,
                "meshes/test.nif"
            );

        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () =>
                    SkyrimWinningArmorAddonSnapshotConsumerArchiveProjector
                        .Project(
                            PathArchiveResult(
                                Diagnostics(
                                    "/fixture/Data",
                                    winnerSearchComplete:
                                        true,
                                    retained
                                ),
                                ArchiveEvidence(
                                    unretained
                                )
                            )
                        )
            );

        Assert.Contains(
            "object identity",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Project_MissingLooseUnresolvedArchiveEvidence_IsRejected()
    {
        SkyrimArmorAddonSnapshotLoosePathInterpretation interpretation =
            Interpretation(
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .LooseUnresolved,
                "meshes/test.nif"
            );

        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () =>
                    SkyrimWinningArmorAddonSnapshotConsumerArchiveProjector
                        .Project(
                            PathArchiveResult(
                                Diagnostics(
                                    "/fixture/Data",
                                    winnerSearchComplete:
                                        true,
                                    interpretation
                                )
                            )
                        )
            );

        Assert.Contains(
            "missing",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void Project_EmptyResult_ProducesEmptyProjection()
    {
        SkyrimWinningArmorAddonSnapshotConsumerDiagnosticResult diagnostics =
            Diagnostics(
                "/fixture/Data",
                winnerSearchComplete:
                    true
            );

        SkyrimWinningArmorAddonSnapshotConsumerArchiveProjectionResult result =
            SkyrimWinningArmorAddonSnapshotConsumerArchiveProjector
                .Project(
                    PathArchiveResult(
                        diagnostics
                    )
                );

        Assert.Empty(
            result.Consumers
        );

        Assert.Equal(
            0,
            result.ConsumerCount
        );
    }

    private static
        SkyrimWinningArmorAddonSnapshotConsumerDiagnosticResult
        Diagnostics(
            string dataRoot,
            bool winnerSearchComplete,
            params SkyrimArmorAddonSnapshotLoosePathInterpretation[]
                interpretations)
    {
        SkyrimWinningArmorAddonSnapshotConsumerProjection[] consumers =
            interpretations
                .Select(
                    (interpretation, index) =>
                        Consumer(
                            interpretation,
                            $"Winner{index}.esp",
                            index
                        )
                )
                .ToArray();

        return SkyrimWinningArmorAddonSnapshotConsumerDiagnosticClassifier
            .Classify(
                Projection(
                    dataRoot,
                    winnerSearchComplete,
                    interpretations,
                    consumers
                )
            );
    }

    private static SkyrimWinningArmorAddonSnapshotConsumerProjectionResult
        Projection(
            string dataRoot,
            bool winnerSearchComplete,
            IReadOnlyList<
                SkyrimArmorAddonSnapshotLoosePathInterpretation
            > interpretations,
            IReadOnlyList<
                SkyrimWinningArmorAddonSnapshotConsumerProjection
            > consumers)
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
                            "Missing.esp"
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

        SkyrimWinningArmorAddonSnapshotEvidenceScanResult scan =
            new(
                Inventory:
                    inventory,
                Paths:
                    interpretations
                        .Select(
                            interpretation =>
                                interpretation.Evidence
                        )
                        .Distinct()
                        .ToArray()
            );

        return new
            SkyrimWinningArmorAddonSnapshotConsumerProjectionResult(
                Scan:
                    scan,
                PathInterpretations:
                    interpretations,
                Consumers:
                    consumers
            );
    }

    private static SkyrimWinningArmorAddonSnapshotConsumerProjection
        Consumer(
            SkyrimArmorAddonSnapshotLoosePathInterpretation interpretation,
            string pluginName,
            int loadOrderIndex)
    {
        string requestedPath =
            interpretation.Evidence.RequestedPath;

        return new SkyrimWinningArmorAddonSnapshotConsumerProjection(
            WinningPluginName:
                pluginName,
            WinningLoadOrderIndex:
                loadOrderIndex,
            Reference:
                new SkyrimArmorAddonModelReference(
                    FormKey:
                        $"Form{loadOrderIndex}",
                    EditorId:
                        $"Editor{loadOrderIndex}",
                    Field:
                        "WorldModel.Male",
                    GivenPath:
                        requestedPath,
                    DataRelativePath:
                        requestedPath
                ),
            PathInterpretation:
                interpretation
        );
    }

    private static SkyrimArmorAddonSnapshotLoosePathInterpretation
        Interpretation(
            SkyrimArmorAddonSnapshotLoosePathInterpretationState state,
            string requestedPath)
    {
        SkyrimWinningArmorAddonSnapshotPathEvidence evidence =
            new(
                RequestedPath:
                    requestedPath,
                References:
                    Array.Empty<
                        SkyrimWinningArmorAddonSnapshotReferenceContext
                    >(),
                RequestedRootLogicalPath:
                    null,
                State:
                    SkyrimArmorAddonSnapshotLookupEvidenceState
                        .LookupProduced,
                MatchingAnalysisCount:
                    1,
                SelectedAnalysis:
                    null,
                Lookup:
                    null,
                Error:
                    null
            );

        return new SkyrimArmorAddonSnapshotLoosePathInterpretation(
            Evidence:
                evidence,
            State:
                state,
            EvidenceStructureValid:
                true,
            InterpretationError:
                null
        );
    }

    private static
        SkyrimWinningArmorAddonSnapshotPathArchiveEvidence
        ArchiveEvidence(
            SkyrimArmorAddonSnapshotLoosePathInterpretation interpretation)
    {
        return new SkyrimWinningArmorAddonSnapshotPathArchiveEvidence(
            PathInterpretation:
                interpretation,
            ArchiveCandidates:
                Array.Empty<
                    SkyrimArchiveAssetProvider
                >(),
            ArchivePrecedence:
                new SkyrimRuntimeArchivePrecedenceDecision(
                    State:
                        SkyrimRuntimeArchivePrecedenceState
                            .NoRuntimeEvidencedProvider,
                    RuntimeEvidencedProviders:
                        Array.Empty<
                            SkyrimArchiveAssetProvider
                        >(),
                    WinningProvider:
                        null
                )
        );
    }

    private static
        SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceResult
        PathArchiveResult(
            SkyrimWinningArmorAddonSnapshotConsumerDiagnosticResult
                diagnostics,
            params SkyrimWinningArmorAddonSnapshotPathArchiveEvidence[]
                paths)
    {
        string dataRoot =
            diagnostics
                .Projection
                .Scan
                .Inventory
                .DataRoot;

        return new
            SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceResult(
                Diagnostics:
                    diagnostics,
                ArchiveIndex:
                    new SkyrimArchiveCandidateIndexResult(
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
                    ),
                RuntimeArchiveEvidence:
                    new SkyrimRuntimeArchiveEvidenceResult(
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
                    ),
                Paths:
                    paths
            );
    }
}
