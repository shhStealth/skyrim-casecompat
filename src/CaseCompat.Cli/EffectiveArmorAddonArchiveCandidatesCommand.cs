using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Findings;
using CaseCompat.Core.LoadOrder;

public static class EffectiveArmorAddonArchiveCandidatesCommand
{
    public static int Run(string[] args)
    {
        if (args.Length < 6 ||
            args.Length > 7)
        {
            Console.Error.WriteLine(
                "Error: effective-armor-addon-archive-candidates " +
                "requires a Data root, Plugins.txt, loadorder.txt, " +
                "Skyrim.ccc, INI directory, and optional path search."
            );

            return 2;
        }

        try
        {
            SkyrimRuntimeLoadOrder loadOrder =
                SkyrimRuntimeLoadOrderReader.Read(
                    pluginsPath:
                        args[2],
                    loadOrderPath:
                        args[3]
                );

            SkyrimRuntimePluginSet runtimePluginSet =
                SkyrimRuntimePluginSetReader.Read(
                    loadOrder,
                    args[4]
                );

            if (!runtimePluginSet.IsConsistent)
            {
                Console.Error.WriteLine(
                    "Error: runtime plugin set is inconsistent."
                );

                return 4;
            }

            SkyrimWinningArmorAddonInventoryResult inventory =
                SkyrimWinningArmorAddonInventory.Inspect(
                    dataRoot:
                        args[1],
                    runtimePluginSet:
                        runtimePluginSet
                );

            SkyrimWinningArmorAddonEffectiveScanResult effectiveScan =
                SkyrimWinningArmorAddonEffectiveScanner.Inspect(
                    inventory
                );

            SkyrimArchiveCandidateIndexResult archiveIndex =
                SkyrimArchiveCandidateIndex.Inspect(
                    args[1]
                );

            SkyrimRuntimeArchiveEvidenceResult runtimeArchiveEvidence =
                SkyrimRuntimeArchiveEvidence.Inspect(
                    dataRoot:
                        args[1],
                    runtimePluginSet:
                        runtimePluginSet,
                    iniDirectory:
                        args[5]
                );

            SkyrimEffectiveArmorAddonArchiveCandidateScanResult result =
                SkyrimEffectiveArmorAddonArchiveCandidateScan.Inspect(
                    effectiveScan,
                    archiveIndex,
                    runtimeArchiveEvidence
                );

            Console.WriteLine(
                "CaseCompat Effective ArmorAddon Archive Candidates"
            );
            Console.WriteLine(
                "================================================="
            );
            Console.WriteLine();

            Console.WriteLine(
                $"Effective findings:                  {result.Findings.Count:N0}"
            );

            Console.WriteLine(
                $"Unique requested paths:              {effectiveScan.UniqueRequestedPathCount:N0}"
            );

            Console.WriteLine(
                $"Physically present BSAs indexed:     {archiveIndex.ArchivesRead:N0}"
            );

            Console.WriteLine(
                $"Archive logical asset paths:         {archiveIndex.UniqueLogicalAssetCount:N0}"
            );

            Console.WriteLine(
                $"Runtime-evidenced physical BSAs:     {runtimeArchiveEvidence.RuntimeEvidencedArchiveCount:N0}"
            );

            Console.WriteLine(
                $"Physical BSAs without runtime evidence:{runtimeArchiveEvidence.NoRuntimeEvidenceArchiveCount,6:N0}"
            );

            Console.WriteLine();
            Console.WriteLine(
                $"Findings with BSA candidates:        {result.FindingsWithArchiveCandidates:N0}"
            );

            Console.WriteLine(
                $"Findings without BSA candidates:     {result.FindingsWithoutArchiveCandidates:N0}"
            );

            Console.WriteLine(
                $"Unique paths with BSA candidates:    {result.UniqueRequestedPathsWithArchiveCandidates:N0}"
            );

            Console.WriteLine(
                $"Unique paths without BSA candidates: {result.UniqueRequestedPathsWithoutArchiveCandidates:N0}"
            );

            Console.WriteLine();

            Console.WriteLine(
                $"Findings with runtime BSA candidates:{result.FindingsWithRuntimeEvidencedArchiveCandidates,10:N0}"
            );

            Console.WriteLine(
                $"Findings without runtime BSA candidates:{result.FindingsWithoutRuntimeEvidencedArchiveCandidates,7:N0}"
            );

            Console.WriteLine(
                $"Unique paths with runtime BSA:       {result.UniqueRequestedPathsWithRuntimeEvidencedArchiveCandidates:N0}"
            );

            Console.WriteLine(
                $"Unique paths without runtime BSA:    {result.UniqueRequestedPathsWithoutRuntimeEvidencedArchiveCandidates:N0}"
            );

            Console.WriteLine(
                $"Complete:                            {(result.Complete ? "YES" : "NO")}"
            );

            Console.WriteLine();
            Console.WriteLine(
                "Loose evidence state / physical BSA candidates:"
            );

            Console.WriteLine(
                "  State                            Total   With BSA    No BSA"
            );

            foreach (
                EffectiveAssetReferenceEvidenceState state
                in Enum.GetValues<
                    EffectiveAssetReferenceEvidenceState>())
            {
                SkyrimEffectiveArmorAddonArchiveCandidateFinding[]
                    stateFindings =
                        result.Findings
                            .Where(finding =>
                                finding.LooseEvidenceState == state
                            )
                            .ToArray();

                int withArchive =
                    stateFindings.Count(finding =>
                        finding.HasArchiveCandidates
                    );

                int withoutArchive =
                    stateFindings.Length -
                    withArchive;

                Console.WriteLine(
                    $"  {state,-30} " +
                    $"{stateFindings.Length,8:N0} " +
                    $"{withArchive,10:N0} " +
                    $"{withoutArchive,9:N0}"
                );
            }

            Console.WriteLine();
            Console.WriteLine(
                "Loose evidence state / runtime-evidenced BSA candidates:"
            );

            Console.WriteLine(
                "  State                            Total   With BSA    No BSA"
            );

            foreach (
                EffectiveAssetReferenceEvidenceState state
                in Enum.GetValues<
                    EffectiveAssetReferenceEvidenceState>())
            {
                SkyrimEffectiveArmorAddonArchiveCandidateFinding[]
                    stateFindings =
                        result.Findings
                            .Where(finding =>
                                finding.LooseEvidenceState == state
                            )
                            .ToArray();

                int withArchive =
                    stateFindings.Count(finding =>
                        finding.HasRuntimeEvidencedArchiveCandidates
                    );

                int withoutArchive =
                    stateFindings.Length -
                    withArchive;

                Console.WriteLine(
                    $"  {state,-30} " +
                    $"{stateFindings.Length,8:N0} " +
                    $"{withArchive,10:N0} " +
                    $"{withoutArchive,9:N0}"
                );
            }

            Console.WriteLine();
            Console.WriteLine(
                "Unique requested paths by loose state / physical BSA candidates:"
            );

            Console.WriteLine(
                "  State                            Total   With BSA    No BSA"
            );

            foreach (
                EffectiveAssetReferenceEvidenceState state
                in Enum.GetValues<
                    EffectiveAssetReferenceEvidenceState>())
            {
                var pathGroups =
                    result.Findings
                        .Where(finding =>
                            finding.LooseEvidenceState == state
                        )
                        .GroupBy(
                            finding =>
                                finding.EffectiveFinding.RequestedPath,
                            StringComparer.Ordinal
                        )
                        .ToArray();

                int totalPaths =
                    pathGroups.Length;

                int withArchive =
                    pathGroups.Count(group =>
                        group.Any(finding =>
                            finding.HasArchiveCandidates
                        )
                    );

                int withoutArchive =
                    totalPaths -
                    withArchive;

                Console.WriteLine(
                    $"  {state,-30} " +
                    $"{totalPaths,8:N0} " +
                    $"{withArchive,10:N0} " +
                    $"{withoutArchive,9:N0}"
                );
            }

            Console.WriteLine();
            Console.WriteLine(
                "Unique requested paths by loose state / runtime-evidenced BSA candidates:"
            );

            Console.WriteLine(
                "  State                            Total   With BSA    No BSA"
            );

            foreach (
                EffectiveAssetReferenceEvidenceState state
                in Enum.GetValues<
                    EffectiveAssetReferenceEvidenceState>())
            {
                var pathGroups =
                    result.Findings
                        .Where(finding =>
                            finding.LooseEvidenceState == state
                        )
                        .GroupBy(
                            finding =>
                                finding.EffectiveFinding.RequestedPath,
                            StringComparer.Ordinal
                        )
                        .ToArray();

                int totalPaths =
                    pathGroups.Length;

                int withArchive =
                    pathGroups.Count(group =>
                        group.Any(finding =>
                            finding.HasRuntimeEvidencedArchiveCandidates
                        )
                    );

                int withoutArchive =
                    totalPaths -
                    withArchive;

                Console.WriteLine(
                    $"  {state,-30} " +
                    $"{totalPaths,8:N0} " +
                    $"{withArchive,10:N0} " +
                    $"{withoutArchive,9:N0}"
                );
            }

            if (args.Length == 7)
            {
                string filter =
                    args[6];

                SkyrimEffectiveArmorAddonArchiveCandidateFinding[]
                    matches =
                        result.Findings
                            .Where(finding =>
                                finding.EffectiveFinding.RawPath.Contains(
                                    filter,
                                    StringComparison.OrdinalIgnoreCase
                                ) ||
                                finding.EffectiveFinding.RequestedPath.Contains(
                                    filter,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                            .ToArray();

                Console.WriteLine();
                Console.WriteLine(
                    $"Path filter:                        {filter}"
                );

                Console.WriteLine(
                    $"Matching findings:                  {matches.Length:N0}"
                );

                foreach (
                    SkyrimEffectiveArmorAddonArchiveCandidateFinding
                    finding
                    in matches)
                {
                    EffectiveAssetReferenceFinding effective =
                        finding.EffectiveFinding;

                    Console.WriteLine();
                    Console.WriteLine(
                        $"FormKey:       {effective.ConsumerFormKey}"
                    );

                    Console.WriteLine(
                        $"Field:         {effective.ReferenceField}"
                    );

                    Console.WriteLine(
                        $"Requested:     {effective.RequestedPath}"
                    );

                    Console.WriteLine(
                        $"Loose state:   {finding.LooseEvidenceState}"
                    );

                    Console.WriteLine(
                        $"BSA candidates:{finding.ArchiveCandidateCount,5:N0}"
                    );

                    foreach (
                        SkyrimArchiveAssetProvider provider
                        in finding.ArchiveCandidates.Take(10))
                    {
                        Console.WriteLine(
                            $"  {provider.ArchiveName}"
                        );
                    }

                    if (finding.ArchiveCandidateCount > 10)
                    {
                        Console.WriteLine(
                            $"  ... {finding.ArchiveCandidateCount - 10:N0} more"
                        );
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine(
                "Physical BSA presence and runtime archive evidence " +
                "are reported separately."
            );

            Console.WriteLine(
                "Runtime evidence does not infer archive precedence " +
                "or a winning provider."
            );

            Console.WriteLine(
                "Read-only scan: no files were modified or extracted."
            );

            return result.Complete
                ? 0
                : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Archive-candidate correlation error: {ex.Message}"
            );

            return 3;
        }
    }
}
