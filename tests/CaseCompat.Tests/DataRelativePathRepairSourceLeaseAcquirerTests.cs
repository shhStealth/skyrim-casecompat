using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace CaseCompat.Tests;

public sealed class DataRelativePathRepairSourceLeaseAcquirerTests
{
    [Fact]
    public void Acquire_UnchangedSource_ReturnsValidatedOpenLease()
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

        string source =
            CreateSource(
                dataRoot,
                "meshes/example/fixture.nif",
                "lease-fixture"
            );

        DataRelativePathRepairSourceSnapshot expected =
            Snapshot(
                source
            );

        DataRelativePathRepairSourceLeaseAcquisition result =
            DataRelativePathRepairSourceLeaseAcquirer.Acquire(
                dataRoot,
                expected
            );

        Assert.True(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairSourceValidationState
                .Matched,
            result.Validation.State
        );

        DataRelativePathRepairValidatedSourceLease lease =
            Assert.IsType<
                DataRelativePathRepairValidatedSourceLease
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

            Assert.Equal(
                expected.Sha256,
                lease.ActualSnapshot.Sha256
            );

            Assert.True(
                expected.Identity.SameObjectAs(
                    lease.ActualSnapshot.Identity!
                )
            );
        }
    }

    [Fact]
    public void Acquire_PathReplacedBeforeAcquire_ReturnsNoLease()
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

        string source =
            CreateSource(
                dataRoot,
                "meshes/example/fixture.nif",
                "original"
            );

        DataRelativePathRepairSourceSnapshot expected =
            Snapshot(
                source
            );

        string moved =
            Path.Combine(
                Path.GetDirectoryName(
                    source
                )!,
                "original-moved.nif"
            );

        File.Move(
            source,
            moved
        );

        File.WriteAllText(
            source,
            "replacement"
        );

        DataRelativePathRepairSourceLeaseAcquisition result =
            DataRelativePathRepairSourceLeaseAcquirer.Acquire(
                dataRoot,
                expected
            );

        Assert.False(
            result.Success
        );

        Assert.Equal(
            DataRelativePathRepairSourceValidationState
                .IdentityChanged,
            result.Validation.State
        );

        Assert.Null(
            result.Lease
        );
    }

    [Fact]
    public void Acquire_PathReplacedAfterAcquire_LeaseStillReferencesOriginalFile()
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

        const string originalContent =
            "leased-original-content";

        string source =
            CreateSource(
                dataRoot,
                "meshes/example/fixture.nif",
                originalContent
            );

        DataRelativePathRepairSourceSnapshot expected =
            Snapshot(
                source
            );

        DataRelativePathRepairSourceLeaseAcquisition result =
            DataRelativePathRepairSourceLeaseAcquirer.Acquire(
                dataRoot,
                expected
            );

        DataRelativePathRepairValidatedSourceLease lease =
            Assert.IsType<
                DataRelativePathRepairValidatedSourceLease
            >(
                result.Lease
            );

        using (lease)
        {
            string moved =
                Path.Combine(
                    Path.GetDirectoryName(
                        source
                    )!,
                    "leased-original-moved.nif"
                );

            File.Move(
                source,
                moved
            );

            File.WriteAllText(
                source,
                "replacement-content"
            );

            LinuxOpenedFileSnapshotResult afterReplacement =
                LinuxOpenedFileSnapshot.Capture(
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
                    source
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

            string expectedHash =
                Convert.ToHexString(
                    SHA256.HashData(
                        Encoding.UTF8.GetBytes(
                            originalContent
                        )
                    )
                );

            Assert.Equal(
                expectedHash,
                afterReplacement.Sha256
            );
        }
    }

    [Fact]
    public void Dispose_ValidatedLease_ClosesOpenedDescriptor()
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

        string source =
            CreateSource(
                dataRoot,
                "meshes/example/fixture.nif",
                "fixture"
            );

        DataRelativePathRepairSourceSnapshot expected =
            Snapshot(
                source
            );

        DataRelativePathRepairSourceLeaseAcquisition result =
            DataRelativePathRepairSourceLeaseAcquirer.Acquire(
                dataRoot,
                expected
            );

        DataRelativePathRepairValidatedSourceLease lease =
            Assert.IsType<
                DataRelativePathRepairValidatedSourceLease
            >(
                result.Lease
            );

        Assert.False(
            lease.OpenedPath.Handle.IsClosed
        );

        lease.Dispose();

        Assert.True(
            lease.OpenedPath.Handle.IsClosed
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

    private static string CreateSource(
        string dataRoot,
        string relativePath,
        string content)
    {
        string fullPath =
            Path.Combine(
                dataRoot,
                relativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar
                )
            );

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                fullPath
            )!
        );

        File.WriteAllText(
            fullPath,
            content
        );

        return fullPath;
    }

    private static DataRelativePathRepairSourceSnapshot
        Snapshot(
            string physicalPath)
    {
        LinuxFileIdentityResult identity =
            LinuxFileIdentity.Inspect(
                physicalPath
            );

        Assert.True(
            identity.Success
        );

        byte[] bytes =
            File.ReadAllBytes(
                physicalPath
            );

        return new DataRelativePathRepairSourceSnapshot(
            PhysicalPath:
                Path.GetFullPath(
                    physicalPath
                ),
            Size:
                bytes.LongLength,
            Sha256:
                Convert.ToHexString(
                    SHA256.HashData(
                        bytes
                    )
                ),
            Identity:
                identity
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
                    "casecompat-source-lease-tests",
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
