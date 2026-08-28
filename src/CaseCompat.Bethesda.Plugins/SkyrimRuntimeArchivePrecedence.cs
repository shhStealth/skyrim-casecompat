namespace CaseCompat.Bethesda.Plugins;

public enum SkyrimRuntimeArchivePrecedenceState
{
    NoRuntimeEvidencedProvider,
    SingleRuntimeEvidencedProvider,
    ResolvedPluginOverIni,
    ResolvedByPluginLoadOrder,
    ResolvedByIniListingOrder,
    AmbiguousDuplicateLogicalEntryWithinArchive,
    AmbiguousDualSourceArchive,
    AmbiguousPluginAssociationMultiplicity,
    AmbiguousSamePluginLoadOrderIndex,
    AmbiguousIniListingMultiplicity,
    AmbiguousDifferentIniFiles,
    AmbiguousSameIniListingIndex,
    AmbiguousRuntimeEvidence
}

public sealed record SkyrimRuntimeArchivePrecedenceDecision(
    SkyrimRuntimeArchivePrecedenceState State,
    IReadOnlyList<SkyrimArchiveAssetProvider> RuntimeEvidencedProviders,
    SkyrimArchiveAssetProvider? WinningProvider
)
{
    public bool HasWinner =>
        WinningProvider is not null;

    public bool IsAmbiguous =>
        State is
            SkyrimRuntimeArchivePrecedenceState
                .AmbiguousDuplicateLogicalEntryWithinArchive
            or SkyrimRuntimeArchivePrecedenceState
                .AmbiguousDualSourceArchive
            or SkyrimRuntimeArchivePrecedenceState
                .AmbiguousPluginAssociationMultiplicity
            or SkyrimRuntimeArchivePrecedenceState
                .AmbiguousSamePluginLoadOrderIndex
            or SkyrimRuntimeArchivePrecedenceState
                .AmbiguousIniListingMultiplicity
            or SkyrimRuntimeArchivePrecedenceState
                .AmbiguousDifferentIniFiles
            or SkyrimRuntimeArchivePrecedenceState
                .AmbiguousSameIniListingIndex
            or SkyrimRuntimeArchivePrecedenceState
                .AmbiguousRuntimeEvidence;
}

public sealed class SkyrimRuntimeArchivePrecedenceResolver
{
    private readonly IReadOnlyDictionary<
        string,
        SkyrimRuntimeArchiveEvidenceEntry
    > _evidenceByArchivePath;

    public SkyrimRuntimeArchivePrecedenceResolver(
        SkyrimRuntimeArchiveEvidenceResult runtimeArchiveEvidence)
    {
        ArgumentNullException.ThrowIfNull(
            runtimeArchiveEvidence
        );

        _evidenceByArchivePath =
            runtimeArchiveEvidence.Archives.ToDictionary(
                archive =>
                    archive.ArchivePath,
                archive =>
                    archive,
                StringComparer.Ordinal
            );
    }

