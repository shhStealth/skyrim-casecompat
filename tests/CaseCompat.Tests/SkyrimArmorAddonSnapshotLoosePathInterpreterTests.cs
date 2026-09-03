using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Analysis;

namespace CaseCompat.Tests;

public sealed class SkyrimArmorAddonSnapshotLoosePathInterpreterTests
{
    [Theory]
    [InlineData(
        SkyrimArmorAddonSnapshotLookupEvidenceState.InvalidRequestedPath)]
    [InlineData(
        SkyrimArmorAddonSnapshotLookupEvidenceState.NoMatchingNamespaceAnalysis)]
    [InlineData(
        SkyrimArmorAddonSnapshotLookupEvidenceState.AmbiguousMatchingNamespaceAnalysis)]
    public void Interpret_CompositionFailureStates_AreIndeterminate(
        SkyrimArmorAddonSnapshotLookupEvidenceState state)
    {
        SkyrimWinningArmorAddonSnapshotPathEvidence evidence =
            CompositionFailureEvidence(
                state
            );

        SkyrimArmorAddonSnapshotLoosePathInterpretation result =
            SkyrimArmorAddonSnapshotLoosePathInterpreter.Interpret(
                evidence
            );

        Assert.Same(
            evidence,
            result.Evidence
        );

        Assert.True(
            result.EvidenceStructureValid
        );

        Assert.Equal(
            SkyrimArmorAddonSnapshotLoosePathInterpretationState
                .IndeterminateEvidence,
            result.State
        );

        Assert.False(
            result.Definitive
        );

        Assert.False(
            result.LooseResolves
        );

        Assert.Null(
            result.SnapshotState
        );

        Assert.Null(
            result.InterpretationError
        );
    }

    [Theory]
    [InlineData(
        WindowsNamespaceSnapshotFileLookupState.Resolved,
        SkyrimArmorAddonSnapshotLoosePathInterpretationState.LooseResolved)]
    [InlineData(
        WindowsNamespaceSnapshotFileLookupState.Missing,
        SkyrimArmorAddonSnapshotLoosePathInterpretationState.LooseUnresolved)]
    [InlineData(
        WindowsNamespaceSnapshotFileLookupState.CasefoldUnknown,
        SkyrimArmorAddonSnapshotLoosePathInterpretationState.IndeterminateEvidence)]
    [InlineData(
        WindowsNamespaceSnapshotFileLookupState.CasefoldEquivalenceUnknown,
        SkyrimArmorAddonSnapshotLoosePathInterpretationState.IndeterminateEvidence)]
    [InlineData(
        WindowsNamespaceSnapshotFileLookupState.AmbiguousEquivalent,
        SkyrimArmorAddonSnapshotLoosePathInterpretationState.IndeterminateEvidence)]
    [InlineData(
        WindowsNamespaceSnapshotFileLookupState.NotDirectory,
        SkyrimArmorAddonSnapshotLoosePathInterpretationState.LooseUnresolved)]
    [InlineData(
        WindowsNamespaceSnapshotFileLookupState.NotFile,
        SkyrimArmorAddonSnapshotLoosePathInterpretationState.LooseUnresolved)]
    [InlineData(
        WindowsNamespaceSnapshotFileLookupState.UnsupportedObject,
        SkyrimArmorAddonSnapshotLoosePathInterpretationState.IndeterminateEvidence)]
    [InlineData(
        WindowsNamespaceSnapshotFileLookupState.IncompleteAnalysis,
        SkyrimArmorAddonSnapshotLoosePathInterpretationState.IndeterminateEvidence)]
    [InlineData(
        WindowsNamespaceSnapshotFileLookupState.InvalidRequestedPath,
        SkyrimArmorAddonSnapshotLoosePathInterpretationState.IndeterminateEvidence)]
    [InlineData(
        WindowsNamespaceSnapshotFileLookupState.RequestOutsideAnalyzedNamespace,
        SkyrimArmorAddonSnapshotLoosePathInterpretationState.IndeterminateEvidence)]
    [InlineData(
        WindowsNamespaceSnapshotFileLookupState.InvalidSnapshotEvidence,
        SkyrimArmorAddonSnapshotLoosePathInterpretationState.IndeterminateEvidence)]
    public void Interpret_AllCheckpoint10AStates_MapExplicitly(
        WindowsNamespaceSnapshotFileLookupState lookupState,
        SkyrimArmorAddonSnapshotLoosePathInterpretationState expected)
    {
        SkyrimWinningArmorAddonSnapshotPathEvidence evidence =
            LookupProducedEvidence(
                lookupState
            );

        SkyrimArmorAddonSnapshotLoosePathInterpretation result =
            SkyrimArmorAddonSnapshotLoosePathInterpreter.Interpret(
                evidence
            );

        Assert.Same(
            evidence,
            result.Evidence
        );

        Assert.True(
            result.EvidenceStructureValid
        );

        Assert.Equal(
            expected,
            result.State
        );

        Assert.Equal(
            lookupState,
            result.SnapshotState
        );

        Assert.Equal(
            expected !=
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .IndeterminateEvidence,
            result.Definitive
        );

        Assert.Equal(
            expected ==
                SkyrimArmorAddonSnapshotLoosePathInterpretationState
                    .LooseResolved,
            result.LooseResolves
        );

        Assert.Null(
            result.InterpretationError
        );
    }

