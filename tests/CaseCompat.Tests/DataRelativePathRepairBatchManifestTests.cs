using CaseCompat.Core.Repair;
using System.Text;
using Xunit;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathRepairBatchManifestTests
{
    private static readonly DateTimeOffset T0 =
        new(
            2026,
            9,
            1,
            12,
            0,
            0,
            TimeSpan.Zero
        );

    private static readonly Guid BatchId =
        Guid.Parse(
            "11111111-2222-3333-4444-555555555555"
        );

    private static readonly Guid Plan1Id =
        Guid.Parse(
            "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeee1"
        );

    private static readonly Guid Plan2Id =
        Guid.Parse(
            "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeee2"
        );

    private const string Hash1 =
        "0123456789ABCDEF0123456789ABCDEF" +
        "0123456789ABCDEF0123456789ABCDEF";

    private const string Hash2 =
        "abcdef0123456789abcdef0123456789" +
        "abcdef0123456789abcdef0123456789";

    [Fact]
    public void Create_ValidRecord_SucceedsAndCopiesChildren()
    {
        var children =
            new[]
            {
                Child(
                    "plan-000001",
                    Plan1Id,
                    Hash1
                ),
                Child(
                    "plan-000002",
                    Plan2Id,
                    Hash2
                )
            };

        DataRelativePathRepairBatchManifestCreation creation =
            DataRelativePathRepairBatchManifest.Create(
                BatchId,
                T0,
                "/tmp/Skyrim/Data",
                "repair-plan.json",
                inputPathCount:
                    5,
                safeRejectionCount:
                    3,
                children
            );

        Assert.True(
            creation.Success,
            creation.Error
        );

        DataRelativePathRepairBatchManifestRecord manifest =
            Assert.IsType<
                DataRelativePathRepairBatchManifestRecord
            >(
                creation.Manifest
            );

        Assert.Equal(
            DataRelativePathRepairBatchManifestRecord
                .SchemaVersion1,
            manifest.SchemaVersion
        );

        Assert.Equal(
            BatchId,
            manifest.BatchId
        );

        Assert.Equal(
            T0,
            manifest.CreatedUtc
        );

        Assert.Equal(
            "/tmp/Skyrim/Data",
            manifest.DataRoot
        );

        Assert.Equal(
            "repair-plan.json",
            manifest.ChildManifestName
        );

        Assert.Equal(
            5,
            manifest.InputPathCount
        );

        Assert.Equal(
            3,
            manifest.SafeRejectionCount
        );

        Assert.Equal(
            2,
            manifest.Children.Count
        );

        Assert.NotSame(
            children,
            manifest.Children
        );
    }

    [Fact]
    public void Validate_UnsupportedSchema_Fails()
    {
        DataRelativePathRepairBatchManifestRecord manifest =
            ValidManifest() with
            {
                SchemaVersion =
                    2
            };

        Assert.NotNull(
            DataRelativePathRepairBatchManifest.Validate(
                manifest
            )
        );
    }

    [Fact]
    public void Validate_EmptyBatchId_Fails()
    {
        DataRelativePathRepairBatchManifestRecord manifest =
            ValidManifest() with
            {
                BatchId =
                    Guid.Empty
            };

        Assert.NotNull(
            DataRelativePathRepairBatchManifest.Validate(
                manifest
            )
        );
    }

    [Fact]
    public void Validate_RelativeDataRoot_Fails()
    {
        DataRelativePathRepairBatchManifestRecord manifest =
            ValidManifest() with
            {
                DataRoot =
                    "Data"
            };

        Assert.NotNull(
            DataRelativePathRepairBatchManifest.Validate(
                manifest
            )
        );
    }

    [Fact]
    public void Validate_InvalidChildManifestName_Fails()
    {
        DataRelativePathRepairBatchManifestRecord manifest =
            ValidManifest() with
            {
                ChildManifestName =
                    "nested/repair-plan.json"
            };

        Assert.NotNull(
            DataRelativePathRepairBatchManifest.Validate(
                manifest
            )
        );
    }

    [Fact]
    public void Validate_InconsistentCounts_Fails()
    {
        DataRelativePathRepairBatchManifestRecord manifest =
            ValidManifest() with
            {
                InputPathCount =
                    4
            };

        Assert.NotNull(
            DataRelativePathRepairBatchManifest.Validate(
                manifest
            )
        );
    }

    [Fact]
    public void Validate_NoncontiguousChildName_Fails()
    {
        DataRelativePathRepairBatchManifestRecord manifest =
            ValidManifest() with
            {
                Children =
                    [
                        Child(
                            "plan-000001",
                            Plan1Id,
                            Hash1
                        ),
                        Child(
                            "plan-000003",
                            Plan2Id,
                            Hash2
                        )
                    ]
            };

        Assert.NotNull(
            DataRelativePathRepairBatchManifest.Validate(
                manifest
            )
        );
    }

    [Fact]
    public void Validate_DuplicatePlanId_Fails()
    {
        DataRelativePathRepairBatchManifestRecord manifest =
            ValidManifest() with
            {
                Children =
                    [
                        Child(
                            "plan-000001",
                            Plan1Id,
                            Hash1
                        ),
                        Child(
                            "plan-000002",
                            Plan1Id,
                            Hash2
                        )
                    ]
            };

        Assert.NotNull(
            DataRelativePathRepairBatchManifest.Validate(
                manifest
            )
        );
    }

    [Fact]
    public void Validate_InvalidManifestSha256_Fails()
    {
        DataRelativePathRepairBatchManifestRecord manifest =
            ValidManifest() with
            {
                Children =
                    [
                        Child(
                            "plan-000001",
                            Plan1Id,
                            Hash1
                        ),
                        Child(
                            "plan-000002",
                            Plan2Id,
                            "not-a-sha256"
                        )
                    ]
            };

        Assert.NotNull(
            DataRelativePathRepairBatchManifest.Validate(
                manifest
            )
        );
    }

    [Fact]
    public void Json_RoundTrip_PreservesValidatedRecordAndPascalCase()
    {
        DataRelativePathRepairBatchManifestRecord manifest =
            ValidManifest();

        byte[] json =
            DataRelativePathRepairBatchManifestJson.Serialize(
                manifest
            );

        string text =
            Encoding.UTF8.GetString(
                json
            );

        Assert.Contains(
            "\"SchemaVersion\"",
            text
        );

        Assert.Contains(
            "\"BatchId\"",
            text
        );

        Assert.Contains(
            "\"ManifestSha256\"",
            text
        );

        Assert.DoesNotContain(
            "\"schemaVersion\"",
            text
        );

        DataRelativePathRepairBatchManifestRecord? restored =
            DataRelativePathRepairBatchManifestJson.Deserialize(
                json
            );

        Assert.NotNull(
            restored
        );

        Assert.Null(
            DataRelativePathRepairBatchManifest.Validate(
                restored
            )
        );

        Assert.Equal(
            manifest.SchemaVersion,
            restored.SchemaVersion
        );

        Assert.Equal(
            manifest.BatchId,
            restored.BatchId
        );

        Assert.Equal(
            manifest.CreatedUtc,
            restored.CreatedUtc
        );

        Assert.Equal(
            manifest.DataRoot,
            restored.DataRoot
        );

        Assert.Equal(
            manifest.ChildManifestName,
            restored.ChildManifestName
        );

        Assert.Equal(
            manifest.InputPathCount,
            restored.InputPathCount
        );

        Assert.Equal(
            manifest.SafeRejectionCount,
            restored.SafeRejectionCount
        );

        Assert.Equal(
            manifest.Children,
            restored.Children
        );
    }

    private static
        DataRelativePathRepairBatchManifestRecord
        ValidManifest()
    {
        return new(
            SchemaVersion:
                DataRelativePathRepairBatchManifestRecord
                    .SchemaVersion1,
            BatchId:
                BatchId,
            CreatedUtc:
                T0,
            DataRoot:
                "/tmp/Skyrim/Data",
            ChildManifestName:
                "repair-plan.json",
            InputPathCount:
                5,
            SafeRejectionCount:
                3,
            Children:
                [
                    Child(
                        "plan-000001",
                        Plan1Id,
                        Hash1
                    ),
                    Child(
                        "plan-000002",
                        Plan2Id,
                        Hash2
                    )
                ]
        );
    }

    private static DataRelativePathRepairBatchManifestChild
        Child(
            string childName,
            Guid planId,
            string manifestSha256)
    {
        return new(
            ChildName:
                childName,
            PlanId:
                planId,
            ManifestSha256:
                manifestSha256
        );
    }
}
