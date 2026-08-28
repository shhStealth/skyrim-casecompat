using CaseCompat.Bethesda.Plugins;
using Xunit;

namespace CaseCompat.Tests;

public sealed class SkyrimEffectiveAssetProviderEvidenceClassifierTests
{
    [Fact]
    public void IncompleteWinnerSearch_TakesPriority()
    {
        SkyrimEffectiveAssetProviderEvidenceState state =
            SkyrimEffectiveAssetProviderEvidenceClassifier.Classify(
                winnerSearchComplete:
                    false,
                linuxResolves:
                    true,
                archiveCandidateIndexComplete:
                    false,
                runtimeArchiveEvidenceComplete:
                    false,
                archivePrecedence:
                    NoProviderDecision()
            );

        Assert.Equal(
            SkyrimEffectiveAssetProviderEvidenceState
                .IncompleteWinnerSearch,
            state
        );
    }

    [Fact]
    public void LooseResolvable_DoesNotDependOnArchiveCompleteness()
    {
        SkyrimEffectiveAssetProviderEvidenceState state =
            SkyrimEffectiveAssetProviderEvidenceClassifier.Classify(
                winnerSearchComplete:
                    true,
                linuxResolves:
                    true,
                archiveCandidateIndexComplete:
                    false,
                runtimeArchiveEvidenceComplete:
                    false,
                archivePrecedence:
                    NoProviderDecision()
            );

        Assert.Equal(
            SkyrimEffectiveAssetProviderEvidenceState
                .LooseResolvable,
            state
        );
    }

    [Fact]
    public void UnresolvedWithIncompleteArchiveIndex_IsIncomplete()
    {
        SkyrimEffectiveAssetProviderEvidenceState state =
            SkyrimEffectiveAssetProviderEvidenceClassifier.Classify(
                winnerSearchComplete:
                    true,
                linuxResolves:
                    false,
                archiveCandidateIndexComplete:
                    false,
                runtimeArchiveEvidenceComplete:
                    true,
                archivePrecedence:
                    WinnerDecision()
            );

        Assert.Equal(
            SkyrimEffectiveAssetProviderEvidenceState
                .IncompleteArchiveCandidateIndex,
            state
        );
    }

    [Fact]
    public void UnresolvedWithIncompleteRuntimeEvidence_IsIncomplete()
    {
        SkyrimEffectiveAssetProviderEvidenceState state =
            SkyrimEffectiveAssetProviderEvidenceClassifier.Classify(
                winnerSearchComplete:
                    true,
                linuxResolves:
                    false,
                archiveCandidateIndexComplete:
                    true,
                runtimeArchiveEvidenceComplete:
                    false,
                archivePrecedence:
                    WinnerDecision()
            );

        Assert.Equal(
            SkyrimEffectiveAssetProviderEvidenceState
                .IncompleteRuntimeArchiveEvidence,
            state
        );
    }

    [Fact]
    public void UnresolvedWithArchiveWinner_IsClassified()
    {
        SkyrimEffectiveAssetProviderEvidenceState state =
            SkyrimEffectiveAssetProviderEvidenceClassifier.Classify(
                winnerSearchComplete:
                    true,
                linuxResolves:
                    false,
                archiveCandidateIndexComplete:
                    true,
                runtimeArchiveEvidenceComplete:
                    true,
                archivePrecedence:
                    WinnerDecision()
            );

        Assert.Equal(
            SkyrimEffectiveAssetProviderEvidenceState
                .LooseUnresolvedWithRuntimeArchiveWinner,
            state
        );
    }

    [Fact]
    public void UnresolvedWithAmbiguousArchivePrecedence_IsClassified()
    {
        SkyrimEffectiveAssetProviderEvidenceState state =
            SkyrimEffectiveAssetProviderEvidenceClassifier.Classify(
                winnerSearchComplete:
                    true,
                linuxResolves:
                    false,
                archiveCandidateIndexComplete:
                    true,
                runtimeArchiveEvidenceComplete:
                    true,
                archivePrecedence:
                    AmbiguousDecision()
            );

        Assert.Equal(
            SkyrimEffectiveAssetProviderEvidenceState
                .LooseUnresolvedWithAmbiguousArchivePrecedence,
            state
        );
    }

    [Fact]
    public void UnresolvedWithoutRuntimeArchiveProvider_IsClassified()
    {
        SkyrimEffectiveAssetProviderEvidenceState state =
            SkyrimEffectiveAssetProviderEvidenceClassifier.Classify(
                winnerSearchComplete:
                    true,
                linuxResolves:
                    false,
                archiveCandidateIndexComplete:
                    true,
                runtimeArchiveEvidenceComplete:
                    true,
                archivePrecedence:
                    NoProviderDecision()
            );

        Assert.Equal(
            SkyrimEffectiveAssetProviderEvidenceState
                .LooseUnresolvedWithoutRuntimeArchiveProvider,
            state
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
                Array.Empty<SkyrimArchiveAssetProvider>(),
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
                    "/fixture",
                    archiveName
                ),
            InternalPath:
                "meshes/test/fixture.nif",
            Size:
                123
        );
    }
}