    [Fact]
    public void Interpret_LookupProducedWithoutLookup_FailsClosed()
    {
        WindowsNamespaceAnalysis analysis =
            Analysis();

        SkyrimWinningArmorAddonSnapshotPathEvidence evidence =
            new(
                RequestedPath:
                    "Meshes/Sword.nif",
                References:
                    References(
                        "Meshes/Sword.nif"
                    ),
                RequestedRootLogicalPath:
                    analysis.RootLogicalPath,
                State:
                    SkyrimArmorAddonSnapshotLookupEvidenceState
                        .LookupProduced,
                MatchingAnalysisCount:
                    1,
                SelectedAnalysis:
                    analysis,
                Lookup:
                    null,
                Error:
                    null
            );

        AssertMalformed(
            evidence
        );
    }

    [Fact]
    public void Interpret_LookupAnalysisMismatch_FailsClosed()
    {
        WindowsNamespaceAnalysis selected =
            Analysis();

        WindowsNamespaceAnalysis different =
            Analysis();

        WindowsNamespaceSnapshotFileLookup lookup =
            Lookup(
                different,
                WindowsNamespaceSnapshotFileLookupState.Resolved,
                "Meshes/Sword.nif"
            );

        SkyrimWinningArmorAddonSnapshotPathEvidence evidence =
            Evidence(
                "Meshes/Sword.nif",
                selected,
                lookup
            );

        AssertMalformed(
            evidence
        );
    }

    [Fact]
    public void Interpret_LookupRequestedSpellingMismatch_FailsClosed()
    {
        WindowsNamespaceAnalysis analysis =
            Analysis();

        WindowsNamespaceSnapshotFileLookup lookup =
            Lookup(
                analysis,
                WindowsNamespaceSnapshotFileLookupState.Resolved,
                "Meshes/Other.nif"
            );

        SkyrimWinningArmorAddonSnapshotPathEvidence evidence =
            Evidence(
                "Meshes/Sword.nif",
                analysis,
                lookup
            );

        AssertMalformed(
            evidence
        );
    }

    [Fact]
    public void Interpret_GroupReferenceRequestedSpellingMismatch_FailsClosed()
    {
        SkyrimWinningArmorAddonSnapshotPathEvidence evidence =
            LookupProducedEvidence(
                WindowsNamespaceSnapshotFileLookupState.Resolved
            );

        SkyrimWinningArmorAddonSnapshotPathEvidence malformed =
            evidence with
            {
                References =
                    References(
                        "Meshes/Other.nif"
                    )
            };

        AssertMalformed(
            malformed
        );
    }

    [Fact]
    public void Interpret_NonResolvedLookupWithParticipant_FailsClosed()
    {
        WindowsNamespaceAnalysis analysis =
            Analysis();

        WindowsNamespaceSnapshotFileLookup lookup =
            new(
                Analysis:
                    analysis,
                RequestedRelativePath:
                    "Meshes/Sword.nif",
                RequestedLogicalPath:
                    WindowsLogicalPath.FromRelativePath(
                        "Meshes/Sword.nif"
                    ),
                State:
                    WindowsNamespaceSnapshotFileLookupState.Missing,
                ResolvedParticipant:
                    Participant(
                        "Meshes/Sword.nif"
                    ),
                FailedComponentIndex:
                    1,
                Steps:
                    Array.Empty<
                        WindowsNamespaceSnapshotFileLookupStep
                    >(),
                Error:
                    "fixture"
            );

        SkyrimWinningArmorAddonSnapshotPathEvidence evidence =
            Evidence(
                "Meshes/Sword.nif",
                analysis,
                lookup
            );

        AssertMalformed(
            evidence
        );
    }

