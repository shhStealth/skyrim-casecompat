using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Analysis;
using CaseCompat.Core.LoadOrder;

public static class ArmorAddonSnapshotDiagnosticsCommand
{
    public static int Run(string[] args)
    {
        if (args.Length < 6 ||
            args.Length > 7)
        {
            Console.Error.WriteLine(
                "Error: armor-addon-snapshot-diagnostics requires a " +
                "Data root, Plugins.txt, loadorder.txt, Skyrim.ccc, " +
                "INI directory, and optional path search."
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

            IReadOnlyList<WindowsNamespaceAnalysis> analyses =
                SkyrimWinningArmorAddonSnapshotNamespaceAnalysisProducer
                    .Produce(
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

            SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticResult
                result =
                    SkyrimWinningArmorAddonSnapshotDiagnosticPipeline
                        .Compose(
                            inventory,
                            analyses,
                            archiveIndex,
                            runtimeArchiveEvidence
                        );

            int completeNamespaceAnalyses =
                analyses.Count(
                    analysis =>
                        analysis.Complete
                );

            bool namespaceEvidenceComplete =
                analyses.All(
                    analysis =>
                        analysis.Complete
                );

            bool aggregateEvidenceComplete =
                namespaceEvidenceComplete &&
                result.WinnerSearchComplete &&
                result.ArchiveCandidateIndexComplete &&
                result.RuntimeArchiveEvidenceComplete;

            Console.WriteLine(
                "CaseCompat ArmorAddon Snapshot Diagnostics"
            );

            Console.WriteLine(
                "========================================"
            );

            Console.WriteLine();

            Console.WriteLine(
                $"Runtime-active plugins:        " +
                $"{inventory.RuntimeActivePluginCount:N0}"
            );

            Console.WriteLine(
                $"Plugins opened:                " +
                $"{inventory.PluginsOpened:N0}"
            );

            Console.WriteLine(
                $"Missing plugin files:          " +
                $"{inventory.MissingPluginFiles.Count:N0}"
            );

            Console.WriteLine(
                $"Plugin read errors:            " +
                $"{inventory.ReadErrors.Count:N0}"
            );

            Console.WriteLine(
                $"Winner search complete:        " +
                $"{(result.WinnerSearchComplete ? "YES" : "NO")}"
            );

            Console.WriteLine();

            Console.WriteLine(
                $"Namespace analyses:            " +
                $"{analyses.Count:N0}"
            );

            Console.WriteLine(
                $"Complete namespace analyses:   " +
                $"{completeNamespaceAnalyses:N0}"
            );

            Console.WriteLine(
                $"Namespace evidence complete:   " +
                $"{(namespaceEvidenceComplete ? "YES" : "NO")}"
            );

            Console.WriteLine();

            Console.WriteLine(
                $"Archives read:                  " +
                $"{archiveIndex.ArchivesRead:N0}"
            );

            Console.WriteLine(
                $"Archive logical asset paths:   " +
                $"{archiveIndex.UniqueLogicalAssetCount:N0}"
            );

            Console.WriteLine(
                $"Archive index complete:        " +
                $"{(result.ArchiveCandidateIndexComplete ? "YES" : "NO")}"
            );

            Console.WriteLine();

            Console.WriteLine(
                $"Runtime-evidenced BSAs:         " +
                $"{runtimeArchiveEvidence.RuntimeEvidencedArchiveCount:N0}"
            );

            Console.WriteLine(
                $"BSAs without runtime evidence: " +
                $"{runtimeArchiveEvidence.NoRuntimeEvidenceArchiveCount:N0}"
            );

            Console.WriteLine(
                $"Runtime archive evidence complete: " +
                $"{(result.RuntimeArchiveEvidenceComplete ? "YES" : "NO")}"
            );

            Console.WriteLine();

            Console.WriteLine(
                $"Final consumer diagnostics:    " +
                $"{result.DiagnosticCount:N0}"
            );

            Console.WriteLine(
                $"Aggregate evidence complete:   " +
                $"{(aggregateEvidenceComplete ? "YES" : "NO")}"
            );

            Console.WriteLine();

            Console.WriteLine(
                "Final diagnostic states:"
            );

            Console.WriteLine(
                "  State                                             Consumers"
            );

            foreach (
                SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState
                    state
                in Enum.GetValues<
                    SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnosticState>())
            {
                int count =
                    result.Diagnostics.Count(
                        diagnostic =>
                            diagnostic.State ==
                            state
                    );

                Console.WriteLine(
                    $"  {state,-48} {count,9:N0}"
                );
            }

            if (args.Length == 7)
            {
                string filter =
                    args[6];

                SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnostic[]
                    matches =
                        result.Diagnostics
                            .Where(
                                diagnostic =>
                                    MatchesFilter(
                                        diagnostic,
                                        filter
                                    )
                            )
                            .ToArray();

                Console.WriteLine();

                Console.WriteLine(
                    $"Path filter:                  {filter}"
                );

                Console.WriteLine(
                    $"Matching diagnostics:         {matches.Length:N0}"
                );

                foreach (
                    SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnostic
                        diagnostic
                    in matches)
                {
                    SkyrimWinningArmorAddonSnapshotConsumerProjection
                        consumer =
                            diagnostic.Consumer;

                    SkyrimArmorAddonModelReference reference =
                        consumer.Reference;

                    Console.WriteLine();

                    Console.WriteLine(
                        $"State:          {diagnostic.State}"
                    );

                    Console.WriteLine(
                        $"Winning plugin: {consumer.WinningPluginName}"
                    );

                    Console.WriteLine(
                        $"Load order:     {consumer.WinningLoadOrderIndex}"
                    );

                    Console.WriteLine(
                        $"FormKey:        {reference.FormKey}"
                    );

                    Console.WriteLine(
                        $"Editor ID:      {reference.EditorId ?? "(none)"}"
                    );

                    Console.WriteLine(
                        $"Field:          {reference.Field}"
                    );

                    Console.WriteLine(
                        $"Given path:     {reference.GivenPath}"
                    );

                    Console.WriteLine(
                        $"Data path:      {reference.DataRelativePath}"
                    );

                    Console.WriteLine(
                        $"Loose state:    {diagnostic.PathInterpretation.State}"
                    );

                    Console.WriteLine(
                        $"Lookup evidence:{diagnostic.PathInterpretation.Evidence.State,21}"
                    );

                    if (!string.IsNullOrWhiteSpace(
                            diagnostic.PathInterpretation.InterpretationError))
                    {
                        Console.WriteLine(
                            $"Interpretation error: " +
                            $"{diagnostic.PathInterpretation.InterpretationError}"
                        );
                    }

                    if (!string.IsNullOrWhiteSpace(
                            diagnostic.PathInterpretation.Evidence.Error))
                    {
                        Console.WriteLine(
                            $"Evidence error: " +
                            $"{diagnostic.PathInterpretation.Evidence.Error}"
                        );
                    }

                    if (
                        diagnostic.PathArchiveEvidence
                        is SkyrimWinningArmorAddonSnapshotPathArchiveEvidence
                            archiveEvidence)
                    {
                        Console.WriteLine(
                            $"BSA candidates: {archiveEvidence.ArchiveCandidates.Count:N0}"
                        );

                        Console.WriteLine(
                            $"BSA precedence: {archiveEvidence.ArchivePrecedence.State}"
                        );
                    }

                    if (
                        diagnostic.WinningArchiveProvider
                        is SkyrimArchiveAssetProvider winningArchive)
                    {
                        Console.WriteLine(
                            $"BSA winner:     {winningArchive.ArchiveName}"
                        );

                        Console.WriteLine(
                            $"Winner path:    {winningArchive.ArchivePath}"
                        );
                    }
                }
            }

            Console.WriteLine();

            Console.WriteLine(
                "The reported state is observational evidence, not a " +
                "health, severity, canonical-spelling, or repair verdict."
            );

            Console.WriteLine(
                "Archive precedence applies only to already-produced " +
                "runtime-evidenced archive candidates."
            );

            Console.WriteLine(
                "Read-only scan: no files were modified or extracted."
            );

            return aggregateEvidenceComplete
                ? 0
                : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"ArmorAddon snapshot diagnostic error: {ex.Message}"
            );

            return 3;
        }
    }

    private static bool MatchesFilter(
        SkyrimWinningArmorAddonSnapshotConsumerProviderDiagnostic diagnostic,
        string filter)
    {
        SkyrimWinningArmorAddonSnapshotConsumerProjection consumer =
            diagnostic.Consumer;

        SkyrimArmorAddonModelReference reference =
            consumer.Reference;

        return
            consumer.WinningPluginName.Contains(
                filter,
                StringComparison.OrdinalIgnoreCase
            ) ||
            reference.FormKey.Contains(
                filter,
                StringComparison.OrdinalIgnoreCase
            ) ||
            (
                reference.EditorId?.Contains(
                    filter,
                    StringComparison.OrdinalIgnoreCase
                ) ??
                false
            ) ||
            reference.Field.Contains(
                filter,
                StringComparison.OrdinalIgnoreCase
            ) ||
            reference.GivenPath.Contains(
                filter,
                StringComparison.OrdinalIgnoreCase
            ) ||
            reference.DataRelativePath.Contains(
                filter,
                StringComparison.OrdinalIgnoreCase
            );
    }
}
