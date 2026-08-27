using CaseCompat.Core.Analysis;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public class CollisionTreeContentBatchAnalyzerTests
{
    [Fact]
    public void Analyze_SummarizesContentStatesAcrossTrees()
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
                "same"
            );

            File.WriteAllText(
                Path.Combine(fooB, "shared.NIF"),
                "same"
            );

            File.WriteAllText(
                Path.Combine(fooA, "OnlyA.nif"),
                "one"
            );

            string normal = Path.Combine(root, "normal");

            string barA = Path.Combine(normal, "Bar");
            string barB = Path.Combine(normal, "bar");

            Directory.CreateDirectory(barA);
            Directory.CreateDirectory(barB);

            File.WriteAllText(
                Path.Combine(barA, "Mesh.nif"),
                "AAAA"
            );

            File.WriteAllText(
                Path.Combine(barB, "mesh.NIF"),
                "BBBB"
            );

            CollisionTreeContentBatchAnalysis batch =
                Analyze(root);

            Assert.Equal(2, batch.Trees.Count);
            Assert.Equal(1, batch.TotalSingleOccurrence);
            Assert.Equal(1, batch.TotalIdentical);
            Assert.Equal(0, batch.TotalDifferentSize);
            Assert.Equal(1, batch.TotalDifferentContent);

            Assert.Equal(
                1,
                batch.TreesWithContentConflicts
            );
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Analyze_PreservesAmbiguityWithoutHashing()
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

            CollisionTreeContentBatchAnalysis batch =
                Analyze(root);

            Assert.Equal(1, batch.TotalAmbiguous);
            Assert.Equal(1, batch.TreesWithAmbiguity);
            Assert.Equal(0, batch.TotalDifferentContent);
            Assert.Equal(0, batch.TotalDifferentSize);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static CollisionTreeContentBatchAnalysis Analyze(
        string root)
    {
        RecursiveCollisionScanResult scan =
            RecursiveCollisionScanner.Scan(root);

        CollisionTreeAnalysis trees =
            CollisionTreeAnalyzer.Analyze(
                scan.Findings
            );

        CollisionTreeNamespaceBatchAnalysis namespaceBatch =
            CollisionTreeNamespaceBatchAnalyzer.Analyze(
                trees
            );

        return CollisionTreeContentBatchAnalyzer.Analyze(
            namespaceBatch
        );
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"casecompat-content-batch-{Guid.NewGuid():N}"
        );

        Directory.CreateDirectory(path);
        return path;
    }
}
