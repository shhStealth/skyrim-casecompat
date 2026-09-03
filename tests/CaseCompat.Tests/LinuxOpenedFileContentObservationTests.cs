using CaseCompat.Filesystem.Linux;
using Microsoft.Win32.SafeHandles;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxOpenedFileContentObservationTests
{
    [Fact]
    public void Observe_UnchangedRegularFile_ReturnsStableContentEvidence()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        byte[] content =
            Encoding.UTF8.GetBytes(
                "stable-content-evidence"
            );

        File.WriteAllBytes(
            Path.Combine(
                temp.RootPath,
                "Fixture.nif"
            ),
            content
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        LinuxOpenChildRegularFileReadOnlyAtResult open =
            LinuxOpenChildRegularFileReadOnlyAt.Open(
                parent,
                "Fixture.nif"
            );

        Assert.True(
            open.Success,
            open.Error
        );

        using LinuxOpenedChildHandle opened =
            Assert.IsType<
                LinuxOpenedChildHandle
            >(
                open.OpenedFile
            );

        const string displayPath =
            "/diagnostic/Data/Fixture.nif";

        LinuxOpenedFileContentObservationResult result =
            LinuxOpenedFileContentObservation.Observe(
                opened,
                displayPath
            );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            LinuxOpenedFileContentObservationState
                .StableContentEvidence,
            result.State
        );

        Assert.Equal(
            displayPath,
            result.DisplayPath
        );

        Assert.Equal(
            content.LongLength,
            result.Size
        );

        Assert.Equal(
            Convert.ToHexString(
                SHA256.HashData(
                    content
                )
            ),
            result.Sha256
        );

        Assert.True(
            result.Before!.SameObservedStateAs(
                result.After!
            )
        );
    }

    [Fact]
    public void Observe_PathReplacedAfterOpen_StillObservesOriginalDescriptor()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string originalPath =
            Path.Combine(
                temp.RootPath,
                "Fixture.nif"
            );

        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "original-opened-content"
            );

        File.WriteAllBytes(
            originalPath,
            originalContent
        );

        using LinuxNoFollowPathHandle parent =
            OpenRoot(
                temp.RootPath
            );

        LinuxOpenChildRegularFileReadOnlyAtResult open =
            LinuxOpenChildRegularFileReadOnlyAt.Open(
                parent,
                "Fixture.nif"
            );

        Assert.True(
            open.Success,
            open.Error
        );

        using LinuxOpenedChildHandle opened =
            Assert.IsType<
                LinuxOpenedChildHandle
            >(
                open.OpenedFile
            );

        string movedPath =
            Path.Combine(
                temp.RootPath,
                "Moved.nif"
            );

        File.Move(
            originalPath,
            movedPath
        );

        File.WriteAllText(
            originalPath,
            "replacement-path-content"
        );

        LinuxOpenedFileContentObservationResult result =
            LinuxOpenedFileContentObservation.Observe(
                opened,
                "/diagnostic/Data/Fixture.nif"
            );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            Convert.ToHexString(
                SHA256.HashData(
                    originalContent
                )
            ),
            result.Sha256
        );

        string replacementHash =
            Convert.ToHexString(
                SHA256.HashData(
                    File.ReadAllBytes(
                        originalPath
                    )
                )
            );

        Assert.NotEqual(
            replacementHash,
            result.Sha256
        );
    }

    [Fact]
    public void Observe_SameSizeMutationBetweenPreStampAndHash_IsChanged()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string file =
            Path.Combine(
                temp.RootPath,
                "Fixture.nif"
            );

        File.WriteAllText(
            file,
            "AAAAAAAA"
        );

        using LinuxNoFollowPathHandle opened =
            OpenFile(
                temp.RootPath,
                "Fixture.nif"
            );

        byte[] replacement =
            Encoding.UTF8.GetBytes(
                "BBBBBBBB"
            );

        bool mutationRan =
            false;

        using var mutatingHandle =
            new MutateOnHandleAccess(
                opened,
                triggerAccess:
                    3,
                mutation:
                    () =>
                    {
                        using SafeFileHandle writer =
                            File.OpenHandle(
                                file,
                                FileMode.Open,
                                FileAccess.Write,
                                FileShare.ReadWrite |
                                FileShare.Delete
                            );

                        RandomAccess.Write(
                            writer,
                            replacement,
                            0
                        );

                        mutationRan =
                            true;
                    }
            );

        LinuxOpenedFileContentObservationResult result =
            LinuxOpenedFileContentObservation.Observe(
                mutatingHandle,
                "/diagnostic/Data/Fixture.nif"
            );

        Assert.True(
            mutationRan
        );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxOpenedFileContentObservationState
                .ChangedDuringObservation,
            result.State
        );

        Assert.NotNull(
            result.Before
        );

        Assert.NotNull(
            result.Snapshot
        );

        Assert.NotNull(
            result.After
        );

        Assert.True(
            result.Before!.Success,
            result.Before.Error
        );

        Assert.True(
            result.After!.Success,
            result.After.Error
        );

        Assert.True(
            result.Before.Identity!.SameObjectAs(
                result.After.Identity!
            )
        );

        Assert.Equal(
            result.Before.Size,
            result.After.Size
        );

        Assert.False(
            result.Before.SameObservedStateAs(
                result.After
            )
        );

        Assert.Null(
            result.Sha256
        );
    }

    [Fact]
    public void Observe_Directory_ReturnsIncompleteEvidence()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string directory =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Directory"
                )
            ).FullName;

        LinuxNoFollowPathOpenResult open =
            LinuxNoFollowPath.OpenRootReadOnly(
                directory
            );

        Assert.True(
            open.Success,
            open.Error
        );

        using LinuxNoFollowPathHandle opened =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                open.OpenedPath
            );

        LinuxOpenedFileContentObservationResult result =
            LinuxOpenedFileContentObservation.Observe(
                opened,
                "/diagnostic/Data/Directory"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxOpenedFileContentObservationState
                .IncompleteEvidence,
            result.State
        );

        Assert.Equal(
            LinuxOpenedFileObservationStampState
                .NotRegularFile,
            result.Before!.State
        );

        Assert.Null(
            result.Snapshot
        );

        Assert.Null(
            result.Sha256
        );
    }

    [Fact]
    public void Observe_ClosedHandle_ReturnsIncompleteEvidence()
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
                "Fixture.nif"
            ),
            "fixture"
        );

        LinuxNoFollowPathHandle opened =
            OpenFile(
                temp.RootPath,
                "Fixture.nif"
            );

        opened.Dispose();

        LinuxOpenedFileContentObservationResult result =
            LinuxOpenedFileContentObservation.Observe(
                opened,
                "/diagnostic/Data/Fixture.nif"
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxOpenedFileContentObservationState
                .IncompleteEvidence,
            result.State
        );

        Assert.Equal(
            LinuxOpenedFileObservationStampState
                .InvalidHandle,
            result.Before!.State
        );

        Assert.Null(
            result.Snapshot
        );

        Assert.Null(
            result.Sha256
        );
    }

    private static LinuxNoFollowPathHandle OpenRoot(
        string path)
    {
        LinuxNoFollowPathOpenResult result =
            LinuxNoFollowPath.OpenRootReadOnly(
                path
            );

        Assert.True(
            result.Success,
            result.Error
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
            result.Success,
            result.Error
        );

        return Assert.IsType<
            LinuxNoFollowPathHandle
        >(
            result.OpenedPath
        );
    }

    private sealed class MutateOnHandleAccess
        : ILinuxOpenedHandle
    {
        private readonly ILinuxOpenedHandle _inner;
        private readonly int _triggerAccess;
        private readonly Action _mutation;

        private int _accessCount;
        private int _mutationExecuted;

        public MutateOnHandleAccess(
            ILinuxOpenedHandle inner,
            int triggerAccess,
            Action mutation)
        {
            _inner =
                inner;

            _triggerAccess =
                triggerAccess;

            _mutation =
                mutation;
        }

        public SafeFileHandle Handle
        {
            get
            {
                int access =
                    Interlocked.Increment(
                        ref _accessCount
                    );

                if (
                    access ==
                        _triggerAccess &&
                    Interlocked.Exchange(
                        ref _mutationExecuted,
                        1
                    ) == 0)
                {
                    _mutation();
                }

                return _inner.Handle;
            }
        }

        public void Dispose()
        {
            /*
             * The wrapped descriptor remains owned by the test's outer
             * LinuxNoFollowPathHandle. This wrapper owns no descriptor.
             */
        }
    }

    private sealed class TemporaryDirectory
        : IDisposable
    {
        public TemporaryDirectory()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-content-observation-tests",
                    Guid.NewGuid().ToString("N")
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
