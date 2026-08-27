using System.Security.Cryptography;

namespace CaseCompat.Core.Analysis;

public static class BranchContentComparer
{
    public static BranchContentComparison Compare(
        BranchComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);

        var results =
            new List<BranchContentFileComparison>();

        foreach (
            BranchFileComparison file
            in comparison.Files)
        {
            if (file.Presence !=
                BranchFilePresence.PresentInBoth)
            {
                results.Add(
                    new BranchContentFileComparison(
                        NamespaceComparison: file,
                        ContentState:
                            BranchContentState.NotApplicable,
                        Sha256A: null,
                        Sha256B: null
                    )
                );

                continue;
            }

            if (file.FileA is null ||
                file.FileB is null)
            {
                throw new InvalidOperationException(
                    "PresentInBoth requires both physical files."
                );
            }

            if (file.FileA.Size != file.FileB.Size)
            {
                results.Add(
                    new BranchContentFileComparison(
                        NamespaceComparison: file,
                        ContentState:
                            BranchContentState.DifferentSize,
                        Sha256A: null,
                        Sha256B: null
                    )
                );

                continue;
            }

            string hashA =
                ComputeSha256(file.FileA.PhysicalPath);

            string hashB =
                ComputeSha256(file.FileB.PhysicalPath);

            results.Add(
                new BranchContentFileComparison(
                    NamespaceComparison: file,
                    ContentState:
                        hashA == hashB
                            ? BranchContentState.Identical
                            : BranchContentState.DifferentContent,
                    Sha256A: hashA,
                    Sha256B: hashB
                )
            );
        }

        return new BranchContentComparison(
            NamespaceComparison: comparison,
            Files: results
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
