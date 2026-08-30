using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxOpenedFileIdentityTests
{
    [Fact]
    public void Capture_TwoDescriptorsForSameFile_HaveSameIdentity()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        File.WriteAllText(
            Path.Combine(
                temp.RootPath,
                "fixture.bin"
            ),
            "fixture"
        );

        using LinuxNoFollowPathHandle first =
            OpenFile(
                temp.RootPath,
                "fixture.bin"
            );

        using LinuxNoFollowPathHandle second =
            OpenFile(
                temp.RootPath,
                "fixture.bin"
            );

        LinuxOpenedFileIdentityResult firstIdentity =
            LinuxOpenedFileIdentity.Capture(
                first
            );

        LinuxOpenedFileIdentityResult secondIdentity =
            LinuxOpenedFileIdentity.Capture(
                second
            );

        Assert.True(
            firstIdentity.Success
        );

        Assert.True(
            secondIdentity.Success
        );

        Assert.True(
            firstIdentity.SameObjectAs(
                secondIdentity
            )
        );
    }

    [Fact]
    public void Capture_UnnamedFile_PreservesIdentityAcrossPublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        LinuxCreateUnnamedFileAtResult create =
            LinuxCreateUnnamedFileAt.Create(
                parent
            );

        if (
            create.State ==
            LinuxCreateUnnamedFileAtState
                .TmpfileUnsupported)
        {
            return;
        }

        using LinuxUnnamedFileHandle unnamed =
            Assert.IsType<
                LinuxUnnamedFileHandle
            >(
                create.OpenedFile
            );

        LinuxOpenedFileIdentityResult before =
            LinuxOpenedFileIdentity.Capture(
                unnamed
            );

        Assert.True(
            before.Success
        );

        Assert.Equal(
            0U,
            before.LinkCount
        );

        LinuxPublishUnnamedFileAtResult publish =
            LinuxPublishUnnamedFileAt.Publish(
                unnamed,
                parent,
                "Final.nif"
            );

        Assert.True(
            publish.Success
        );

        LinuxOpenedFileIdentityResult after =
            LinuxOpenedFileIdentity.Capture(
                unnamed
            );

        Assert.True(
            before.SameObjectAs(
                after
            )
        );

        Assert.NotNull(
            after.LinkCount
        );

        Assert.True(
            after.LinkCount >= 1U
        );

        using LinuxNoFollowPathHandle final =
            OpenFile(
                temp.RootPath,
                "Final.nif"
            );

        LinuxOpenedFileIdentityResult finalIdentity =
            LinuxOpenedFileIdentity.Capture(
                final
            );

        Assert.True(
            after.SameObjectAs(
                finalIdentity
            )
        );
    }

    [Fact]
    public void Capture_FinalPathReplaced_DetectsDifferentInode()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        LinuxCreateUnnamedFileAtResult create =
            LinuxCreateUnnamedFileAt.Create(
                parent
            );

        if (
            create.State ==
            LinuxCreateUnnamedFileAtState
                .TmpfileUnsupported)
        {
            return;
        }

        using LinuxUnnamedFileHandle unnamed =
            Assert.IsType<
                LinuxUnnamedFileHandle
            >(
                create.OpenedFile
            );

        Assert.True(
            LinuxPublishUnnamedFileAt.Publish(
                unnamed,
                parent,
                "Final.nif"
            ).Success
        );

        LinuxOpenedFileIdentityResult publishedIdentity =
            LinuxOpenedFileIdentity.Capture(
                unnamed
            );

        string finalPath =
            Path.Combine(
                temp.RootPath,
                "Final.nif"
            );

        string movedPath =
            Path.Combine(
                temp.RootPath,
                "Final-original.nif"
            );

        File.Move(
            finalPath,
            movedPath
        );

        File.WriteAllText(
            finalPath,
            "replacement"
        );

        using LinuxNoFollowPathHandle moved =
            OpenFile(
                temp.RootPath,
                "Final-original.nif"
            );

        using LinuxNoFollowPathHandle replacement =
            OpenFile(
                temp.RootPath,
                "Final.nif"
            );

        LinuxOpenedFileIdentityResult movedIdentity =
            LinuxOpenedFileIdentity.Capture(
                moved
            );

        LinuxOpenedFileIdentityResult replacementIdentity =
            LinuxOpenedFileIdentity.Capture(
                replacement
            );

        Assert.True(
            publishedIdentity.SameObjectAs(
                movedIdentity
            )
        );

        Assert.False(
            publishedIdentity.SameObjectAs(
                replacementIdentity
            )
        );
    }

    [Fact]
    public void Capture_DirectoryDescriptor_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        using LinuxNoFollowPathHandle root =
            OpenRoot(
                temp.RootPath
            );

        LinuxOpenedFileIdentityResult result =
            LinuxOpenedFileIdentity.Capture(
                root
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxOpenedFileIdentityState
                .NotRegularFile,
            result.State
        );
    }

    [Fact]
    public void Capture_ClosedDescriptor_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        File.WriteAllText(
            Path.Combine(
                temp.RootPath,
                "fixture.bin"
            ),
            "fixture"
        );

        LinuxNoFollowPathHandle opened =
            OpenFile(
                temp.RootPath,
                "fixture.bin"
            );

        opened.Dispose();

        LinuxOpenedFileIdentityResult result =
            LinuxOpenedFileIdentity.Capture(
                opened
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxOpenedFileIdentityState
                .InvalidHandle,
            result.State
        );
    }

    private static LinuxNoFollowPathHandle OpenRoot(
        string root)
    {
        LinuxNoFollowPathOpenResult result =
            LinuxNoFollowPath.OpenRootReadOnly(
                root
            );

        Assert.True(
            result.Success
        );

        return Assert.IsType<
            LinuxNoFollowPathHandle
        >(
            result.OpenedPath
        );
    }

    private static LinuxNoFollowPathHandle OpenFile(
        string root,
        string relativePath)
    {
        LinuxNoFollowPathOpenResult result =
            LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                root,
                relativePath
            );

        Assert.True(
            result.Success
        );

        return Assert.IsType<
            LinuxNoFollowPathHandle
        >(
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
                    "casecompat-opened-file-identity-tests",
                    Guid.NewGuid()
                        .ToString("N")
                );

            Directory.CreateDirectory(
                RootPath
            );
        }

        public string RootPath { get; }

        public void Dispose()
        {
            if (
                Directory.Exists(
                    RootPath
                ))
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
