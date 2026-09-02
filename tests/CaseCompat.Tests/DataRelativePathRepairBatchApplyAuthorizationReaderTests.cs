using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;
using System.Security.Cryptography;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairBatchApplyAuthorizationReaderTests
{
    private const string AuthorizationName =
        "batch-apply-authorization.json";

    private const string BatchSha =
        "0123456789ABCDEF0123456789ABCDEF" +
        "0123456789ABCDEF0123456789ABCDEF";

    [Fact]
    public void Read_ValidAuthorization_ReturnsExactByteHash()
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

        byte[] serialized =
            DataRelativePathRepairBatchApplyAuthorizationJson
                .Serialize(
                    authorization
                );

        byte[] exactBytes =
        [
            .. serialized,
            (byte)'\n',
            (byte)' ',
            (byte)'\t'
        ];

        File.WriteAllBytes(
            fixture.AuthorizationPath,
            exactBytes
        );

        string expectedSha256 =
            Convert.ToHexString(
                SHA256.HashData(
                    exactBytes
                )
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
            DataRelativePathRepairBatchApplyAuthorizationReadState
                .Read,
            read.State
        );

        Assert.Equal(
            authorization,
            read.Authorization
        );

        Assert.Equal(
            exactBytes.LongLength,
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
    public void Read_SymbolicLinkAuthorization_IsRejected()
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
                "authorization-target.json"
            );

        File.WriteAllBytes(
            target,
            DataRelativePathRepairBatchApplyAuthorizationJson
                .Serialize(
                    CreateAuthorization(
                        Guid.Parse(
                            "11111111-2222-3333-4444-555555555555"
                        )
                    )
                )
        );

        File.CreateSymbolicLink(
            fixture.AuthorizationPath,
            target
        );

        DataRelativePathRepairBatchApplyAuthorizationReaderResult
            read =
                DataRelativePathRepairBatchApplyAuthorizationReader
                    .Read(
                        fixture.BatchDirectory,
                        AuthorizationName
                    );

        Assert.False(
            read.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchApplyAuthorizationReadState
                .AuthorizationSymbolicLinkRejected,
            read.State
        );

        Assert.Null(
            read.AuthorizationSha256
        );
    }

    [Fact]
    public void Read_DirectoryAuthorization_IsRejectedAsNotRegularFile()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using Fixture fixture =
            new();

        Directory.CreateDirectory(
            fixture.AuthorizationPath
        );

        DataRelativePathRepairBatchApplyAuthorizationReaderResult
            read =
                DataRelativePathRepairBatchApplyAuthorizationReader
                    .Read(
                        fixture.BatchDirectory,
                        AuthorizationName
                    );

        Assert.False(
            read.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchApplyAuthorizationReadState
                .AuthorizationNotRegularFile,
            read.State
        );

        Assert.Null(
            read.AuthorizationSha256
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
            fixture.AuthorizationPath,
            "{ definitely not valid json"
        );

        DataRelativePathRepairBatchApplyAuthorizationReaderResult
            read =
                DataRelativePathRepairBatchApplyAuthorizationReader
                    .Read(
                        fixture.BatchDirectory,
                        AuthorizationName
                    );

        Assert.False(
            read.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchApplyAuthorizationReadState
                .DeserializeFailed,
            read.State
        );

        Assert.Null(
            read.AuthorizationSha256
        );
    }

    [Fact]
    public void Read_StructurallyInvalidAuthorization_FailsValidation()
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

        File.WriteAllBytes(
            fixture.AuthorizationPath,
            DataRelativePathRepairBatchApplyAuthorizationJson
                .Serialize(
                    invalid
                )
        );

        DataRelativePathRepairBatchApplyAuthorizationReaderResult
            read =
                DataRelativePathRepairBatchApplyAuthorizationReader
                    .Read(
                        fixture.BatchDirectory,
                        AuthorizationName
                    );

        Assert.False(
            read.Success
        );

        Assert.Equal(
            DataRelativePathRepairBatchApplyAuthorizationReadState
                .AuthorizationInvalid,
            read.State
        );

        Assert.Null(
            read.AuthorizationSha256
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
                    "casecompat-batch-apply-auth-reader-" +
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