    private static void AssertMalformed(
        SkyrimWinningArmorAddonSnapshotPathEvidence evidence)
    {
        SkyrimArmorAddonSnapshotLoosePathInterpretation result =
            SkyrimArmorAddonSnapshotLoosePathInterpreter.Interpret(
                evidence
            );

        Assert.Same(
            evidence,
            result.Evidence
        );

        Assert.False(
            result.EvidenceStructureValid
        );

        Assert.Equal(
            SkyrimArmorAddonSnapshotLoosePathInterpretationState
                .IndeterminateEvidence,
            result.State
        );

        Assert.False(
            result.Definitive
        );

        Assert.False(
            result.LooseResolves
        );

        Assert.False(
            string.IsNullOrWhiteSpace(
                result.InterpretationError
            )
        );
    }

    private static SkyrimWinningArmorAddonSnapshotPathEvidence
        CompositionFailureEvidence(
            SkyrimArmorAddonSnapshotLookupEvidenceState state)
    {
        string requestedPath =
            state ==
            SkyrimArmorAddonSnapshotLookupEvidenceState
                .InvalidRequestedPath
                ? "Meshes//Sword.nif"
                : "Meshes/Sword.nif";

        WindowsLogicalPath? root =
            state ==
            SkyrimArmorAddonSnapshotLookupEvidenceState
                .InvalidRequestedPath
                ? null
                : WindowsLogicalPath.FromRelativePath(
                    "Meshes"
                );

        int matchingCount =
            state switch
            {
                SkyrimArmorAddonSnapshotLookupEvidenceState
                    .AmbiguousMatchingNamespaceAnalysis =>
                        2,

                _ =>
                    0
            };

        return new SkyrimWinningArmorAddonSnapshotPathEvidence(
            RequestedPath:
                requestedPath,
            References:
                References(
                    requestedPath
                ),
            RequestedRootLogicalPath:
                root,
            State:
                state,
            MatchingAnalysisCount:
                matchingCount,
            SelectedAnalysis:
                null,
            Lookup:
                null,
            Error:
                "fixture"
        );
    }

    private static SkyrimWinningArmorAddonSnapshotPathEvidence
        LookupProducedEvidence(
            WindowsNamespaceSnapshotFileLookupState state)
    {
        WindowsNamespaceAnalysis analysis =
            Analysis();

        WindowsNamespaceSnapshotFileLookup lookup =
            Lookup(
                analysis,
                state,
                "Meshes/Sword.nif"
            );

        return Evidence(
            "Meshes/Sword.nif",
            analysis,
            lookup
        );
    }

    private static SkyrimWinningArmorAddonSnapshotPathEvidence Evidence(
        string requestedPath,
        WindowsNamespaceAnalysis selectedAnalysis,
        WindowsNamespaceSnapshotFileLookup lookup)
    {
        return new SkyrimWinningArmorAddonSnapshotPathEvidence(
            RequestedPath:
                requestedPath,
            References:
                References(
                    requestedPath
                ),
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
                selectedAnalysis,
            Lookup:
                lookup,
            Error:
                null
        );
    }

    private static IReadOnlyList<
        SkyrimWinningArmorAddonSnapshotReferenceContext
    > References(
        string requestedPath)
    {
        SkyrimArmorAddonModelReference reference =
            new(
                FormKey:
                    "ArmorA",
                EditorId:
                    "ArmorA",
                Field:
                    "WorldModel.Male",
                GivenPath:
                    requestedPath,
                DataRelativePath:
                    requestedPath
            );

        return new[]
        {
            new SkyrimWinningArmorAddonSnapshotReferenceContext(
                WinningPluginName:
                    "Winner.esp",
                WinningLoadOrderIndex:
                    10,
                Reference:
                    reference
            )
        };
    }

    private static WindowsNamespaceSnapshotFileLookup Lookup(
        WindowsNamespaceAnalysis analysis,
        WindowsNamespaceSnapshotFileLookupState state,
        string requestedPath)
    {
        WindowsNamespacePhysicalParticipant? resolved =
            state ==
            WindowsNamespaceSnapshotFileLookupState.Resolved
                ? Participant(
                    requestedPath
                )
                : null;

        return new WindowsNamespaceSnapshotFileLookup(
            Analysis:
                analysis,
            RequestedRelativePath:
                requestedPath,
            RequestedLogicalPath:
                WindowsLogicalPath.FromRelativePath(
                    requestedPath
                ),
            State:
                state,
            ResolvedParticipant:
                resolved,
            FailedComponentIndex:
                state ==
                WindowsNamespaceSnapshotFileLookupState.Resolved
                    ? null
                    : 1,
            Steps:
                Array.Empty<
                    WindowsNamespaceSnapshotFileLookupStep
                >(),
            Error:
                state ==
                WindowsNamespaceSnapshotFileLookupState.Resolved
                    ? null
                    : "fixture"
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
