using CaseCompat.Core.Analysis;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public class CollisionTreeNamespaceBatchAnalyzerTests
{
    [Fact]
    public void Analyze_SummarizesIndependentTrees()
    {
        string root = CreateTempDirectory();

        try
        {
            string fooA = Path.Combine(root, "Foo");
            string fooB = Path.Combine(root, "foo");

            Directory.CreateDirectory(fooA);
            Directory.CreateDirectory(fooB);

            File.WriteAllText(
                Path.Combine(fooA, "Shared.nif"),
                "one"
            );

            File.WriteAllText(
                Path.Combine(fooB, "shared.NIF"),
                "two"
            );

            File.WriteAllText(
                Path.Combine(fooA, "OnlyA.nif"),
                "extra"
            );

            string normal =
                Path.Combine(root, "normal");

            string barA =
                Path.Combine(normal, "Bar");

            string barB =
                Path.Combine(normal, "bar");

            Directory.CreateDirectory(barA);
            Directory.CreateDirectory(barB);

            File.WriteAllText(
                Path.Combine(barA, "Mesh.nif"),
                "one"
            );

            File.WriteAllText(
                Path.Combine(barB, "mesh.NIF"),
                "two"
            );

            RecursiveCollisionScanResult scan =
                RecursiveCollisionScanner.Scan(root);

            CollisionTreeAnalysis trees =
                CollisionTreeAnalyzer.Analyze(
                    scan.Findings
                );

            CollisionTreeNamespaceBatchAnalysis batch =
                CollisionTreeNamespaceBatchAnalyzer.Analyze(
                    trees
                );

            Assert.Equal(2, batch.Trees.Count);
            Assert.Equal(1, batch.DivergentTrees);
            Assert.Equal(
                1,
                batch.EquivalentNamespaceTrees
            );
            Assert.Equal(0, batch.AmbiguousTrees);
            Assert.Equal(0, batch.TreesWithErrors);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Analyze_PreservesThreeBranchTree()
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

            RecursiveCollisionScanResult scan =
                RecursiveCollisionScanner.Scan(root);

            CollisionTreeAnalysis trees =
                CollisionTreeAnalyzer.Analyze(
                    scan.Findings
                );

            CollisionTreeNamespaceBatchAnalysis batch =
                CollisionTreeNamespaceBatchAnalyzer.Analyze(
                    trees
                );

            CollisionTreeNamespaceBatchItem item =
                Assert.Single(batch.Trees);

            Assert.Equal(
                3,
                item.Analysis.Branches.Count
            );

            Assert.Equal(
                1,
                item.Analysis.PresentInEveryBranch
            );

            Assert.Equal(
                0,
                item.Analysis.PartialPresence
            );
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"casecompat-batch-{Guid.NewGuid():N}"
        );

        Directory.CreateDirectory(path);
        return path;
    }
}
