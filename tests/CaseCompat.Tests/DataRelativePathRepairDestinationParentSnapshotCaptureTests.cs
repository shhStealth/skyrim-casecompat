using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairDestinationParentSnapshotCaptureTests
{
    [Fact]
    public void Capture_StrictNestedParent_UsesOpenedDescriptorSnapshot()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairDestinationParentSnapshotCaptureResult
            captured =
                DataRelativePathRepairDestinationParentSnapshotCapture
                    .Capture(
                        fixture.DataRoot,
                        fixture.ParentPath
                    );

        Assert.True(
            captured.Success,
            captured.Error
        );

        Assert.Equal(
            DataRelativePathRepairDestinationParentSnapshotCaptureState
                .Captured,
            captured.State
        );

        Assert.NotNull(
            captured.OpenedSnapshot
        );

        DataRelativePathRepairDestinationParentSnapshot snapshot =
            Assert.IsType<
                DataRelativePathRepairDestinationParentSnapshot
            >(
                captured.Snapshot
            );

        Assert.Equal(
            Path.GetFullPath(
                fixture.ParentPath
            ),
            snapshot.PhysicalPath
        );

        Assert.False(
            snapshot.CasefoldEnabled
        );

        LinuxNoFollowPathOpenResult directOpen =
            LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                fixture.DataRoot,
                "Parent"
            );

        Assert.True(
            directOpen.Success,
            directOpen.Error
        );

        using LinuxNoFollowPathHandle direct =
            directOpen.OpenedPath!;

        LinuxOpenedDirectorySnapshotResult directSnapshot =
            LinuxOpenedDirectorySnapshot.Capture(
                direct
            );

        Assert.True(
            directSnapshot.Success,
            directSnapshot.Error
        );

        Assert.True(
            snapshot.Identity.SameObjectAs(
                directSnapshot.Identity!
            )
        );

        Assert.Equal(
            directSnapshot.CasefoldEnabled,
            snapshot.CasefoldEnabled
        );

        Assert.Equal(
            directSnapshot.RawFlags,
            snapshot.RawFlags
        );
    }

    [Fact]
    public void Capture_OutsideTrustedDataRoot_IsRejectedBeforeOpen()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        string outside =
            Path.Combine(
                fixture.RootPath,
                "Outside"
            );

        Directory.CreateDirectory(
            outside
        );

        DataRelativePathRepairDestinationParentSnapshotCaptureResult
            captured =
                DataRelativePathRepairDestinationParentSnapshotCapture
                    .Capture(
                        fixture.DataRoot,
                        outside
                    );

        Assert.False(
            captured.Success
        );

        Assert.Equal(
            DataRelativePathRepairDestinationParentSnapshotCaptureState
                .ParentOutsideDataRoot,
            captured.State
        );

        Assert.Null(
            captured.OpenState
        );

        Assert.Null(
            captured.OpenedSnapshot
        );

        Assert.Null(
            captured.Snapshot
        );
    }

    [Fact]
    public void Capture_SymbolicLinkParent_IsRejectedByNoFollowOpen()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        string real =
            Path.Combine(
                fixture.DataRoot,
                "Real"
            );

        string link =
            Path.Combine(
                fixture.DataRoot,
                "Link"
            );

        Directory.CreateDirectory(
            real
        );

        Directory.CreateSymbolicLink(
            link,
            real
        );

        DataRelativePathRepairDestinationParentSnapshotCaptureResult
            captured =
                DataRelativePathRepairDestinationParentSnapshotCapture
                    .Capture(
                        fixture.DataRoot,
                        link
                    );

        Assert.False(
            captured.Success
        );

        Assert.Equal(
            DataRelativePathRepairDestinationParentSnapshotCaptureState
                .ParentOpenFailed,
            captured.State
        );

        Assert.NotNull(
            captured.OpenState
        );

        Assert.Null(
            captured.OpenedSnapshot
        );

        Assert.Null(
            captured.Snapshot
        );
    }

    [Fact]
    public void Capture_RelativeParentPath_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairDestinationParentSnapshotCaptureResult
            captured =
                DataRelativePathRepairDestinationParentSnapshotCapture
                    .Capture(
                        fixture.DataRoot,
                        "Parent"
                    );

        Assert.False(
            captured.Success
        );

        Assert.Equal(
            DataRelativePathRepairDestinationParentSnapshotCaptureState
                .InvalidParentPath,
            captured.State
        );

        Assert.Null(
            captured.OpenState
        );

        Assert.Null(
            captured.OpenedSnapshot
        );

        Assert.Null(
            captured.Snapshot
        );
    }

    private sealed class Fixture
        : IDisposable
    {
        public Fixture()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-parent-snapshot-tests",
                    Guid.NewGuid().ToString("N")
                );

            DataRoot =
                Path.Combine(
                    RootPath,
                    "Data"
                );

            ParentPath =
                Path.Combine(
                    DataRoot,
                    "Parent"
                );

            Directory.CreateDirectory(
                ParentPath
            );
        }

        public string RootPath { get; }

        public string DataRoot { get; }

        public string ParentPath { get; }

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
