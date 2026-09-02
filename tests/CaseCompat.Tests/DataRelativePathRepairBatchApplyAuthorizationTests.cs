using CaseCompat.Core.Repair;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairBatchApplyAuthorizationTests
{
    private const string BatchSha =
        "0123456789ABCDEF0123456789ABCDEF" +
        "0123456789ABCDEF0123456789ABCDEF";

    private static readonly Guid BatchId =
        Guid.Parse(
            "11111111-2222-3333-4444-555555555555"
        );

    private static readonly DateTimeOffset T0 =
        new(
            2026,
            9,
            2,
            17,
            0,
            0,
            TimeSpan.Zero
        );

    [Fact]
    public void
        CreateForCompletedBatch_CoverageAuthorizedV2_Succeeds()
    {
        DataRelativePathRepairBatchManifestRecord batch =
            CoverageBatch();

        DataRelativePathRepairBatchApplyAuthorizationCreation
            creation =
                DataRelativePathRepairBatchApplyAuthorization
                    .CreateForCompletedBatch(
                        batch,
                        BatchSha,
                        T0
                    );

        Assert.True(
            creation.Success,
            creation.Error
        );

        DataRelativePathRepairBatchApplyAuthorizationRecord
            authorization =
                Assert.IsType<
                    DataRelativePathRepairBatchApplyAuthorizationRecord
                >(
                    creation.Authorization
                );

        Assert.Equal(
            DataRelativePathRepairBatchApplyAuthorizationRecord
                .SchemaVersion1,
            authorization.SchemaVersion
        );

        Assert.Equal(
            BatchId,
            authorization.BatchId
        );

        Assert.Equal(
            batch.DataRoot,
            authorization.DataRoot
        );

        Assert.Equal(
            BatchSha,
            authorization.BatchManifestSha256
        );

        Assert.Equal(
            DataRelativePathRepairBatchManifestRecord
                .CoveragePolicyVersion1,
            authorization.CoveragePolicyVersion
        );
    }

    [Fact]
    public void
        CreateForCompletedBatch_LegacyV1_IsRejected()
    {
        DataRelativePathRepairBatchManifestCreation creation =
            DataRelativePathRepairBatchManifest.Create(
                BatchId,
                T0,
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
            creation.Success,
            creation.Error
        );

        DataRelativePathRepairBatchApplyAuthorizationCreation
            authorization =
                DataRelativePathRepairBatchApplyAuthorization
                    .CreateForCompletedBatch(
                        creation.Manifest!,
                        BatchSha,
                        T0
                    );

        Assert.False(
            authorization.Success
        );

        Assert.Contains(
            "schema-v2",
            authorization.Error
        );
    }

    [Fact]
    public void
        ValidateCompletedBatchBinding_ExactBinding_Succeeds()
    {
        DataRelativePathRepairBatchManifestRecord batch =
            CoverageBatch();

        DataRelativePathRepairBatchApplyAuthorizationRecord
            authorization =
                DataRelativePathRepairBatchApplyAuthorization
                    .CreateForCompletedBatch(
                        batch,
                        BatchSha,
                        T0
                    )
                    .Authorization!;

        Assert.Null(
            DataRelativePathRepairBatchApplyAuthorization
                .ValidateCompletedBatchBinding(
                    authorization,
                    batch,
                    BatchSha
                )
        );
    }

    [Fact]
    public void
        ValidateCompletedBatchBinding_DifferentManifestSha_Fails()
    {
        DataRelativePathRepairBatchManifestRecord batch =
            CoverageBatch();

        DataRelativePathRepairBatchApplyAuthorizationRecord
            authorization =
                DataRelativePathRepairBatchApplyAuthorization
                    .CreateForCompletedBatch(
                        batch,
                        BatchSha,
                        T0
                    )
                    .Authorization!;

        const string differentSha =
            "abcdef0123456789abcdef0123456789" +
            "abcdef0123456789abcdef0123456789";

        string? error =
            DataRelativePathRepairBatchApplyAuthorization
                .ValidateCompletedBatchBinding(
                    authorization,
                    batch,
                    differentSha
                );

        Assert.NotNull(
            error
        );

        Assert.Contains(
            "exact current batch-manifest bytes",
            error
        );
    }

    [Fact]
    public void
        JsonRoundTrip_PreservesExactBinding()
    {
        DataRelativePathRepairBatchManifestRecord batch =
            CoverageBatch();

        DataRelativePathRepairBatchApplyAuthorizationRecord
            authorization =
                DataRelativePathRepairBatchApplyAuthorization
                    .CreateForCompletedBatch(
                        batch,
                        BatchSha,
                        T0
                    )
                    .Authorization!;

        byte[] json =
            DataRelativePathRepairBatchApplyAuthorizationJson
                .Serialize(
                    authorization
                );

        DataRelativePathRepairBatchApplyAuthorizationRecord restored =
            Assert.IsType<
                DataRelativePathRepairBatchApplyAuthorizationRecord
            >(
                DataRelativePathRepairBatchApplyAuthorizationJson
                    .Deserialize(
                        json
                    )
            );

        Assert.Equal(
            authorization,
            restored
        );

        Assert.Null(
            DataRelativePathRepairBatchApplyAuthorization.Validate(
                restored
            )
        );
    }

    private static
        DataRelativePathRepairBatchManifestRecord
        CoverageBatch()
    {
        DataRelativePathRepairBatchManifestCreation creation =
            DataRelativePathRepairBatchManifest
                .CreateCoverageAuthorized(
                    BatchId,
                    T0,
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
            creation.Success,
            creation.Error
        );

        return creation.Manifest!;
    }
}
