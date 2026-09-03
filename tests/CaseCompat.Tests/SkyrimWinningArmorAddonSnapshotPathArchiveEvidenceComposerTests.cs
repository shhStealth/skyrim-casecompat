using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Analysis;

namespace CaseCompat.Tests;

public sealed class
    SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceComposerTests
{
    [Fact]
    public void Compose_LooseUnresolved_ProducesArchiveEvidence()
    {
        const string dataRoot =
            "/fixture/Data";

        SkyrimArmorAddonSnapshotLoosePathInterpretation interpretation =
            Interpretation(
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .LooseUnresolved,
                "meshes/armor/test.nif"
            );

        SkyrimArchiveAssetProvider provider =
            Provider(
                "Armor.bsa",
                "/fixture/Data/Armor.bsa",
                "meshes/armor/test.nif"
            );

        SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceResult result =
            SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceComposer
                .Compose(
                    Diagnostics(
                        dataRoot,
                        winnerSearchComplete:
                            true,
                        interpretation
                    ),
                    ArchiveIndex(
                        dataRoot,
                        "meshes/armor/test.nif",
                        provider
                    ),
                    RuntimeEvidence(
                        dataRoot,
                        RuntimeEntry(
                            provider,
                            loadOrderIndex:
                                10
                        )
                    )
                );

        SkyrimWinningArmorAddonSnapshotPathArchiveEvidence evidence =
            Assert.Single(
                result.Paths
            );

        Assert.Same(
            interpretation,
            evidence.PathInterpretation
        );

        Assert.Equal(
            "meshes/armor/test.nif",
            evidence.RequestedPath
        );

        Assert.Same(
            provider,
            Assert.Single(
                evidence.ArchiveCandidates
            )
        );

        Assert.Same(
            provider,
            evidence.ArchivePrecedence.WinningProvider
        );
    }

    [Fact]
    public void Compose_ArchiveLookupUsesSharedWindowsLogicalKeySemantics()
    {
        const string dataRoot =
            "/fixture/Data";

        SkyrimArmorAddonSnapshotLoosePathInterpretation interpretation =
            Interpretation(
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .LooseUnresolved,
                "meshes/armor/test.nif"
            );

        SkyrimArchiveAssetProvider provider =
            Provider(
                "Armor.bsa",
                "/fixture/Data/Armor.bsa",
                "MESHES\\Armor\\TEST.NIF"
            );

        SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceResult result =
            SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceComposer
                .Compose(
                    Diagnostics(
                        dataRoot,
                        winnerSearchComplete:
                            true,
                        interpretation
                    ),
                    ArchiveIndex(
                        dataRoot,
                        "MESHES\\Armor\\TEST.NIF",
                        provider
                    ),
                    RuntimeEvidence(
                        dataRoot
                    )
                );

        SkyrimWinningArmorAddonSnapshotPathArchiveEvidence evidence =
            Assert.Single(
                result.Paths
            );

        Assert.Same(
            provider,
            Assert.Single(
                evidence.ArchiveCandidates
            )
        );
    }

    [Fact]
    public void Compose_PreservesAllArchiveProviders()
    {
        const string dataRoot =
            "/fixture/Data";

        SkyrimArmorAddonSnapshotLoosePathInterpretation interpretation =
            Interpretation(
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .LooseUnresolved,
                "meshes/test.nif"
            );

        SkyrimArchiveAssetProvider first =
            Provider(
                "First.bsa",
                "/fixture/Data/First.bsa",
                "meshes/test.nif"
            );

        SkyrimArchiveAssetProvider second =
            Provider(
                "Second.bsa",
                "/fixture/Data/Second.bsa",
                "meshes/test.nif"
            );

        SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceResult result =
            SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceComposer
                .Compose(
                    Diagnostics(
                        dataRoot,
                        winnerSearchComplete:
                            true,
                        interpretation
                    ),
                    ArchiveIndex(
                        dataRoot,
                        "meshes/test.nif",
                        first,
                        second
                    ),
                    RuntimeEvidence(
                        dataRoot
                    )
                );

        SkyrimWinningArmorAddonSnapshotPathArchiveEvidence evidence =
            Assert.Single(
                result.Paths
            );

        Assert.Equal(
            new[]
            {
                first,
                second
            },
            evidence.ArchiveCandidates
        );

        Assert.Equal(
            2,
            evidence.ArchiveCandidateCount
        );
    }

    [Fact]
    public void Compose_ReusesRuntimeArchivePrecedence()
    {
        const string dataRoot =
            "/fixture/Data";

        SkyrimArmorAddonSnapshotLoosePathInterpretation interpretation =
            Interpretation(
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .LooseUnresolved,
                "meshes/test.nif"
            );

        SkyrimArchiveAssetProvider first =
            Provider(
                "First.bsa",
                "/fixture/Data/First.bsa",
                "meshes/test.nif"
            );

        SkyrimArchiveAssetProvider second =
            Provider(
                "Second.bsa",
                "/fixture/Data/Second.bsa",
                "meshes/test.nif"
            );

        SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceResult result =
            SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceComposer
                .Compose(
                    Diagnostics(
                        dataRoot,
                        winnerSearchComplete:
                            true,
                        interpretation
                    ),
                    ArchiveIndex(
                        dataRoot,
                        "meshes/test.nif",
                        first,
                        second
                    ),
                    RuntimeEvidence(
                        dataRoot,
                        RuntimeEntry(
                            first,
                            loadOrderIndex:
                                10
                        ),
                        RuntimeEntry(
                            second,
                            loadOrderIndex:
                                20
                        )
                    )
                );

        SkyrimWinningArmorAddonSnapshotPathArchiveEvidence evidence =
            Assert.Single(
                result.Paths
            );

        Assert.Equal(
            SkyrimRuntimeArchivePrecedenceState
                .ResolvedByPluginLoadOrder,
            evidence.ArchivePrecedence.State
        );

        Assert.Same(
            second,
            evidence.ArchivePrecedence.WinningProvider
        );
    }

    [Fact]
    public void Compose_LooseResolved_IsNotArchiveEligible()
    {
        const string dataRoot =
            "/fixture/Data";

        SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceResult result =
            SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceComposer
                .Compose(
                    Diagnostics(
                        dataRoot,
                        winnerSearchComplete:
                            true,
                        Interpretation(
                            SkyrimArmorAddonSnapshotLoosePathInterpretationState
                                .LooseResolved,
                            "meshes/resolved.nif"
                        )
                    ),
                    ArchiveIndex(
                        dataRoot
                    ),
                    RuntimeEvidence(
                        dataRoot
                    )
                );

        Assert.Empty(
            result.Paths
        );
    }

    [Fact]
    public void Compose_IndeterminateEvidence_IsNotArchiveEligible()
    {
        const string dataRoot =
            "/fixture/Data";

        SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceResult result =
            SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceComposer
                .Compose(
                    Diagnostics(
                        dataRoot,
                        winnerSearchComplete:
                            true,
                        Interpretation(
                            SkyrimArmorAddonSnapshotLoosePathInterpretationState
                                .IndeterminateEvidence,
                            "meshes/unknown.nif"
                        )
                    ),
                    ArchiveIndex(
                        dataRoot
                    ),
                    RuntimeEvidence(
                        dataRoot
                    )
                );

        Assert.Empty(
            result.Paths
        );
    }

    [Fact]
    public void Compose_IncompleteWinnerSearch_SuppressesArchiveEvaluation()
    {
        const string dataRoot =
            "/fixture/Data";

        SkyrimArmorAddonSnapshotLoosePathInterpretation interpretation =
            Interpretation(
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .LooseUnresolved,
                "meshes/test.nif"
            );

        /*
         * Duplicate runtime ArchivePath entries would make the existing
         * precedence resolver's dictionary construction throw.
         *
         * Checkpoint-10F winner precedence must suppress archive evaluation
         * before that resolver is constructed.
         */
        SkyrimRuntimeArchiveEvidenceEntry duplicateA =
            new(
                ArchiveName:
                    "A.bsa",
                ArchivePath:
                    "/fixture/Data/A.bsa",
                PluginAssociations:
                    Array.Empty<
                        SkyrimRuntimeArchivePluginAssociation
                    >(),
                IniListings:
                    Array.Empty<
                        SkyrimRuntimeArchiveIniListing
                    >()
            );

        SkyrimRuntimeArchiveEvidenceEntry duplicateB =
            duplicateA;

        SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceResult result =
            SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceComposer
                .Compose(
                    Diagnostics(
                        dataRoot,
                        winnerSearchComplete:
                            false,
                        interpretation
                    ),
                    ArchiveIndex(
                        dataRoot
                    ),
                    RuntimeEvidence(
                        dataRoot,
                        duplicateA,
                        duplicateB
                    )
                );

        Assert.Empty(
            result.Paths
        );

        Assert.False(
            result.WinnerSearchComplete
        );
    }

    [Fact]
    public void Compose_SharedPathInterpretation_IsEvaluatedOnce()
    {
        const string dataRoot =
            "/fixture/Data";

        SkyrimArmorAddonSnapshotLoosePathInterpretation interpretation =
            Interpretation(
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .LooseUnresolved,
                "meshes/shared.nif"
            );

        SkyrimWinningArmorAddonSnapshotConsumerProjection first =
            Consumer(
                interpretation,
                "First.esp",
                10
            );

        SkyrimWinningArmorAddonSnapshotConsumerProjection second =
            Consumer(
                interpretation,
                "Second.esp",
                20
            );

        SkyrimWinningArmorAddonSnapshotConsumerProjectionResult projection =
            Projection(
                dataRoot,
                winnerSearchComplete:
                    true,
                new[]
                {
                    interpretation
                },
                new[]
                {
                    first,
                    second
                }
            );

        SkyrimWinningArmorAddonSnapshotConsumerDiagnosticResult diagnostics =
            SkyrimWinningArmorAddonSnapshotConsumerDiagnosticClassifier
                .Classify(
                    projection
                );

        SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceResult result =
            SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceComposer
                .Compose(
                    diagnostics,
                    ArchiveIndex(
                        dataRoot
                    ),
                    RuntimeEvidence(
                        dataRoot
                    )
                );

        Assert.Equal(
            2,
            diagnostics.DiagnosticCount
        );

        Assert.Single(
            result.Paths
        );

        Assert.Same(
            interpretation,
            result.Paths[0].PathInterpretation
        );
    }

    [Fact]
    public void Compose_RetainsPartialArchiveEvidenceAndCompletenessFlags()
    {
        const string dataRoot =
            "/fixture/Data";

        SkyrimArmorAddonSnapshotLoosePathInterpretation interpretation =
            Interpretation(
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .LooseUnresolved,
                "meshes/test.nif"
            );

        SkyrimArchiveAssetProvider provider =
            Provider(
                "Partial.bsa",
                "/fixture/Data/Partial.bsa",
                "meshes/test.nif"
            );

        SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceResult result =
            SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceComposer
                .Compose(
                    Diagnostics(
                        dataRoot,
                        winnerSearchComplete:
                            true,
                        interpretation
                    ),
                    ArchiveIndex(
                        dataRoot,
                        requestedPath:
                            "meshes/test.nif",
                        complete:
                            false,
                        provider
                    ),
                    RuntimeEvidence(
                        dataRoot,
                        complete:
                            false
                    )
                );

        SkyrimWinningArmorAddonSnapshotPathArchiveEvidence evidence =
            Assert.Single(
                result.Paths
            );

        Assert.Same(
            provider,
            Assert.Single(
                evidence.ArchiveCandidates
            )
        );

        Assert.False(
            result.ArchiveCandidateIndexComplete
        );

        Assert.False(
            result.RuntimeArchiveEvidenceComplete
        );
    }

    [Fact]
    public void Compose_DataRootMismatch_IsRejected()
    {
        const string dataRoot =
            "/fixture/Data";

        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () =>
                    SkyrimWinningArmorAddonSnapshotPathArchiveEvidenceComposer
                        .Compose(
                            Diagnostics(
                                dataRoot,
                                winnerSearchComplete:
                                    true,
                                Interpretation(
                                    SkyrimArmorAddonSnapshotLoosePathInterpretationState
                                        .LooseUnresolved,
                                    "meshes/test.nif"
                                )
                            ),
                            ArchiveIndex(
                                "/other/Data"
                            ),
                            RuntimeEvidence(
                                dataRoot
                            )
                        )
            );

        Assert.Contains(
            "same Data root",
            exception.Message,
            StringComparison.Ordinal
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

    private static SkyrimArchiveAssetProvider Provider(
        string archiveName,
        string archivePath,
        string internalPath)
    {
        return new SkyrimArchiveAssetProvider(
            ArchiveName:
                archiveName,
            ArchivePath:
                archivePath,
            InternalPath:
                internalPath,
            Size:
                100
        );
    }

    private static SkyrimArchiveCandidateIndexResult ArchiveIndex(
        string dataRoot,
        string? requestedPath = null,
        params SkyrimArchiveAssetProvider[] providers)
    {
        return ArchiveIndex(
            dataRoot,
            requestedPath,
            complete:
                true,
            providers
        );
    }

    private static SkyrimArchiveCandidateIndexResult ArchiveIndex(
        string dataRoot,
        string? requestedPath,
        bool complete,
        params SkyrimArchiveAssetProvider[] providers)
    {
        var assets =
            new Dictionary<
                WindowsLogicalPath,
                IReadOnlyList<SkyrimArchiveAssetProvider>
            >();

        if (requestedPath is not null)
        {
            assets.Add(
                WindowsLogicalPath.FromRelativePath(
                    requestedPath
                ),
                providers
            );
        }

        int archiveCount =
            providers
                .Select(
                    provider =>
                        provider.ArchivePath
                )
                .Distinct(
                    StringComparer.Ordinal
                )
                .Count();

        return new SkyrimArchiveCandidateIndexResult(
            DataRoot:
                dataRoot,
            ArchivesDiscovered:
                complete
                    ? archiveCount
                    : archiveCount + 1,
            ArchivesRead:
                archiveCount,
            TotalFileEntries:
                providers.Length,
            DuplicateLogicalEntriesWithinArchive:
                0,
            ReadErrors:
                Array.Empty<
                    SkyrimArchiveReadError
                >(),
            Assets:
                assets
        );
    }

    private static SkyrimRuntimeArchiveEvidenceResult RuntimeEvidence(
        string dataRoot,
        params SkyrimRuntimeArchiveEvidenceEntry[] entries)
    {
        return RuntimeEvidence(
            dataRoot,
            complete:
                true,
            entries
        );
    }

    private static SkyrimRuntimeArchiveEvidenceResult RuntimeEvidence(
        string dataRoot,
        bool complete,
        params SkyrimRuntimeArchiveEvidenceEntry[] entries)
    {
        return new SkyrimRuntimeArchiveEvidenceResult(
            DataRoot:
                dataRoot,
            IniDirectory:
                "/fixture",
            Archives:
                entries,
            MissingIniArchives:
                Array.Empty<
                    SkyrimRuntimeArchiveMissingIniArchive
                >(),
            AssociationErrors:
                Array.Empty<
                    SkyrimRuntimeArchiveAssociationError
                >(),
            IniReadErrors:
                complete
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
    }

    private static SkyrimRuntimeArchiveEvidenceEntry RuntimeEntry(
        SkyrimArchiveAssetProvider provider,
        int loadOrderIndex)
    {
        return new SkyrimRuntimeArchiveEvidenceEntry(
            ArchiveName:
                provider.ArchiveName,
            ArchivePath:
                provider.ArchivePath,
            PluginAssociations:
                new[]
                {
                    new SkyrimRuntimeArchivePluginAssociation(
                        PluginName:
                            $"Plugin{loadOrderIndex}.esp",
                        LoadOrderIndex:
                            loadOrderIndex
                    )
                },
            IniListings:
                Array.Empty<
                    SkyrimRuntimeArchiveIniListing
                >()
        );
    }
}
