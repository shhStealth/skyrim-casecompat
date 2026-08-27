using CaseCompat.Core.Analysis;

namespace CaseCompat.Tests;

public class BranchContentComparerTests
{
    [Fact]
    public void Compare_IdenticalFiles_ReturnsIdentical()
    {
        using TestBranches branches =
            TestBranches.Create(
                contentA: "same bytes",
                contentB: "same bytes"
            );

        BranchContentComparison result =
            Compare(branches);

        Assert.Equal(1, result.Identical);
        Assert.Equal(0, result.DifferentSize);
        Assert.Equal(0, result.DifferentContent);
    }

    [Fact]
    public void Compare_DifferentSizes_ReturnsDifferentSize()
    {
        using TestBranches branches =
            TestBranches.Create(
                contentA: "short",
                contentB: "much longer content"
            );

        BranchContentComparison result =
            Compare(branches);

        Assert.Equal(0, result.Identical);
        Assert.Equal(1, result.DifferentSize);
        Assert.Equal(0, result.DifferentContent);
    }

    [Fact]
    public void Compare_SameSizeDifferentBytes_ReturnsDifferentContent()
    {
        using TestBranches branches =
            TestBranches.Create(
                contentA: "AAAA",
                contentB: "BBBB"
            );

        BranchContentComparison result =
            Compare(branches);

        Assert.Equal(0, result.Identical);
        Assert.Equal(0, result.DifferentSize);
        Assert.Equal(1, result.DifferentContent);
    }

    private static BranchContentComparison Compare(
        TestBranches branches)
    {
        BranchComparison namespaceComparison =
            BranchComparer.Compare(
                BranchInventoryScanner.Scan(branches.A),
                BranchInventoryScanner.Scan(branches.B)
            );

        return BranchContentComparer.Compare(
            namespaceComparison
        );
    }

    private sealed class TestBranches : IDisposable
    {
        public string Root { get; }
        public string A { get; }
        public string B { get; }

        private TestBranches(
            string root,
            string a,
            string b)
        {
            Root = root;
            A = a;
            B = b;
        }

        public static TestBranches Create(
            string contentA,
            string contentB)
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                $"casecompat-content-{Guid.NewGuid():N}"
            );

            string a = Path.Combine(root, "A");
            string b = Path.Combine(root, "B");

            Directory.CreateDirectory(a);
            Directory.CreateDirectory(b);

            File.WriteAllText(
                Path.Combine(a, "Mesh.nif"),
                contentA
            );

            File.WriteAllText(
                Path.Combine(b, "mesh.NIF"),
                contentB
            );

            return new TestBranches(root, a, b);
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
