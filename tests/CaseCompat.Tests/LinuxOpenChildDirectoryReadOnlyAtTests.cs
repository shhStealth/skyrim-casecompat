using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class LinuxOpenChildDirectoryReadOnlyAtTests
{
    [Fact]
    public void Open_ExistingDirectory_ReturnsNoFollowDirectoryHandle()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string temporaryRoot =
            CreateTemporaryRoot();

        try
        {
            string childPath =
                Path.Combine(
                    temporaryRoot,
                    "plan-000001"
                );

            Directory.CreateDirectory(
                childPath
            );

            LinuxNoFollowPathOpenResult rootOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    temporaryRoot
                );

            Assert.True(
                rootOpen.Success,
                rootOpen.Error
            );

            using LinuxNoFollowPathHandle root =
                rootOpen.OpenedPath!;

            LinuxOpenChildDirectoryReadOnlyAtResult opened =
                LinuxOpenChildDirectoryReadOnlyAt.Open(
                    root,
                    "plan-000001"
                );

            Assert.True(
                opened.Success,
                opened.Error
            );

            Assert.Equal(
                LinuxOpenChildDirectoryReadOnlyAtState
                    .Opened,
                opened.State
            );

            using LinuxNoFollowPathHandle child =
                opened.OpenedDirectory!;

            Assert.Equal(
                root.RootPath,
                child.RootPath
            );

            Assert.Equal(
                "plan-000001",
                child.RelativePath
            );

            Assert.Equal(
                Path.GetFullPath(
                    childPath
                ),
                child.FullPath
            );
        }
        finally
        {
            DeleteTemporaryRoot(
                temporaryRoot
            );
        }
    }

    [Fact]
    public void Open_MissingChild_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string temporaryRoot =
            CreateTemporaryRoot();

        try
        {
            LinuxNoFollowPathOpenResult rootOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    temporaryRoot
                );

            Assert.True(
                rootOpen.Success,
                rootOpen.Error
            );

            using LinuxNoFollowPathHandle root =
                rootOpen.OpenedPath!;

            LinuxOpenChildDirectoryReadOnlyAtResult opened =
                LinuxOpenChildDirectoryReadOnlyAt.Open(
                    root,
                    "missing"
                );

            Assert.False(
                opened.Success
            );

            Assert.Equal(
                LinuxOpenChildDirectoryReadOnlyAtState
                    .ChildUnavailable,
                opened.State
            );

            Assert.Null(
                opened.OpenedDirectory
            );
        }
        finally
        {
            DeleteTemporaryRoot(
                temporaryRoot
            );
        }
    }

    [Fact]
    public void Open_RegularFile_IsRejectedAsNotDirectory()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string temporaryRoot =
            CreateTemporaryRoot();

        try
        {
            File.WriteAllText(
                Path.Combine(
                    temporaryRoot,
                    "plan-000001"
                ),
                "not a directory"
            );

            LinuxNoFollowPathOpenResult rootOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    temporaryRoot
                );

            Assert.True(
                rootOpen.Success,
                rootOpen.Error
            );

            using LinuxNoFollowPathHandle root =
                rootOpen.OpenedPath!;

            LinuxOpenChildDirectoryReadOnlyAtResult opened =
                LinuxOpenChildDirectoryReadOnlyAt.Open(
                    root,
                    "plan-000001"
                );

            Assert.False(
                opened.Success
            );

            Assert.Equal(
                LinuxOpenChildDirectoryReadOnlyAtState
                    .NotDirectory,
                opened.State
            );

            Assert.Null(
                opened.OpenedDirectory
            );
        }
        finally
        {
            DeleteTemporaryRoot(
                temporaryRoot
            );
        }
    }

    [Fact]
    public void Open_SymbolicLink_DoesNotReturnDirectoryCapability()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string temporaryRoot =
            CreateTemporaryRoot();

        try
        {
            string target =
                Path.Combine(
                    temporaryRoot,
                    "target"
                );

            Directory.CreateDirectory(
                target
            );

            Directory.CreateSymbolicLink(
                Path.Combine(
                    temporaryRoot,
                    "plan-000001"
                ),
                target
            );

            LinuxNoFollowPathOpenResult rootOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    temporaryRoot
                );

            Assert.True(
                rootOpen.Success,
                rootOpen.Error
            );

            using LinuxNoFollowPathHandle root =
                rootOpen.OpenedPath!;

            LinuxOpenChildDirectoryReadOnlyAtResult opened =
                LinuxOpenChildDirectoryReadOnlyAt.Open(
                    root,
                    "plan-000001"
                );

            Assert.False(
                opened.Success
            );

            Assert.True(
                opened.State is
                    LinuxOpenChildDirectoryReadOnlyAtState
                        .ChildSymbolicLinkRejected or
                    LinuxOpenChildDirectoryReadOnlyAtState
                        .NotDirectory,
                $"Unexpected state: {opened.State}"
            );

            Assert.Null(
                opened.OpenedDirectory
            );
        }
        finally
        {
            DeleteTemporaryRoot(
                temporaryRoot
            );
        }
    }

    [Fact]
    public void Open_InvalidChildName_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string temporaryRoot =
            CreateTemporaryRoot();

        try
        {
            LinuxNoFollowPathOpenResult rootOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    temporaryRoot
                );

            Assert.True(
                rootOpen.Success,
                rootOpen.Error
            );

            using LinuxNoFollowPathHandle root =
                rootOpen.OpenedPath!;

            LinuxOpenChildDirectoryReadOnlyAtResult opened =
                LinuxOpenChildDirectoryReadOnlyAt.Open(
                    root,
                    "../plan-000001"
                );

            Assert.False(
                opened.Success
            );

            Assert.Equal(
                LinuxOpenChildDirectoryReadOnlyAtState
                    .InvalidName,
                opened.State
            );

            Assert.Null(
                opened.OpenedDirectory
            );
        }
        finally
        {
            DeleteTemporaryRoot(
                temporaryRoot
            );
        }
    }

    [Fact]
    public void Open_ClosedParentHandle_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string temporaryRoot =
            CreateTemporaryRoot();

        try
        {
            Directory.CreateDirectory(
                Path.Combine(
                    temporaryRoot,
                    "plan-000001"
                )
            );

            LinuxNoFollowPathOpenResult rootOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    temporaryRoot
                );

            Assert.True(
                rootOpen.Success,
                rootOpen.Error
            );

            LinuxNoFollowPathHandle root =
                rootOpen.OpenedPath!;

            root.Dispose();

            LinuxOpenChildDirectoryReadOnlyAtResult opened =
                LinuxOpenChildDirectoryReadOnlyAt.Open(
                    root,
                    "plan-000001"
                );

            Assert.False(
                opened.Success
            );

            Assert.Equal(
                LinuxOpenChildDirectoryReadOnlyAtState
                    .InvalidParentHandle,
                opened.State
            );

            Assert.Null(
                opened.OpenedDirectory
            );
        }
        finally
        {
            DeleteTemporaryRoot(
                temporaryRoot
            );
        }
    }

    [Fact]
    public void Open_RetainedParentDescriptor_RejectsPathReplacementAsAuthority()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string temporaryRoot =
            CreateTemporaryRoot();

        try
        {
            string batchPath =
                Path.Combine(
                    temporaryRoot,
                    "batch"
                );

            string movedBatchPath =
                Path.Combine(
                    temporaryRoot,
                    "batch-original"
                );

            string originalChildPath =
                Path.Combine(
                    batchPath,
                    "plan-000001"
                );

            Directory.CreateDirectory(
                originalChildPath
            );

            LinuxNoFollowPathOpenResult batchOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    batchPath
                );

            Assert.True(
                batchOpen.Success,
                batchOpen.Error
            );

            using LinuxNoFollowPathHandle batch =
                batchOpen.OpenedPath!;

            /*
             * Replace the external batch pathname after retaining the
             * authoritative descriptor.
             */
            Directory.Move(
                batchPath,
                movedBatchPath
            );

            string replacementChildPath =
                Path.Combine(
                    batchPath,
                    "plan-000001"
                );

            Directory.CreateDirectory(
                replacementChildPath
            );

            LinuxOpenChildDirectoryReadOnlyAtResult opened =
                LinuxOpenChildDirectoryReadOnlyAt.Open(
                    batch,
                    "plan-000001"
                );

            Assert.True(
                opened.Success,
                opened.Error
            );

            using LinuxNoFollowPathHandle child =
                opened.OpenedDirectory!;

            /*
             * Prove the returned capability can flow directly into an
             * existing mutation primitive without reopening a pathname.
             */
            LinuxCreateDirectoryAtResult create =
                LinuxCreateDirectoryAt.Create(
                    child,
                    "created-through-retained-descriptor"
                );

            Assert.True(
                create.Success,
                create.Error
            );

            Assert.True(
                Directory.Exists(
                    Path.Combine(
                        movedBatchPath,
                        "plan-000001",
                        "created-through-retained-descriptor"
                    )
                )
            );

            Assert.False(
                Directory.Exists(
                    Path.Combine(
                        replacementChildPath,
                        "created-through-retained-descriptor"
                    )
                )
            );
        }
        finally
        {
            DeleteTemporaryRoot(
                temporaryRoot
            );
        }
    }

    private static string CreateTemporaryRoot()
    {
        string path =
            Path.Combine(
                Path.GetTempPath(),
                "casecompat-open-child-directory-" +
                Guid.NewGuid().ToString("N")
            );

        Directory.CreateDirectory(
            path
        );

        return path;
    }

    private static void DeleteTemporaryRoot(
        string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(
                path,
                recursive:
                    true
            );
        }
    }
}
