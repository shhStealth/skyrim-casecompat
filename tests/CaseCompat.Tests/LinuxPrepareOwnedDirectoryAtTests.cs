using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxPrepareOwnedDirectoryAtTests
{
    [Fact]
    public void Prepare_NewStagingDirectory_ReturnsDurableOwnedDescriptor()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        string stagingName =
            ".casecompat-stage";

        LinuxPrepareOwnedDirectoryAtResult result =
            LinuxPrepareOwnedDirectoryAt.Prepare(
                fixture.Parent,
                stagingName,
                fixture.PathFor(
                    stagingName
                )
            );

        Assert.True(
            result.Success,
            result.Error
        );

        Assert.Equal(
            LinuxPrepareOwnedDirectoryAtState.PreparedDurably,
            result.State
        );

        Assert.True(
            result.StagingEntryChanged
        );

        Assert.False(
            result.StagingEntryMayRemain
        );

        Assert.NotNull(
            result.ParentSync
        );

        Assert.True(
            result.ParentSync!.Success
        );

        using LinuxPreparedOwnedDirectoryLease lease =
            Assert.IsType<
                LinuxPreparedOwnedDirectoryLease
            >(
                result.Lease
            );

        Assert.Equal(
            stagingName,
            lease.StagingChildName
        );

        Assert.True(
            lease.Identity.Success
        );

        Assert.NotNull(
            lease.Identity.DeviceMajor
        );

        Assert.NotNull(
            lease.Identity.DeviceMinor
        );

        Assert.NotNull(
            lease.Identity.Inode
        );

        Assert.NotNull(
            lease.Identity.MountId
        );

        Assert.True(
            Directory.Exists(
                fixture.PathFor(
                    stagingName
                )
            )
        );

        using LinuxOpenedChildHandle independentlyOpened =
            fixture.OpenDirectory(
                stagingName
            );

        LinuxFileIdentityResult independentIdentity =
            fixture.CaptureIdentity(
                independentlyOpened,
                stagingName
            );

        AssertSameIdentity(
            lease.Identity,
            independentIdentity
        );
    }

    [Fact]
    public void Prepare_ExistingStagingName_DoesNotAdoptOrReplaceIt()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        fixture.CreateDirectory(
            ".casecompat-stage"
        );

        string payload =
            Path.Combine(
                fixture.PathFor(
                    ".casecompat-stage"
                ),
                "existing.txt"
            );

        File.WriteAllText(
            payload,
            "existing"
        );

        LinuxPrepareOwnedDirectoryAtResult result =
            LinuxPrepareOwnedDirectoryAt.Prepare(
                fixture.Parent,
                ".casecompat-stage",
                fixture.PathFor(
                    ".casecompat-stage"
                )
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            LinuxPrepareOwnedDirectoryAtState
                .StagingAlreadyExists,
            result.State
        );

        Assert.False(
            result.StagingEntryChanged
        );

        Assert.False(
            result.StagingEntryMayRemain
        );

        Assert.Null(
            result.Lease
        );

        Assert.Equal(
            "existing",
            File.ReadAllText(
                payload
            )
        );
    }

    [Fact]
    public void Prepare_ReturnedDescriptor_CanAnchorNestedPreparation()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        LinuxPrepareOwnedDirectoryAtResult outer =
            LinuxPrepareOwnedDirectoryAt.Prepare(
                fixture.Parent,
                ".outer",
                fixture.PathFor(
                    ".outer"
                )
            );

        Assert.True(
            outer.Success,
            outer.Error
        );

        using LinuxPreparedOwnedDirectoryLease outerLease =
            Assert.IsType<
                LinuxPreparedOwnedDirectoryLease
            >(
                outer.Lease
            );

        string nestedDisplayPath =
            Path.Combine(
                fixture.PathFor(
                    ".outer"
                ),
                ".inner"
            );

        /*
         * No pathname reopen of .outer occurs here.
         *
         * Its already-open descriptor anchors the nested mkdirat,
         * openat, snapshot, and parent fsync.
         */
        LinuxPrepareOwnedDirectoryAtResult inner =
            LinuxPrepareOwnedDirectoryAt.Prepare(
                outerLease.OpenedDirectory,
                ".inner",
                nestedDisplayPath
            );

        Assert.True(
            inner.Success,
            inner.Error
        );

        using LinuxPreparedOwnedDirectoryLease innerLease =
            Assert.IsType<
                LinuxPreparedOwnedDirectoryLease
            >(
                inner.Lease
            );

        Assert.True(
            innerLease.Identity.Success
        );

        Assert.True(
            Directory.Exists(
                nestedDisplayPath
            )
        );
    }

    [Fact]
    public void Prepare_ResultCanBePublishedWithSameRecordedIdentity()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        LinuxPrepareOwnedDirectoryAtResult prepared =
            LinuxPrepareOwnedDirectoryAt.Prepare(
                fixture.Parent,
                ".stage",
                fixture.PathFor(
                    ".stage"
                )
            );

        Assert.True(
            prepared.Success,
            prepared.Error
        );

        using LinuxPreparedOwnedDirectoryLease lease =
            Assert.IsType<
                LinuxPreparedOwnedDirectoryLease
            >(
                prepared.Lease
            );

        LinuxPublishOwnedDirectoryAtResult publication =
            LinuxPublishOwnedDirectoryAt.Publish(
                fixture.Parent,
                ".stage",
                "Final",
                lease.OpenedDirectory,
                lease.IncarnationIdentity!
            );

        if (
            publication.State ==
            LinuxPublishOwnedDirectoryAtState
                .NoReplaceUnsupported)
        {
            return;
        }

        Assert.True(
            publication.Success,
            publication.Error
        );

        Assert.False(
            Directory.Exists(
                fixture.PathFor(
                    ".stage"
                )
            )
        );

        Assert.True(
            Directory.Exists(
                fixture.PathFor(
                    "Final"
                )
            )
        );

        LinuxFileIdentityResult afterPublication =
            fixture.CaptureIdentity(
                lease.OpenedDirectory,
                "Final"
            );

        AssertSameIdentity(
            lease.Identity,
            afterPublication
        );
    }

    private static void AssertSameIdentity(
        LinuxFileIdentityResult expected,
        LinuxFileIdentityResult actual)
    {
        Assert.Equal(
            expected.DeviceMajor,
            actual.DeviceMajor
        );

        Assert.Equal(
            expected.DeviceMinor,
            actual.DeviceMinor
        );

        Assert.Equal(
            expected.Inode,
            actual.Inode
        );

        Assert.Equal(
            expected.MountId,
            actual.MountId
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
                    "casecompat-owned-directory-prepare-tests",
                    Guid.NewGuid().ToString("N")
                );

            Directory.CreateDirectory(
                RootPath
            );

            Parent =
                OpenRoot(
                    RootPath
                );
        }

        public string RootPath { get; }

        public LinuxNoFollowPathHandle Parent { get; }

        public string PathFor(
            string childName)
        {
            return Path.Combine(
                RootPath,
                childName
            );
        }

        public void CreateDirectory(
            string childName)
        {
            LinuxCreateDirectoryAtResult result =
                LinuxCreateDirectoryAt.Create(
                    Parent,
                    childName
                );

            Assert.True(
                result.Success,
                result.Error
            );
        }

        public LinuxOpenedChildHandle OpenDirectory(
            string childName)
        {
            LinuxOpenChildReadOnlyAtResult result =
                LinuxOpenChildReadOnlyAt.Open(
                    Parent,
                    childName
                );

            Assert.True(
                result.Success,
                result.Error
            );

            return Assert.IsType<
                LinuxOpenedChildHandle
            >(
                result.OpenedChild
            );
        }

        public LinuxFileIdentityResult CaptureIdentity(
            ILinuxOpenedHandle handle,
            string displayName)
        {
            LinuxOpenedDirectorySnapshotResult snapshot =
                LinuxOpenedDirectorySnapshot.Capture(
                    handle,
                    PathFor(
                        displayName
                    )
                );

            Assert.True(
                snapshot.Success,
                snapshot.Error
            );

            return Assert.IsType<
                LinuxFileIdentityResult
            >(
                snapshot.Identity
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

        public void Dispose()
        {
            Parent.Dispose();

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
