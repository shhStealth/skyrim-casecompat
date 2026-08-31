using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairDestinationParentIncarnationTests
{
    [Fact]
    public void Acquire_ValidParent_LeaseRetainsSameIncarnation()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string root =
            Path.Combine(
                Path.GetTempPath(),
                "casecompat-parent-incarnation-tests",
                Guid.NewGuid().ToString("N")
            );

        string dataRoot =
            Path.Combine(
                root,
                "Data"
            );

        string parentPath =
            Path.Combine(
                dataRoot,
                "Parent"
            );

        Directory.CreateDirectory(
            parentPath
        );

        try
        {
            LinuxNoFollowPathOpenResult parentOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    parentPath
                );

            Assert.True(
                parentOpen.Success,
                parentOpen.Error
            );

            DataRelativePathRepairDestinationParentSnapshot
                expectedSnapshot;

            using (
                LinuxNoFollowPathHandle parent =
                    Assert.IsType<
                        LinuxNoFollowPathHandle
                    >(
                        parentOpen.OpenedPath
                    ))
            {
                LinuxOpenedDirectorySnapshotResult snapshot =
                    LinuxOpenedDirectorySnapshot.Capture(
                        parent
                    );

                Assert.True(
                    snapshot.Success,
                    snapshot.Error
                );

                Assert.NotNull(
                    snapshot.Identity
                );

                Assert.NotNull(
                    snapshot.CasefoldEnabled
                );

                Assert.NotNull(
                    snapshot.RawFlags
                );

                Assert.False(
                    snapshot.CasefoldEnabled!.Value
                );

                expectedSnapshot =
                    new
                        DataRelativePathRepairDestinationParentSnapshot(
                            PhysicalPath:
                                parentPath,
                            Identity:
                                snapshot.Identity!,
                            CasefoldEnabled:
                                snapshot.CasefoldEnabled.Value,
                            RawFlags:
                                snapshot.RawFlags!.Value
                        );
            }

            DataRelativePathRepairDestinationParentLeaseAcquisition
                acquisition =
                    DataRelativePathRepairDestinationParentLeaseAcquirer
                        .Acquire(
                            dataRoot,
                            expectedSnapshot
                        );

            Assert.True(
                acquisition.Success,
                acquisition.Validation.Error
            );

            using DataRelativePathRepairValidatedDestinationParentLease
                lease =
                    Assert.IsType<
                        DataRelativePathRepairValidatedDestinationParentLease
                    >(
                        acquisition.Lease
                    );

            LinuxOpenedDirectoryIncarnationResult incarnation =
                lease.ActualIncarnation;

            if (
                incarnation.State ==
                LinuxOpenedDirectoryIncarnationState
                    .GenerationUnavailable)
            {
                return;
            }

            Assert.True(
                incarnation.Success,
                incarnation.Error
            );

            Assert.NotNull(
                lease.IncarnationIdentity
            );

            LinuxFileIdentityResult snapshotIdentity =
                lease.ActualSnapshot.Identity!;

            LinuxFileIdentityResult incarnationPhysical =
                lease.IncarnationIdentity!.PhysicalIdentity;

            Assert.Equal(
                snapshotIdentity.DeviceMajor,
                incarnationPhysical.DeviceMajor
            );

            Assert.Equal(
                snapshotIdentity.DeviceMinor,
                incarnationPhysical.DeviceMinor
            );

            Assert.Equal(
                snapshotIdentity.Inode,
                incarnationPhysical.Inode
            );

            Assert.Equal(
                snapshotIdentity.MountId,
                incarnationPhysical.MountId
            );

            /*
             * Re-capturing from the exact retained descriptor must
             * produce the same directory incarnation.
             */
            LinuxOpenedDirectoryIncarnationResult recaptured =
                LinuxOpenedDirectoryIncarnation.Capture(
                    lease.OpenedPath
                );

            Assert.True(
                recaptured.Success,
                recaptured.Error
            );

            Assert.True(
                lease.IncarnationIdentity
                    .SameIncarnationAs(
                        recaptured.Identity!
                    )
            );
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true
                );
            }
        }
    }
}
