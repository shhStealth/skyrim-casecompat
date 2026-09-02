using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairBatchApplyAuthorizationWriterTests
{
    private const string AuthorizationName =
        "batch-apply-authorization.json";

    private const string BatchSha =
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

        DataRelativePathRepairBatchApplyAuthorizationRecord
            authorization =
                CreateAuthorization(
                    Guid.Parse(
                        "11111111-2222-3333-4444-555555555555"
                    )
                );

        byte[] expectedBytes =
            DataRelativePathRepairBatchApplyAuthorizationJson
                .Serialize(
                    authorization
                );

        string expectedSha256 =
            Convert.ToHexString(
                SHA256.HashData(
                    expectedBytes
                )
            );

        DataRelativePathRepairBatchApplyAuthorizationWriterResult
            write =
                DataRelativePathRepairBatchApplyAuthorizationWriter
                    .CreateInitial(
                        fixture.BatchDirectory,
                        AuthorizationName,
                        authorization
                    );

        Assert.True(
            write.Success,
            write.Error
        );

        Assert.Equal(
            DataRelativePathRepairBatchApplyAuthorizationWriteState
                .CreatedDurably,
            write.State
        );

        Assert.True(
            write.AuthorizationEntryChanged
        );

        DataRelativePathRepairBatchApplyAuthorizationReaderResult
            read =
                DataRelativePathRepairBatchApplyAuthorizationReader
                    .Read(
                        fixture.BatchDirectory,
                        AuthorizationName
                    );

        Assert.True(
            read.Success,
            read.Error
        );

        Assert.Equal(
            authorization,
            read.Authorization
        );

        Assert.Equal(
            expectedBytes.LongLength,
            read.Length
        );

        Assert.Equal(
            expectedSha256,
            read.AuthorizationSha256
        );

        Assert.NotNull(
            read.AuthorizationIncarnationIdentity
        );
    }

    [Fact]
    public void CreateInitial_ExistingAuthorization_IsNotOverwritten()
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

        DataRelativePathRepairBatchApplyAuthorizationRecord first =
            CreateAuthorization(
                Guid.Parse(
                    "11111111-2222-3333-4444-555555555555"
                )
            );

        DataRelativePathRepairBatchApplyAuthorizationRecord second =
            CreateAuthorization(
                Guid.Parse(
                    "66666666-7777-8888-9999-aaaaaaaaaaaa"
                )
            );

        DataRelativePathRepairBatchApplyAuthorizationWriterResult
            firstWrite =
                DataRelativePathRepairBatchApplyAuthorizationWriter
                    .CreateInitial(
                        fixture.BatchDirectory,
                        AuthorizationName,
                        first
                    );

        Assert.True(
            firstWrite.Success,
            firstWrite.Error
        );

        DataRelativePathRepairBatchApplyAuthorizationWriterResult
            duplicate =
                DataRelativePathRepairBatchApplyAuthorizationWriter
                    .CreateInitial(
                        fixture.BatchDirectory,
                        AuthorizationName,
                        second
                    );

        Assert.False(
            duplicate.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchApplyAuthorizationWriteState
                .AuthorizationAlreadyExists,
            duplicate.State
        );

        Assert.False(
            duplicate.AuthorizationEntryChanged
        );

        DataRelativePathRepairBatchApplyAuthorizationReaderResult
            read =
                DataRelativePathRepairBatchApplyAuthorizationReader
                    .Read(
                        fixture.BatchDirectory,
                        AuthorizationName
                    );

        Assert.True(
            read.Success,
            read.Error
        );

        Assert.Equal(
            first.BatchId,
            read.Authorization!.BatchId
        );

        Assert.NotEqual(
            second.BatchId,
            read.Authorization.BatchId
        );
    }

    [Fact]
    public void CreateInitial_InvalidAuthorization_IsRejectedBeforePublication()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairBatchApplyAuthorizationRecord invalid =
            CreateAuthorization(
                Guid.Parse(
                    "11111111-2222-3333-4444-555555555555"
                )
            ) with
            {
                BatchId =
                    Guid.Empty
            };

        DataRelativePathRepairBatchApplyAuthorizationWriterResult
            write =
                DataRelativePathRepairBatchApplyAuthorizationWriter
                    .CreateInitial(
                        fixture.BatchDirectory,
                        AuthorizationName,
                        invalid
                    );

        Assert.False(
            write.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchApplyAuthorizationWriteState
                .InvalidAuthorization,
            write.State
        );

        Assert.False(
            write.AuthorizationEntryChanged
        );

        Assert.False(
            File.Exists(
                fixture.AuthorizationPath
            )
        );
    }

    [Fact]
    public void CreateInitial_InvalidAuthorizationName_IsRejected()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        DataRelativePathRepairBatchApplyAuthorizationRecord
            authorization =
                CreateAuthorization(
                    Guid.Parse(
                        "11111111-2222-3333-4444-555555555555"
                    )
                );

        DataRelativePathRepairBatchApplyAuthorizationWriterResult
            write =
                DataRelativePathRepairBatchApplyAuthorizationWriter
                    .CreateInitial(
                        fixture.BatchDirectory,
                        "nested/batch-apply-authorization.json",
                        authorization
                    );

        Assert.False(
            write.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchApplyAuthorizationWriteState
                .InvalidAuthorizationName,
            write.State
        );

        Assert.False(
            write.AuthorizationEntryChanged
        );
    }

    private static
        DataRelativePathRepairBatchApplyAuthorizationRecord
        CreateAuthorization(
            Guid batchId)
    {
        DateTimeOffset createdUtc =
            new(
                2026,
                9,
                2,
                17,
                0,
                0,
                TimeSpan.Zero
            );

        DataRelativePathRepairBatchManifestCreation
            batchCreation =
                DataRelativePathRepairBatchManifest
                    .CreateCoverageAuthorized(
                        batchId,
                        createdUtc,
                        "/tmp/Skyrim/Data",
                        "repair-plan.json",
                        inputPathCount:
                            0,
                        safeRejectionCount:
                            0,
                        children:
                            []
                    );

        Assert.True(
            batchCreation.Success,
            batchCreation.Error
        );

        DataRelativePathRepairBatchApplyAuthorizationCreation
            authorizationCreation =
                DataRelativePathRepairBatchApplyAuthorization
                    .CreateForCompletedBatch(
                        batchCreation.Manifest!,
                        BatchSha,
                        createdUtc
                    );

        Assert.True(
            authorizationCreation.Success,
            authorizationCreation.Error
        );

        return authorizationCreation.Authorization!;
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            RootPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "casecompat-batch-apply-auth-writer-" +
                    Guid.NewGuid().ToString("N")
                );

            BatchPath =
                Path.Combine(
                    RootPath,
                    "Batch"
                );

            AuthorizationPath =
                Path.Combine(
                    BatchPath,
                    AuthorizationName
                );

            Directory.CreateDirectory(
                BatchPath
            );

            LinuxNoFollowPathOpenResult open =
                LinuxNoFollowPath.OpenRootReadOnly(
                    BatchPath
                );

            if (
                !open.Success ||
                open.OpenedPath is null)
            {
                throw new InvalidOperationException(
                    open.Error ??
                    open.State.ToString()
                );
            }

            BatchDirectory =
                open.OpenedPath;
        }

        public string RootPath
        {
            get;
        }

        public string BatchPath
        {
            get;
        }

        public string AuthorizationPath
        {
            get;
        }

        public LinuxNoFollowPathHandle BatchDirectory
        {
            get;
        }

        public bool SupportsUnnamedFiles()
        {
            var create =
                LinuxCreateUnnamedFileAt.Create(
                    BatchDirectory
                );

            if (
                !create.Success ||
                create.OpenedFile is null)
            {
                return false;
            }

            create.OpenedFile.Dispose();

            return true;
        }

        public void Dispose()
        {
            BatchDirectory.Dispose();

            try
            {
                Directory.Delete(
                    RootPath,
                    recursive:
                        true
                );
            }
            catch
            {
            }
        }
    }
}
