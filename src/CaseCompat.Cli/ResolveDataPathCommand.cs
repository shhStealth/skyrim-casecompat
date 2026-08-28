using CaseCompat.Core.Resolution;

public static class ResolveDataPathCommand
{
    public static int Run(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine(
                "Error: resolve-data-path requires " +
                "a Data root and Data-relative file path."
            );

            return 2;
        }

        DataRelativePathResolution result;

        try
        {
            result =
                DataRelativePathResolver.ResolveFile(
                    args[1],
                    args[2]
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Path resolver error: {ex.Message}"
            );

            return 3;
        }

        Console.WriteLine(
            "CaseCompat Data Path Resolver"
        );
        Console.WriteLine(
            "============================="
        );
        Console.WriteLine();

        Console.WriteLine(
            $"Data root:       {result.DataRoot}"
        );
        Console.WriteLine(
            $"Requested path:  {result.RequestedPath}"
        );
        Console.WriteLine(
            $"Linux resolves:  " +
            $"{(result.LinuxResolves ? "YES" : "NO")}"
        );
        Console.WriteLine(
            $"Resolved path:   " +
            $"{result.ResolvedPhysicalPath ?? "(none)"}"
        );

        if (!result.LinuxResolves)
        {
            Console.WriteLine(
                $"Failed component: " +
                $"{result.FailedComponentIndex?.ToString() ?? "(unknown)"}"
            );
            Console.WriteLine(
                $"Failure:          " +
                $"{result.FailureReason ?? "(unknown)"}"
            );
        }

        Console.WriteLine();
        Console.WriteLine("Resolution steps:");

        foreach (PathResolutionStep step in result.Steps)
        {
            string casefold =
                step.ParentCasefoldEnabled switch
                {
                    true => "enabled",
                    false => "disabled",
                    null => "unknown"
                };

            Console.WriteLine();
            Console.WriteLine(
                $"[{step.ComponentIndex}] " +
                $"{step.RequestedComponent}"
            );
            Console.WriteLine(
                $"  Parent:   {step.ParentPhysicalPath}"
            );
            Console.WriteLine(
                $"  Casefold: {casefold}"
            );
            Console.WriteLine(
                $"  Result:   {step.Kind}"
            );
            Console.WriteLine(
                $"  Physical: " +
                $"{step.SelectedPhysicalName ?? "(none)"}"
            );

            if (step.EquivalentPhysicalNames.Count > 0)
            {
                Console.WriteLine(
                    "  Equivalents:"
                );

                foreach (
                    string name
                    in step.EquivalentPhysicalNames)
                {
                    Console.WriteLine(
                        $"    {name}"
                    );
                }
            }

            if (!string.IsNullOrWhiteSpace(
                    step.ParentCasefoldError))
            {
                Console.WriteLine(
                    $"  Casefold error: " +
                    step.ParentCasefoldError
                );
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            $"Equivalent physical candidates: " +
            $"{result.CandidateCount}"
        );

        foreach (
            string candidate
            in result.EquivalentPhysicalCandidates)
        {
            Console.WriteLine(
                $"  {candidate}"
            );
        }

        if (result.CandidateSearchErrors.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine(
                "Candidate search errors:"
            );

            foreach (
                string error
                in result.CandidateSearchErrors)
            {
                Console.WriteLine(
                    $"  {error}"
                );
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            "Read-only resolver: no files were modified."
        );

        return 0;
    }
}
