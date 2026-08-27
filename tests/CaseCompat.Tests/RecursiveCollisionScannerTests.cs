using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public class RecursiveCollisionScannerTests
{
    [Fact]
    public void Scan_FindsNestedCollision()
    {
        string root = CreateTempDirectory();

        try
        {
            string nested = Path.Combine(
                root,
                "meshes",
                "example"
            );

            Directory.CreateDirectory(nested);

            Directory.CreateDirectory(
                Path.Combine(nested, "Bishop")
            );

            Directory.CreateDirectory(
                Path.Combine(nested, "bishop")
            );

            RecursiveCollisionScanResult result =
                RecursiveCollisionScanner.Scan(root);

            RecursiveCollisionFinding finding =
                Assert.Single(result.Findings);

            Assert.Equal("BISHOP", finding.Collision.LogicalName);
            Assert.Equal(2, finding.Collision.Members.Count);
            Assert.True(result.DirectoriesScanned >= 3);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Scan_FindsCollisionsAtMultipleDepths()
    {
        string root = CreateTempDirectory();

        try
        {
            Directory.CreateDirectory(
                Path.Combine(root, "Foo")
            );

            Directory.CreateDirectory(
                Path.Combine(root, "foo")
            );

            string nested = Path.Combine(root, "normal");

            Directory.CreateDirectory(
                Path.Combine(nested, "Bar")
            );

            Directory.CreateDirectory(
                Path.Combine(nested, "bar")
            );

            RecursiveCollisionScanResult result =
                RecursiveCollisionScanner.Scan(root);

            Assert.Equal(2, result.Findings.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Scan_SkipsDirectorySymbolicLinks()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string root = CreateTempDirectory();
        string outside = CreateTempDirectory();

        try
        {
            Directory.CreateDirectory(
                Path.Combine(outside, "ShouldNotBeScanned")
            );

            string link = Path.Combine(root, "linked");

            Directory.CreateSymbolicLink(link, outside);

            RecursiveCollisionScanResult result =
                RecursiveCollisionScanner.Scan(root);

            Assert.True(result.SymbolicLinksSkipped >= 1);

            Assert.DoesNotContain(
                result.Findings,
                finding =>
                    finding.Collision.ParentPath.StartsWith(
                        outside,
                        StringComparison.Ordinal
                    )
            );
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"casecompat-recursive-{Guid.NewGuid():N}"
        );

        Directory.CreateDirectory(path);
        return path;
    }
}
