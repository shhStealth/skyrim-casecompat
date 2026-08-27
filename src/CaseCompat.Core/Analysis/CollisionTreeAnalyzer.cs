using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Core.Analysis;

public static class CollisionTreeAnalyzer
{
    public static CollisionTreeAnalysis Analyze(
        IReadOnlyList<RecursiveCollisionFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);

        var directoryFindings = findings
            .Where(IsPureDirectoryCollision)
            .ToArray();

        var fileFindings = findings
            .Where(IsPureFileCollision)
            .ToArray();

        var otherFindings = findings
            .Where(f =>
                !IsPureDirectoryCollision(f) &&
                !IsPureFileCollision(f))
            .ToArray();

        var roots = directoryFindings
            .Where(candidate =>
                !directoryFindings.Any(other =>
                    !ReferenceEquals(candidate, other) &&
                    IsFindingInsideDirectoryCollision(
                        candidate,
                        other)))
            .ToArray();

        var trees = new List<CollisionTree>();

        foreach (RecursiveCollisionFinding root in roots)
        {
            RecursiveCollisionFinding[] descendants = findings
                .Where(finding =>
                    !ReferenceEquals(finding, root) &&
                    IsFindingInsideDirectoryCollision(
                        finding,
                        root))
                .OrderBy(finding => finding.Depth)
                .ThenBy(
                    finding => finding.Collision.ParentPath,
                    StringComparer.Ordinal)
                .ThenBy(
                    finding => finding.Collision.LogicalName,
                    StringComparer.Ordinal)
                .ToArray();

            trees.Add(
                new CollisionTree(
                    Root: root,
                    Descendants: descendants
                )
            );
        }

        RecursiveCollisionFinding[] assigned =
            trees
                .SelectMany(tree =>
                    tree.Descendants.Append(tree.Root))
                .Distinct()
                .ToArray();

        RecursiveCollisionFinding[] unassigned = findings
            .Where(finding => !assigned.Contains(finding))
            .ToArray();

        return new CollisionTreeAnalysis(
            RawFindings: findings.Count,
            DirectoryCollisionFindings:
                directoryFindings.Length,
            FileCollisionFindings:
                fileFindings.Length,
            OtherCollisionFindings:
                otherFindings.Length,
            Trees: trees
                .OrderBy(
                    tree => tree.Root.Collision.ParentPath,
                    StringComparer.Ordinal)
                .ThenBy(
                    tree => tree.Root.Collision.LogicalName,
                    StringComparer.Ordinal)
                .ToArray(),
            UnassignedFindings: unassigned
        );
    }

    private static bool IsPureDirectoryCollision(
        RecursiveCollisionFinding finding)
    {
        return finding.Collision.Members.Count > 0 &&
               finding.Collision.Members.All(member =>
                   member.IsDirectory &&
                   !member.IsSymbolicLink);
    }

    private static bool IsPureFileCollision(
        RecursiveCollisionFinding finding)
    {
        return finding.Collision.Members.Count > 0 &&
               finding.Collision.Members.All(member =>
                   !member.IsDirectory &&
                   !member.IsSymbolicLink);
    }

    private static bool IsFindingInsideDirectoryCollision(
        RecursiveCollisionFinding candidate,
        RecursiveCollisionFinding possibleAncestor)
    {
        foreach (
            DirectoryCollisionMember member
            in possibleAncestor.Collision.Members)
        {
            if (!member.IsDirectory ||
                member.IsSymbolicLink)
            {
                continue;
            }

            if (IsSameOrDescendant(
                    candidate.Collision.ParentPath,
                    member.FullPath))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSameOrDescendant(
        string candidate,
        string ancestor)
    {
        string relative = Path.GetRelativePath(
            ancestor,
            candidate
        );

        if (Path.IsPathRooted(relative))
        {
            return false;
        }

        if (relative == ".")
        {
            return true;
        }

        if (relative == "..")
        {
            return false;
        }

        string parentPrefix =
            ".." + Path.DirectorySeparatorChar;

        return !relative.StartsWith(
            parentPrefix,
            StringComparison.Ordinal
        );
    }
}
