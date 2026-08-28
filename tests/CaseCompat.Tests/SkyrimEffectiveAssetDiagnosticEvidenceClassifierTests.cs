using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Findings;
using CaseCompat.Core.Resolution;
using Xunit;

namespace CaseCompat.Tests;

public sealed class SkyrimEffectiveAssetDiagnosticEvidenceClassifierTests
{
    private const string RequestedPath =
        "Meshes/Test/Fixture.nif";

    [Fact]
    public void IncompleteWinnerSearch_TakesPriority()
    {
        SkyrimEffectiveArmorAddonArchiveCandidateFinding finding =
            CreateFinding(
                winnerSearchComplete:
                    false
            );

        Assert.Equal(
            SkyrimEffectiveAssetDiagnosticEvidenceState
                .IncompleteWinnerSearch,
            Classify(
                finding
            )
        );
    }

    [Fact]
    public void LooseResolvable_DoesNotDependOnArchiveCompleteness()
    {
        SkyrimEffectiveArmorAddonArchiveCandidateFinding finding =
            CreateFinding(
                linuxResolves:
                    true
            );

        Assert.Equal(
            SkyrimEffectiveAssetDiagnosticEvidenceState
                .LooseResolvable,
            SkyrimEffectiveAssetDiagnosticEvidenceClassifier.Classify(
                finding,
                archiveCandidateIndexComplete:
                    false,
                runtimeArchiveEvidenceComplete:
                    false
            )
        );
    }

    [Fact]
    public void UnresolvedWithIncompleteArchiveIndex_IsIncomplete()
    {
        SkyrimEffectiveArmorAddonArchiveCandidateFinding finding =
            CreateFinding(
                archivePrecedence:
                    WinnerDecision()
            );

        Assert.Equal(
            SkyrimEffectiveAssetDiagnosticEvidenceState
                .IncompleteArchiveCandidateIndex,
            SkyrimEffectiveAssetDiagnosticEvidenceClassifier.Classify(
                finding,
                archiveCandidateIndexComplete:
                    false,
                runtimeArchiveEvidenceComplete:
                    true
            )
        );
    }

    [Fact]
    public void UnresolvedWithIncompleteRuntimeEvidence_IsIncomplete()
    {
        SkyrimEffectiveArmorAddonArchiveCandidateFinding finding =
            CreateFinding(
                archivePrecedence:
                    WinnerDecision()
            );

        Assert.Equal(
            SkyrimEffectiveAssetDiagnosticEvidenceState
                .IncompleteRuntimeArchiveEvidence,
            SkyrimEffectiveAssetDiagnosticEvidenceClassifier.Classify(
                finding,
                archiveCandidateIndexComplete:
                    true,
                runtimeArchiveEvidenceComplete:
                    false
            )
        );
    }

    [Fact]
    public void ArchiveWinner_TakesPriorityOverIncompleteLooseCandidateSearch()
    {
        SkyrimEffectiveArmorAddonArchiveCandidateFinding finding =
            CreateFinding(
                candidateSearchComplete:
                    false,
                archivePrecedence:
                    WinnerDecision()
            );

        Assert.Equal(
            EffectiveAssetReferenceEvidenceState
                .IncompleteCandidateSearch,
            finding.LooseEvidenceState
        );

        Assert.Equal(
            SkyrimEffectiveAssetDiagnosticEvidenceState
                .LooseUnresolvedWithRuntimeArchiveWinner,
            Classify(
                finding
            )
        );
    }

    [Fact]
    public void UnresolvedWithAmbiguousArchivePrecedence_IsClassified()
    {
        SkyrimEffectiveArmorAddonArchiveCandidateFinding finding =
            CreateFinding(
                archivePrecedence:
                    AmbiguousDecision()
            );

        Assert.Equal(
            SkyrimEffectiveAssetDiagnosticEvidenceState
                .LooseUnresolvedWithAmbiguousArchivePrecedence,
            Classify(
                finding
            )
        );
    }

    [Fact]
    public void NoProviderWithIncompleteLooseCandidateSearch_IsIncomplete()
    {
        SkyrimEffectiveArmorAddonArchiveCandidateFinding finding =
            CreateFinding(
                candidateSearchComplete:
                    false
            );

        Assert.Equal(
            SkyrimEffectiveAssetDiagnosticEvidenceState
                .IncompleteLooseCandidateSearch,
            Classify(
                finding
            )
        );
    }

    [Fact]
    public void NoProviderWithNoEquivalent_IsClassified()
    {
        SkyrimEffectiveArmorAddonArchiveCandidateFinding finding =
            CreateFinding(
                equivalentCandidateCount:
                    0
            );

        Assert.Equal(
            SkyrimEffectiveAssetDiagnosticEvidenceState
                .LooseUnresolvedNoProviderNoEquivalent,
            Classify(
                finding
            )
        );
    }

