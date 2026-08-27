namespace CaseCompat.Filesystem.Linux;

public static class RecursiveCollisionScanner
{
    private readonly record struct DirectoryIdentity(
        uint DeviceMajor,
        uint DeviceMinor,
        ulong Inode
    );

    public static RecursiveCollisionScanResult Scan(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException(
                "A root directory is required.",
                nameof(rootDirectory)
            );
        }

        string rootPath = Path.GetFullPath(rootDirectory);

        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException(rootPath);
        }

        var findings = new List<RecursiveCollisionFinding>();
        var errors = new List<RecursiveScanError>();

        var visited = new HashSet<DirectoryIdentity>();

        var pending = new Stack<(string Path, int Depth)>();
        pending.Push((rootPath, 0));

        long directoriesScanned = 0;
        long entriesScanned = 0;
        long symbolicLinksSkipped = 0;
        long duplicatePhysicalDirectoriesSkipped = 0;

        while (pending.Count > 0)
        {
            (string currentPath, int depth) = pending.Pop();

            LinuxFileIdentityResult identity =
                LinuxFileIdentity.Inspect(currentPath);

            if (!identity.Success ||
                identity.DeviceMajor is null ||
                identity.DeviceMinor is null ||
                identity.Inode is null)
            {
                errors.Add(
                    new RecursiveScanError(
                        currentPath,
                        identity.Error ?? "Physical identity unavailable."
                    )
                );

                continue;
            }

            var physicalIdentity = new DirectoryIdentity(
                identity.DeviceMajor.Value,
                identity.DeviceMinor.Value,
                identity.Inode.Value
            );

            if (!visited.Add(physicalIdentity))
            {
                duplicatePhysicalDirectoriesSkipped++;
                continue;
            }

            directoriesScanned++;

            DirectoryCasefoldResult casefold =
                LinuxDirectoryFlags.Inspect(currentPath);

            try
            {
                IReadOnlyList<DirectoryCaseCollision> collisions =
                    DirectoryCollisionScanner.Scan(currentPath);

                foreach (DirectoryCaseCollision collision in collisions)
                {
                    findings.Add(
                        new RecursiveCollisionFinding(
                            Depth: depth,
                            Collision: collision,
                            ParentCasefoldEnabled:
                                casefold.CasefoldEnabled,
                            ParentRawFlags:
                                casefold.RawFlags
                        )
                    );
                }
            }
            catch (Exception ex)
            {
                errors.Add(
                    new RecursiveScanError(
                        currentPath,
                        $"Collision scan failed: {ex.Message}"
                    )
                );
            }

            IEnumerable<string> entries;

            try
            {
                entries =
                    Directory
                        .EnumerateFileSystemEntries(currentPath)
                        .ToArray();
            }
            catch (Exception ex)
            {
                errors.Add(
                    new RecursiveScanError(
                        currentPath,
                        $"Directory enumeration failed: {ex.Message}"
                    )
                );

                continue;
            }

            foreach (string entry in entries)
            {
                entriesScanned++;

                FileAttributes attributes;

                try
                {
                    attributes = File.GetAttributes(entry);
                }
                catch (Exception ex)
                {
                    errors.Add(
                        new RecursiveScanError(
                            entry,
                            $"Attribute inspection failed: {ex.Message}"
                        )
                    );

                    continue;
                }

                bool isDirectory =
                    (attributes & FileAttributes.Directory) != 0;

                bool isSymbolicLink =
                    (attributes & FileAttributes.ReparsePoint) != 0;

                if (isSymbolicLink)
                {
                    symbolicLinksSkipped++;
                    continue;
                }

                if (isDirectory)
                {
                    pending.Push((entry, depth + 1));
                }
            }
        }

        return new RecursiveCollisionScanResult(
            RootPath: rootPath,
            DirectoriesScanned: directoriesScanned,
            EntriesScanned: entriesScanned,
            SymbolicLinksSkipped: symbolicLinksSkipped,
            DuplicatePhysicalDirectoriesSkipped:
                duplicatePhysicalDirectoriesSkipped,
            Findings: findings
                .OrderBy(
                    finding => finding.Collision.ParentPath,
                    StringComparer.Ordinal
                )
                .ThenBy(
                    finding => finding.Collision.LogicalName,
                    StringComparer.Ordinal
                )
                .ToArray(),
            Errors: errors.ToArray()
        );
    }
}
