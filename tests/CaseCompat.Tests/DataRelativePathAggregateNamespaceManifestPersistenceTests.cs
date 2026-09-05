using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;
using Xunit;

namespace CaseCompat.Tests;

public sealed partial class
    DataRelativePathAggregateNamespaceManifestTests
{
    private const string AggregateManifestPersistenceName =
        "aggregate-namespace-manifest.json";

    [Fact]
    public void Persistence_CreateInitial_ThenRead_RoundTripsExactBytesDurably()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using AggregatePersistenceFixture fixture =
            new();

        if (!fixture.SupportsUnnamedFiles())
        {
            return;
        }

        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        byte[] expectedBytes =
            DataRelativePathAggregateNamespaceManifestJson.Serialize(
                manifest
            );

        string expectedSha256 =
            Convert.ToHexString(
                SHA256.HashData(
                    expectedBytes
                )
            );

        DataRelativePathAggregateNamespaceManifestWriterResult write =
            DataRelativePathAggregateNamespaceManifestWriter.CreateInitial(
                fixture.ManifestDirectory,
                AggregateManifestPersistenceName,
                manifest
            );

        Assert.True(
            write.Success,
            write.Error
        );

        Assert.Equal(
            DataRelativePathAggregateNamespaceManifestWriteState
                .CreatedDurably,
            write.State
        );

        Assert.True(
            write.ManifestEntryChanged
        );

        DataRelativePathAggregateNamespaceManifestReaderResult read =
            DataRelativePathAggregateNamespaceManifestReader.Read(
                fixture.ManifestDirectory,
                AggregateManifestPersistenceName
            );

        Assert.True(
            read.Success,
            read.Error
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

        Assert.Equal(
            manifest.DataRoot,
            read.Manifest!.DataRoot
        );

        Assert.Equal(
            manifest.RootWindowsLogicalPath,
            read.Manifest.RootWindowsLogicalPath
        );
    }

    [Fact]
    public void Persistence_CreateInitial_ExistingManifest_IsNotOverwritten()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using AggregatePersistenceFixture fixture =
            new();

        if (!fixture.SupportsUnnamedFiles())
        {
            return;
        }

        DataRelativePathAggregateNamespaceManifestRecord first =
            CreateValidManifest();

        DataRelativePathAggregateNamespaceManifestRecord second =
            first with
            {
                CreatedUtc =
                    first.CreatedUtc.AddMinutes(
                        1
                    )
            };

        DataRelativePathAggregateNamespaceManifestWriterResult firstWrite =
            DataRelativePathAggregateNamespaceManifestWriter.CreateInitial(
                fixture.ManifestDirectory,
                AggregateManifestPersistenceName,
                first
            );

        Assert.True(
            firstWrite.Success,
            firstWrite.Error
        );

        DataRelativePathAggregateNamespaceManifestWriterResult duplicate =
            DataRelativePathAggregateNamespaceManifestWriter.CreateInitial(
                fixture.ManifestDirectory,
                AggregateManifestPersistenceName,
                second
            );

        Assert.False(
            duplicate.Success
        );

        Assert.Equal(
            DataRelativePathAggregateNamespaceManifestWriteState
                .ManifestAlreadyExists,
            duplicate.State
        );

        Assert.False(
            duplicate.ManifestEntryChanged
        );

        DataRelativePathAggregateNamespaceManifestReaderResult read =
            DataRelativePathAggregateNamespaceManifestReader.Read(
                fixture.ManifestDirectory,
                AggregateManifestPersistenceName
            );

        Assert.True(
            read.Success,
            read.Error
        );

        Assert.Equal(
            first.CreatedUtc,
            read.Manifest!.CreatedUtc
        );

        Assert.NotEqual(
            second.CreatedUtc,
            read.Manifest.CreatedUtc
        );
    }

    [Fact]
    public void Persistence_CreateInitial_InvalidManifest_IsRejectedBeforePublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using AggregatePersistenceFixture fixture =
            new();

        DataRelativePathAggregateNamespaceManifestRecord invalid =
            CreateValidManifest() with
            {
                SchemaVersion =
                    int.MaxValue
            };

        DataRelativePathAggregateNamespaceManifestWriterResult write =
            DataRelativePathAggregateNamespaceManifestWriter.CreateInitial(
                fixture.ManifestDirectory,
                AggregateManifestPersistenceName,
                invalid
            );

        Assert.False(
            write.Success
        );

        Assert.Equal(
            DataRelativePathAggregateNamespaceManifestWriteState
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
    public void Persistence_CreateInitial_InvalidManifestName_IsRejectedBeforePublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using AggregatePersistenceFixture fixture =
            new();

        DataRelativePathAggregateNamespaceManifestWriterResult write =
            DataRelativePathAggregateNamespaceManifestWriter.CreateInitial(
                fixture.ManifestDirectory,
                "nested/aggregate-namespace-manifest.json",
                CreateValidManifest()
            );

        Assert.False(
            write.Success
        );

        Assert.Equal(
            DataRelativePathAggregateNamespaceManifestWriteState
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
    public void Persistence_Read_ValidManifest_ReturnsExactByteHash()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using AggregatePersistenceFixture fixture =
            new();

        DataRelativePathAggregateNamespaceManifestRecord manifest =
            CreateValidManifest();

        byte[] serialized =
            DataRelativePathAggregateNamespaceManifestJson.Serialize(
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

        DataRelativePathAggregateNamespaceManifestReaderResult read =
            DataRelativePathAggregateNamespaceManifestReader.Read(
                fixture.ManifestDirectory,
                AggregateManifestPersistenceName
            );

        Assert.True(
            read.Success,
            read.Error
        );

        Assert.Equal(
            DataRelativePathAggregateNamespaceManifestReadState.Read,
            read.State
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
            DataRelativePathAggregateNamespaceManifest.Validate(
                read.Manifest!
            )
        );
    }

    [Fact]
    public void Persistence_Read_DescriptorRelativeDirectoryHandle_Succeeds()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using AggregatePersistenceFixture fixture =
            new();

        fixture.WriteManifest(
            CreateValidManifest()
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

        LinuxOpenChildReadOnlyAtResult openedDirectory =
            LinuxOpenChildReadOnlyAt.Open(
                root,
                "Evidence"
            );

        Assert.True(
            openedDirectory.Success,
            openedDirectory.Error
        );

        using LinuxOpenedChildHandle directory =
            Assert.IsType<
                LinuxOpenedChildHandle
            >(
                openedDirectory.OpenedChild
            );

        DataRelativePathAggregateNamespaceManifestReaderResult read =
            DataRelativePathAggregateNamespaceManifestReader.Read(
                directory,
                AggregateManifestPersistenceName
            );

        Assert.True(
            read.Success,
            read.Error
        );

        Assert.NotNull(
            read.ManifestSha256
        );
    }

    [Fact]
    public void Persistence_Read_InvalidManifestName_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using AggregatePersistenceFixture fixture =
            new();

        DataRelativePathAggregateNamespaceManifestReaderResult read =
            DataRelativePathAggregateNamespaceManifestReader.Read(
                fixture.ManifestDirectory,
                "nested/aggregate-namespace-manifest.json"
            );

        Assert.False(
            read.Success
        );

        Assert.Equal(
            DataRelativePathAggregateNamespaceManifestReadState
                .InvalidManifestName,
            read.State
        );
    }

    [Fact]
    public void Persistence_Read_MissingManifest_IsUnavailable()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using AggregatePersistenceFixture fixture =
            new();

        DataRelativePathAggregateNamespaceManifestReaderResult read =
            DataRelativePathAggregateNamespaceManifestReader.Read(
                fixture.ManifestDirectory,
                AggregateManifestPersistenceName
            );

        Assert.False(
            read.Success
        );

        Assert.Equal(
            DataRelativePathAggregateNamespaceManifestReadState
                .ManifestUnavailable,
            read.State
        );

        Assert.Null(
            read.ManifestSha256
        );
    }

    [Fact]
    public void Persistence_Read_SymbolicLinkManifest_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using AggregatePersistenceFixture fixture =
            new();

        string target =
            Path.Combine(
                fixture.RootPath,
                "target.json"
            );

        File.WriteAllBytes(
            target,
            DataRelativePathAggregateNamespaceManifestJson.Serialize(
                CreateValidManifest()
            )
        );

        File.CreateSymbolicLink(
            fixture.ManifestPath,
            target
        );

        DataRelativePathAggregateNamespaceManifestReaderResult read =
            DataRelativePathAggregateNamespaceManifestReader.Read(
                fixture.ManifestDirectory,
                AggregateManifestPersistenceName
            );

        Assert.False(
            read.Success
        );

        Assert.Equal(
            DataRelativePathAggregateNamespaceManifestReadState
                .ManifestSymbolicLinkRejected,
            read.State
        );

        Assert.Null(
            read.ManifestSha256
        );
    }

    [Fact]
    public void Persistence_Read_MalformedJson_FailsDeserialization()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using AggregatePersistenceFixture fixture =
            new();

        File.WriteAllText(
            fixture.ManifestPath,
            "{ definitely not valid json"
        );

        DataRelativePathAggregateNamespaceManifestReaderResult read =
            DataRelativePathAggregateNamespaceManifestReader.Read(
                fixture.ManifestDirectory,
                AggregateManifestPersistenceName
            );

        Assert.False(
            read.Success
        );

        Assert.Equal(
            DataRelativePathAggregateNamespaceManifestReadState
                .DeserializeFailed,
            read.State
        );

        Assert.Null(
            read.ManifestSha256
        );
    }

    [Fact]
    public void Persistence_Read_StructurallyInvalidManifest_FailsValidation()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using AggregatePersistenceFixture fixture =
            new();

        DataRelativePathAggregateNamespaceManifestRecord invalid =
            CreateValidManifest() with
            {
                SchemaVersion =
                    int.MaxValue
            };

        File.WriteAllBytes(
            fixture.ManifestPath,
            DataRelativePathAggregateNamespaceManifestJson.Serialize(
                invalid
            )
        );

        DataRelativePathAggregateNamespaceManifestReaderResult read =
            DataRelativePathAggregateNamespaceManifestReader.Read(
                fixture.ManifestDirectory,
                AggregateManifestPersistenceName
            );

        Assert.False(
            read.Success
        );

        Assert.Equal(
            DataRelativePathAggregateNamespaceManifestReadState
                .ManifestInvalid,
            read.State
        );

        Assert.Null(
            read.ManifestSha256
        );
    }

    private sealed class AggregatePersistenceFixture : IDisposable
    {
        public AggregatePersistenceFixture()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-aggregate-persistence-" +
                    Guid.NewGuid().ToString(
                        "N"
                    )
                );

            DirectoryPath =
                Path.Combine(
                    RootPath,
                    "Evidence"
                );

            ManifestPath =
                Path.Combine(
                    DirectoryPath,
                    AggregateManifestPersistenceName
                );

            Directory.CreateDirectory(
                DirectoryPath
            );

            LinuxNoFollowPathOpenResult opened =
                LinuxNoFollowPath.OpenRootReadOnly(
                    DirectoryPath
                );

            Assert.True(
                opened.Success,
                opened.Error
            );

            ManifestDirectory =
                Assert.IsType<
                    LinuxNoFollowPathHandle
                >(
                    opened.OpenedPath
                );
        }

        public string RootPath { get; }

        public string DirectoryPath { get; }

        public string ManifestPath { get; }

        public LinuxNoFollowPathHandle
            ManifestDirectory { get; }

        public bool SupportsUnnamedFiles()
        {
            LinuxCreateUnnamedFileAtResult create =
                LinuxCreateUnnamedFileAt.Create(
                    ManifestDirectory
                );

            if (!create.Success)
            {
                return false;
            }

            using LinuxUnnamedFileHandle temporary =
                Assert.IsType<
                    LinuxUnnamedFileHandle
                >(
                    create.OpenedFile
                );

            return true;
        }

        public void WriteManifest(
            DataRelativePathAggregateNamespaceManifestRecord manifest)
        {
            File.WriteAllBytes(
                ManifestPath,
                DataRelativePathAggregateNamespaceManifestJson.Serialize(
                    manifest
                )
            );
        }

        public void Dispose()
        {
            ManifestDirectory.Dispose();

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
