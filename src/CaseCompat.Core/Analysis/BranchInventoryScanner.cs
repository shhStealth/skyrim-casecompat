namespace CaseCompat.Core.Analysis;

public static class BranchInventoryScanner
{
    public static BranchInventory Scan(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            rootDirectory
        );

        string rootPath =
            Path.GetFullPath(rootDirectory);

        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException(
                rootPath
            );
        }

        var files = new List<BranchFile>();
        var errors = new List<string>();

        var pending = new Stack<string>();
        pending.Push(rootPath);

        long directoriesScanned = 0;
        long symbolicLinksSkipped = 0;

        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            directoriesScanned++;

            string[] entries;

            try
            {
                entries =
                    Directory
                        .EnumerateFileSystemEntries(
                            directory
                        )
                        .ToArray();
            }
            catch (Exception ex)
            {
                errors.Add(
                    $"{directory}: {ex.Message}"
                );

                continue;
            }

            foreach (string entry in entries)
            {
                FileAttributes attributes;

                try
                {
                    attributes =
                        File.GetAttributes(entry);
                }
                catch (Exception ex)
                {
                    errors.Add(
                        $"{entry}: {ex.Message}"
                    );

                    continue;
                }

                bool isDirectory =
                    (attributes &
                     FileAttributes.Directory) != 0;

                bool isSymbolicLink =
                    (attributes &
                     FileAttributes.ReparsePoint) != 0;

                if (isSymbolicLink)
                {
                    symbolicLinksSkipped++;
                    continue;
                }

                if (isDirectory)
                {
                    pending.Push(entry);
                    continue;
                }

                string relative =
                    Path.GetRelativePath(
                        rootPath,
                        entry
                    );

                FileInfo info =
                    new(entry);

                files.Add(
                    new BranchFile(
                        PhysicalPath: entry,
                        RelativePath: relative,
                        LogicalPath:
                            WindowsLogicalPath
                                .FromRelativePath(
                                    relative
                                ),
                        Size: info.Length
                    )
                );
            }
        }

        return new BranchInventory(
            RootPath: rootPath,
            Files: files
                .OrderBy(
                    file => file.LogicalPath.Value,
                    StringComparer.Ordinal
                )
                .ThenBy(
                    file => file.RelativePath,
                    StringComparer.Ordinal
                )
                .ToArray(),
            DirectoriesScanned:
                directoriesScanned,
            SymbolicLinksSkipped:
                symbolicLinksSkipped,
            Errors: errors.ToArray()
        );
    }
}
