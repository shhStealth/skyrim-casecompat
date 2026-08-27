using CaseCompat.Core.Analysis;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public class CollisionTreeContentAnalyzerTests
{
    [Fact]
    public void Analyze_SingleOccurrence_DoesNotHash()
    {
        using TestTree tree = TestTree.Create();

        File.WriteAllText(
            Path.Combine(tree.A, "OnlyA.nif"),
            "one"
        );

        CollisionTreeAssetContentAnalysis asset =
            Assert.Single(Analyze(tree.Root).Assets);

        Assert.Equal(
            CollisionTreeAssetContentState.SingleOccurrence,
            asset.State
        );

        Assert.All(
            asset.Occurrences,
            occurrence =>
                Assert.Null(occurrence.Sha256)
        );
    }

    [Fact]
    public void Analyze_IdenticalAcrossThreeBranches()
    {
        using TestTree tree =
            TestTree.Create(includeThirdBranch: true);

        File.WriteAllText(
            Path.Combine(tree.A, "Mesh.nif"),
            "same bytes"
        );

        File.WriteAllText(
            Path.Combine(tree.B, "mesh.NIF"),
            "same bytes"
        );

        File.WriteAllText(
            Path.Combine(tree.C!, "MESH.NIF"),
            "same bytes"
        );

        CollisionTreeAssetContentAnalysis asset =
            Assert.Single(Analyze(tree.Root).Assets);

        Assert.Equal(
            CollisionTreeAssetContentState.Identical,
            asset.State
        );

        Assert.All(
            asset.Occurrences,
            occurrence =>
                Assert.NotNull(occurrence.Sha256)
        );

        Assert.Single(
            asset.Occurrences
                .Select(occurrence =>
                    occurrence.Sha256)
                .Distinct()
        );
    }

    [Fact]
    public void Analyze_DifferentSizes_DoesNotHash()
    {
        using TestTree tree = TestTree.Create();

        File.WriteAllText(
            Path.Combine(tree.A, "Mesh.nif"),
            "short"
        );

        File.WriteAllText(
            Path.Combine(tree.B, "mesh.NIF"),
            "much longer content"
        );

        CollisionTreeAssetContentAnalysis asset =
            Assert.Single(Analyze(tree.Root).Assets);

        Assert.Equal(
            CollisionTreeAssetContentState.DifferentSize,
            asset.State
        );

        Assert.All(
            asset.Occurrences,
            occurrence =>
                Assert.Null(occurrence.Sha256)
        );
    }

    [Fact]
    public void Analyze_SameSizeDifferentBytes_DetectsConflict()
    {
        using TestTree tree = TestTree.Create();

        File.WriteAllText(
            Path.Combine(tree.A, "Mesh.nif"),
            "AAAA"
        );

        File.WriteAllText(
            Path.Combine(tree.B, "mesh.NIF"),
            "BBBB"
        );

        CollisionTreeAssetContentAnalysis asset =
            Assert.Single(Analyze(tree.Root).Assets);

        Assert.Equal(
            CollisionTreeAssetContentState.DifferentContent,
            asset.State
        );

        Assert.Equal(
            2,
            asset.Occurrences
                .Select(occurrence =>
                    occurrence.Sha256)
                .Distinct()
                .Count()
        );
    }

    [Fact]
    public void Analyze_AmbiguousWithinBranch_TakesPriority()
    {
        using TestTree tree = TestTree.Create();

        File.WriteAllText(
            Path.Combine(tree.A, "Mesh.nif"),
            "one"
        );

        File.WriteAllText(
            Path.Combine(tree.A, "mesh.NIF"),
            "two"
        );

        File.WriteAllText(
            Path.Combine(tree.B, "MESH.nif"),
            "three"
        );

        CollisionTreeAssetContentAnalysis asset =
            Assert.Single(Analyze(tree.Root).Assets);

        Assert.Equal(
            CollisionTreeAssetContentState
                .AmbiguousWithinBranch,
            asset.State
        );

        Assert.All(
            asset.Occurrences,
            occurrence =>
                Assert.Null(occurrence.Sha256)
        );
    }

    private static CollisionTreeContentAnalysis Analyze(
        string root)
    {
        RecursiveCollisionScanResult scan =
            RecursiveCollisionScanner.Scan(root);

        CollisionTreeAnalysis trees =
            CollisionTreeAnalyzer.Analyze(
                scan.Findings
            );

        CollisionTree tree =
            Assert.Single(trees.Trees);

        CollisionTreeNamespaceAnalysis namespaceAnalysis =
            CollisionTreeNamespaceAnalyzer.Analyze(
                tree
            );

        return CollisionTreeContentAnalyzer.Analyze(
            namespaceAnalysis
        );
    }

    private sealed class TestTree : IDisposable
    {
        public string Root { get; }
        public string A { get; }
        public string B { get; }
        public string? C { get; }

        private TestTree(
            string root,
            string a,
            string b,
            string? c)
        {
            Root = root;
            A = a;
            B = b;
            C = c;
        }

        public static TestTree Create(
            bool includeThirdBranch = false)
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                $"casecompat-content-tree-{Guid.NewGuid():N}"
            );

            string a =
                Path.Combine(root, "Foo");

            string b =
                Path.Combine(root, "foo");

            Directory.CreateDirectory(a);
            Directory.CreateDirectory(b);

            string? c = null;

            if (includeThirdBranch)
            {
                c = Path.Combine(root, "FOO");
                Directory.CreateDirectory(c);
            }

            return new TestTree(
                root,
                a,
                b,
                c
            );
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(
                    Root,
                    recursive: true
                );
            }
        }
    }
}
