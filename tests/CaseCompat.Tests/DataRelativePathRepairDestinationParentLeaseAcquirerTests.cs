using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairDestinationParentLeaseAcquirerTests
{
    [Fact]
    public void Acquire_UnchangedParent_ReturnsValidatedOpenLease()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            CreateDataRoot(
                temp
            );

        string parent =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "00Taliesin"
                )
            ).FullName;

        DataRelativePathRepairDestinationParentSnapshot expected =
            Snapshot(
                dataRoot,
                parent
            );

        DataRelativePathRepairDestinationParentLeaseAcquisition
            result =
                DataRelativePathRepairDestinationParentLeaseAcquirer
                    .Acquire(
                        dataRoot,
                        expected
                    );

        Assert.True(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDestinationParentValidationState
                .Matched,
            result.Validation.State
        );

        DataRelativePathRepairValidatedDestinationParentLease
            lease =
                Assert.IsType<
                    DataRelativePathRepairValidatedDestinationParentLease
                >(
                    result.Lease
                );

        using (lease)
        {
            Assert.False(
                lease.OpenedPath.Handle.IsInvalid
            );

            Assert.False(
                lease.OpenedPath.Handle.IsClosed
            );

            Assert.True(
                expected.Identity.SameObjectAs(
                    lease.ActualSnapshot.Identity!
                )
            );

            Assert.False(
                lease.ActualSnapshot.CasefoldEnabled
            );
        }
    }

    [Fact]
    public void Acquire_DataRootParent_ReturnsValidatedOpenLease()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            CreateDataRoot(
                temp
            );

        DataRelativePathRepairDestinationParentSnapshot expected =
            Snapshot(
                dataRoot,
                dataRoot
            );

        DataRelativePathRepairDestinationParentLeaseAcquisition
            result =
                DataRelativePathRepairDestinationParentLeaseAcquirer
                    .Acquire(
                        dataRoot,
                        expected
                    );

        Assert.True(
            result.Success
        );

        DataRelativePathRepairValidatedDestinationParentLease
            lease =
                Assert.IsType<
                    DataRelativePathRepairValidatedDestinationParentLease
                >(
                    result.Lease
                );

        using (lease)
        {
            Assert.Equal(
                Path.GetFullPath(
                    dataRoot
                ),
                lease.OpenedPath.FullPath
            );

            Assert.True(
                expected.Identity.SameObjectAs(
                    lease.ActualSnapshot.Identity!
                )
            );
        }
    }

    [Fact]
    public void Acquire_PathReplacedBeforeAcquire_ReportsIdentityChanged()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            CreateDataRoot(
                temp
            );

        string parent =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "00Taliesin"
                )
            ).FullName;

        DataRelativePathRepairDestinationParentSnapshot expected =
            Snapshot(
                dataRoot,
                parent
            );

        string moved =
            Path.Combine(
                Path.GetDirectoryName(
                    parent
                )!,
                "00Taliesin-original"
            );

        Directory.Move(
            parent,
            moved
        );

        Directory.CreateDirectory(
            parent
        );

        DataRelativePathRepairDestinationParentLeaseAcquisition
            result =
                DataRelativePathRepairDestinationParentLeaseAcquirer
                    .Acquire(
                        dataRoot,
                        expected
                    );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDestinationParentValidationState
                .IdentityChanged,
            result.Validation.State
        );

        Assert.Null(
            result.Lease
        );
    }

    [Fact]
    public void Acquire_PathReplacedAfterAcquire_LeaseStillReferencesOriginalDirectory()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            CreateDataRoot(
                temp
            );

        string parent =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes",
                    "00Taliesin"
                )
            ).FullName;

        DataRelativePathRepairDestinationParentSnapshot expected =
            Snapshot(
                dataRoot,
                parent
            );

        DataRelativePathRepairDestinationParentLeaseAcquisition
            result =
                DataRelativePathRepairDestinationParentLeaseAcquirer
                    .Acquire(
                        dataRoot,
                        expected
                    );

        DataRelativePathRepairValidatedDestinationParentLease
            lease =
                Assert.IsType<
                    DataRelativePathRepairValidatedDestinationParentLease
                >(
                    result.Lease
                );

        using (lease)
        {
            string moved =
                Path.Combine(
                    Path.GetDirectoryName(
                        parent
                    )!,
                    "00Taliesin-original"
                );

            Directory.Move(
                parent,
                moved
            );

            Directory.CreateDirectory(
                parent
            );

            LinuxOpenedDirectorySnapshotResult
                afterReplacement =
                    LinuxOpenedDirectorySnapshot.Capture(
                        lease.OpenedPath
                    );

            Assert.True(
                afterReplacement.Success
            );

            LinuxFileIdentityResult movedIdentity =
                LinuxFileIdentity.Inspect(
                    moved
                );

            LinuxFileIdentityResult replacementIdentity =
                LinuxFileIdentity.Inspect(
                    parent
                );

            Assert.True(
                afterReplacement.Identity!
                    .SameObjectAs(
                        movedIdentity
                    )
            );

            Assert.False(
                afterReplacement.Identity!
                    .SameObjectAs(
                        replacementIdentity
                    )
            );
        }
    }

    [Fact]
    public void Acquire_ParentOutsideDataRoot_IsRejectedBeforeOpen()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            CreateDataRoot(
                temp
            );

        string outside =
            Directory.CreateDirectory(
                Path.Combine(
                    temp.RootPath,
                    "Outside"
                )
            ).FullName;

        DataRelativePathRepairDestinationParentSnapshot expected =
            Snapshot(
                outside,
                outside
            );

        DataRelativePathRepairDestinationParentLeaseAcquisition
            result =
                DataRelativePathRepairDestinationParentLeaseAcquirer
                    .Acquire(
                        dataRoot,
                        expected
                    );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDestinationParentValidationState
                .ParentOutsideDataRoot,
            result.Validation.State
        );

        Assert.Null(
            result.Validation.OpenState
        );

        Assert.Null(
            result.Validation.ActualSnapshot
        );

        Assert.Null(
            result.Lease
        );
    }

    [Fact]
    public void Acquire_CasefoldEnabledExpectedSnapshot_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var temp =
            new TemporaryDirectory();

        string dataRoot =
            CreateDataRoot(
                temp
            );

        string parent =
            Directory.CreateDirectory(
                Path.Combine(
                    dataRoot,
                    "meshes"
                )
            ).FullName;

        DataRelativePathRepairDestinationParentSnapshot actual =
            Snapshot(
                dataRoot,
                parent
            );

        DataRelativePathRepairDestinationParentSnapshot invalid =
            actual with
            {
                CasefoldEnabled =
                    true,
                RawFlags =
                    actual.RawFlags |
                    LinuxDirectoryFlags.FsCasefoldFlag
            };

        DataRelativePathRepairDestinationParentLeaseAcquisition
            result =
                DataRelativePathRepairDestinationParentLeaseAcquirer
                    .Acquire(
                        dataRoot,
                        invalid
                    );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairDestinationParentValidationState
                .InvalidExpectedSnapshot,
            result.Validation.State
        );

        Assert.Null(
            result.Validation.OpenState
        );

        Assert.Null(
            result.Lease
        );
    }

    private static string CreateDataRoot(
        TemporaryDirectory temp)
    {
        return Directory.CreateDirectory(
            Path.Combine(
                temp.RootPath,
                "Data"
            )
        ).FullName;
    }

    private static
        DataRelativePathRepairDestinationParentSnapshot
        Snapshot(
            string root,
            string directory)
    {
        string fullRoot =
            Path.GetFullPath(
                root
            );

        string fullDirectory =
            Path.GetFullPath(
                directory
            );

        string relative =
            Path.GetRelativePath(
                fullRoot,
                fullDirectory
            );

        LinuxNoFollowPathOpenResult openResult =
            relative == "."
                ? LinuxNoFollowPath.OpenRootReadOnly(
                    fullRoot
                )
                : LinuxNoFollowPath.OpenReadOnlyUnderRoot(
                    fullRoot,
                    relative
                );

        Assert.True(
            openResult.Success
        );

        LinuxNoFollowPathHandle opened =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                openResult.OpenedPath
            );

        using (opened)
        {
            LinuxOpenedDirectorySnapshotResult captured =
                LinuxOpenedDirectorySnapshot.Capture(
                    opened
                );

            Assert.True(
                captured.Success
            );

            return new
                DataRelativePathRepairDestinationParentSnapshot(
                    PhysicalPath:
                        fullDirectory,
                    Identity:
                        captured.Identity!,
                    CasefoldEnabled:
                        captured.CasefoldEnabled!.Value,
                    RawFlags:
                        captured.RawFlags!.Value
                );
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
                    "casecompat-destination-parent-lease-tests",
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
