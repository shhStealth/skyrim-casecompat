using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.Findings;
using CaseCompat.Core.LoadOrder;

public static class EffectiveArmorAddonScanCommand
{
    public static int Run(string[] args)
    {
        if (args.Length < 5 ||
            args.Length > 6)
        {
            Console.Error.WriteLine(
                "Error: effective-armor-addon-scan requires " +
                "a Data root, Plugins.txt, loadorder.txt, " +
                "Skyrim.ccc, and optional path search."
            );

            return 2;
        }

        try
        {
            SkyrimRuntimeLoadOrder loadOrder =
                SkyrimRuntimeLoadOrderReader.Read(
                    pluginsPath: args[2],
                    loadOrderPath: args[3]
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
                    dataRoot: args[1],
                    runtimePluginSet: runtimePluginSet
                );

            SkyrimWinningArmorAddonEffectiveScanResult result =
                SkyrimWinningArmorAddonEffectiveScanner.Inspect(
                    inventory
                );

            Console.WriteLine(
                "CaseCompat Effective ArmorAddon Scan"
            );
            Console.WriteLine(
                "===================================="
            );
            Console.WriteLine();

            Console.WriteLine(
                $"Runtime-active plugins:    {inventory.RuntimeActivePluginCount:N0}"
            );

            Console.WriteLine(
                $"Plugins opened:            {inventory.PluginsOpened:N0}"
            );

            Console.WriteLine(
                $"Missing plugin files:      {inventory.MissingPluginFiles.Count:N0}"
            );

            Console.WriteLine(
                $"Plugin read errors:        {inventory.ReadErrors.Count:N0}"
            );

            Console.WriteLine(
                $"Winner search complete:    {(inventory.SearchComplete ? "YES" : "NO")}"
            );

            Console.WriteLine();
            Console.WriteLine(
                $"Winning ArmorAddons:       {inventory.WinningArmorAddonCount:N0}"
            );

            Console.WriteLine(
                $"Winning model references: {inventory.WinningModelReferenceCount:N0}"
            );

            Console.WriteLine(
                $"Unique requested paths:    {result.UniqueRequestedPathCount:N0}"
            );

            Console.WriteLine(
                $"Resolution calls avoided:  {result.AvoidedResolutionCalls:N0}"
            );

            Console.WriteLine(
                $"Unique paths resolved:     {result.ResolvedUniquePathCount:N0}"
            );

            Console.WriteLine(
                $"Resolution errors:         {result.ResolutionErrors.Count:N0}"
            );

            Console.WriteLine(
                $"Effective findings built:  {result.Findings.Count:N0}"
            );

            Console.WriteLine(
                $"Complete:                  {(result.Complete ? "YES" : "NO")}"
            );

            Console.WriteLine();
            Console.WriteLine(
                "Evidence states:"
            );

            foreach (
                EffectiveAssetReferenceEvidenceState state
                in Enum.GetValues<
                    EffectiveAssetReferenceEvidenceState>())
            {
                int count =
                    result.Findings.Count(finding =>
                        EffectiveAssetReferenceEvidenceClassifier
                            .Classify(finding) == state
                    );

                Console.WriteLine(
                    $"  {state,-30} {count,8:N0}"
                );
            }

            if (args.Length == 6)
            {
                string filter =
                    args[5];

                EffectiveAssetReferenceFinding[] matches =
                    result.Findings
                        .Where(finding =>
                            finding.RawPath.Contains(
                                filter,
                                StringComparison.OrdinalIgnoreCase
                            ) ||
                            finding.RequestedPath.Contains(
                                filter,
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        .ToArray();

                Console.WriteLine();
                Console.WriteLine(
                    $"Path filter:              {filter}"
                );

                Console.WriteLine(
                    $"Matching findings:        {matches.Length:N0}"
                );

                foreach (
                    EffectiveAssetReferenceFinding finding
                    in matches)
                {
                    EffectiveAssetReferenceEvidenceState state =
                        EffectiveAssetReferenceEvidenceClassifier
                            .Classify(finding);

                    Console.WriteLine();
                    Console.WriteLine(
                        $"FormKey:    {finding.ConsumerFormKey}"
                    );

                    Console.WriteLine(
                        $"EditorID:   {finding.ConsumerEditorId ?? "(none)"}"
                    );

                    Console.WriteLine(
                        $"Provider:   {finding.WinningPluginName}"
                    );

                    Console.WriteLine(
                        $"Field:      {finding.ReferenceField}"
                    );

                    Console.WriteLine(
                        $"Requested:  {finding.RequestedPath}"
                    );

                    Console.WriteLine(
                        $"Resolution: {(finding.LinuxResolves ? "RESOLVES" : "UNRESOLVED")}"
                    );

                    Console.WriteLine(
                        $"Candidates: {finding.EquivalentCandidateCount}"
                    );

                    Console.WriteLine(
                        $"Evidence:   {state}"
                    );
                }
            }

            if (result.ResolutionErrors.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine(
                    "First resolution errors:"
                );

                foreach (
                    SkyrimAssetPathResolutionError error
                    in result.ResolutionErrors.Take(20))
                {
                    Console.WriteLine(
                        $"  {error.RequestedPath} " +
                        $"({error.AffectedReferenceCount:N0} refs): " +
                        $"{error.Error}"
                    );
                }
            }

            Console.WriteLine();
            Console.WriteLine(
                "Read-only scan: no files were modified."
            );

            return result.Complete
                ? 0
                : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Effective ArmorAddon scan error: {ex.Message}"
            );

            return 3;
        }
    }
}
