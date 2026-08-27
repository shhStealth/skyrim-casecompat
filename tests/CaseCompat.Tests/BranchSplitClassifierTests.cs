using CaseCompat.Core.Analysis;

namespace CaseCompat.Tests;

public class BranchSplitClassifierTests
{
    [Fact]
    public void Classify_Equivalent_WhenNamespacesAndContentMatch()
    {
        BranchSplitClassification result =
            Analyze(
                filesA:
                [
                    ("Mesh.nif", "same")
                ],
                filesB:
                [
                    ("mesh.NIF", "same")
                ]
            );

        Assert.Equal(
            BranchSplitState.Equivalent,
            result.State
        );
    }

    [Fact]
    public void Classify_OneSidedDivergence()
    {
        BranchSplitClassification result =
            Analyze(
                filesA:
                [
                    ("Shared.nif", "same"),
                    ("Extra.nif", "extra")
                ],
                filesB:
                [
                    ("shared.NIF", "same")
                ]
            );

        Assert.Equal(
            BranchSplitState.OneSidedDivergence,
            result.State
        );
    }

    [Fact]
    public void Classify_BidirectionalDivergence()
    {
        BranchSplitClassification result =
            Analyze(
                filesA:
                [
                    ("OnlyA.nif", "a")
                ],
                filesB:
                [
                    ("OnlyB.nif", "b")
                ]
            );

        Assert.Equal(
            BranchSplitState.BidirectionalDivergence,
            result.State
        );
    }

    [Fact]
    public void Classify_ContentConflict_TakesPriority()
    {
        BranchSplitClassification result =
            Analyze(
                filesA:
                [
                    ("Mesh.nif", "AAAA"),
                    ("OnlyA.nif", "a")
                ],
                filesB:
                [
                    ("mesh.NIF", "BBBB"),
                    ("OnlyB.nif", "b")
                ]
            );

        Assert.Equal(
            BranchSplitState.ContentConflict,
            result.State
        );

        Assert.Equal(
            1,
            result.DifferentContentOverlaps
        );
    }

    private static BranchSplitClassification Analyze(
        (string Path, string Content)[] filesA,
        (string Path, string Content)[] filesB)
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"casecompat-classification-{Guid.NewGuid():N}"
        );

        string a = Path.Combine(root, "A");
        string b = Path.Combine(root, "B");

        Directory.CreateDirectory(a);
        Directory.CreateDirectory(b);

        try
        {
            WriteFiles(a, filesA);
            WriteFiles(b, filesB);

            BranchComparison namespaceComparison =
                BranchComparer.Compare(
                    BranchInventoryScanner.Scan(a),
                    BranchInventoryScanner.Scan(b)
                );

            BranchContentComparison contentComparison =
                BranchContentComparer.Compare(
                    namespaceComparison
                );

            return BranchSplitClassifier.Classify(
                contentComparison
            );
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true
            );
        }
    }

    private static void WriteFiles(
        string root,
        IEnumerable<(string Path, string Content)> files)
    {
        foreach ((string path, string content) in files)
        {
            string fullPath =
                Path.Combine(root, path);

            string? directory =
                Path.GetDirectoryName(fullPath);

            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                fullPath,
                content
            );
        }
    }
}
