namespace CaseCompat.Core.Analysis;

public static class CollisionTreeNamespaceAnalyzer
{
    public static CollisionTreeNamespaceAnalysis Analyze(
        CollisionTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        var branches =
            new List<CollisionTreeBranch>();

        var errors =
            new List<string>();

        int branchIndex = 0;

        foreach (
            var member
            in tree.Root.Collision.Members)
        {
            if (!member.IsDirectory ||
                member.IsSymbolicLink)
            {
                errors.Add(
                    $"Tree member is not a safe physical directory: " +
                    member.FullPath
                );

                continue;
            }

            BranchInventory inventory;

            try
            {
                inventory =
                    BranchInventoryScanner.Scan(
                        member.FullPath
                    );
            }
            catch (Exception ex)
            {
                errors.Add(
                    $"{member.FullPath}: {ex.Message}"
                );

                continue;
            }

            foreach (string error in inventory.Errors)
            {
                errors.Add(
                    $"{member.FullPath}: {error}"
                );
            }

            branches.Add(
                new CollisionTreeBranch(
                    Index: branchIndex,
                    Root: member,
                    Inventory: inventory
                )
            );

            branchIndex++;
        }

        var occurrences =
            branches
                .SelectMany(branch =>
                    branch.Inventory.Files.Select(file =>
                        new CollisionTreeAssetOccurrence(
                            BranchIndex: branch.Index,
                            File: file
                        )
                    )
                )
                .ToArray();

        CollisionTreeLogicalAsset[] assets =
            occurrences
                .GroupBy(
                    occurrence =>
                        occurrence.File.LogicalPath
                )
                .Select(group =>
                {
                    CollisionTreeAssetOccurrence[] members =
                        group.ToArray();

                    int branchesPresent =
                        members
                            .Select(member =>
                                member.BranchIndex)
                            .Distinct()
                            .Count();

                    bool ambiguousWithinBranch =
                        members
                            .GroupBy(member =>
                                member.BranchIndex)
                            .Any(branchGroup =>
                                branchGroup.Count() > 1);

                    return new CollisionTreeLogicalAsset(
                        LogicalPath: group.Key,
                        Occurrences: members
                            .OrderBy(member =>
                                member.BranchIndex)
                            .ThenBy(
                                member =>
                                    member.File.RelativePath,
                                StringComparer.Ordinal)
                            .ToArray(),
                        BranchesPresent:
                            branchesPresent,
                        PresentInEveryBranch:
                            branches.Count > 0 &&
                            branchesPresent ==
                                branches.Count,
                        AmbiguousWithinBranch:
                            ambiguousWithinBranch
                    );
                })
                .OrderBy(
                    asset => asset.LogicalPath.Value,
                    StringComparer.Ordinal)
                .ToArray();

        return new CollisionTreeNamespaceAnalysis(
            Tree: tree,
            Branches: branches.ToArray(),
            Assets: assets,
            PresentInEveryBranch:
                assets.Count(asset =>
                    asset.PresentInEveryBranch),
            PartialPresence:
                assets.Count(asset =>
                    !asset.PresentInEveryBranch),
            AmbiguousLogicalAssets:
                assets.Count(asset =>
                    asset.AmbiguousWithinBranch),
            Errors: errors.ToArray()
        );
    }
}
