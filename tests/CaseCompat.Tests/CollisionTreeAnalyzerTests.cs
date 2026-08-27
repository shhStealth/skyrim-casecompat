using CaseCompat.Core.Analysis;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public class CollisionTreeAnalyzerTests
{
    [Fact]
    public void Analyze_CollapsesNestedSplitsIntoOneTree()
    {
        string root = CreateTempDirectory();

        try
        {
            string upper = Path.Combine(root, "Foo");
            string lower = Path.Combine(root, "foo");

            Directory.CreateDirectory(upper);
            Directory.CreateDirectory(lower);

            Directory.CreateDirectory(
                Path.Combine(upper, "Bar")
            );

            Directory.CreateDirectory(
                Path.Combine(upper, "bar")
            );

            File.WriteAllText(
                Path.Combine(upper, "Mesh.nif"),
                "one"
            );

            File.WriteAllText(
                Path.Combine(upper, "mesh.nif"),
                "two"
            );

            RecursiveCollisionScanResult scan =
                RecursiveCollisionScanner.Scan(root);

            CollisionTreeAnalysis analysis =
                CollisionTreeAnalyzer.Analyze(
                    scan.Findings
                );

            CollisionTree tree =
                Assert.Single(analysis.Trees);

            Assert.Equal(
                "FOO",
                tree.Root.Collision.LogicalName
            );

            Assert.Equal(
                2,
                tree.Descendants.Count
            );
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Analyze_KeepsIndependentSplitsSeparate()
    {
        string root = CreateTempDirectory();

        try
        {
            Directory.CreateDirectory(
                Path.Combine(root, "Alpha")
            );

            Directory.CreateDirectory(
                Path.Combine(root, "alpha")
            );

            string normal =
                Path.Combine(root, "normal");

            Directory.CreateDirectory(
                Path.Combine(normal, "Beta")
            );

            Directory.CreateDirectory(
                Path.Combine(normal, "beta")
            );

            RecursiveCollisionScanResult scan =
                RecursiveCollisionScanner.Scan(root);

            CollisionTreeAnalysis analysis =
                CollisionTreeAnalyzer.Analyze(
                    scan.Findings
                );

            Assert.Equal(2, analysis.Trees.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Analyze_DoesNotTurnStandaloneFileCollisionIntoTree()
    {
        string root = CreateTempDirectory();

        try
        {
            File.WriteAllText(
                Path.Combine(root, "Foo.nif"),
                "one"
            );

            File.WriteAllText(
                Path.Combine(root, "foo.nif"),
                "two"
            );

            RecursiveCollisionScanResult scan =
                RecursiveCollisionScanner.Scan(root);

            CollisionTreeAnalysis analysis =
                CollisionTreeAnalyzer.Analyze(
                    scan.Findings
                );

            Assert.Empty(analysis.Trees);

            Assert.Equal(
                1,
                analysis.FileCollisionFindings
            );

            Assert.Single(
                analysis.UnassignedFindings
            );
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"casecompat-tree-analysis-{Guid.NewGuid():N}"
        );

        Directory.CreateDirectory(path);
        return path;
    }
}
