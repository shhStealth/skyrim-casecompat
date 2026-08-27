using CaseCompat.Core.Analysis;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public class CollisionTreeNamespaceAnalyzerTests
{
    [Fact]
    public void Analyze_SupportsThreePhysicalBranches()
    {
        string root = CreateTempDirectory();

        try
        {
            string a = Path.Combine(root, "Foo");
            string b = Path.Combine(root, "foo");
            string c = Path.Combine(root, "FOO");

            Directory.CreateDirectory(a);
            Directory.CreateDirectory(b);
            Directory.CreateDirectory(c);

            File.WriteAllText(
                Path.Combine(a, "Shared.nif"),
                "a"
            );

            File.WriteAllText(
                Path.Combine(b, "shared.NIF"),
                "b"
            );

            File.WriteAllText(
                Path.Combine(c, "SHARED.NIF"),
                "c"
            );

            File.WriteAllText(
                Path.Combine(a, "OnlyA.nif"),
                "a"
            );

            CollisionTreeNamespaceAnalysis result =
                AnalyzeSingleTree(root);

            Assert.Equal(3, result.Branches.Count);
            Assert.Equal(1, result.PresentInEveryBranch);
            Assert.Equal(1, result.PartialPresence);
            Assert.True(result.NamespaceDiverges);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Analyze_DetectsWithinBranchLogicalAmbiguity()
    {
        string root = CreateTempDirectory();

        try
        {
            string a = Path.Combine(root, "Foo");
            string b = Path.Combine(root, "foo");

            Directory.CreateDirectory(a);
            Directory.CreateDirectory(b);

            File.WriteAllText(
                Path.Combine(a, "Mesh.nif"),
                "one"
            );

            File.WriteAllText(
                Path.Combine(a, "mesh.NIF"),
                "two"
            );

            File.WriteAllText(
                Path.Combine(b, "MESH.nif"),
                "three"
            );

            CollisionTreeNamespaceAnalysis result =
                AnalyzeSingleTree(root);

            Assert.Equal(
                1,
                result.AmbiguousLogicalAssets
            );

            Assert.True(result.HasAmbiguity);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Analyze_EquivalentNamespaceAcrossThreeBranches()
    {
        string root = CreateTempDirectory();

        try
        {
            string a = Path.Combine(root, "Foo");
            string b = Path.Combine(root, "foo");
            string c = Path.Combine(root, "FOO");

            Directory.CreateDirectory(a);
            Directory.CreateDirectory(b);
            Directory.CreateDirectory(c);

            File.WriteAllText(
                Path.Combine(a, "Mesh.nif"),
                "a"
            );

            File.WriteAllText(
                Path.Combine(b, "mesh.NIF"),
                "b"
            );

            File.WriteAllText(
                Path.Combine(c, "MESH.NIF"),
                "c"
            );

            CollisionTreeNamespaceAnalysis result =
                AnalyzeSingleTree(root);

            Assert.Single(result.Assets);
            Assert.Equal(1, result.PresentInEveryBranch);
            Assert.Equal(0, result.PartialPresence);
            Assert.False(result.NamespaceDiverges);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static CollisionTreeNamespaceAnalysis
        AnalyzeSingleTree(string root)
    {
        RecursiveCollisionScanResult scan =
            RecursiveCollisionScanner.Scan(root);

        CollisionTreeAnalysis trees =
            CollisionTreeAnalyzer.Analyze(
                scan.Findings
            );

        CollisionTree tree =
            Assert.Single(trees.Trees);

        return CollisionTreeNamespaceAnalyzer.Analyze(
            tree
        );
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"casecompat-tree-namespace-{Guid.NewGuid():N}"
        );

        Directory.CreateDirectory(path);
        return path;
    }
}
