using CaseCompat.Core.Analysis;
using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;

public static class AggregateNamespaceManifestCommand
{
    public const string DefaultManifestName =
        "aggregate-namespace-manifest.json";

    public static int Run(
        string[] args)
    {
        return Run(
            args,
            createdUtcOverride:
                null,
            afterManifestPublishBeforeRead:
                null
        );
    }

    internal static int Run(
        string[] args,
        DateTimeOffset? createdUtcOverride,
        Action<
            LinuxNoFollowPathHandle,
            string,
            DataRelativePathAggregateNamespaceManifestRecord>?
                afterManifestPublishBeforeRead,
        Func<
            WindowsNamespaceAnalysis,
            WindowsNamespaceRegularFileContentAnalysis>?
                contentAnalysisOverride = null)
    {
        if (
            args.Length < 4 ||
            args.Length > 5)
        {
            Console.Error.WriteLine(
                "Error: aggregate-namespace-manifest requires a Skyrim " +
                "Data directory, one direct Data child namespace name, " +
                "an existing output directory, and an optional manifest " +
                "file name."
            );

            Console.Error.WriteLine();

            Console.Error.WriteLine(
                "Usage:"
            );

            Console.Error.WriteLine(
                "  casecompat aggregate-namespace-manifest " +
                "<Skyrim Data directory> <direct Data child namespace> " +
                "<output directory> [manifest file name]"
            );

            return 2;
        }

        string namespaceName =
            args[2];

        string manifestChildName =
            args.Length == 5
                ? args[4]
                : DefaultManifestName;

        if (
            string.IsNullOrWhiteSpace(
                namespaceName) ||
            namespaceName.Contains('/') ||
            namespaceName.Contains('\\'))
        {
            Console.Error.WriteLine(
                "Aggregate namespace name must identify exactly one " +
                "direct Skyrim Data child."
            );

            return 2;
        }

        if (
            !IsValidManifestChildName(
                manifestChildName))
        {
            Console.Error.WriteLine(
                "Aggregate namespace manifest file name must identify " +
                "exactly one direct child and cannot be '.', '..', " +
                "or contain path separators or NUL."
            );

            return 2;
        }

        string fullDataRoot;
        string fullOutputDirectory;

        try
        {
            fullDataRoot =
                Path.GetFullPath(
                    args[1]
                );

            fullOutputDirectory =
                Path.GetFullPath(
                    args[3]
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Aggregate namespace manifest path error: {ex.Message}"
            );

            return 3;
        }

        /*
         * Evidence metadata must never be published inside Skyrim Data.
         *
         * The output directory is opened and retained before the potentially
         * long namespace/content observation phase. All later publication and
         * readback authority comes from that retained descriptor, not from
         * re-resolving the output path string.
         */
        if (
            IsPathAtOrBelow(
                fullDataRoot,
                fullOutputDirectory))
        {
            Console.Error.WriteLine(
                "Aggregate namespace manifest output directory must be " +
                "outside the Skyrim Data directory."
            );

            return 3;
        }

        LinuxNoFollowPathOpenResult outputOpen;

        try
        {
            outputOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    fullOutputDirectory
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                "Aggregate namespace manifest output directory open " +
                $"error: {ex.Message}"
            );

            return 3;
        }

        if (
            !outputOpen.Success ||
            outputOpen.OpenedPath is not
                LinuxNoFollowPathHandle openedOutputDirectory)
        {
            Console.Error.WriteLine(
                "Aggregate namespace manifest output directory could " +
                "not be opened safely."
            );

            Console.Error.WriteLine(
                outputOpen.Error ??
                outputOpen.State.ToString()
            );

            return 3;
        }

        using LinuxNoFollowPathHandle outputDirectory =
            openedOutputDirectory;

        WindowsNamespaceAnalysis namespaceAnalysis;

        try
        {
            namespaceAnalysis =
                WindowsNamespaceAnalyzer.Analyze(
                    fullDataRoot,
                    namespaceName
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Aggregate namespace analysis error: {ex.Message}"
            );

            return 4;
        }

        if (!namespaceAnalysis.Complete)
        {
            Console.Error.WriteLine(
                "Aggregate namespace analysis is incomplete."
            );

            PrintErrors(
                namespaceAnalysis.Errors
            );

            return 4;
        }

        WindowsNamespaceRegularFileContentAnalysis
            contentAnalysis;

        try
        {
            contentAnalysis =
                contentAnalysisOverride?.Invoke(
                    namespaceAnalysis
                ) ??
                WindowsNamespaceRegularFileContentAnalyzer
                    .Analyze(
                        namespaceAnalysis
                    );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Aggregate namespace content analysis error: {ex.Message}"
            );

