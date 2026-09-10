using System.Text;
using CaseCompat.Core.Repair;
using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Tests;

public sealed class
    DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonTests
{
    [Fact]
    public void SerializeValidated_ValidPlan_IsDeterministicAndUsesStringEnums()
    {
        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord record =
            CreateRecord();

        DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonSerializationResult
            first =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanJson
                    .SerializeValidated(
                        record
                    );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonSerializationResult
            second =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanJson
                    .SerializeValidated(
                        record
                    );

        Assert.True(
            first.Success,
            first.Error
        );

        Assert.True(
            second.Success,
            second.Error
        );

        Assert.Equal(
            first.Bytes,
            second.Bytes
        );

        string json =
            Encoding.UTF8.GetString(
                first.Bytes!
            );

        Assert.Contains(
            "\"Kind\": \"CreateDirectory\"",
            json,
            StringComparison.Ordinal
        );

        Assert.Contains(
            "\"Kind\": \"CreateFile\"",
            json,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void SerializeValidated_InvalidPlan_IsRejectedBeforeBytes()
    {
        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord changed =
            CreateRecord() with
            {
                SchemaVersion =
                    DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
                        .SchemaVersion1 +
                    1
            };

        DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonSerializationResult
            result =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanJson
                    .SerializeValidated(
                        changed
                    );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonSerializationState
                .InvalidPlan,
            result.State
        );

        Assert.Null(
            result.Bytes
        );

        Assert.NotNull(
            result.Error
        );
    }

    [Fact]
    public void DeserializeValidated_RoundTripPreservesRecordAndCanonicalBytes()
    {
        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord record =
            CreateRecord();

        DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonSerializationResult
            encoded =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanJson
                    .SerializeValidated(
                        record
                    );

        Assert.True(
            encoded.Success,
            encoded.Error
        );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonDeserializationResult
            decoded =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanJson
                    .DeserializeValidated(
                        encoded.Bytes!
                    );

        Assert.True(
            decoded.Success,
            decoded.Error
        );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord restored =
            Assert.IsType<
                DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
            >(
                decoded.Record
            );

        Assert.Equal(
            record.SchemaVersion,
            restored.SchemaVersion
        );

        Assert.Equal(
            record.PlanId,
            restored.PlanId
        );

        Assert.Equal(
            record.CreatedUtc,
            restored.CreatedUtc
        );

        Assert.Equal(
            record.DataRoot,
            restored.DataRoot
        );

        Assert.Equal(
            record.RequestedPath,
            restored.RequestedPath
        );

        Assert.Equal(
            record.SourceSnapshot,
            restored.SourceSnapshot
        );

        Assert.Equal(
            record.SourceInodeGeneration,
            restored.SourceInodeGeneration
        );

        Assert.Equal(
            record.InitialDestinationParentSnapshot,
            restored.InitialDestinationParentSnapshot
        );

        Assert.Equal(
            record.Operations.Count,
            restored.Operations.Count
        );

        for (
            int index = 0;
            index < record.Operations.Count;
            index++)
        {
            Assert.Equal(
                record.Operations[index],
                restored.Operations[index]
            );
        }

        DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonSerializationResult
            reencoded =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanJson
                    .SerializeValidated(
                        restored
                    );

        Assert.True(
            reencoded.Success,
            reencoded.Error
        );

        Assert.Equal(
            encoded.Bytes,
            reencoded.Bytes
        );
    }

    [Fact]
    public void
        DeserializeValidated_RoundTripPreservesAliasSourcesAndUsesStringEnum()
    {
        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord record =
            CreateRecordWithAlias();

        DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonSerializationResult
            encoded =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanJson
                    .SerializeValidated(
                        record
                    );

        Assert.True(
            encoded.Success,
            encoded.Error
        );

        string json =
            Encoding.UTF8.GetString(
                encoded.Bytes!
            );

        Assert.Contains(
            "\"Kind\": \"CreateAliasSymlink\"",
            json,
            StringComparison.Ordinal
        );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonDeserializationResult
            decoded =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanJson
                    .DeserializeValidated(
                        encoded.Bytes!
                    );

        Assert.True(
            decoded.Success,
            decoded.Error
        );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord restored =
            Assert.IsType<
                DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
            >(
                decoded.Record
            );

        Assert.Single(
            restored.AliasSources
        );

        Assert.Equal(
            record.AliasSources[0],
            restored.AliasSources[0]
        );

        Assert.Equal(
            record.Operations.Count,
            restored.Operations.Count
        );

        for (
            int index = 0;
            index < record.Operations.Count;
            index++)
        {
            Assert.Equal(
                record.Operations[index],
                restored.Operations[index]
            );
        }

        DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonSerializationResult
            reencoded =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanJson
                    .SerializeValidated(
                        restored
                    );

        Assert.True(
            reencoded.Success,
            reencoded.Error
        );

        Assert.Equal(
            encoded.Bytes,
            reencoded.Bytes
        );
    }

    [Fact]
    public void DeserializeValidated_MalformedJson_IsRejected()
    {
        byte[] malformed =
            Encoding.UTF8.GetBytes(
                "{"
            );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonDeserializationResult
            result =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanJson
                    .DeserializeValidated(
                        malformed
                    );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonDeserializationState
                .DeserializeFailed,
            result.State
        );

        Assert.Null(
            result.Record
        );
    }

    [Fact]
    public void DeserializeValidated_UnknownTopLevelMember_IsRejected()
    {
        string json =
            SerializeText(
                CreateRecord()
            );

        int closingBrace =
            json.LastIndexOf(
                '}'
            );

        Assert.True(
            closingBrace > 0
        );

        string changed =
            json.Insert(
                closingBrace,
                ",\n  \"UnexpectedAuthority\": true\n"
            );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonDeserializationResult
            result =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanJson
                    .DeserializeValidated(
                        Encoding.UTF8.GetBytes(
                            changed
                        )
                    );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonDeserializationState
                .DeserializeFailed,
            result.State
        );

        Assert.Null(
            result.Record
        );
    }

    [Fact]
    public void DeserializeValidated_UnknownNestedMember_IsRejected()
    {
        string json =
            SerializeText(
                CreateRecord()
            );

        string marker =
            "\"Size\": 6,";

        Assert.Contains(
            marker,
            json,
            StringComparison.Ordinal
        );

        string changed =
            json.Replace(
                marker,
                "\"Size\": 6,\n    \"UnexpectedSourceAuthority\": true,",
                StringComparison.Ordinal
            );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonDeserializationResult
            result =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanJson
                    .DeserializeValidated(
                        Encoding.UTF8.GetBytes(
                            changed
                        )
                    );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonDeserializationState
                .DeserializeFailed,
            result.State
        );

        Assert.Null(
            result.Record
        );
    }

    [Fact]
    public void DeserializeValidated_SemanticallyInvalidPlan_IsRejectedAfterParsing()
    {
        string json =
            SerializeText(
                CreateRecord()
            );

        string changed =
            json.Replace(
                "\"RequestedPath\": \"Meshes/Actors/Character/File.NIF\"",
                "\"RequestedPath\": \"Meshes/Actors/Character/Other.NIF\"",
                StringComparison.Ordinal
            );

        Assert.NotEqual(
            json,
            changed
        );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonDeserializationResult
            result =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanJson
                    .DeserializeValidated(
                        Encoding.UTF8.GetBytes(
                            changed
                        )
                    );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonDeserializationState
                .InvalidPlan,
            result.State
        );

        Assert.Null(
            result.Record
        );

        Assert.NotNull(
            result.Error
        );
    }

    [Fact]
    public void DeserializeValidated_NumericEnumRepresentation_IsRejected()
    {
        string json =
            SerializeText(
                CreateRecord()
            );

        string changed =
            json.Replace(
                "\"Kind\": \"CreateDirectory\"",
                "\"Kind\": 0",
                StringComparison.Ordinal
            );

        Assert.NotEqual(
            json,
            changed
        );

        DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonDeserializationResult
            result =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanJson
                    .DeserializeValidated(
                        Encoding.UTF8.GetBytes(
                            changed
                        )
                    );

        Assert.Equal(
            DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonDeserializationState
                .DeserializeFailed,
            result.State
        );

        Assert.Null(
            result.Record
        );
    }

    private static string SerializeText(
        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord record)
    {
        DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonSerializationResult
            result =
                DataRelativePathTargetedConsumerCaseRepairDurablePlanJson
                    .SerializeValidated(
                        record
                    );

        Assert.True(
            result.Success,
            result.Error
        );

        return Encoding.UTF8.GetString(
            result.Bytes!
        );
    }

    private static
        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
        CreateRecord()
    {
        const string dataRoot =
            "/game/Data";

        const string source =
            "/game/Data/meshes/actors/character/file.nif";

        const string requested =
            "Meshes/Actors/Character/File.NIF";

        var sourceSnapshot =
            new DataRelativePathRepairSourceSnapshot(
                PhysicalPath:
                    source,
                Size:
                    6,
                Sha256:
                    new string(
                        'A',
                        64
                    ),
                Identity:
                    Identity(
                        source,
                        inode:
                            100UL
                    )
            );

        var parentSnapshot =
            new DataRelativePathRepairDestinationParentSnapshot(
                PhysicalPath:
                    dataRoot,
                Identity:
                    Identity(
                        dataRoot,
                        inode:
                            200UL
                    ),
                CasefoldEnabled:
                    false,
                RawFlags:
                    0
            );

        DataRelativePathRepairPlanOperation[] operations =
        [
            new(
                Kind:
                    DataRelativePathRepairPlanOperationKind
                        .CreateDirectory,
                DestinationPath:
                    "/game/Data/Meshes",
                SourcePath:
                    null
            ),
            new(
                Kind:
                    DataRelativePathRepairPlanOperationKind
                        .CreateDirectory,
                DestinationPath:
                    "/game/Data/Meshes/Actors",
                SourcePath:
                    null
            ),
            new(
                Kind:
                    DataRelativePathRepairPlanOperationKind
                        .CreateDirectory,
                DestinationPath:
                    "/game/Data/Meshes/Actors/Character",
                SourcePath:
                    null
            ),
            new(
                Kind:
                    DataRelativePathRepairPlanOperationKind
                        .CreateFile,
                DestinationPath:
                    "/game/Data/Meshes/Actors/Character/File.NIF",
                SourcePath:
                    source
            )
        ];

        var record =
            new DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord(
                SchemaVersion:
                    DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
                        .SchemaVersion1,
                PlanId:
                    Guid.Parse(
                        "718d85ad-07f3-4dc3-8f08-b9e5ded7bf77"
                    ),
                CreatedUtc:
                    new DateTimeOffset(
                        2026,
                        9,
                        8,
                        4,
                        0,
                        0,
                        TimeSpan.Zero
                    ),
                DataRoot:
                    dataRoot,
                RequestedPath:
                    requested,
                SourceSnapshot:
                    sourceSnapshot,
                SourceInodeGeneration:
                    12345U,
                InitialDestinationParentSnapshot:
                    parentSnapshot,
                Operations:
                    operations,
                DirectoryRenameSources:
                    Array.Empty<
                        DataRelativePathRepairDirectoryRenameSource
                    >(),
                AliasSources:
                    Array.Empty<
                        DataRelativePathRepairAliasSource
                    >()
            );

        Assert.Null(
            DataRelativePathTargetedConsumerCaseRepairDurablePlan.Validate(
                record
            )
        );

        return record;
    }

    private static
        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
        CreateRecordWithAlias()
    {
        const string dataRoot =
            "/game/Data";

        const string source =
            "/game/Data/meshes/actors/character/file.nif";

        const string requested =
            "Meshes/Actors/Character/File.NIF";

        const string realActorsDirectory =
            "/game/Data/Meshes/actors";

        var sourceSnapshot =
            new DataRelativePathRepairSourceSnapshot(
                PhysicalPath:
                    source,
                Size:
                    6,
                Sha256:
                    new string(
                        'A',
                        64
                    ),
                Identity:
                    Identity(
                        source,
                        inode:
                            100UL
                    )
            );

        var parentSnapshot =
            new DataRelativePathRepairDestinationParentSnapshot(
                PhysicalPath:
                    dataRoot,
                Identity:
                    Identity(
                        dataRoot,
                        inode:
                            200UL
                    ),
                CasefoldEnabled:
                    false,
                RawFlags:
                    0
            );

        // "Meshes" and "Character" are plain new directories. "Actors" is
        // a genuinely contested ancestor: a different, unrelated
        // candidate's own winning consumer needs the real, lowercase
        // "actors" directory left exactly where it is, so this operation
        // is an alias rather than a rename.
        DataRelativePathRepairPlanOperation[] operations =
        [
            new(
                Kind:
                    DataRelativePathRepairPlanOperationKind
                        .CreateDirectory,
                DestinationPath:
                    "/game/Data/Meshes",
                SourcePath:
                    null
            ),
            new(
                Kind:
                    DataRelativePathRepairPlanOperationKind
                        .CreateAliasSymlink,
                DestinationPath:
                    "/game/Data/Meshes/Actors",
                SourcePath:
                    realActorsDirectory
            ),
            new(
                Kind:
                    DataRelativePathRepairPlanOperationKind
                        .CreateDirectory,
                DestinationPath:
                    "/game/Data/Meshes/Actors/Character",
                SourcePath:
                    null
            ),
            new(
                Kind:
                    DataRelativePathRepairPlanOperationKind
                        .CreateFile,
                DestinationPath:
                    "/game/Data/Meshes/Actors/Character/File.NIF",
                SourcePath:
                    source
            )
        ];

        DataRelativePathRepairAliasSource[] aliasSources =
        [
            new(
                DestinationPath:
                    "/game/Data/Meshes/Actors",
                PhysicalPath:
                    realActorsDirectory,
                Identity:
                    Identity(
                        realActorsDirectory,
                        inode:
                            300UL
                    ),
                InodeGeneration:
                    7U
            )
        ];

        var record =
            new DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord(
                SchemaVersion:
                    DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
                        .SchemaVersion1,
                PlanId:
                    Guid.Parse(
                        "9c1a6f0e-2222-4444-8888-abcdefabcdef"
                    ),
                CreatedUtc:
                    new DateTimeOffset(
                        2026,
                        9,
                        8,
                        4,
                        0,
                        0,
                        TimeSpan.Zero
                    ),
                DataRoot:
                    dataRoot,
                RequestedPath:
                    requested,
                SourceSnapshot:
                    sourceSnapshot,
                SourceInodeGeneration:
                    12345U,
                InitialDestinationParentSnapshot:
                    parentSnapshot,
                Operations:
                    operations,
                DirectoryRenameSources:
                    Array.Empty<
                        DataRelativePathRepairDirectoryRenameSource
                    >(),
                AliasSources:
                    aliasSources
            );

        Assert.Null(
            DataRelativePathTargetedConsumerCaseRepairDurablePlan.Validate(
                record
            )
        );

        return record;
    }

    private static LinuxFileIdentityResult Identity(
        string fullPath,
        ulong inode)
    {
        return new(
            FullPath:
                fullPath,
            DeviceMajor:
                8U,
            DeviceMinor:
                1U,
            Inode:
                inode,
            LinkCount:
                1U,
            MountId:
                44UL,
            Error:
                null
        );
    }
}
