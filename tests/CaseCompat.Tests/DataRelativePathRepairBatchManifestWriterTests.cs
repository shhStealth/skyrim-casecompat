using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairBatchManifestWriterTests
{
    private const string BatchManifestName =
        "batch-manifest.json";

    private const string ChildManifestName =
        "repair-plan.json";

    private const string ChildManifestSha256 =
        "0123456789ABCDEF0123456789ABCDEF" +
        "0123456789ABCDEF0123456789ABCDEF";

    [Fact]
    public void CreateInitial_ThenRead_RoundTripsExactBytesDurably()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        if (!fixture.SupportsUnnamedFiles())
        {
            return;
        }

        DataRelativePathRepairBatchManifestRecord manifest =
            CreateManifest(
                Guid.Parse(
                    "11111111-2222-3333-4444-555555555555"
                )
            );

        byte[] expectedBytes =
            DataRelativePathRepairBatchManifestJson.Serialize(
                manifest
            );

        string expectedSha256 =
            Convert.ToHexString(
                SHA256.HashData(
                    expectedBytes
                )
            );

        DataRelativePathRepairBatchManifestWriterResult write =
            DataRelativePathRepairBatchManifestWriter.CreateInitial(
                fixture.BatchDirectory,
                BatchManifestName,
                manifest
            );

        Assert.True(
            write.Success,
            write.Error
        );

        Assert.Equal(
            DataRelativePathRepairBatchManifestWriteState
                .CreatedDurably,
            write.State
        );

        Assert.True(
            write.ManifestEntryChanged
        );

        DataRelativePathRepairBatchManifestReaderResult read =
            DataRelativePathRepairBatchManifestReader.Read(
                fixture.BatchDirectory,
                BatchManifestName
            );

        Assert.True(
            read.Success,
            read.Error
        );

        Assert.Equal(
            manifest.BatchId,
            read.Manifest!.BatchId
        );

        Assert.Equal(
            expectedBytes.LongLength,
            read.Length
        );

        Assert.Equal(
            expectedSha256,
            read.ManifestSha256
        );

        Assert.NotNull(
            read.ManifestIncarnationIdentity
        );
    }

    [Fact]
    public void CreateInitial_ExistingManifest_IsNotOverwritten()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        if (!fixture.SupportsUnnamedFiles())
        {
            return;
        }

        DataRelativePathRepairBatchManifestRecord first =
            CreateManifest(
                Guid.Parse(
                    "11111111-2222-3333-4444-555555555555"
                )
            );

        DataRelativePathRepairBatchManifestRecord second =
            CreateManifest(
                Guid.Parse(
                    "66666666-7777-8888-9999-aaaaaaaaaaaa"
                )
            );

        DataRelativePathRepairBatchManifestWriterResult firstWrite =
            DataRelativePathRepairBatchManifestWriter.CreateInitial(
                fixture.BatchDirectory,
                BatchManifestName,
                first
            );

        Assert.True(
            firstWrite.Success,
            firstWrite.Error
        );

        DataRelativePathRepairBatchManifestWriterResult duplicate =
            DataRelativePathRepairBatchManifestWriter.CreateInitial(
                fixture.BatchDirectory,
                BatchManifestName,
                second
            );

        Assert.False(
            duplicate.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchManifestWriteState
                .ManifestAlreadyExists,
            duplicate.State
        );

        Assert.False(
            duplicate.ManifestEntryChanged
        );

        DataRelativePathRepairBatchManifestReaderResult read =
            DataRelativePathRepairBatchManifestReader.Read(
                fixture.BatchDirectory,
                BatchManifestName
            );

        Assert.True(
            read.Success,
            read.Error
        );

        Assert.Equal(
            first.BatchId,
            read.Manifest!.BatchId
        );

        Assert.NotEqual(
            second.BatchId,
            read.Manifest.BatchId
        );
    }

    [Fact]
    public void CreateInitial_InvalidManifest_IsRejectedBeforePublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairBatchManifestRecord invalid =
            CreateManifest(
                Guid.Parse(
                    "11111111-2222-3333-4444-555555555555"
                )
            ) with
            {
                BatchId =
                    Guid.Empty
            };

        DataRelativePathRepairBatchManifestWriterResult write =
            DataRelativePathRepairBatchManifestWriter.CreateInitial(
                fixture.BatchDirectory,
                BatchManifestName,
                invalid
            );

        Assert.False(
            write.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchManifestWriteState
                .InvalidManifest,
            write.State
        );

        Assert.False(
            write.ManifestEntryChanged
        );

        Assert.False(
            File.Exists(
                fixture.ManifestPath
            )
        );
    }

    [Fact]
    public void CreateInitial_InvalidManifestName_IsRejectedBeforePublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairBatchManifestRecord manifest =
            CreateManifest(
                Guid.Parse(
                    "11111111-2222-3333-4444-555555555555"
                )
            );

        DataRelativePathRepairBatchManifestWriterResult write =
            DataRelativePathRepairBatchManifestWriter.CreateInitial(
                fixture.BatchDirectory,
                "nested/batch-manifest.json",
                manifest
            );

        Assert.False(
            write.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchManifestWriteState
                .InvalidManifestName,
            write.State
        );

        Assert.False(
            write.ManifestEntryChanged
        );

        Assert.False(
            File.Exists(
                fixture.ManifestPath
            )
        );
    }

    [Fact]
    public void CreateInitial_OversizedManifest_IsRejectedBeforePublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairBatchManifestRecord oversized =
            CreateOversizedManifest();

        byte[] bytes =
            DataRelativePathRepairBatchManifestJson.Serialize(
                oversized
            );

        Assert.True(
            bytes.LongLength >
            DataRelativePathRepairBatchManifestReader
                .MaxManifestBytes,
            $"Test setup produced only {bytes.LongLength} bytes."
        );

        DataRelativePathRepairBatchManifestWriterResult write =
            DataRelativePathRepairBatchManifestWriter.CreateInitial(
                fixture.BatchDirectory,
                BatchManifestName,
                oversized
            );

        Assert.False(
            write.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchManifestWriteState
                .ManifestTooLarge,
            write.State
        );

        Assert.False(
            write.ManifestEntryChanged
        );

        Assert.False(
            File.Exists(
                fixture.ManifestPath
            )
        );
    }

    private static
        DataRelativePathRepairBatchManifestRecord
        CreateManifest(
            Guid batchId)
    {
        DataRelativePathRepairBatchManifestCreation creation =
            DataRelativePathRepairBatchManifest.Create(
                batchId:
                    batchId,
                createdUtc:
                    new DateTimeOffset(
                        2026,
                        9,
                        1,
                        12,
                        0,
                        0,
                        TimeSpan.Zero
                    ),
                dataRoot:
                    "/tmp/Skyrim/Data",
                childManifestName:
                    ChildManifestName,
                inputPathCount:
                    1,
                safeRejectionCount:
                    0,
                children:
                    [
                        new(
                            ChildName:
                                "plan-000001",
                            PlanId:
                                Guid.Parse(
                                    "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
                                ),
                            ManifestSha256:
                                ChildManifestSha256
                        )
                    ]
            );

        Assert.True(
            creation.Success,
            creation.Error
        );

        return Assert.IsType<
            DataRelativePathRepairBatchManifestRecord
        >(
            creation.Manifest
        );
    }

    private static
        DataRelativePathRepairBatchManifestRecord
        CreateOversizedManifest()
    {
        const int childCount =
            30_000;

        var children =
            new DataRelativePathRepairBatchManifestChild[
                childCount
            ];

        for (
            int index = 0;
            index < children.Length;
            index++)
        {
            children[index] =
                new(
                    ChildName:
                        $"plan-{index + 1:D6}",
                    PlanId:
                        Guid.ParseExact(
                            (index + 1).ToString(
                                "x32"
                            ),
                            "N"
                        ),
                    ManifestSha256:
                        ChildManifestSha256
                );
        }

        DataRelativePathRepairBatchManifestCreation creation =
            DataRelativePathRepairBatchManifest.Create(
                batchId:
                    Guid.Parse(
                        "11111111-2222-3333-4444-555555555555"
                    ),
                createdUtc:
                    new DateTimeOffset(
                        2026,
                        9,
                        1,
                        12,
                        0,
                        0,
                        TimeSpan.Zero
                    ),
                dataRoot:
                    "/tmp/Skyrim/Data",
                childManifestName:
                    ChildManifestName,
                inputPathCount:
                    childCount,
                safeRejectionCount:
                    0,
                children:
                    children
            );

        Assert.True(
            creation.Success,
            creation.Error
        );

        return Assert.IsType<
            DataRelativePathRepairBatchManifestRecord
        >(
            creation.Manifest
        );
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-batch-writer-" +
                    Guid.NewGuid().ToString("N")
                );

            BatchPath =
                Path.Combine(
                    RootPath,
                    "Batch"
                );

            ManifestPath =
                Path.Combine(
                    BatchPath,
                    BatchManifestName
                );

            Directory.CreateDirectory(
                BatchPath
            );

            LinuxNoFollowPathOpenResult opened =
                LinuxNoFollowPath.OpenRootReadOnly(
                    BatchPath
                );

            Assert.True(
                opened.Success,
                opened.Error
            );

            BatchDirectory =
                Assert.IsType<
                    LinuxNoFollowPathHandle
                >(
                    opened.OpenedPath
                );
        }

        public string RootPath { get; }

        public string BatchPath { get; }

        public string ManifestPath { get; }

        public LinuxNoFollowPathHandle
            BatchDirectory { get; }

        public bool SupportsUnnamedFiles()
        {
            LinuxCreateUnnamedFileAtResult probe =
                LinuxCreateUnnamedFileAt.Create(
                    BatchDirectory
                );

            if (
                probe.State ==
                LinuxCreateUnnamedFileAtState
                    .TmpfileUnsupported)
            {
                return false;
            }

            Assert.True(
                probe.Success,
                probe.Error
            );

            probe.OpenedFile!.Dispose();

            return true;
        }

        public void Dispose()
        {
            BatchDirectory.Dispose();

            if (
                Directory.Exists(
                    RootPath))
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