            return 5;
        }

        if (!contentAnalysis.Complete)
        {
            Console.Error.WriteLine(
                "Aggregate namespace stable content evidence is incomplete."
            );

            PrintErrors(
                contentAnalysis.Errors
            );

            return 5;
        }

        DataRelativePathAggregateNamespaceManifestRecord
            manifest;

        byte[] intendedBytes;
        string intendedSha256;

        DateTimeOffset createdUtc =
            createdUtcOverride ??
            DateTimeOffset.UtcNow;

        try
        {
            manifest =
                WindowsNamespaceAggregateManifestProjector
                    .Project(
                        namespaceAnalysis,
                        contentAnalysis,
                        createdUtc
                    );

            intendedBytes =
                DataRelativePathAggregateNamespaceManifestJson
                    .Serialize(
                        manifest
                    );

            intendedSha256 =
                Convert.ToHexString(
                    SHA256.HashData(
                        intendedBytes
                    )
                );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                "Aggregate namespace manifest projection/serialization " +
                $"error: {ex.Message}"
            );

            return 6;
        }

        DataRelativePathAggregateNamespaceManifestWriterResult
            write;

        try
        {
            write =
                DataRelativePathAggregateNamespaceManifestWriter
                    .CreateInitial(
                        outputDirectory,
                        manifestChildName,
                        manifest
                    );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Aggregate namespace manifest publication error: {ex.Message}"
            );

            return 7;
        }

        if (!write.Success)
        {
            Console.Error.WriteLine(
                "Aggregate namespace manifest was not durably published."
            );

            Console.Error.WriteLine(
                $"State: {write.State}"
            );

            if (
                !string.IsNullOrWhiteSpace(
                    write.Error))
            {
                Console.Error.WriteLine(
                    write.Error
                );
            }

            if (write.ManifestEntryChanged)
            {
                Console.Error.WriteLine(
                    "Warning: the manifest directory entry may now exist " +
                    "even though durable creation could not be proven."
                );
            }

            return 7;
        }

        try
        {
            afterManifestPublishBeforeRead?.Invoke(
                outputDirectory,
                manifestChildName,
                manifest
            );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                "Aggregate namespace manifest post-publication " +
                $"verification seam failed: {ex.Message}"
            );

            Console.Error.WriteLine(
                "The manifest was already durably published before this " +
                "verification failure."
            );

            return 8;
        }

        DataRelativePathAggregateNamespaceManifestReaderResult
            read;

        try
        {
            read =
                DataRelativePathAggregateNamespaceManifestReader
                    .Read(
                        outputDirectory,
                        manifestChildName
                    );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Aggregate namespace manifest readback error: {ex.Message}"
            );

            return 8;
        }

        if (!read.Success)
        {
            Console.Error.WriteLine(
                "Aggregate namespace manifest readback validation failed."
            );

            Console.Error.WriteLine(
                $"State: {read.State}"
            );

            if (
                !string.IsNullOrWhiteSpace(
                    read.Error))
            {
                Console.Error.WriteLine(
                    read.Error
                );
            }

            return 8;
        }

        if (
            read.Length !=
                intendedBytes.LongLength ||
            !string.Equals(
                read.ManifestSha256,
                intendedSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine(
                "Aggregate namespace manifest exact-byte readback mismatch."
            );

            Console.Error.WriteLine(
                $"Expected length: {intendedBytes.LongLength}"
            );

            Console.Error.WriteLine(
                $"Observed length: {read.Length?.ToString() ?? "<missing>"}"
            );

            Console.Error.WriteLine(
                $"Expected SHA-256: {intendedSha256}"
            );

            Console.Error.WriteLine(
                $"Observed SHA-256: {read.ManifestSha256 ?? "<missing>"}"
            );

            return 8;
        }

        Console.WriteLine(
            "CaseCompat Aggregate Namespace Manifest"
        );

        Console.WriteLine(
            "======================================="
        );

        Console.WriteLine();

        Console.WriteLine(
            $"Data root:       {fullDataRoot}"
        );

        Console.WriteLine(
            $"Namespace:       {namespaceName}"
        );

        Console.WriteLine(
            $"Logical root:    {manifest.RootWindowsLogicalPath}"
        );

        Console.WriteLine(
            $"Output:          {fullOutputDirectory}"
        );

        Console.WriteLine(
            $"Manifest:        {manifestChildName}"
        );

        Console.WriteLine(
            $"Logical leaves:  {manifest.LogicalLeaves.Count:N0}"
        );

        Console.WriteLine(
            $"Manifest bytes:  {read.Length:N0}"
        );

        Console.WriteLine(
            $"Manifest SHA256: {read.ManifestSha256}"
        );

        Console.WriteLine();

        Console.WriteLine(
            "This artifact is durable namespace evidence only."
        );

        Console.WriteLine(
            "It grants no repair planning, apply, rollback, recovery, " +
            "or execution authority."
        );

        return 0;
    }

    private static void PrintErrors(
        IReadOnlyList<string> errors)
    {
        foreach (
            string error
            in errors)
        {
            Console.Error.WriteLine(
                $"  {error}"
            );
        }
    }

    private static bool
        IsValidManifestChildName(
            string? childName)
    {
        if (
            string.IsNullOrEmpty(
                childName) ||
            childName is "." or "..")
        {
            return false;
        }

        return
            !childName.Contains('/') &&
            !childName.Contains('\\') &&
            !childName.Contains('\0');
    }

    private static bool IsPathAtOrBelow(
        string rootPath,
        string candidatePath)
    {
        string relative =
            Path.GetRelativePath(
                rootPath,
                candidatePath
            );

        if (
            string.Equals(
                relative,
                ".",
                StringComparison.Ordinal))
        {
            return true;
        }

        if (Path.IsPathRooted(relative))
        {
            return false;
        }

        return
            !string.Equals(
                relative,
                "..",
                StringComparison.Ordinal) &&
            !relative.StartsWith(
                ".." +
                Path.DirectorySeparatorChar,
                StringComparison.Ordinal) &&
            !relative.StartsWith(
                ".." +
                Path.AltDirectorySeparatorChar,
                StringComparison.Ordinal);
    }
}
