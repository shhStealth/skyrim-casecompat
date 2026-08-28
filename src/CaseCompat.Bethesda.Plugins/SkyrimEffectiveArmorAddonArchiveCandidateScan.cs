using CaseCompat.Core.Findings;

namespace CaseCompat.Bethesda.Plugins;

public sealed record SkyrimEffectiveArmorAddonArchiveCandidateFinding(
    EffectiveAssetReferenceFinding EffectiveFinding,
    IReadOnlyList<SkyrimArchiveAssetProvider> ArchiveCandidates,
    IReadOnlyList<SkyrimArchiveAssetProvider>
        RuntimeEvidencedArchiveCandidates,
    SkyrimRuntimeArchivePrecedenceDecision ArchivePrecedence
)
{
    public bool HasArchiveCandidates =>
        ArchiveCandidates.Count > 0;

    public int ArchiveCandidateCount =>
        ArchiveCandidates.Count;

    public bool HasRuntimeEvidencedArchiveCandidates =>
        RuntimeEvidencedArchiveCandidates.Count > 0;

    public int RuntimeEvidencedArchiveCandidateCount =>
        RuntimeEvidencedArchiveCandidates.Count;

    public bool HasWinningRuntimeArchiveProvider =>
        ArchivePrecedence.HasWinner;

    public SkyrimArchiveAssetProvider?
        WinningRuntimeArchiveProvider =>
            ArchivePrecedence.WinningProvider;

    public bool HasAmbiguousArchivePrecedence =>
        ArchivePrecedence.IsAmbiguous;

    public EffectiveAssetReferenceEvidenceState LooseEvidenceState =>
        EffectiveAssetReferenceEvidenceClassifier.Classify(
            EffectiveFinding
        );
}

public sealed record SkyrimEffectiveArmorAddonArchiveCandidateScanResult(
    SkyrimWinningArmorAddonEffectiveScanResult EffectiveScan,
    SkyrimArchiveCandidateIndexResult ArchiveIndex,
    SkyrimRuntimeArchiveEvidenceResult RuntimeArchiveEvidence,
    IReadOnlyList<SkyrimEffectiveArmorAddonArchiveCandidateFinding> Findings
)
{
    public bool Complete =>
        EffectiveScan.Complete &&
        ArchiveIndex.SearchComplete &&
        RuntimeArchiveEvidence.SearchComplete;

    public int FindingsWithArchiveCandidates =>
        Findings.Count(finding =>
            finding.HasArchiveCandidates
        );

    public int FindingsWithoutArchiveCandidates =>
        Findings.Count -
        FindingsWithArchiveCandidates;

    public int UniqueRequestedPathsWithArchiveCandidates =>
        Findings
            .Where(finding =>
                finding.HasArchiveCandidates
            )
            .Select(finding =>
                finding.EffectiveFinding.RequestedPath
            )
            .Distinct(
                StringComparer.Ordinal
            )
            .Count();

    public int UniqueRequestedPathsWithoutArchiveCandidates =>
        EffectiveScan.UniqueRequestedPathCount -
        UniqueRequestedPathsWithArchiveCandidates;

    public int FindingsWithRuntimeEvidencedArchiveCandidates =>
        Findings.Count(finding =>
            finding.HasRuntimeEvidencedArchiveCandidates
        );

    public int FindingsWithoutRuntimeEvidencedArchiveCandidates =>
        Findings.Count -
        FindingsWithRuntimeEvidencedArchiveCandidates;

    public int UniqueRequestedPathsWithRuntimeEvidencedArchiveCandidates =>
        Findings
            .Where(finding =>
                finding.HasRuntimeEvidencedArchiveCandidates
            )
            .Select(finding =>
                finding.EffectiveFinding.RequestedPath
            )
            .Distinct(
                StringComparer.Ordinal
            )
            .Count();

    public int UniqueRequestedPathsWithoutRuntimeEvidencedArchiveCandidates =>
        EffectiveScan.UniqueRequestedPathCount -
        UniqueRequestedPathsWithRuntimeEvidencedArchiveCandidates;
}

public static class SkyrimEffectiveArmorAddonArchiveCandidateScan
{
    public static SkyrimEffectiveArmorAddonArchiveCandidateScanResult
        Inspect(
            SkyrimWinningArmorAddonEffectiveScanResult effectiveScan,
            SkyrimArchiveCandidateIndexResult archiveIndex,
            SkyrimRuntimeArchiveEvidenceResult runtimeArchiveEvidence)
    {
        ArgumentNullException.ThrowIfNull(
            effectiveScan
        );

        ArgumentNullException.ThrowIfNull(
            archiveIndex
        );

        ArgumentNullException.ThrowIfNull(
            runtimeArchiveEvidence
        );

        string effectiveDataRoot =
            Path.GetFullPath(
                effectiveScan.Inventory.DataRoot
            );

        string archiveDataRoot =
            Path.GetFullPath(
                archiveIndex.DataRoot
            );

        string runtimeArchiveDataRoot =
            Path.GetFullPath(
                runtimeArchiveEvidence.DataRoot
            );

        if (!string.Equals(
                effectiveDataRoot,
                archiveDataRoot,
                StringComparison.Ordinal) ||
            !string.Equals(
                effectiveDataRoot,
                runtimeArchiveDataRoot,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Effective scan, archive index, and runtime archive " +
                "evidence must refer to the same Data root."
            );
        }

        var precedenceResolver =
            new SkyrimRuntimeArchivePrecedenceResolver(
                runtimeArchiveEvidence
            );

        var findings =
            new List<
                SkyrimEffectiveArmorAddonArchiveCandidateFinding
            >(
                effectiveScan.Findings.Count
            );

        foreach (
            EffectiveAssetReferenceFinding finding
            in effectiveScan.Findings)
        {
            archiveIndex.TryGetProviders(
                finding.RequestedPath,
                out IReadOnlyList<SkyrimArchiveAssetProvider>
                    archiveCandidates
            );

            SkyrimRuntimeArchivePrecedenceDecision
                archivePrecedence =
                    precedenceResolver.Resolve(
                        archiveCandidates
                    );

            findings.Add(
                new SkyrimEffectiveArmorAddonArchiveCandidateFinding(
                    EffectiveFinding:
                        finding,
                    ArchiveCandidates:
                        archiveCandidates,
                    RuntimeEvidencedArchiveCandidates:
                        archivePrecedence
                            .RuntimeEvidencedProviders,
                    ArchivePrecedence:
                        archivePrecedence
                )
            );
        }

        return new SkyrimEffectiveArmorAddonArchiveCandidateScanResult(
            EffectiveScan:
                effectiveScan,
            ArchiveIndex:
                archiveIndex,
            RuntimeArchiveEvidence:
                runtimeArchiveEvidence,
            Findings:
                findings.ToArray()
        );
    }
}
