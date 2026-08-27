using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public class DirectoryCollisionScannerTests
{
    [Fact]
    public void Scan_FindsCaseVariantDirectories()
    {
        string root = CreateTempDirectory();

        try
        {
            Directory.CreateDirectory(
                Path.Combine(root, "Fafny stash")
            );

            Directory.CreateDirectory(
                Path.Combine(root, "fafny stash")
            );

            IReadOnlyList<DirectoryCaseCollision> collisions =
                DirectoryCollisionScanner.Scan(root);

            DirectoryCaseCollision collision =
                Assert.Single(collisions);

            Assert.Equal(2, collision.Members.Count);

            Assert.Contains(
                collision.Members,
                member => member.Name == "Fafny stash"
            );

            Assert.Contains(
                collision.Members,
                member => member.Name == "fafny stash"
            );
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Scan_DoesNotReportOrdinaryUniqueNames()
    {
        string root = CreateTempDirectory();

        try
        {
            Directory.CreateDirectory(
                Path.Combine(root, "actors")
            );

            Directory.CreateDirectory(
                Path.Combine(root, "architecture")
            );

            IReadOnlyList<DirectoryCaseCollision> collisions =
                DirectoryCollisionScanner.Scan(root);

            Assert.Empty(collisions);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Scan_FindsCaseVariantFiles()
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

            IReadOnlyList<DirectoryCaseCollision> collisions =
                DirectoryCollisionScanner.Scan(root);

            DirectoryCaseCollision collision =
                Assert.Single(collisions);

            Assert.Equal(2, collision.Members.Count);
            Assert.All(
                collision.Members,
                member => Assert.False(member.IsDirectory)
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
            $"casecompat-collision-{Guid.NewGuid():N}"
        );

        Directory.CreateDirectory(path);
        return path;
    }
}
