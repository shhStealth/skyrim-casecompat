using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Analysis;

namespace CaseCompat.Tests;

public sealed class
    SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticClassifierTests
{
    [Fact]
    public void Classify_IncompleteWinnerSearch_DominatesArchiveCompleteness()
    {
        var consumer =
            ConsumerArchiveProjection(
                SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                    .IncompleteWinnerSearch,
                "meshes/test.nif",
                archivePrecedence:
                    null
            );

        var result =
            SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticClassifier
                .Classify(
                    Projection(
                        winnerSearchComplete:
                            false,
                        archiveCandidateIndexComplete:
                            false,
                        runtimeArchiveEvidenceComplete:
                            false,
                        consumer
                    )
                );

        Assert.Equal(
            SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
                .IncompleteWinnerSearch,
            Assert.Single(
                result.Diagnostics
            ).State
        );
    }

    [Fact]
    public void Classify_LooseResolved_IgnoresArchiveCompleteness()
    {
        var consumer =
            ConsumerArchiveProjection(
                SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                    .LooseResolved,
                "meshes/resolved.nif",
                archivePrecedence:
                    null
            );

        var result =
            SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticClassifier
                .Classify(
                    Projection(
                        winnerSearchComplete:
                            true,
                        archiveCandidateIndexComplete:
                            false,
                        runtimeArchiveEvidenceComplete:
                            false,
                        consumer
                    )
                );

        Assert.Equal(
            SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
                .LooseResolved,
            Assert.Single(
                result.Diagnostics
            ).State
        );
    }

    [Fact]
    public void Classify_IndeterminateEvidence_RemainsIndeterminate()
    {
        var consumer =
            ConsumerArchiveProjection(
                SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                    .IndeterminateEvidence,
                "meshes/unknown.nif",
                archivePrecedence:
                    null
            );

        var result =
            SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticClassifier
                .Classify(
                    Projection(
                        winnerSearchComplete:
                            true,
                        archiveCandidateIndexComplete:
                            false,
                        runtimeArchiveEvidenceComplete:
                            false,
                        consumer
                    )
                );

        Assert.Equal(
            SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
                .IndeterminateEvidence,
            Assert.Single(
                result.Diagnostics
            ).State
        );
    }

    [Fact]
    public void Classify_LooseUnresolved_IncompleteArchiveIndexDominatesWinner()
    {
        SkyrimArchiveAssetProvider provider =
            Provider(
                "Winner.bsa"
            );

        var consumer =
            ConsumerArchiveProjection(
                SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                    .LooseUnresolved,
                "meshes/test.nif",
                WinnerDecision(
                    provider
                )
            );

        var result =
            SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticClassifier
                .Classify(
                    Projection(
                        winnerSearchComplete:
                            true,
                        archiveCandidateIndexComplete:
                            false,
                        runtimeArchiveEvidenceComplete:
                            true,
                        consumer
                    )
                );

        Assert.Equal(
            SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
                .IncompleteArchiveCandidateIndex,
            Assert.Single(
                result.Diagnostics
            ).State
        );
    }

    [Fact]
    public void Classify_LooseUnresolved_IncompleteRuntimeEvidenceDominatesWinner()
    {
        SkyrimArchiveAssetProvider provider =
            Provider(
                "Winner.bsa"
            );

        var consumer =
            ConsumerArchiveProjection(
                SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                    .LooseUnresolved,
                "meshes/test.nif",
                WinnerDecision(
                    provider
                )
            );

        var result =
            SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticClassifier
                .Classify(
                    Projection(
                        winnerSearchComplete:
                            true,
                        archiveCandidateIndexComplete:
                            true,
                        runtimeArchiveEvidenceComplete:
                            false,
                        consumer
                    )
                );

        Assert.Equal(
            SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
                .IncompleteRuntimeArchiveEvidence,
            Assert.Single(
                result.Diagnostics
            ).State
        );
    }

    [Fact]
    public void Classify_LooseUnresolved_RuntimeWinnerIsClassifiedAndExposed()
    {
        SkyrimArchiveAssetProvider provider =
            Provider(
                "Winner.bsa"
            );

        var sourceProjection =
            ConsumerArchiveProjection(
                SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                    .LooseUnresolved,
                "meshes/test.nif",
                WinnerDecision(
                    provider
                )
            );

        var diagnostic =
            Assert.Single(
                SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticClassifier
                    .Classify(
                        Projection(
                            winnerSearchComplete:
                                true,
                            archiveCandidateIndexComplete:
                                true,
                            runtimeArchiveEvidenceComplete:
                                true,
                            sourceProjection
                        )
                    )
                    .Diagnostics
            );

        Assert.Equal(
            SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
                .LooseUnresolvedWithRuntimeArchiveWinner,
            diagnostic.State
        );

        Assert.Same(
            provider,
            diagnostic.WinningArchiveProvider
        );

        Assert.Same(
            sourceProjection,
            diagnostic.Projection
        );
    }

    [Fact]
    public void Classify_LooseUnresolved_AmbiguousArchivePrecedenceIsClassified()
    {
        var consumer =
            ConsumerArchiveProjection(
                SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                    .LooseUnresolved,
                "meshes/test.nif",
                AmbiguousDecision()
            );

        var result =
            SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticClassifier
                .Classify(
                    Projection(
                        winnerSearchComplete:
                            true,
                        archiveCandidateIndexComplete:
                            true,
                        runtimeArchiveEvidenceComplete:
                            true,
                        consumer
                    )
                );

        Assert.Equal(
            SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
                .LooseUnresolvedWithAmbiguousArchivePrecedence,
            Assert.Single(
                result.Diagnostics
            ).State
        );
    }

    [Fact]
    public void Classify_LooseUnresolved_NoRuntimeProviderIsClassified()
    {
        var consumer =
            ConsumerArchiveProjection(
                SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                    .LooseUnresolved,
                "meshes/missing.nif",
                NoProviderDecision()
            );

        var result =
            SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticClassifier
                .Classify(
                    Projection(
                        winnerSearchComplete:
                            true,
                        archiveCandidateIndexComplete:
                            true,
                        runtimeArchiveEvidenceComplete:
                            true,
                        consumer
                    )
                );

        Assert.Equal(
            SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
                .LooseUnresolvedWithoutRuntimeArchiveProvider,
            Assert.Single(
                result.Diagnostics
            ).State
        );
    }

    [Fact]
    public void Classify_MalformedPrecedenceShape_FailsClosed()
    {
        SkyrimArchiveAssetProvider provider =
            Provider(
                "Malformed.bsa"
            );

        var malformed =
            new SkyrimRuntimeArchivePrecedenceDecision(
                State:
                    SkyrimRuntimeArchivePrecedenceState
                        .SingleRuntimeEvidencedProvider,
                RuntimeEvidencedProviders:
                    new[]
                    {
                        provider
                    },
                WinningProvider:
                    null
            );

        var consumer =
            ConsumerArchiveProjection(
                SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                    .LooseUnresolved,
                "meshes/test.nif",
                malformed
            );

        var result =
            SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticClassifier
                .Classify(
                    Projection(
                        winnerSearchComplete:
                            true,
                        archiveCandidateIndexComplete:
                            true,
                        runtimeArchiveEvidenceComplete:
                            true,
                        consumer
                    )
                );

        Assert.Equal(
            SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
                .IndeterminateEvidence,
            Assert.Single(
                result.Diagnostics
            ).State
        );
    }

    [Fact]
    public void Classify_MultipleConsumers_PreserveInputOrderAndExactProjection()
    {
        var first =
            ConsumerArchiveProjection(
                SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                    .LooseResolved,
                "meshes/first.nif",
                archivePrecedence:
                    null
            );

        var second =
            ConsumerArchiveProjection(
                SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                    .LooseUnresolved,
                "meshes/second.nif",
                NoProviderDecision()
            );

        var result =
            SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticClassifier
                .Classify(
                    Projection(
                        winnerSearchComplete:
                            true,
                        archiveCandidateIndexComplete:
                            true,
                        runtimeArchiveEvidenceComplete:
                            true,
                        first,
                        second
                    )
                );

        Assert.Equal(
            2,
            result.DiagnosticCount
        );

        Assert.Same(
            first,
            result.Diagnostics[0].Projection
        );

        Assert.Same(
            second,
            result.Diagnostics[1].Projection
        );
    }

    [Fact]
    public void Classify_EmptyProjection_ProducesEmptyDiagnostics()
    {
        var result =
            SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticClassifier
                .Classify(
                    Projection(
                        winnerSearchComplete:
                            true,
                        archiveCandidateIndexComplete:
                            true,
                        runtimeArchiveEvidenceComplete:
                            true
                    )
                );

        Assert.Empty(
            result.Diagnostics
        );

        Assert.Equal(
            0,
            result.DiagnosticCount
        );
    }

    private static
        SkyrimWinningArmorAddonSnapshotConsumerArchiveProjection
        ConsumerArchiveProjection(
            SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState state,
            string requestedPath,
            SkyrimRuntimeArchivePrecedenceDecision? archivePrecedence)
    {
        SkyrimArmorAddonSnapshotLoosePathInterpretation interpretation =
            Interpretation(
                state,
                requestedPath
            );

        SkyrimWinningArmorAddonSnapshotConsumerProjection consumer =
            new(
                WinningPluginName:
                    "Winner.esp",
                WinningLoadOrderIndex:
                    10,
                Reference:
                    new SkyrimArmorAddonModelReference(
                        FormKey:
                            "000800:Winner.esp",
                        EditorId:
                            "Fixture",
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

        SkyrimWinningArmorAddonSnapshotConsumerDiagnostic diagnostic =
            new(
                Consumer:
                    consumer,
                State:
                    state
            );

        SkyrimWinningArmorAddonSnapshotPathArchiveEvidence?
            pathArchiveEvidence =
                archivePrecedence is null
                    ? null
                    : new
                        SkyrimWinningArmorAddonSnapshotPathArchiveEvidence(
                            PathInterpretation:
                                interpretation,
                            ArchiveCandidates:
                                archivePrecedence
                                    .RuntimeEvidencedProviders,
                            ArchivePrecedence:
                                archivePrecedence
                        );

        return new
            SkyrimWinningArmorAddonSnapshotConsumerArchiveProjection(
                Diagnostic:
                    diagnostic,
                PathArchiveEvidence:
                    pathArchiveEvidence
            );
    }

    private static
        SkyrimArmorAddonSnapshotLoosePathInterpretation
        Interpretation(
            SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState state,
            string requestedPath)
    {
        SkyrimArmorAddonSnapshotLoosePathInterpretationState
            interpretationState =
                state switch
                {
                    SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                        .LooseResolved =>
                            SkyrimArmorAddonSnapshotLoosePathInterpretationState
                                .LooseResolved,

                    SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                        .LooseUnresolved =>
                            SkyrimArmorAddonSnapshotLoosePathInterpretationState
                                .LooseUnresolved,

                    _ =>
                        SkyrimArmorAddonSnapshotLoosePathInterpretationState
                            .IndeterminateEvidence
                };

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
                interpretationState,
            EvidenceStructureValid:
                true,
            InterpretationError:
                null
        );
    }

    private static
        SkyrimWinningArmorAddonSnapshotConsumerArchiveProjectionResult
        Projection(
            bool winnerSearchComplete,
            bool archiveCandidateIndexComplete,
            bool runtimeArchiveEvidenceComplete,
            params
                SkyrimWinningArmorAddonSnapshotConsumerArchiveProjection[]
                    consumers)
    {
        const string dataRoot =
            "/fixture/Data";

        SkyrimWinningArmorAddonSnapshotConsumerProjection[] sourceConsumers =
            consumers
                .Select(
                    consumer =>
                        consumer.Consumer
                )
                .ToArray();

        SkyrimArmorAddonSnapshotLoosePathInterpretation[]
            interpretations =
                sourceConsumers
                    .Select(
                        consumer =>
                            consumer.PathInterpretation
                    )
                    .Distinct<
                        SkyrimArmorAddonSnapshotLoosePathInterpretation
                    >(
                        ReferenceEqualityComparer.Instance
                    )
                    .ToArray();

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
                        .ToArray()
            );

        SkyrimWinningArmorAddonSnapshotConsumerProjectionResult
            consumerProjection =
                new(
                    Scan:
                        scan,
                    PathInterpretations:
                        interpretations,
                    Consumers:
                        sourceConsumers
                );

        SkyrimWinningArmorAddonSnapshotConsumerDiagnosticResult
            sourceDiagnostics =
                new(
                    Projection:
                        consumerProjection,
                    Diagnostics:
                        consumers
                            .Select(
                                consumer =>
                                    consumer.Diagnostic
                            )
                            .ToArray()
                );

        SkyrimArchiveCandidateIndexResult archiveIndex =
            new(
                DataRoot:
                    dataRoot,
                ArchivesDiscovered:
                    archiveCandidateIndexComplete
                        ? 0
                        : 1,
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

        SkyrimRuntimeArchiveEvidenceResult runtimeEvidence =
            new(
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
                    runtimeArchiveEvidenceComplete
                        ? Array.Empty<
                            SkyrimRuntimeArchiveIniReadError
                        >()
                        : new[]
                        {
                            new SkyrimRuntimeArchiveIniReadError(
                                IniName:
                                    "Skyrim.ini",
                                IniPath:
                                    "/fixture/Skyrim.ini",
                                Error:
                                    "fixture"
                            )
                        },
                IniProvenanceErrors:
                    Array.Empty<
                        SkyrimRuntimeArchiveIniProvenanceError
                    >()
            );

        SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceResult
            pathArchiveResult =
                new(
                    Diagnostics:
                        sourceDiagnostics,
                    ArchiveIndex:
                        archiveIndex,
                    RuntimeArchiveEvidence:
                        runtimeEvidence,
                    Paths:
                        consumers
                            .Where(
                                consumer =>
                                    consumer.PathArchiveEvidence is not null
                            )
                            .Select(
                                consumer =>
                                    consumer.PathArchiveEvidence!
                            )
                            .Distinct<
                                SkyrimWinningArmorAddonSnapshotPathArchiveEvidence
                            >(
                                ReferenceEqualityComparer.Instance
                            )
                            .ToArray()
                );

        return new
            SkyrimWinningArmorAddonSnapshotConsumerArchiveProjectionResult(
                PathArchiveResult:
                    pathArchiveResult,
                Consumers:
                    consumers
            );
    }

    private static SkyrimArchiveAssetProvider Provider(
        string archiveName)
    {
        return new SkyrimArchiveAssetProvider(
            ArchiveName:
                archiveName,
            ArchivePath:
                $"/fixture/Data/{archiveName}",
            InternalPath:
                "meshes/test.nif",
            Size:
                100
        );
    }

    private static SkyrimRuntimeArchivePrecedenceDecision WinnerDecision(
        SkyrimArchiveAssetProvider provider)
    {
        return new SkyrimRuntimeArchivePrecedenceDecision(
            State:
                SkyrimRuntimeArchivePrecedenceState
                    .SingleRuntimeEvidencedProvider,
            RuntimeEvidencedProviders:
                new[]
                {
                    provider
                },
            WinningProvider:
                provider
        );
    }

    private static SkyrimRuntimeArchivePrecedenceDecision
        AmbiguousDecision()
    {
        SkyrimArchiveAssetProvider first =
            Provider(
                "First.bsa"
            );

        SkyrimArchiveAssetProvider second =
            Provider(
                "Second.bsa"
            );

        return new SkyrimRuntimeArchivePrecedenceDecision(
            State:
                SkyrimRuntimeArchivePrecedenceState
                    .AmbiguousSamePluginLoadOrderIndex,
            RuntimeEvidencedProviders:
                new[]
                {
                    first,
                    second
                },
            WinningProvider:
                null
        );
    }

    private static SkyrimRuntimeArchivePrecedenceDecision
        NoProviderDecision()
    {
        return new SkyrimRuntimeArchivePrecedenceDecision(
            State:
                SkyrimRuntimeArchivePrecedenceState
                    .NoRuntimeEvidencedProvider,
            RuntimeEvidencedProviders:
                Array.Empty<
                    SkyrimArchiveAssetProvider
                >(),
            WinningProvider:
                null
        );
    }
}
