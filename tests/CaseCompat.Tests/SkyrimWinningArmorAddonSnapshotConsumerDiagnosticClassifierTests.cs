using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Analysis;

namespace CaseCompat.Tests;

public sealed class
    SkyrimWinningArmorAddonSnapshotConsumerDiagnosticClassifierTests
{
    [Fact]
    public void Classify_IncompleteWinnerSearch_DominatesLooseResolved()
    {
        SkyrimWinningArmorAddonSnapshotConsumerDiagnostic diagnostic =
            SingleDiagnostic(
                winnerSearchComplete:
                    false,
                PathInterpretation(
                    SkyrimArmorAddonSnapshotLoosePathInterpretationState
                        .LooseResolved,
                    WindowsNamespaceSnapshotFileLookupState.Resolved
                )
            );

        Assert.Equal(
            SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                .IncompleteWinnerSearch,
            diagnostic.State
        );
    }

    [Fact]
    public void Classify_IncompleteWinnerSearch_DominatesLooseUnresolved()
    {
        SkyrimWinningArmorAddonSnapshotConsumerDiagnostic diagnostic =
            SingleDiagnostic(
                winnerSearchComplete:
                    false,
                PathInterpretation(
                    SkyrimArmorAddonSnapshotLoosePathInterpretationState
                        .LooseUnresolved,
                    WindowsNamespaceSnapshotFileLookupState.Missing
                )
            );

        Assert.Equal(
            SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                .IncompleteWinnerSearch,
            diagnostic.State
        );
    }

    [Fact]
    public void Classify_IncompleteWinnerSearch_DominatesIndeterminateEvidence()
    {
        SkyrimWinningArmorAddonSnapshotConsumerDiagnostic diagnostic =
            SingleDiagnostic(
                winnerSearchComplete:
                    false,
                PathInterpretation(
                    SkyrimArmorAddonSnapshotLoosePathInterpretationState
                        .IndeterminateEvidence,
                    WindowsNamespaceSnapshotFileLookupState
                        .CasefoldUnknown
                )
            );

        Assert.Equal(
            SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                .IncompleteWinnerSearch,
            diagnostic.State
        );
    }

    [Fact]
    public void Classify_CompleteWinnerSearch_MapsLooseResolved()
    {
        SkyrimWinningArmorAddonSnapshotConsumerDiagnostic diagnostic =
            SingleDiagnostic(
                winnerSearchComplete:
                    true,
                PathInterpretation(
                    SkyrimArmorAddonSnapshotLoosePathInterpretationState
                        .LooseResolved,
                    WindowsNamespaceSnapshotFileLookupState.Resolved
                )
            );

        Assert.Equal(
            SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                .LooseResolved,
            diagnostic.State
        );
    }

    [Fact]
    public void Classify_CompleteWinnerSearch_MapsLooseUnresolved()
    {
        SkyrimWinningArmorAddonSnapshotConsumerDiagnostic diagnostic =
            SingleDiagnostic(
                winnerSearchComplete:
                    true,
                PathInterpretation(
                    SkyrimArmorAddonSnapshotLoosePathInterpretationState
                        .LooseUnresolved,
                    WindowsNamespaceSnapshotFileLookupState.NotFile
                )
            );

        Assert.Equal(
            SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                .LooseUnresolved,
            diagnostic.State
        );
    }

    [Fact]
    public void Classify_CompleteWinnerSearch_MapsIndeterminateEvidence()
    {
        SkyrimWinningArmorAddonSnapshotConsumerDiagnostic diagnostic =
            SingleDiagnostic(
                winnerSearchComplete:
                    true,
                PathInterpretation(
                    SkyrimArmorAddonSnapshotLoosePathInterpretationState
                        .IndeterminateEvidence,
                    WindowsNamespaceSnapshotFileLookupState
                        .AmbiguousEquivalent
                )
            );

        Assert.Equal(
            SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                .IndeterminateEvidence,
            diagnostic.State
        );
    }

    [Fact]
    public void Classify_RetainsExactConsumerAndUnderlyingSnapshotEvidence()
    {
        SkyrimArmorAddonSnapshotLoosePathInterpretation interpretation =
            PathInterpretation(
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .LooseUnresolved,
                WindowsNamespaceSnapshotFileLookupState.NotDirectory
            );

        SkyrimWinningArmorAddonSnapshotConsumerProjection consumer =
            Consumer(
                interpretation,
                pluginName:
                    "Winner.esp",
                loadOrderIndex:
                    42
            );

        SkyrimWinningArmorAddonSnapshotConsumerProjectionResult projection =
            Projection(
                winnerSearchComplete:
                    true,
                consumer
            );

        SkyrimWinningArmorAddonSnapshotConsumerDiagnosticResult result =
            SkyrimWinningArmorAddonSnapshotConsumerDiagnosticClassifier
                .Classify(
                    projection
                );

        SkyrimWinningArmorAddonSnapshotConsumerDiagnostic diagnostic =
            Assert.Single(
                result.Diagnostics
            );

        Assert.Same(
            projection,
            result.Projection
        );

        Assert.Same(
            consumer,
            diagnostic.Consumer
        );

        Assert.Same(
            interpretation,
            diagnostic.PathInterpretation
        );

        Assert.Equal(
            WindowsNamespaceSnapshotFileLookupState.NotDirectory,
            diagnostic
                .PathInterpretation
                .SnapshotState
        );

        Assert.Equal(
            "Winner.esp",
            diagnostic.Consumer.WinningPluginName
        );

        Assert.Equal(
            42,
            diagnostic.Consumer.WinningLoadOrderIndex
        );
    }

    [Fact]
    public void Classify_MultipleConsumers_PreserveInputOrder()
    {
        SkyrimWinningArmorAddonSnapshotConsumerProjection first =
            Consumer(
                PathInterpretation(
                    SkyrimArmorAddonSnapshotLoosePathInterpretationState
                        .LooseResolved,
                    WindowsNamespaceSnapshotFileLookupState.Resolved,
                    requestedPath:
                        "Meshes/A.nif"
                ),
                pluginName:
                    "A.esp",
                loadOrderIndex:
                    1
            );

        SkyrimWinningArmorAddonSnapshotConsumerProjection second =
            Consumer(
                PathInterpretation(
                    SkyrimArmorAddonSnapshotLoosePathInterpretationState
                        .LooseUnresolved,
                    WindowsNamespaceSnapshotFileLookupState.Missing,
                    requestedPath:
                        "Meshes/B.nif"
                ),
                pluginName:
                    "B.esp",
                loadOrderIndex:
                    2
            );

        SkyrimWinningArmorAddonSnapshotConsumerProjection third =
            Consumer(
                PathInterpretation(
                    SkyrimArmorAddonSnapshotLoosePathInterpretationState
                        .IndeterminateEvidence,
                    WindowsNamespaceSnapshotFileLookupState.CasefoldUnknown,
                    requestedPath:
                        "Meshes/C.nif"
                ),
                pluginName:
                    "C.esp",
                loadOrderIndex:
                    3
            );

        SkyrimWinningArmorAddonSnapshotConsumerDiagnosticResult result =
            SkyrimWinningArmorAddonSnapshotConsumerDiagnosticClassifier
                .Classify(
                    Projection(
                        winnerSearchComplete:
                            true,
                        first,
                        second,
                        third
                    )
                );

        Assert.Equal(
            new[]
            {
                first,
                second,
                third
            },
            result.Diagnostics
                .Select(
                    item =>
                        item.Consumer
                )
                .ToArray()
        );

        Assert.Equal(
            new[]
            {
                SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                    .LooseResolved,
                SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                    .LooseUnresolved,
                SkyrimWinningArmorAddonSnapshotConsumerDiagnosticState
                    .IndeterminateEvidence
            },
            result.Diagnostics
                .Select(
                    item =>
                        item.State
                )
                .ToArray()
        );
    }

    [Fact]
    public void Classify_EmptyProjection_ProducesEmptyDiagnostics()
    {
        SkyrimWinningArmorAddonSnapshotConsumerProjectionResult projection =
            Projection(
                winnerSearchComplete:
                    true
            );

        SkyrimWinningArmorAddonSnapshotConsumerDiagnosticResult result =
            SkyrimWinningArmorAddonSnapshotConsumerDiagnosticClassifier
                .Classify(
                    projection
                );

        Assert.Same(
            projection,
            result.Projection
        );

        Assert.Empty(
            result.Diagnostics
        );

        Assert.Equal(
            0,
            result.DiagnosticCount
        );

        Assert.True(
            result.WinnerSearchComplete
        );
    }

    private static SkyrimWinningArmorAddonSnapshotConsumerDiagnostic
        SingleDiagnostic(
            bool winnerSearchComplete,
            SkyrimArmorAddonSnapshotLoosePathInterpretation interpretation)
    {
        SkyrimWinningArmorAddonSnapshotConsumerDiagnosticResult result =
            SkyrimWinningArmorAddonSnapshotConsumerDiagnosticClassifier
                .Classify(
                    Projection(
                        winnerSearchComplete,
                        Consumer(
                            interpretation
                        )
                    )
                );

        return Assert.Single(
            result.Diagnostics
        );
    }

    private static SkyrimWinningArmorAddonSnapshotConsumerProjectionResult
        Projection(
            bool winnerSearchComplete,
            params SkyrimWinningArmorAddonSnapshotConsumerProjection[]
                consumers)
    {
        SkyrimWinningArmorAddonInventoryResult inventory =
            new(
                DataRoot:
                    "/fixture/Data",
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
                    consumers
                        .Select(
                            consumer =>
                                consumer
                                    .PathInterpretation
                                    .Evidence
                        )
                        .Distinct()
                        .ToArray()
            );

        SkyrimArmorAddonSnapshotLoosePathInterpretation[]
            interpretations =
                consumers
                    .Select(
                        consumer =>
                            consumer.PathInterpretation
                    )
                    .Distinct()
                    .ToArray();

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
            string pluginName = "Winner.esp",
            int loadOrderIndex = 10)
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
                        "ArmorForm",
                    EditorId:
                        "ArmorEditor",
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
        PathInterpretation(
            SkyrimArmorAddonSnapshotLoosePathInterpretationState state,
            WindowsNamespaceSnapshotFileLookupState snapshotState,
            string requestedPath = "Meshes/Test.nif")
    {
        WindowsNamespaceAnalysis analysis =
            Analysis();

        WindowsNamespacePhysicalParticipant? participant =
            snapshotState ==
            WindowsNamespaceSnapshotFileLookupState.Resolved
                ? Participant(
                    requestedPath
                )
                : null;

        WindowsNamespaceSnapshotFileLookup lookup =
            new(
                Analysis:
                    analysis,
                RequestedRelativePath:
                    requestedPath,
                RequestedLogicalPath:
                    WindowsLogicalPath.FromRelativePath(
                        requestedPath
                    ),
                State:
                    snapshotState,
                ResolvedParticipant:
                    participant,
                FailedComponentIndex:
                    snapshotState ==
                    WindowsNamespaceSnapshotFileLookupState.Resolved
                        ? null
                        : 1,
                Steps:
                    Array.Empty<
                        WindowsNamespaceSnapshotFileLookupStep
                    >(),
                Error:
                    snapshotState ==
                    WindowsNamespaceSnapshotFileLookupState.Resolved
                        ? null
                        : "fixture"
            );

        SkyrimWinningArmorAddonSnapshotPathEvidence evidence =
            new(
                RequestedPath:
                    requestedPath,
                References:
                    Array.Empty<
                        SkyrimWinningArmorAddonSnapshotReferenceContext
                    >(),
                RequestedRootLogicalPath:
                    WindowsLogicalPath.FromRelativePath(
                        "Meshes"
                    ),
                State:
                    SkyrimArmorAddonSnapshotLookupEvidenceState
                        .LookupProduced,
                MatchingAnalysisCount:
                    1,
                SelectedAnalysis:
                    analysis,
                Lookup:
                    lookup,
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

    private static WindowsNamespaceAnalysis Analysis()
    {
        return new WindowsNamespaceAnalysis(
            DataRootPath:
                "/fixture/Data",
            RootLogicalPath:
                WindowsLogicalPath.FromRelativePath(
                    "Meshes"
                ),
            DirectoryLookupObservations:
                Array.Empty<
                    WindowsNamespaceDirectoryLookupObservation
                >(),
            DirectoryIncarnationObservations:
                Array.Empty<
                    WindowsNamespaceDirectoryIncarnationObservation
                >(),
            FileIncarnationObservations:
                Array.Empty<
                    WindowsNamespaceFileIncarnationObservation
                >(),
            Nodes:
                Array.Empty<
                    WindowsNamespaceNode
                >(),
            Errors:
                Array.Empty<string>()
        ) with
        {
            DataRootChildNames =
                Array.Empty<string>()
        };
    }

    private static WindowsNamespacePhysicalParticipant Participant(
        string requestedPath)
    {
        string normalized =
            requestedPath.Replace(
                '\\',
                '/'
            );

        string name =
            normalized
                .Split('/')
                [^1];

        return new WindowsNamespacePhysicalParticipant(
            FullPath:
                "/fixture/Data/" +
                normalized,
            RelativePath:
                normalized,
            Name:
                name,
            Kind:
                WindowsNamespacePhysicalObjectKind.File,
            DeviceMajor:
                8,
            DeviceMinor:
                1,
            Inode:
                100,
            MountId:
                42,
            IdentityError:
                null
        );
    }
}
