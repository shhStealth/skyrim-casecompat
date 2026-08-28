using CaseCompat.Bethesda.Plugins;
using CaseCompat.Core.LoadOrder;

public static class RuntimeArchiveEvidenceCommand
{
    public static int Run(string[] args)
    {
        if (args.Length != 6)
        {
            Console.Error.WriteLine(
                "Error: runtime-archive-evidence requires " +
                "a Data root, Plugins.txt, loadorder.txt, " +
                "Skyrim.ccc, and INI directory."
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

            SkyrimRuntimeArchiveEvidenceResult result =
                SkyrimRuntimeArchiveEvidence.Inspect(
                    dataRoot:
                        args[1],
                    runtimePluginSet:
                        runtimePluginSet,
                    iniDirectory:
                        args[5]
                );

            Console.WriteLine(
                "CaseCompat Runtime Archive Evidence"
            );

            Console.WriteLine(
                "=================================="
            );

            Console.WriteLine();

            Console.WriteLine(
                $"Runtime-active plugins:              {runtimePluginSet.RuntimeActiveCount,8:N0}"
            );

            Console.WriteLine(
                $"Physical BSAs:                       {result.PhysicalArchiveCount,8:N0}"
            );

            Console.WriteLine();

            Console.WriteLine(
                $"Plugin-associated physical BSAs:     {result.PluginAssociatedArchiveCount,8:N0}"
            );

            Console.WriteLine(
                $"INI-listed physical BSAs:            {result.IniListedPhysicalArchiveCount,8:N0}"
            );

            Console.WriteLine(
                $"INI-listed but physically missing:   {result.MissingIniArchives.Count,8:N0}"
            );

            Console.WriteLine();

            Console.WriteLine(
                $"Physical BSAs with runtime evidence: {result.RuntimeEvidencedArchiveCount,8:N0}"
            );

            Console.WriteLine(
                $"Physical BSAs without evidence:      {result.NoRuntimeEvidenceArchiveCount,8:N0}"
            );

            Console.WriteLine();

            Console.WriteLine(
                $"Archives associated with >1 plugin:  {result.MultiPluginAssociationArchiveCount,8:N0}"
            );

            Console.WriteLine(
                $"Maximum plugin associations/archive: {result.MaximumPluginAssociationsPerArchive,8:N0}"
            );

            Console.WriteLine(
                $"Association errors:                  {result.AssociationErrors.Count,8:N0}"
            );

            Console.WriteLine(
                $"INI parsing errors:                  {result.IniReadErrors.Count,8:N0}"
            );

            Console.WriteLine(
                $"INI provenance errors:               {result.IniProvenanceErrors.Count,8:N0}"
            );

            Console.WriteLine(
                $"Search complete:                     {(result.SearchComplete ? "YES" : "NO")}"
            );

            Console.WriteLine();

            Console.WriteLine(
                "INI-listed but physically missing:"
            );

            if (result.MissingIniArchives.Count == 0)
            {
                Console.WriteLine(
                    "  (none)"
                );
            }
            else
            {
                foreach (
                    SkyrimRuntimeArchiveMissingIniArchive missing
                    in result.MissingIniArchives)
                {
                    Console.WriteLine(
                        $"  {missing.ArchiveName}"
                    );
                }
            }

            Console.WriteLine();

            Console.WriteLine(
                "Physical BSAs without runtime evidence:"
            );

            SkyrimRuntimeArchiveEvidenceEntry[] noEvidence =
                result.Archives
                    .Where(archive =>
                        !archive.HasRuntimeEvidence
                    )
                    .ToArray();

            if (noEvidence.Length == 0)
            {
                Console.WriteLine(
                    "  (none)"
                );
            }
            else
            {
                foreach (
                    SkyrimRuntimeArchiveEvidenceEntry archive
                    in noEvidence)
                {
                    Console.WriteLine(
                        $"  {archive.ArchiveName}"
                    );
                }
            }

            Console.WriteLine();

            Console.WriteLine(
                "Multi-plugin archive associations:"
            );

            foreach (
                SkyrimRuntimeArchiveEvidenceEntry archive
                in result.Archives
                    .Where(archive =>
                        archive.PluginAssociations.Count > 1
                    )
                    .OrderByDescending(archive =>
                        archive.PluginAssociations.Count
                    )
                    .ThenBy(
                        archive =>
                            archive.ArchiveName,
                        StringComparer.OrdinalIgnoreCase
                    ))
            {
                Console.WriteLine();

                Console.WriteLine(
                    $"{archive.PluginAssociations.Count,3} plugins  " +
                    $"{archive.ArchiveName}"
                );

                foreach (
                    SkyrimRuntimeArchivePluginAssociation association
                    in archive.PluginAssociations)
                {
                    Console.WriteLine(
                        $"    {association.LoadOrderIndex,5}  " +
                        $"{association.PluginName}"
                    );
                }
            }

            Console.WriteLine();
            Console.WriteLine(
                "Runtime evidence only: archive loading order " +
                "and winning provider precedence are not inferred."
            );

            Console.WriteLine(
                "Read-only census: no files were modified or extracted."
            );

            return result.SearchComplete
                ? 0
                : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Runtime archive evidence error: {ex.Message}"
            );

            return 3;
        }
    }
}
