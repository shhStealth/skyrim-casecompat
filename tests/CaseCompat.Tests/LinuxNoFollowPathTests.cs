using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxNoFollowPathTests
{
    [Fact]
    public void OpenReadOnlyUnderRoot_RegularNestedFile_Opens()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Data"
                )
            ).FullName;

        string directory =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "Example"
                )
            ).FullName;

        string file =
            Path.Combine(
                directory,
                "fixture.nif"
            );

        File.WriteAllText(
            file,
            "fixture"
        );

        LinuxNoFollowPathOpenResult result =
            LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                dataRoot,
                "meshes/Example/fixture.nif"
            );

        Assert.True(
            result.Success
        );

        Assert.Equal(
            LinuxNoFollowPathOpenState.Opened,
            result.State
        );

        LinuxNoFollowPathHandle opened =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                result.OpenedPath
            );

        using (opened)
        {
            Assert.False(
                opened.Handle.IsInvalid
            );

            Assert.Equal(
                new FileInfo(
                    file
                ).Length,
                RandomAccess.GetLength(
                    opened.Handle
                )
            );
        }
    }

    [Fact]
    public void OpenReadOnlyUnderRoot_SymbolicLinkDirectory_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Data"
                )
            ).FullName;

        string outside =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Outside"
                )
            ).FullName;

        File.WriteAllText(
            Path.Combine(
                outside,
                "fixture.nif"
            ),
            "outside"
        );

        string meshes =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes"
                )
            ).FullName;

        Directory.CreateSymbolicLink(
            Path.Combine(
                meshes,
                "Linked"
            ),
            outside
        );

        LinuxNoFollowPathOpenResult result =
            LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                dataRoot,
                "meshes/Linked/fixture.nif"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxNoFollowPathOpenState
                .ComponentNotDirectoryOrSymbolicLink,
            result.State
        );

        Assert.Null(
            result.OpenedPath
        );
    }

    [Fact]
    public void OpenReadOnlyUnderRoot_SymbolicLinkTarget_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Data"
                )
            ).FullName;

        string meshes =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes"
                )
            ).FullName;

        string realFile =
            Path.Combine(
                temp.RootPath,
                "real.nif"
            );

        File.WriteAllText(
            realFile,
            "real"
        );

        File.CreateSymbolicLink(
            Path.Combine(
                meshes,
                "linked.nif"
            ),
            realFile
        );

        LinuxNoFollowPathOpenResult result =
            LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                dataRoot,
                "meshes/linked.nif"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxNoFollowPathOpenState
                .TargetSymbolicLinkRejected,
            result.State
        );

        Assert.Null(
            result.OpenedPath
        );
    }

    [Fact]
    public void OpenReadOnlyUnderRoot_Traversal_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Data"
                )
            ).FullName;

        LinuxNoFollowPathOpenResult result =
            LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                dataRoot,
                "../outside.nif"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxNoFollowPathOpenState
                .InvalidRelativePath,
            result.State
        );

        Assert.Null(
            result.OpenedPath
        );
    }

    [Fact]
    public void OpenReadOnlyUnderRoot_StrictWrongCase_DoesNotAlias()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Data"
                )
            ).FullName;

        string meshes =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes"
                )
            ).FullName;

        Directory.CreateDirectory(
            Path.Combine(
                meshes,
                "lowercase"
            )
        );

        File.WriteAllText(
            Path.Combine(
                meshes,
                "lowercase",
                "fixture.nif"
            ),
            "fixture"
        );

        LinuxNoFollowPathOpenResult result =
            LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                dataRoot,
                "meshes/LOWERCASE/fixture.nif"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxNoFollowPathOpenState
                .ComponentUnavailable,
            result.State
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
                    "casecompat-nofollow-tests",
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
