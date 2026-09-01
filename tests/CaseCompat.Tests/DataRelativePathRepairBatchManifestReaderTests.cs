using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairBatchManifestReaderTests
{
    private const string BatchManifestName =
        "batch-manifest.json";

    private const string ChildManifestName =
        "repair-plan.json";

    private const string ChildManifestSha256 =
        "0123456789ABCDEF0123456789ABCDEF" +
        "0123456789ABCDEF0123456789ABCDEF";

    [Fact]
    public void Read_ValidManifest_ReturnsExactByteHash()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairBatchManifestRecord manifest =
            CreateManifest();

        byte[] serialized =
            DataRelativePathRepairBatchManifestJson.Serialize(
                manifest
            );

        byte[] exactBytes =
            [
                .. serialized,
                (byte)'\n',
                (byte)' ',
                (byte)'\t'
            ];

        File.WriteAllBytes(
            fixture.ManifestPath,
            exactBytes
        );

        string expectedSha256 =
            Convert.ToHexString(
                SHA256.HashData(
                    exactBytes
                )
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
            DataRelativePathRepairBatchManifestReadState.Read,
            read.State
        );

        Assert.Equal(
            manifest.BatchId,
            read.Manifest!.BatchId
        );

        Assert.Equal(
            exactBytes.LongLength,
            read.Length
        );

        Assert.Equal(
            expectedSha256,
            read.ManifestSha256
        );

        Assert.NotNull(
            read.ManifestIncarnationIdentity
        );

        Assert.Null(
            DataRelativePathRepairBatchManifest.Validate(
                read.Manifest
            )
        );
    }

    [Fact]
    public void Read_DescriptorRelativeBatchHandle_Succeeds()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        WriteManifest(
            fixture.ManifestPath,
            CreateManifest()
        );

        LinuxNoFollowPathOpenResult openedRoot =
            LinuxNoFollowPath.OpenRootReadOnly(
                fixture.RootPath
            );

        Assert.True(
            openedRoot.Success,
            openedRoot.Error
        );

        using LinuxNoFollowPathHandle root =
            Assert.IsType<
                LinuxNoFollowPathHandle
            >(
                openedRoot.OpenedPath
            );

        LinuxOpenChildReadOnlyAtResult openedBatch =
            LinuxOpenChildReadOnlyAt.Open(
                root,
                "Batch"
            );

        Assert.True(
            openedBatch.Success,
            openedBatch.Error
        );

        using LinuxOpenedChildHandle batch =
            Assert.IsType<
                LinuxOpenedChildHandle
            >(
                openedBatch.OpenedChild
            );

        DataRelativePathRepairBatchManifestReaderResult read =
            DataRelativePathRepairBatchManifestReader.Read(
                batch,
                BatchManifestName
            );

        Assert.True(
            read.Success,
            read.Error
        );

        Assert.Equal(
            CreateManifest().BatchId,
            read.Manifest!.BatchId
        );

        Assert.NotNull(
            read.ManifestSha256
        );
    }

    [Fact]
    public void Read_InvalidManifestName_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairBatchManifestReaderResult read =
            DataRelativePathRepairBatchManifestReader.Read(
                fixture.BatchDirectory,
                "nested/batch-manifest.json"
            );

        Assert.False(
            read.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchManifestReadState
                .InvalidManifestName,
            read.State
        );
    }

    [Fact]
    public void Read_MissingManifest_IsUnavailable()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairBatchManifestReaderResult read =
            DataRelativePathRepairBatchManifestReader.Read(
                fixture.BatchDirectory,
                BatchManifestName
            );

        Assert.False(
            read.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchManifestReadState
                .ManifestUnavailable,
            read.State
        );

        Assert.Null(
            read.ManifestSha256
        );
    }

    [Fact]
    public void Read_SymbolicLinkManifest_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        string target =
            Path.Combine(
                fixture.RootPath,
                "target.json"
            );

        WriteManifest(
            target,
            CreateManifest()
        );

        File.CreateSymbolicLink(
            fixture.ManifestPath,
            target
        );

        DataRelativePathRepairBatchManifestReaderResult read =
            DataRelativePathRepairBatchManifestReader.Read(
                fixture.BatchDirectory,
                BatchManifestName
            );

        Assert.False(
            read.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchManifestReadState
                .ManifestSymbolicLinkRejected,
            read.State
        );

        Assert.Null(
            read.ManifestSha256
        );
    }

    [Fact]
    public void Read_MalformedJson_FailsDeserialization()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        File.WriteAllText(
            fixture.ManifestPath,
            "{ definitely not valid json"
        );

        DataRelativePathRepairBatchManifestReaderResult read =
            DataRelativePathRepairBatchManifestReader.Read(
                fixture.BatchDirectory,
                BatchManifestName
            );

        Assert.False(
            read.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchManifestReadState
                .DeserializeFailed,
            read.State
        );

        Assert.Null(
            read.ManifestSha256
        );
    }

    [Fact]
    public void Read_StructurallyInvalidBatchManifest_FailsValidation()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairBatchManifestRecord invalid =
            CreateManifest() with
            {
                Children =
                    [
                        new(
                            ChildName:
                                "plan-000002",
                            PlanId:
                                Guid.Parse(
                                    "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
                                ),
                            ManifestSha256:
                                ChildManifestSha256
                        )
                    ]
            };

        byte[] json =
            DataRelativePathRepairBatchManifestJson.Serialize(
                invalid
            );

        File.WriteAllBytes(
            fixture.ManifestPath,
            json
        );

        DataRelativePathRepairBatchManifestReaderResult read =
            DataRelativePathRepairBatchManifestReader.Read(
                fixture.BatchDirectory,
                BatchManifestName
            );

        Assert.False(
            read.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchManifestReadState
                .ManifestInvalid,
            read.State
        );

        Assert.Null(
            read.ManifestSha256
        );
    }

    private static
        DataRelativePathRepairBatchManifestRecord
        CreateManifest()
    {
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

    private static void WriteManifest(
        string path,
        DataRelativePathRepairBatchManifestRecord manifest)
    {
        File.WriteAllBytes(
            path,
            DataRelativePathRepairBatchManifestJson.Serialize(
                manifest
            )
        );
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-batch-reader-" +
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