    public SkyrimRuntimeArchivePrecedenceDecision Resolve(
        IReadOnlyList<SkyrimArchiveAssetProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(
            providers
        );

        SkyrimArchiveAssetProvider[] runtimeProviders =
            providers
                .Where(provider =>
                    _evidenceByArchivePath.TryGetValue(
                        provider.ArchivePath,
                        out SkyrimRuntimeArchiveEvidenceEntry?
                            evidence
                    ) &&
                    evidence.HasRuntimeEvidence
                )
                .ToArray();

        if (runtimeProviders.Length == 0)
        {
            return new SkyrimRuntimeArchivePrecedenceDecision(
                State:
                    SkyrimRuntimeArchivePrecedenceState
                        .NoRuntimeEvidencedProvider,
                RuntimeEvidencedProviders:
                    runtimeProviders,
                WinningProvider:
                    null
            );
        }

        bool hasDuplicateLogicalEntry =
            runtimeProviders
                .GroupBy(
                    provider =>
                        provider.ArchivePath,
                    StringComparer.Ordinal
                )
                .Any(group =>
                    group.Skip(1).Any()
                );

        if (hasDuplicateLogicalEntry)
        {
            return Ambiguous(
                SkyrimRuntimeArchivePrecedenceState
                    .AmbiguousDuplicateLogicalEntryWithinArchive,
                runtimeProviders
            );
        }

        if (runtimeProviders.Length == 1)
        {
            return new SkyrimRuntimeArchivePrecedenceDecision(
                State:
                    SkyrimRuntimeArchivePrecedenceState
                        .SingleRuntimeEvidencedProvider,
                RuntimeEvidencedProviders:
                    runtimeProviders,
                WinningProvider:
                    runtimeProviders[0]
            );
        }

        var candidates =
            runtimeProviders
                .Select(provider =>
                    new Candidate(
                        Provider:
                            provider,
                        Evidence:
                            _evidenceByArchivePath[
                                provider.ArchivePath
                            ]
                    )
                )
                .ToArray();

        if (candidates.Any(candidate =>
                candidate.Evidence.HasPluginAssociation &&
                candidate.Evidence.IsIniListed))
        {
            return Ambiguous(
                SkyrimRuntimeArchivePrecedenceState
                    .AmbiguousDualSourceArchive,
                runtimeProviders
            );
        }

        Candidate[] pluginCandidates =
            candidates
                .Where(candidate =>
                    candidate.Evidence.HasPluginAssociation
                )
                .ToArray();

        Candidate[] iniCandidates =
            candidates
                .Where(candidate =>
                    candidate.Evidence.IsIniListed
                )
                .ToArray();

        if (pluginCandidates.Length > 0)
        {
            if (pluginCandidates.Any(candidate =>
                    candidate.Evidence
                        .PluginAssociations
                        .Count != 1))
            {
                return Ambiguous(
                    SkyrimRuntimeArchivePrecedenceState
                        .AmbiguousPluginAssociationMultiplicity,
                    runtimeProviders
                );
            }

            int highestLoadOrderIndex =
                pluginCandidates.Max(candidate =>
                    candidate
                        .Evidence
                        .PluginAssociations[0]
                        .LoadOrderIndex
                );

            Candidate[] highestPluginCandidates =
                pluginCandidates
                    .Where(candidate =>
                        candidate
                            .Evidence
                            .PluginAssociations[0]
                            .LoadOrderIndex ==
                        highestLoadOrderIndex
                    )
                    .ToArray();

            if (highestPluginCandidates.Length != 1)
            {
                return Ambiguous(
                    SkyrimRuntimeArchivePrecedenceState
                        .AmbiguousSamePluginLoadOrderIndex,
                    runtimeProviders
                );
            }

            SkyrimRuntimeArchivePrecedenceState state =
                pluginCandidates.Length == 1 &&
                iniCandidates.Length > 0
                    ? SkyrimRuntimeArchivePrecedenceState
                        .ResolvedPluginOverIni
                    : SkyrimRuntimeArchivePrecedenceState
                        .ResolvedByPluginLoadOrder;

            return new SkyrimRuntimeArchivePrecedenceDecision(
                State:
                    state,
                RuntimeEvidencedProviders:
                    runtimeProviders,
                WinningProvider:
                    highestPluginCandidates[0].Provider
            );
        }

        if (iniCandidates.Length != candidates.Length)
        {
            return Ambiguous(
                SkyrimRuntimeArchivePrecedenceState
                    .AmbiguousRuntimeEvidence,
                runtimeProviders
            );
        }

        if (iniCandidates.Any(candidate =>
                candidate.Evidence.IniListings.Count != 1))
        {
            return Ambiguous(
                SkyrimRuntimeArchivePrecedenceState
                    .AmbiguousIniListingMultiplicity,
                runtimeProviders
            );
        }

        string[] iniPaths =
            iniCandidates
                .Select(candidate =>
                    candidate.Evidence.IniListings[0].IniPath
                )
                .Distinct(
                    StringComparer.Ordinal
                )
                .ToArray();

        if (iniPaths.Length != 1)
        {
            return Ambiguous(
                SkyrimRuntimeArchivePrecedenceState
                    .AmbiguousDifferentIniFiles,
                runtimeProviders
            );
        }

        int highestListingIndex =
            iniCandidates.Max(candidate =>
                candidate
                    .Evidence
                    .IniListings[0]
                    .ListingIndex
            );

        Candidate[] highestIniCandidates =
            iniCandidates
                .Where(candidate =>
                    candidate
                        .Evidence
                        .IniListings[0]
                        .ListingIndex ==
                    highestListingIndex
                )
                .ToArray();

        if (highestIniCandidates.Length != 1)
        {
            return Ambiguous(
                SkyrimRuntimeArchivePrecedenceState
                    .AmbiguousSameIniListingIndex,
                runtimeProviders
            );
        }

        return new SkyrimRuntimeArchivePrecedenceDecision(
            State:
                SkyrimRuntimeArchivePrecedenceState
                    .ResolvedByIniListingOrder,
            RuntimeEvidencedProviders:
                runtimeProviders,
            WinningProvider:
                highestIniCandidates[0].Provider
        );
    }

    private static SkyrimRuntimeArchivePrecedenceDecision
        Ambiguous(
            SkyrimRuntimeArchivePrecedenceState state,
            IReadOnlyList<SkyrimArchiveAssetProvider>
                runtimeProviders)
    {
        return new SkyrimRuntimeArchivePrecedenceDecision(
            State:
                state,
            RuntimeEvidencedProviders:
                runtimeProviders,
            WinningProvider:
                null
        );
    }

    private sealed record Candidate(
        SkyrimArchiveAssetProvider Provider,
        SkyrimRuntimeArchiveEvidenceEntry Evidence
    );
}
