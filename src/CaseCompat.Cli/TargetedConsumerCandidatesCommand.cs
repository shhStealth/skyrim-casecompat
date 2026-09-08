using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Analysis;
using CaseCompat.Core.LoadOrder;
using CaseCompat.Core.Repair;

public static class TargetedConsumerCandidatesCommand
{
    public static int Run(string[] args)
    {
        if (args.Length < 5 ||
            args.Length > 6)
        {
            Console.Error.WriteLine(
                "Error: targeted-consumer-candidates requires a Data root, " +
                "Plugins.txt, loadorder.txt, Skyrim.ccc, and optional path " +
                "search."
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

            SkyrimWinningArmorAddonInventoryResult armorAddonInventory =
                SkyrimWinningArmorAddonInventory.Inspect(
                    dataRoot:
                        args[1],
                    runtimePluginSet:
                        runtimePluginSet
                );

            IReadOnlyList<WindowsNamespaceAnalysis> analyses =
                SkyrimWinningArmorAddonSnapshotNamespaceAnalysisProducer
                    .Produce(
                        armorAddonInventory
                    );

            SkyrimWinningArmorAddonSnapshotEvidenceScanResult scan =
                SkyrimWinningArmorAddonSnapshotEvidenceScanner.Inspect(
                    armorAddonInventory,
                    analyses
                );

            SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjectionResult
                armorAddonProjection =
                    SkyrimWinningArmorAddonAggregateConsumerSpellingEvidenceProjector
                        .Project(
                            scan
                        );

            SkyrimWinningHeadPartInventoryResult headPartInventory =
                SkyrimWinningHeadPartInventory.Inspect(
                    dataRoot:
                        args[1],
                    runtimePluginSet:
                        runtimePluginSet
                );

            SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjectionResult
                headPartProjection =
                    SkyrimWinningHeadPartAggregateConsumerSpellingEvidenceProjector
                        .Project(
                            headPartInventory
                        );

            SkyrimWinningConsumerSpellingEvidenceCompositionResult
                composition =
                    SkyrimWinningConsumerSpellingEvidenceComposer.Compose(
                        armorAddonProjection,
                        headPartProjection
                    );

            SkyrimWinningTargetedConsumerCaseRepairCandidateProjectionResult
                result =
                    SkyrimWinningTargetedConsumerCaseRepairCandidateProjector
                        .Project(
                            composition
                        );

            Console.WriteLine(
                "CaseCompat Targeted Consumer-Case Repair Candidates"
            );

            Console.WriteLine(
                "===================================================="
            );

            Console.WriteLine();

            Console.WriteLine(
                $"ArmorAddon runtime-active plugins:  " +
                $"{armorAddonInventory.RuntimeActivePluginCount:N0}"
            );

            Console.WriteLine(
                $"ArmorAddon plugins opened:          " +
                $"{armorAddonInventory.PluginsOpened:N0}"
            );

            Console.WriteLine(
                $"ArmorAddon missing plugin files:    " +
                $"{armorAddonInventory.MissingPluginFiles.Count:N0}"
            );

            Console.WriteLine(
                $"ArmorAddon plugin read errors:      " +
                $"{armorAddonInventory.ReadErrors.Count:N0}"
            );

            Console.WriteLine();

            Console.WriteLine(
                $"HeadPart runtime-active plugins:    " +
                $"{headPartInventory.RuntimeActivePluginCount:N0}"
            );

            Console.WriteLine(
                $"HeadPart plugins opened:            " +
                $"{headPartInventory.PluginsOpened:N0}"
            );

            Console.WriteLine(
                $"HeadPart missing plugin files:      " +
                $"{headPartInventory.MissingPluginFiles.Count:N0}"
            );

            Console.WriteLine(
                $"HeadPart plugin read errors:        " +
                $"{headPartInventory.ReadErrors.Count:N0}"
            );

            Console.WriteLine();

            Console.WriteLine(
                $"Winner search complete:             " +
                $"{(composition.WinnerSearchComplete ? "YES" : "NO")}"
            );

            Console.WriteLine(
                $"Consumer-spelling composition state: " +
                $"{composition.State}"
            );

            Console.WriteLine(
                $"Composed consumer-spelling leaves:  " +
                $"{composition.EvidenceCount:N0}"
            );

            Console.WriteLine();

            Console.WriteLine(
                $"Candidate projection state:         {result.State}"
            );

            Console.WriteLine(
                $"Data root:                          " +
                $"{result.DataRoot ?? "(unavailable)"}"
            );

            Console.WriteLine(
                $"Observed leaves:                    " +
                $"{result.Leaves.Count:N0}"
            );

            Console.WriteLine(
                $"Candidates:                         " +
                $"{result.CandidateCount:N0}"
            );

            Console.WriteLine();

            Console.WriteLine(
                "Leaf states:"
            );

            Console.WriteLine(
                "  State                                 Leaves"
            );

            foreach (
                SkyrimWinningTargetedConsumerCaseRepairLeafState state
                in Enum.GetValues<
                    SkyrimWinningTargetedConsumerCaseRepairLeafState>())
            {
                int count =
                    result.Leaves.Count(
                        leaf =>
                            leaf.State ==
                            state
                    );

                Console.WriteLine(
                    $"  {state,-38} {count,9:N0}"
                );
            }

            string? filter =
                args.Length == 6
                    ? args[5]
                    : null;

            IReadOnlyList<DataRelativePathTargetedConsumerCaseRepairCandidate>
                reportedCandidates =
                    filter is null
                        ? result.Candidates
                        : result.Candidates
                            .Where(
                                candidate =>
                                    MatchesFilter(
                                        candidate,
                                        filter
                                    )
                            )
                            .ToArray();

            if (filter is not null)
            {
                Console.WriteLine();

                Console.WriteLine(
                    $"Path filter:                        {filter}"
                );

                Console.WriteLine(
                    $"Matching candidates:                " +
                    $"{reportedCandidates.Count:N0}"
                );
            }

            foreach (
                DataRelativePathTargetedConsumerCaseRepairCandidate candidate
                in reportedCandidates)
            {
                Console.WriteLine();

                Console.WriteLine(
                    $"Logical leaf:   {candidate.WindowsLogicalPath}"
                );

                Console.WriteLine(
                    $"Requested path: {candidate.AuthoritativeRequestedPath}"
                );

                Console.WriteLine(
                    $"Source path:    {candidate.SourceSnapshot.PhysicalPath}"
                );

                Console.WriteLine(
                    $"Source size:    {candidate.SourceSnapshot.Size:N0}"
                );

                Console.WriteLine(
                    $"Source sha256:  {candidate.SourceSnapshot.Sha256}"
                );

                Console.WriteLine(
                    $"Source inode generation: " +
                    $"{candidate.SourceInodeGeneration}"
                );
            }

            Console.WriteLine();

            Console.WriteLine(
                "Candidate is not authorization: this command performs no " +
                "planning, persistence, or mutation."
            );

            Console.WriteLine(
                "Read-only discovery: no files were modified."
            );

            return result.CandidateEvidenceComplete
                ? 0
                : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Targeted consumer-candidate discovery error: {ex.Message}"
            );

            return 3;
        }
    }

    private static bool MatchesFilter(
        DataRelativePathTargetedConsumerCaseRepairCandidate candidate,
        string filter)
    {
        return
            candidate.WindowsLogicalPath.Contains(
                filter,
                StringComparison.OrdinalIgnoreCase
            ) ||
            candidate.AuthoritativeRequestedPath.Contains(
                filter,
                StringComparison.OrdinalIgnoreCase
            ) ||
            candidate.SourceSnapshot.PhysicalPath.Contains(
                filter,
                StringComparison.OrdinalIgnoreCase
            );
    }
}
