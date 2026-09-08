using System.Text.Json;
using System.Text.Json.Serialization;

namespace CaseCompat.Core.Repair;

public enum
    DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonSerializationState
{
    Serialized,
    InvalidPlan,
    SerializationFailed
}

public sealed record
    DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonSerializationResult(
        DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonSerializationState
            State,
        byte[]? Bytes,
        string? Error
    )
{
    public bool Success =>
        State ==
            DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonSerializationState
                .Serialized &&
        Bytes is not null;
}

public enum
    DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonDeserializationState
{
    Deserialized,
    DeserializeFailed,
    InvalidPlan
}

public sealed record
    DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonDeserializationResult(
        DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonDeserializationState
            State,
        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord? Record,
        string? Error
    )
{
    public bool Success =>
        State ==
            DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonDeserializationState
                .Deserialized &&
        Record is not null;
}

// Strict byte codec for the independent targeted durable-plan schema.
//
// This seam deliberately owns no file IO, descriptor acquisition, durable
// publication, hashing, batch authority, execution authority, or mutation.
//
// Successful serialization proves that the in-memory record passed targeted
// schema validation before any canonical bytes were returned.
//
// Successful deserialization proves both:
// - the JSON conformed to this exact supported object shape; and
// - the resulting targeted record passed semantic validation.
//
// Unknown object members and integer enum representations are rejected so an
// unsupported extension cannot silently disappear while a schema-v1 record is
// accepted.
public static class
    DataRelativePathTargetedConsumerCaseRepairDurablePlanJson
{
    private static readonly JsonSerializerOptions Options =
        CreateOptions();

    public static
        DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonSerializationResult
        SerializeValidated(
            DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord record)
    {
        ArgumentNullException.ThrowIfNull(
            record
        );

        string? validationError =
            DataRelativePathTargetedConsumerCaseRepairDurablePlan.Validate(
                record
            );

        if (validationError is not null)
        {
            return new(
                State:
                    DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonSerializationState
                        .InvalidPlan,
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
                    DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonSerializationState
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
                    DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonSerializationState
                        .SerializationFailed,
                Bytes:
                    null,
                Error:
                    ex.Message
            );
        }
    }

    public static
        DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonDeserializationResult
        DeserializeValidated(
            ReadOnlySpan<byte> utf8Json)
    {
        DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord? record;

        try
        {
            record =
                JsonSerializer.Deserialize<
                    DataRelativePathTargetedConsumerCaseRepairDurablePlanRecord
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
                    DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonDeserializationState
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
                    DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonDeserializationState
                        .DeserializeFailed,
                Record:
                    null,
                Error:
                    "The targeted durable-plan JSON produced no record."
            );
        }

        string? validationError =
            DataRelativePathTargetedConsumerCaseRepairDurablePlan.Validate(
                record
            );

        if (validationError is not null)
        {
            return new(
                State:
                    DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonDeserializationState
                        .InvalidPlan,
                Record:
                    null,
                Error:
                    validationError
            );
        }

        return new(
            State:
                DataRelativePathTargetedConsumerCaseRepairDurablePlanJsonDeserializationState
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
