using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxReplaceOwnedFileAtTests
{
    [Fact]
    public void Replace_MatchingOwnedFiles_ReplacesDestinationAtomically()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        File.WriteAllText(
            Path.Combine(temp.RootPath, "staging.json"),
            "new"
        );

        File.WriteAllText(
            Path.Combine(temp.RootPath, "journal.json"),
            "old"
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(temp.RootPath);

        LinuxOpenedFileIdentityResult sourceExpected =
            Capture(parent, "staging.json");

        LinuxOpenChildReadOnlyAtResult oldOpen =
            LinuxOpenChildReadOnlyAt.Open(
                parent,
                "journal.json"
            );

        Assert.True(oldOpen.Success);

        using LinuxOpenedChildHandle oldDestination =
            Assert.IsType<LinuxOpenedChildHandle>(
                oldOpen.OpenedChild
            );

        LinuxOpenedFileIdentityResult destinationExpected =
            LinuxOpenedFileIdentity.Capture(
                oldDestination
            );

        Assert.True(destinationExpected.Success);

        LinuxReplaceOwnedFileAtResult result =
            LinuxReplaceOwnedFileAt.Replace(
                parent,
                "staging.json",
                "journal.json",
                sourceExpected,
                destinationExpected
            );

        Assert.True(result.Success);

        Assert.Equal(
            LinuxReplaceOwnedFileAtState.Replaced,
            result.State
        );

        Assert.False(
            File.Exists(
                Path.Combine(
                    temp.RootPath,
                    "staging.json"
                )
            )
        );

        Assert.Equal(
            "new",
            File.ReadAllText(
                Path.Combine(
                    temp.RootPath,
                    "journal.json"
                )
            )
        );

        // The old inode remains readable through the descriptor
        // that was open before the atomic replacement.
        byte[] oldBytes =
            new byte[3];

        int read =
            RandomAccess.Read(
                oldDestination.Handle,
                oldBytes,
                0
            );

        Assert.Equal(3, read);

        Assert.Equal(
            "old",
            System.Text.Encoding.UTF8.GetString(
                oldBytes
            )
        );
    }

    [Fact]
    public void Replace_SourceReplacedBeforeCall_Refuses()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string staging =
            Path.Combine(temp.RootPath, "staging.json");

        string moved =
            Path.Combine(temp.RootPath, "staging-original.json");

        string journal =
            Path.Combine(temp.RootPath, "journal.json");

        File.WriteAllText(staging, "expected");
        File.WriteAllText(journal, "old");

        using LinuxNoFollowPathHandle parent =
            OpenRoot(temp.RootPath);

        LinuxOpenedFileIdentityResult sourceExpected =
            Capture(parent, "staging.json");

        LinuxOpenedFileIdentityResult destinationExpected =
            Capture(parent, "journal.json");

        File.Move(staging, moved);
        File.WriteAllText(staging, "replacement");

        LinuxReplaceOwnedFileAtResult result =
            LinuxReplaceOwnedFileAt.Replace(
                parent,
                "staging.json",
                "journal.json",
                sourceExpected,
                destinationExpected
            );

        Assert.False(result.Success);

        Assert.Equal(
            LinuxReplaceOwnedFileAtState
                .SourceIdentityMismatch,
            result.State
        );

        Assert.Equal("old", File.ReadAllText(journal));
        Assert.Equal("replacement", File.ReadAllText(staging));
        Assert.Equal("expected", File.ReadAllText(moved));
    }

    [Fact]
    public void Replace_DestinationReplacedBeforeCall_Refuses()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string staging =
            Path.Combine(temp.RootPath, "staging.json");

        string journal =
            Path.Combine(temp.RootPath, "journal.json");

        string moved =
            Path.Combine(temp.RootPath, "journal-original.json");

        File.WriteAllText(staging, "new");
        File.WriteAllText(journal, "expected-old");

        using LinuxNoFollowPathHandle parent =
            OpenRoot(temp.RootPath);

        LinuxOpenedFileIdentityResult sourceExpected =
            Capture(parent, "staging.json");

        LinuxOpenedFileIdentityResult destinationExpected =
            Capture(parent, "journal.json");

        File.Move(journal, moved);
        File.WriteAllText(journal, "replacement");

        LinuxReplaceOwnedFileAtResult result =
            LinuxReplaceOwnedFileAt.Replace(
                parent,
                "staging.json",
                "journal.json",
                sourceExpected,
                destinationExpected
            );

        Assert.False(result.Success);

        Assert.Equal(
            LinuxReplaceOwnedFileAtState
                .DestinationIdentityMismatch,
            result.State
        );

        Assert.Equal("new", File.ReadAllText(staging));
        Assert.Equal("replacement", File.ReadAllText(journal));
        Assert.Equal("expected-old", File.ReadAllText(moved));
    }

    [Fact]
    public void Replace_SourceSymbolicLink_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        File.WriteAllText(
            Path.Combine(temp.RootPath, "owned-source.json"),
            "owned"
        );

        File.WriteAllText(
            Path.Combine(temp.RootPath, "journal.json"),
            "old"
        );

        File.CreateSymbolicLink(
            Path.Combine(temp.RootPath, "staging.json"),
            Path.Combine(temp.RootPath, "owned-source.json")
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(temp.RootPath);

        LinuxOpenedFileIdentityResult sourceExpected =
            Capture(parent, "owned-source.json");

        LinuxOpenedFileIdentityResult destinationExpected =
            Capture(parent, "journal.json");

        LinuxReplaceOwnedFileAtResult result =
            LinuxReplaceOwnedFileAt.Replace(
                parent,
                "staging.json",
                "journal.json",
                sourceExpected,
                destinationExpected
            );

        Assert.False(result.Success);

        Assert.Equal(
            LinuxReplaceOwnedFileAtState
                .SourceSymbolicLinkRejected,
            result.State
        );

        Assert.Equal(
            "old",
            File.ReadAllText(
                Path.Combine(temp.RootPath, "journal.json")
            )
        );
    }

    [Fact]
    public void Replace_DestinationSymbolicLink_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        File.WriteAllText(
            Path.Combine(temp.RootPath, "staging.json"),
            "new"
        );

        File.WriteAllText(
            Path.Combine(temp.RootPath, "owned-destination.json"),
            "old"
        );

        File.CreateSymbolicLink(
            Path.Combine(temp.RootPath, "journal.json"),
            Path.Combine(temp.RootPath, "owned-destination.json")
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(temp.RootPath);

        LinuxOpenedFileIdentityResult sourceExpected =
            Capture(parent, "staging.json");

        LinuxOpenedFileIdentityResult destinationExpected =
            Capture(parent, "owned-destination.json");

        LinuxReplaceOwnedFileAtResult result =
            LinuxReplaceOwnedFileAt.Replace(
                parent,
                "staging.json",
                "journal.json",
                sourceExpected,
                destinationExpected
            );

        Assert.False(result.Success);

        Assert.Equal(
            LinuxReplaceOwnedFileAtState
                .DestinationSymbolicLinkRejected,
            result.State
        );

        Assert.Equal(
            "old",
            File.ReadAllText(
                Path.Combine(
                    temp.RootPath,
                    "owned-destination.json"
                )
            )
        );
    }

    [Fact]
    public void Replace_ParentPathReplaced_UsesOriginalDirectory()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string parentPath =
            Directory.CreateDirectory(
                Path.Combine(temp.RootPath, "journal-dir")
            ).FullName;

        File.WriteAllText(
            Path.Combine(parentPath, "staging.json"),
            "new"
        );

        File.WriteAllText(
            Path.Combine(parentPath, "journal.json"),
            "old"
        );

        LinuxNoFollowPathOpenResult parentOpen =
            LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                temp.RootPath,
                "journal-dir"
            );

        using LinuxNoFollowPathHandle parent =
            Assert.IsType<LinuxNoFollowPathHandle>(
                parentOpen.OpenedPath
            );

        LinuxOpenedFileIdentityResult sourceExpected =
            Capture(parent, "staging.json");

        LinuxOpenedFileIdentityResult destinationExpected =
            Capture(parent, "journal.json");

        string moved =
            Path.Combine(
                temp.RootPath,
                "journal-dir-original"
            );

        Directory.Move(parentPath, moved);

        Directory.CreateDirectory(parentPath);

        File.WriteAllText(
            Path.Combine(parentPath, "staging.json"),
            "decoy-new"
        );

        File.WriteAllText(
            Path.Combine(parentPath, "journal.json"),
            "decoy-old"
        );

        LinuxReplaceOwnedFileAtResult result =
            LinuxReplaceOwnedFileAt.Replace(
                parent,
                "staging.json",
                "journal.json",
                sourceExpected,
                destinationExpected
            );

        Assert.True(result.Success);

        Assert.Equal(
            "new",
            File.ReadAllText(
                Path.Combine(moved, "journal.json")
            )
        );

        Assert.Equal(
            "decoy-old",
            File.ReadAllText(
                Path.Combine(parentPath, "journal.json")
            )
        );
    }

    [Fact]
    public void Replace_ClosedParent_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        File.WriteAllText(
            Path.Combine(temp.RootPath, "staging.json"),
            "new"
        );

        File.WriteAllText(
            Path.Combine(temp.RootPath, "journal.json"),
            "old"
        );

        LinuxNoFollowPathHandle parent =
            OpenRoot(temp.RootPath);

        LinuxOpenedFileIdentityResult sourceExpected =
            Capture(parent, "staging.json");

        LinuxOpenedFileIdentityResult destinationExpected =
            Capture(parent, "journal.json");

        parent.Dispose();

        LinuxReplaceOwnedFileAtResult result =
            LinuxReplaceOwnedFileAt.Replace(
                parent,
                "staging.json",
                "journal.json",
                sourceExpected,
                destinationExpected
            );

        Assert.False(result.Success);

        Assert.Equal(
            LinuxReplaceOwnedFileAtState
                .InvalidParentHandle,
            result.State
        );
    }

    [Fact]
    public void Replace_SameName_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        File.WriteAllText(
            Path.Combine(temp.RootPath, "journal.json"),
            "old"
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(temp.RootPath);

        LinuxOpenedFileIdentityResult identity =
            Capture(parent, "journal.json");

        LinuxReplaceOwnedFileAtResult result =
            LinuxReplaceOwnedFileAt.Replace(
                parent,
                "journal.json",
                "journal.json",
                identity,
                identity
            );

        Assert.False(result.Success);

        Assert.Equal(
            LinuxReplaceOwnedFileAtState.SameName,
            result.State
        );
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../escape")]
    [InlineData("child/file")]
    [InlineData(@"child\file")]
    [InlineData("")]
    public void Replace_InvalidSourceName_IsRejected(
        string sourceName)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        File.WriteAllText(
            Path.Combine(temp.RootPath, "source.json"),
            "new"
        );

        File.WriteAllText(
            Path.Combine(temp.RootPath, "journal.json"),
            "old"
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(temp.RootPath);

        LinuxOpenedFileIdentityResult sourceIdentity =
            Capture(parent, "source.json");

        LinuxOpenedFileIdentityResult destinationIdentity =
            Capture(parent, "journal.json");

        LinuxReplaceOwnedFileAtResult result =
            LinuxReplaceOwnedFileAt.Replace(
                parent,
                sourceName,
                "journal.json",
                sourceIdentity,
                destinationIdentity
            );

        Assert.False(result.Success);

        Assert.Equal(
            LinuxReplaceOwnedFileAtState.InvalidName,
            result.State
        );

        Assert.Equal(
            "old",
            File.ReadAllText(
                Path.Combine(temp.RootPath, "journal.json")
            )
        );
    }

    private static LinuxOpenedFileIdentityResult Capture(
        LinuxNoFollowPathHandle parent,
        string childName)
    {
        LinuxOpenChildReadOnlyAtResult opened =
            LinuxOpenChildReadOnlyAt.Open(
                parent,
                childName
            );

        Assert.True(opened.Success);

        using LinuxOpenedChildHandle child =
            Assert.IsType<LinuxOpenedChildHandle>(
                opened.OpenedChild
            );

        LinuxOpenedFileIdentityResult identity =
            LinuxOpenedFileIdentity.Capture(
                child
            );

        Assert.True(identity.Success);

        return identity;
    }

    private static LinuxNoFollowPathHandle OpenRoot(
        string root)
    {
        LinuxNoFollowPathOpenResult result =
            LinuxNoFollowPath.OpenRootReadOnly(
                root
            );

        Assert.True(result.Success);

        return Assert.IsType<LinuxNoFollowPathHandle>(
            result.OpenedPath
        );
    }

    private sealed class TemporaryDirectory
        : IDisposable
    {
        public TemporaryDirectory()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-replace-owned-tests",
                    Guid.NewGuid().ToString("N")
                );

            Directory.CreateDirectory(
                RootPath
            );
        }

        public string RootPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(
                    RootPath,
                    recursive:
                        true
                );
            }
        }
    }
}
