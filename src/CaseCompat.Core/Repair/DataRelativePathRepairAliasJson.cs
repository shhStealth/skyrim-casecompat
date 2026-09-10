using System.Text.Json;
using System.Text.Json.Serialization;

namespace CaseCompat.Core.Repair;

public enum DataRelativePathRepairAliasJsonSerializationState
{
    Serialized,
    InvalidRecord,
    SerializationFailed
}

public sealed record DataRelativePathRepairAliasJsonSerializationResult(
    DataRelativePathRepairAliasJsonSerializationState State,
    byte[]? Bytes,
    string? Error
)
{
    public bool Success =>
        State ==
            DataRelativePathRepairAliasJsonSerializationState.Serialized &&
        Bytes is not null;
}

public enum DataRelativePathRepairAliasJsonDeserializationState
{
    Deserialized,
    DeserializeFailed,
    InvalidRecord
}

public sealed record DataRelativePathRepairAliasJsonDeserializationResult(
    DataRelativePathRepairAliasJsonDeserializationState State,
    DataRelativePathRepairAliasRecord? Record,
    string? Error
)
{
    public bool Success =>
        State ==
            DataRelativePathRepairAliasJsonDeserializationState
                .Deserialized &&
        Record is not null;
}

// Strict byte codec for the alias-record schema, matching the same
// strictness as the targeted durable-plan codec: unknown members and
// integer enum representations are rejected, and both directions run
// the same validation so a record that round-trips through disk is
// exactly as trustworthy as one built freshly in memory.
public static class DataRelativePathRepairAliasJson
{
    private static readonly JsonSerializerOptions Options =
        CreateOptions();

    public static DataRelativePathRepairAliasJsonSerializationResult
        SerializeValidated(
            DataRelativePathRepairAliasRecord record)
    {
        ArgumentNullException.ThrowIfNull(
            record
        );

        string? validationError =
            DataRelativePathRepairAliasRecord.Validate(
                record
            );

        if (validationError is not null)
        {
            return new(
                State:
                    DataRelativePathRepairAliasJsonSerializationState
                        .InvalidRecord,
                Bytes:
                    null,
                Error:
                    validationError
            );
        }

        try
        {
            byte[] bytes =
                JsonSerializer.SerializeToUtf8Bytes(
                    record,
                    Options
                );

            return new(
                State:
                    DataRelativePathRepairAliasJsonSerializationState
                        .Serialized,
                Bytes:
                    bytes,
                Error:
                    null
            );
        }
        catch (
            Exception ex)
            when (
                ex is JsonException or
                NotSupportedException)
        {
            return new(
                State:
                    DataRelativePathRepairAliasJsonSerializationState
                        .SerializationFailed,
                Bytes:
                    null,
                Error:
                    ex.Message
            );
        }
    }

    public static DataRelativePathRepairAliasJsonDeserializationResult
        DeserializeValidated(
            ReadOnlySpan<byte> utf8Json)
    {
        DataRelativePathRepairAliasRecord? record;

        try
        {
            record =
                JsonSerializer.Deserialize<
                    DataRelativePathRepairAliasRecord
                >(
                    utf8Json,
                    Options
                );
        }
        catch (
            Exception ex)
            when (
                ex is JsonException or
                NotSupportedException)
        {
            return new(
                State:
                    DataRelativePathRepairAliasJsonDeserializationState
                        .DeserializeFailed,
                Record:
                    null,
                Error:
                    ex.Message
            );
        }

        if (record is null)
        {
            return new(
                State:
                    DataRelativePathRepairAliasJsonDeserializationState
                        .DeserializeFailed,
                Record:
                    null,
                Error:
                    "The alias-record JSON produced no record."
            );
        }

        string? validationError =
            DataRelativePathRepairAliasRecord.Validate(
                record
            );

        if (validationError is not null)
        {
            return new(
                State:
                    DataRelativePathRepairAliasJsonDeserializationState
                        .InvalidRecord,
                Record:
                    null,
                Error:
                    validationError
            );
        }

        return new(
            State:
                DataRelativePathRepairAliasJsonDeserializationState
                    .Deserialized,
            Record:
                record,
            Error:
                null
        );
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options =
            new JsonSerializerOptions
            {
                WriteIndented =
                    true,
                PropertyNamingPolicy =
                    null,
                PropertyNameCaseInsensitive =
                    false,
                AllowTrailingCommas =
                    false,
                ReadCommentHandling =
                    JsonCommentHandling.Disallow,
                UnmappedMemberHandling =
                    JsonUnmappedMemberHandling.Disallow
            };

        options.Converters.Add(
            new JsonStringEnumConverter(
                namingPolicy:
                    null,
                allowIntegerValues:
                    false
            )
        );

        return options;
    }
}
