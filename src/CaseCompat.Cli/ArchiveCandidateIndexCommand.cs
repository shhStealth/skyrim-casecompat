using CaseCompat.Bethesda.Plugins;

public static class ArchiveCandidateIndexCommand
{
    public static int Run(string[] args)
    {
        if (args.Length < 2 ||
            args.Length > 3)
        {
            Console.Error.WriteLine(
                "Error: archive-candidate-index requires " +
                "a Data root and optional requested asset path."
            );

            return 2;
        }

        try
        {
            SkyrimArchiveCandidateIndexResult result =
                SkyrimArchiveCandidateIndex.Inspect(
                    args[1]
                );

            Console.WriteLine(
                "CaseCompat Archive Candidate Index"
            );
            Console.WriteLine(
                "=================================="
            );
            Console.WriteLine();

            Console.WriteLine(
                $"Archives discovered:            {result.ArchivesDiscovered:N0}"
            );

            Console.WriteLine(
                $"Archives read:                  {result.ArchivesRead:N0}"
            );

            Console.WriteLine(
                $"Archive read errors:            {result.ReadErrors.Count:N0}"
            );

            Console.WriteLine(
                $"Search complete:                {(result.SearchComplete ? "YES" : "NO")}"
            );

            Console.WriteLine();
            Console.WriteLine(
                $"Archive file entries:           {result.TotalFileEntries:N0}"
            );

            Console.WriteLine(
                $"Unique logical asset paths:     {result.UniqueLogicalAssetCount:N0}"
            );

            Console.WriteLine(
                $"Multi-provider asset paths:     {result.MultiProviderAssetCount:N0}"
            );

            Console.WriteLine(
                $"Maximum providers for one path: {result.MaximumProviderCount:N0}"
            );

            Console.WriteLine(
                $"Same-archive logical duplicates:{result.DuplicateLogicalEntriesWithinArchive,10:N0}"
            );

            if (args.Length == 3)
            {
                string requestedPath =
                    args[2];

                bool found =
                    result.TryGetProviders(
                        requestedPath,
                        out IReadOnlyList<SkyrimArchiveAssetProvider>
                            providers
                    );

                Console.WriteLine();
                Console.WriteLine(
                    $"Requested path: {requestedPath}"
                );

                Console.WriteLine(
                    $"Archive candidates: {providers.Count:N0}"
                );

                if (found)
                {
                    foreach (
                        SkyrimArchiveAssetProvider provider
                        in providers)
                    {
                        Console.WriteLine();
                        Console.WriteLine(
                            $"Archive:  {provider.ArchiveName}"
                        );

                        Console.WriteLine(
                            $"Internal: {provider.InternalPath}"
                        );

                        Console.WriteLine(
                            $"Size:     {provider.Size:N0}"
                        );
                    }
                }
            }

            if (result.ReadErrors.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine(
                    "Archive read errors:"
                );

                foreach (
                    SkyrimArchiveReadError error
                    in result.ReadErrors.Take(20))
                {
                    Console.WriteLine(
                        $"  {error.ArchiveName}: {error.Error}"
                    );
                }
            }

            Console.WriteLine();
            Console.WriteLine(
                "Candidate evidence only: archive load state " +
                "and precedence are not inferred."
            );

            Console.WriteLine(
                "Read-only index: no files were modified or extracted."
            );

            return result.SearchComplete
                ? 0
                : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Archive candidate index error: {ex.Message}"
            );

            return 3;
        }
    }
}
