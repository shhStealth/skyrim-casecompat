namespace CaseCompat.Filesystem.Linux;

public static class DirectoryCollisionScanner
{
    public static IReadOnlyList<DirectoryCaseCollision> Scan(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException(
                "A directory path is required.",
                nameof(directory)
            );
        }

        string fullPath = Path.GetFullPath(directory);

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException(fullPath);
        }

        var groups = new Dictionary<
            string,
            List<DirectoryCollisionMember>
        >(StringComparer.Ordinal);

        foreach (string entry in Directory.EnumerateFileSystemEntries(fullPath))
        {
            string name = Path.GetFileName(entry);

            // This is only a grouping key.
            // It must never replace the original physical spelling.
            string logicalName = name.ToUpperInvariant();

            if (!groups.TryGetValue(logicalName, out var members))
            {
                members = [];
                groups.Add(logicalName, members);
            }

            members.Add(
                new DirectoryCollisionMember(
                    Name: name,
                    FullPath: entry,
                    IsDirectory: Directory.Exists(entry)
                )
            );
        }

        return groups
            .Where(group =>
                group.Value
                    .Select(member => member.Name)
                    .Distinct(StringComparer.Ordinal)
                    .Count() > 1
            )
            .Select(group =>
                new DirectoryCaseCollision(
                    ParentPath: fullPath,
                    LogicalName: group.Key,
                    Members: group.Value
                        .OrderBy(member => member.Name, StringComparer.Ordinal)
                        .ToArray()
                )
            )
            .OrderBy(
                collision => collision.LogicalName,
                StringComparer.Ordinal
            )
            .ToArray();
    }
}
