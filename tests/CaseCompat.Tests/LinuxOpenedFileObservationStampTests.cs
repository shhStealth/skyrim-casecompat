using CaseCompat.Filesystem.Linux;
using Microsoft.Win32.SafeHandles;
using System.Text;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxOpenedFileObservationStampTests
{
    [Fact]
    public void Capture_RegularFile_CapturesCompleteObservation()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string file =
            temp.CreateFile(
                "Fixture.nif",
                "observation-fixture"
            );

        using LinuxNoFollowPathHandle opened =
            OpenFile(
                temp.RootPath,
                "Fixture.nif"
            );

        LinuxOpenedFileObservationStampResult stamp =
            LinuxOpenedFileObservationStamp.Capture(
                opened
            );

        Assert.True(
            stamp.Success,
            stamp.Error
        );

        Assert.Equal(
            new FileInfo(file).Length,
            stamp.Size
        );

        Assert.NotNull(
            stamp.Identity
        );

        Assert.NotNull(
            stamp.Identity!.MountId
        );

        Assert.NotNull(
            stamp.ChangeTimeSeconds
        );

        Assert.NotNull(
            stamp.ChangeTimeNanoseconds
        );

        Assert.NotNull(
            stamp.ModificationTimeSeconds
        );

        Assert.NotNull(
            stamp.ModificationTimeNanoseconds
        );
    }

    [Fact]
    public void Capture_UnchangedDescriptor_ObservationsCompareEqual()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        temp.CreateFile(
            "Fixture.nif",
            "unchanged"
        );

        using LinuxNoFollowPathHandle opened =
            OpenFile(
                temp.RootPath,
                "Fixture.nif"
            );

        LinuxOpenedFileObservationStampResult first =
            LinuxOpenedFileObservationStamp.Capture(
                opened
            );

        LinuxOpenedFileObservationStampResult second =
            LinuxOpenedFileObservationStamp.Capture(
                opened
            );

        Assert.True(
            first.Success,
            first.Error
        );

        Assert.True(
            second.Success,
            second.Error
        );

        Assert.True(
            first.SameObservedStateAs(
                second
            )
        );
    }

    [Fact]
    public void Capture_HashBetweenObservations_DoesNotChangeStamp()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        temp.CreateFile(
            "Fixture.nif",
            "hash-read-only"
        );

        using LinuxNoFollowPathHandle opened =
            OpenFile(
                temp.RootPath,
                "Fixture.nif"
            );

        LinuxOpenedFileObservationStampResult before =
            LinuxOpenedFileObservationStamp.Capture(
                opened
            );

        LinuxOpenedFileSnapshotResult snapshot =
            LinuxOpenedFileSnapshot.Capture(
                opened,
                "/diagnostic/Data/Fixture.nif"
            );

        LinuxOpenedFileObservationStampResult after =
            LinuxOpenedFileObservationStamp.Capture(
                opened
            );

        Assert.True(
            before.Success,
            before.Error
        );

        Assert.True(
            snapshot.Success,
            snapshot.Error
        );

        Assert.True(
            after.Success,
            after.Error
        );

        Assert.True(
            before.SameObservedStateAs(
                after
            )
        );
    }

    [Fact]
    public void Capture_SameSizeInPlaceWrite_IsObservedAsChange()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string file =
            temp.CreateFile(
                "Fixture.nif",
                "AAAAAAAA"
            );

        using LinuxNoFollowPathHandle opened =
            OpenFile(
                temp.RootPath,
                "Fixture.nif"
            );

        LinuxOpenedFileObservationStampResult before =
            LinuxOpenedFileObservationStamp.Capture(
                opened
            );

        byte[] replacement =
            Encoding.UTF8.GetBytes(
                "BBBBBBBB"
            );

        using (
            SafeFileHandle writer =
                File.OpenHandle(
                    file,
                    FileMode.Open,
                    FileAccess.Write,
                    FileShare.ReadWrite |
                    FileShare.Delete))
        {
            RandomAccess.Write(
                writer,
                replacement,
                0
            );
        }

        LinuxOpenedFileObservationStampResult after =
            LinuxOpenedFileObservationStamp.Capture(
                opened
            );

        Assert.True(
            before.Success,
            before.Error
        );

        Assert.True(
            after.Success,
            after.Error
        );

        Assert.True(
            before.Identity!.SameObjectAs(
                after.Identity!
            )
        );

        Assert.Equal(
            before.Size,
            after.Size
        );

        Assert.False(
            before.SameObservedStateAs(
                after
            )
        );

        bool changeTimeChanged =
            before.ChangeTimeSeconds !=
                after.ChangeTimeSeconds ||
            before.ChangeTimeNanoseconds !=
                after.ChangeTimeNanoseconds;

        bool modificationTimeChanged =
            before.ModificationTimeSeconds !=
                after.ModificationTimeSeconds ||
            before.ModificationTimeNanoseconds !=
                after.ModificationTimeNanoseconds;

        Assert.True(
            changeTimeChanged ||
            modificationTimeChanged
        );
    }

    [Fact]
    public void Capture_Directory_IsRejectedAsNotRegularFile()
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

        LinuxOpenedFileObservationStampResult result =
            LinuxOpenedFileObservationStamp.Capture(
                opened
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxOpenedFileObservationStampState
                .NotRegularFile,
            result.State
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

    private sealed class TemporaryDirectory
        : IDisposable
    {
        public TemporaryDirectory()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-observation-stamp-tests",
                    Guid.NewGuid().ToString("N")
                );

            Directory.CreateDirectory(
                RootPath
            );
        }

        public string RootPath { get; }

        public string CreateFile(
            string name,
            string content)
        {
            string path =
                Path.Combine(
                    RootPath,
                    name
                );

            File.WriteAllText(
                path,
                content
            );

            return path;
        }

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
