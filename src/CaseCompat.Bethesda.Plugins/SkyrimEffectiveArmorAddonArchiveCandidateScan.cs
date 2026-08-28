using CaseCompat.Core.Findings;

namespace CaseCompat.Bethesda.Plugins;

public sealed record SkyrimEffectiveArmorAddonArchiveCandidateFinding(
    EffectiveAssetReferenceFinding EffectiveFinding,
    IReadOnlyList<SkyrimArchiveAssetProvider> ArchiveCandidates
)
{
    public bool HasArchiveCandidates =>
        ArchiveCandidates.Count > 0;

    public int ArchiveCandidateCount =>
        ArchiveCandidates.Count;

    public EffectiveAssetReferenceEvidenceState LooseEvidenceState =>
        EffectiveAssetReferenceEvidenceClassifier.Classify(
            EffectiveFinding
        );
}

public sealed record SkyrimEffectiveArmorAddonArchiveCandidateScanResult(
    SkyrimWinningArmorAddonEffectiveScanResult EffectiveScan,
    SkyrimArchiveCandidateIndexResult ArchiveIndex,
    IReadOnlyList<SkyrimEffectiveArmorAddonArchiveCandidateFinding> Findings
)
{
    public bool Complete =>
        EffectiveScan.Complete &&
        ArchiveIndex.SearchComplete;

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
}

public static class SkyrimEffectiveArmorAddonArchiveCandidateScan
{
    public static SkyrimEffectiveArmorAddonArchiveCandidateScanResult
        Inspect(
            SkyrimWinningArmorAddonEffectiveScanResult effectiveScan,
            SkyrimArchiveCandidateIndexResult archiveIndex)
    {
        ArgumentNullException.ThrowIfNull(
            effectiveScan
        );

        ArgumentNullException.ThrowIfNull(
            archiveIndex
        );

        string effectiveDataRoot =
            Path.GetFullPath(
                effectiveScan.Inventory.DataRoot
            );

        string archiveDataRoot =
            Path.GetFullPath(
                archiveIndex.DataRoot
            );

        if (!string.Equals(
                effectiveDataRoot,
                archiveDataRoot,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Effective scan and archive index " +
                "must refer to the same Data root."
            );
        }

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

            findings.Add(
                new SkyrimEffectiveArmorAddonArchiveCandidateFinding(
                    EffectiveFinding:
                        finding,
                    ArchiveCandidates:
                        archiveCandidates
                )
            );
        }

        return new SkyrimEffectiveArmorAddonArchiveCandidateScanResult(
            EffectiveScan:
                effectiveScan,
            ArchiveIndex:
                archiveIndex,
            Findings:
                findings.ToArray()
        );
    }
}
