using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.LoadOrder;
using CaseCompat.Core.Findings;

public static class EffectiveArmorAddonModelsCommand
{
    public static int Run(string[] args)
    {
        if (args.Length < 5 ||
            args.Length > 6)
        {
            Console.Error.WriteLine(
                "Error: effective-armor-addon-models requires " +
                "a Data root, Plugins.txt, loadorder.txt, " +
                "ArmorAddon FormKey, and optional path search."
            );

            return 2;
        }

        SkyrimRuntimeLoadOrder loadOrder;
        SkyrimTargetArmorAddonWinnerResult winner;

        try
        {
            loadOrder =
                SkyrimRuntimeLoadOrderReader.Read(
                    pluginsPath: args[2],
                    loadOrderPath: args[3]
                );

            if (!loadOrder.IsConsistent)
            {
                Console.Error.WriteLine(
                    "Error: runtime load order is inconsistent."
                );

                return 4;
            }

            winner =
                SkyrimTargetArmorAddonWinnerProbe.Inspect(
                    dataRoot: args[1],
                    loadOrder: loadOrder,
                    targetFormKey: args[4]
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Effective ArmorAddon probe error: {ex.Message}"
            );

            return 3;
        }

        Console.WriteLine(
            "CaseCompat Effective ArmorAddon References"
        );
        Console.WriteLine(
            "=========================================="
        );
        Console.WriteLine();

        Console.WriteLine(
            $"Target FormKey:            {winner.TargetFormKey}"
        );

        Console.WriteLine(
            $"Explicitly active plugins: {winner.ExplicitlyActivePluginCount:N0}"
        );

        Console.WriteLine(
            $"Plugins opened:            {winner.PluginsChecked:N0}"
        );

        Console.WriteLine(
            $"Missing plugin files:      {winner.MissingPluginFiles.Count:N0}"
        );

        Console.WriteLine(
            $"Plugin read errors:        {winner.ReadErrors.Count:N0}"
        );

        Console.WriteLine(
            $"Winner search complete:    {(winner.SearchComplete ? "YES" : "NO")}"
        );

        if (!winner.Found)
        {
            Console.WriteLine(
                "Winner found:              NO"
            );

            Console.WriteLine();
            Console.WriteLine(
                "Read-only analysis: no files were modified."
            );

            return 1;
        }

        Console.WriteLine(
            $"Winner found:              YES"
        );

        Console.WriteLine(
            $"Winning plugin:           {winner.WinningPluginName}"
        );

        Console.WriteLine(
            $"Load-order index:         {winner.WinningLoadOrderIndex}"
        );

        Console.WriteLine(
            $"EditorID:                 {winner.WinningEditorId ?? "(none)"}"
        );

        string? filter =
            args.Length == 6
                ? args[5]
                : null;

        IReadOnlyList<EffectiveAssetReferenceFinding> allFindings =
            SkyrimEffectiveArmorAddonFindingBuilder.Build(
                winner
            );

        IEnumerable<EffectiveAssetReferenceFinding> findings =
            allFindings;

        if (!string.IsNullOrWhiteSpace(filter))
        {
            findings =
                findings.Where(finding =>
                    finding.RawPath.Contains(
                        filter,
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    finding.RequestedPath.Contains(
                        filter,
                        StringComparison.OrdinalIgnoreCase
                    )
                );
        }

        EffectiveAssetReferenceFinding[] displayed =
            findings.ToArray();

        Console.WriteLine(
            $"Winning model references: {allFindings.Count:N0}"
        );

        if (!string.IsNullOrWhiteSpace(filter))
        {
            Console.WriteLine(
                $"Path filter:             {filter}"
            );
        }

        Console.WriteLine(
            $"Displayed references:      {displayed.Length:N0}"
        );

        foreach (
            EffectiveAssetReferenceFinding finding
            in displayed)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"Field:      {finding.ReferenceField}"
            );

            Console.WriteLine(
                $"Given:      {finding.RawPath}"
            );

            Console.WriteLine(
                $"Requested:  {finding.RequestedPath}"
            );

            Console.WriteLine(
                $"Resolution: " +
                $"{(finding.LinuxResolves ? "RESOLVES" : "UNRESOLVED")}"
            );

            Console.WriteLine(
                $"Physical:   " +
                $"{finding.Resolution.ResolvedPhysicalPath ?? "(none)"}"
            );

            Console.WriteLine(
                $"Candidates: {finding.EquivalentCandidateCount}"
            );

            Console.WriteLine(
                $"Evidence:   " +
                $"{EffectiveAssetReferenceEvidenceClassifier.Classify(finding)}"
            );

            if (!finding.LinuxResolves)
            {
                Console.WriteLine(
                    $"Failed at:  " +
                    $"{finding.Resolution.FailedComponentIndex?.ToString() ?? "(unknown)"}"
                );

                Console.WriteLine(
                    $"Failure:    " +
                    $"{finding.Resolution.FailureReason ?? "(unknown)"}"
                );
            }

            foreach (
                string candidate
                in finding.Resolution.EquivalentPhysicalCandidates)
            {
                Console.WriteLine(
                    $"Candidate:  {candidate}"
                );
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            "Scope: explicitly active runtime plugins."
        );

        Console.WriteLine(
            "Read-only analysis: no files were modified."
        );

        return 0;
    }
}
