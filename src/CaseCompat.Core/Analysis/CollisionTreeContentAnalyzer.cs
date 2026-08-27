using System.Security.Cryptography;

namespace CaseCompat.Core.Analysis;

public static class CollisionTreeContentAnalyzer
{
    public static CollisionTreeContentAnalysis Analyze(
        CollisionTreeNamespaceAnalysis namespaceAnalysis)
    {
        ArgumentNullException.ThrowIfNull(
            namespaceAnalysis
        );

        CollisionTreeAssetContentAnalysis[] assets =
            namespaceAnalysis.Assets
                .Select(AnalyzeAsset)
                .ToArray();

        return new CollisionTreeContentAnalysis(
            NamespaceAnalysis: namespaceAnalysis,
            Assets: assets
        );
    }

    private static CollisionTreeAssetContentAnalysis AnalyzeAsset(
        CollisionTreeLogicalAsset asset)
    {
        if (asset.AmbiguousWithinBranch)
        {
            return CreateWithoutHashes(
                asset,
                CollisionTreeAssetContentState
                    .AmbiguousWithinBranch
            );
        }

        if (asset.Occurrences.Count == 1)
        {
            return CreateWithoutHashes(
                asset,
                CollisionTreeAssetContentState
                    .SingleOccurrence
            );
        }

        int distinctSizes =
            asset.Occurrences
                .Select(occurrence =>
                    occurrence.File.Size)
                .Distinct()
                .Take(2)
                .Count();

        if (distinctSizes > 1)
        {
            return CreateWithoutHashes(
                asset,
                CollisionTreeAssetContentState
                    .DifferentSize
            );
        }

        var hashedOccurrences =
            new List<CollisionTreeContentOccurrence>();

        try
        {
            foreach (
                CollisionTreeAssetOccurrence occurrence
                in asset.Occurrences)
            {
                string hash =
                    ComputeSha256(
                        occurrence.File.PhysicalPath
                    );

                hashedOccurrences.Add(
                    new CollisionTreeContentOccurrence(
                        Occurrence: occurrence,
                        Sha256: hash
                    )
                );
            }
        }
        catch (Exception ex)
        {
            return new CollisionTreeAssetContentAnalysis(
                NamespaceAsset: asset,
                State:
                    CollisionTreeAssetContentState.Unreadable,
                Occurrences:
                    hashedOccurrences.ToArray(),
                Error: ex.Message
            );
        }

        int distinctHashes =
            hashedOccurrences
                .Select(occurrence =>
                    occurrence.Sha256)
                .Distinct(StringComparer.Ordinal)
                .Take(2)
                .Count();

        CollisionTreeAssetContentState state =
            distinctHashes == 1
                ? CollisionTreeAssetContentState.Identical
                : CollisionTreeAssetContentState.DifferentContent;

        return new CollisionTreeAssetContentAnalysis(
            NamespaceAsset: asset,
            State: state,
            Occurrences: hashedOccurrences.ToArray(),
            Error: null
        );
    }

    private static CollisionTreeAssetContentAnalysis
        CreateWithoutHashes(
            CollisionTreeLogicalAsset asset,
            CollisionTreeAssetContentState state)
    {
        CollisionTreeContentOccurrence[] occurrences =
            asset.Occurrences
                .Select(occurrence =>
                    new CollisionTreeContentOccurrence(
                        Occurrence: occurrence,
                        Sha256: null
                    )
                )
                .ToArray();

        return new CollisionTreeAssetContentAnalysis(
            NamespaceAsset: asset,
            State: state,
            Occurrences: occurrences,
            Error: null
        );
    }

    private static string ComputeSha256(
        string path)
    {
        using FileStream stream =
            new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read
            );

        byte[] hash =
            SHA256.HashData(stream);

        return Convert.ToHexString(hash);
    }
}