    [Fact]
    public void NoProviderWithUniqueEquivalent_IsClassified()
    {
        SkyrimEffectiveArmorAddonArchiveCandidateFinding finding =
            CreateFinding(
                equivalentCandidateCount:
                    1
            );

        Assert.Equal(
            SkyrimEffectiveAssetDiagnosticEvidenceState
                .LooseUnresolvedNoProviderUniqueEquivalent,
            Classify(
                finding
            )
        );
    }

    [Fact]
    public void NoProviderWithMultipleEquivalents_IsClassified()
    {
        SkyrimEffectiveArmorAddonArchiveCandidateFinding finding =
            CreateFinding(
                equivalentCandidateCount:
                    2
            );

        Assert.Equal(
            SkyrimEffectiveAssetDiagnosticEvidenceState
                .LooseUnresolvedNoProviderAmbiguousEquivalent,
            Classify(
                finding
            )
        );
    }

    private static SkyrimEffectiveAssetDiagnosticEvidenceState
        Classify(
            SkyrimEffectiveArmorAddonArchiveCandidateFinding finding)
    {
        return SkyrimEffectiveAssetDiagnosticEvidenceClassifier.Classify(
            finding,
            archiveCandidateIndexComplete:
                true,
            runtimeArchiveEvidenceComplete:
                true
        );
    }

    private static SkyrimEffectiveArmorAddonArchiveCandidateFinding
        CreateFinding(
            bool winnerSearchComplete = true,
            bool linuxResolves = false,
            bool candidateSearchComplete = true,
            int equivalentCandidateCount = 0,
            SkyrimRuntimeArchivePrecedenceDecision?
                archivePrecedence = null)
    {
        string dataRoot =
            Path.GetFullPath(
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-tests",
                    "diagnostic-evidence"
                )
            );

        string[] equivalentCandidates =
            Enumerable
                .Range(
                    0,
                    equivalentCandidateCount
                )
                .Select(index =>
                    Path.Combine(
                        dataRoot,
                        "Meshes",
                        "Test",
                        $"Equivalent-{index}.nif"
                    )
                )
                .ToArray();

        DataRelativePathResolution resolution =
            new(
                DataRoot:
                    dataRoot,
                RequestedPath:
                    RequestedPath,
                LinuxResolves:
                    linuxResolves,
                ResolvedPhysicalPath:
                    linuxResolves
                        ? Path.Combine(
                            dataRoot,
                            "Meshes",
                            "Test",
                            "Fixture.nif"
                        )
                        : null,
                FailedComponentIndex:
                    linuxResolves
                        ? null
                        : 1,
                FailureReason:
                    linuxResolves
                        ? null
                        : "fixture unresolved path",
                Steps:
                    Array.Empty<
                        PathResolutionStep
                    >(),
                EquivalentPhysicalCandidates:
                    equivalentCandidates,
                CandidateSearchErrors:
                    candidateSearchComplete
                        ? Array.Empty<string>()
                        : new[]
                        {
                            "fixture candidate search error"
                        }
            );

        EffectiveAssetReferenceFinding effectiveFinding =
            new(
                ConsumerKind:
                    "ArmorAddon",
                ConsumerFormKey:
                    "000001:Fixture.esp",
                ConsumerEditorId:
                    "FixtureAA",
                WinningPluginName:
                    "Fixture.esp",
                WinningLoadOrderIndex:
                    100,
                WinnerSearchComplete:
                    winnerSearchComplete,
                ReferenceField:
                    "WorldModel.Female",
                RawPath:
                    @"Test\Fixture.nif",
                RequestedPath:
                    RequestedPath,
                Resolution:
                    resolution
            );

        SkyrimRuntimeArchivePrecedenceDecision decision =
            archivePrecedence ??
            NoProviderDecision();

        return new SkyrimEffectiveArmorAddonArchiveCandidateFinding(
            EffectiveFinding:
                effectiveFinding,
            ArchiveCandidates:
                decision.RuntimeEvidencedProviders,
            RuntimeEvidencedArchiveCandidates:
                decision.RuntimeEvidencedProviders,
            ArchivePrecedence:
                decision
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

    private static SkyrimRuntimeArchivePrecedenceDecision
        WinnerDecision()
    {
        SkyrimArchiveAssetProvider provider =
            CreateProvider(
                "Winner.bsa"
            );

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
            CreateProvider(
                "First.bsa"
            );

        SkyrimArchiveAssetProvider second =
            CreateProvider(
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

    private static SkyrimArchiveAssetProvider
        CreateProvider(
            string archiveName)
    {
        return new SkyrimArchiveAssetProvider(
            ArchiveName:
                archiveName,
            ArchivePath:
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-tests",
                    archiveName
                ),
            InternalPath:
                "meshes/test/fixture.nif",
            Size:
                123
        );
    }
}
