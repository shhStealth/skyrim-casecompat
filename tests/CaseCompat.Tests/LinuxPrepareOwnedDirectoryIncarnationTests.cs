using CaseCompat.Filesystem.Linux;
using Xunit;

namespace CaseCompat.Tests;

public sealed class LinuxPrepareOwnedDirectoryIncarnationTests
{
    [Fact]
    public void Prepare_Success_LeaseCarriesStrongIncarnationIdentity()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string root =
            Path.Combine(
                Path.GetTempPath(),
                "casecompat-prepared-directory-incarnation-tests",
                Guid.NewGuid().ToString("N")
            );

        Directory.CreateDirectory(
            root
        );

        try
        {
            LinuxNoFollowPathOpenResult parentOpen =
                LinuxNoFollowPath.OpenRootReadOnly(
                    root
                );

            Assert.True(
                parentOpen.Success,
                parentOpen.Error
            );

            using LinuxNoFollowPathHandle parent =
                Assert.IsType<
                    LinuxNoFollowPathHandle
                >(
                    parentOpen.OpenedPath
                );

            string stagingName =
                ".casecompat-stage";

            string stagingPath =
                Path.Combine(
                    root,
                    stagingName
                );

            LinuxPrepareOwnedDirectoryAtResult prepared =
                LinuxPrepareOwnedDirectoryAt.Prepare(
                    parent,
                    stagingName,
                    stagingPath
                );

            if (
                prepared.State ==
                LinuxPrepareOwnedDirectoryAtState
                    .StagingGenerationUnavailable)
            {
                return;
            }

            Assert.True(
                prepared.Success,
                prepared.Error
            );

            Assert.NotNull(
                prepared.Incarnation
            );

            Assert.True(
                prepared.Incarnation!.Success,
                prepared.Incarnation.Error
            );

            using LinuxPreparedOwnedDirectoryLease lease =
                Assert.IsType<
                    LinuxPreparedOwnedDirectoryLease
                >(
                    prepared.Lease
                );

            Assert.True(
                lease.IncarnationIdentity.Success
            );

            Assert.Equal(
                lease.Identity,
                lease.IncarnationIdentity.PhysicalIdentity
            );

            LinuxOpenedDirectoryIncarnationResult recaptured =
                LinuxOpenedDirectoryIncarnation.Capture(
                    lease.OpenedDirectory,
                    stagingPath
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

            Assert.Equal(
                lease.IncarnationIdentity.InodeGeneration,
                recaptured.Identity!.InodeGeneration
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
