using CaseCompat.Core.Analysis;

namespace CaseCompat.Tests;

public class BranchComparerTests
{
    [Fact]
    public void Compare_DetectsFilesSplitBetweenBranches()
    {
        string root = CreateTempDirectory();

        string a = Path.Combine(root, "A");
        string b = Path.Combine(root, "B");

        Directory.CreateDirectory(a);
        Directory.CreateDirectory(b);

        try
        {
            Directory.CreateDirectory(
                Path.Combine(a, "Bishop Armor")
            );

            Directory.CreateDirectory(
                Path.Combine(b, "Bishop Armor")
            );

            File.WriteAllText(
                Path.Combine(
                    a,
                    "Bishop Armor",
                    "Body.nif"
                ),
                "a"
            );

            File.WriteAllText(
                Path.Combine(
                    b,
                    "Bishop Armor",
                    "Hands.nif"
                ),
                "b"
            );

            BranchComparison comparison =
                BranchComparer.Compare(
                    BranchInventoryScanner.Scan(a),
                    BranchInventoryScanner.Scan(b)
                );

            Assert.Equal(1, comparison.OnlyInA);
            Assert.Equal(1, comparison.OnlyInB);
            Assert.Equal(0, comparison.PresentInBoth);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Compare_TreatsDifferentCaseAsSameLogicalPath()
    {
        string root = CreateTempDirectory();

        string a = Path.Combine(root, "A");
        string b = Path.Combine(root, "B");

        Directory.CreateDirectory(a);
        Directory.CreateDirectory(b);

        try
        {
            File.WriteAllText(
                Path.Combine(a, "Body.nif"),
                "a"
            );

            File.WriteAllText(
                Path.Combine(b, "body.NIF"),
                "b"
            );

            BranchComparison comparison =
                BranchComparer.Compare(
                    BranchInventoryScanner.Scan(a),
                    BranchInventoryScanner.Scan(b)
                );

            Assert.Equal(0, comparison.OnlyInA);
            Assert.Equal(0, comparison.OnlyInB);
            Assert.Equal(1, comparison.PresentInBoth);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void LogicalPath_NormalizesSlashAndCase()
    {
        WindowsLogicalPath first =
            WindowsLogicalPath.FromRelativePath(
                @"Bishop Armor\Body.nif"
            );

        WindowsLogicalPath second =
            WindowsLogicalPath.FromRelativePath(
                "bishop armor/body.NIF"
            );

        Assert.Equal(first, second);
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"casecompat-branch-{Guid.NewGuid():N}"
        );

        Directory.CreateDirectory(path);
        return path;
    }
}
