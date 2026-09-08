using System.Text.Json;
using System.Text.Json.Serialization;

namespace CaseCompat.Core.Repair;

public enum
    DataRelativePathTargetedConsumerCaseRepairApplyJournalJsonSerializationState
{
    Serialized,
    InvalidRecord,
    SerializationFailed
}

public sealed record
    DataRelativePathTargetedConsumerCaseRepairApplyJournalJsonSerializationResult(
        DataRelativePathTargetedConsumerCaseRepairApplyJournalJsonSerializationState
            State,
        byte[]? Bytes,
        string? Error
    )
{
    public bool Success =>
        State ==
            DataRelativePathTargetedConsumerCaseRepairApplyJournalJsonSerializationState
                .Serialized &&
        Bytes is not null;
}

public enum
    DataRelativePathTargetedConsumerCaseRepairApplyJournalJsonDeserializationState
{
    Deserialized,
    DeserializeFailed,
    InvalidRecord
}

public sealed record
    DataRelativePathTargetedConsumerCaseRepairApplyJournalJsonDeserializationResult(
        DataRelativePathTargetedConsumerCaseRepairApplyJournalJsonDeserializationState
            State,
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord? Record,
        string? Error
    )
{
    public bool Success =>
        State ==
            DataRelativePathTargetedConsumerCaseRepairApplyJournalJsonDeserializationState
                .Deserialized &&
        Record is not null;
}

// Strict byte codec for the independent targeted apply-journal schema.
//
// Mirrors the C4E-4D2C2 durable-plan codec exactly: successful
// serialization proves the record passed schema validation first;
// successful deserialization proves both well-formed JSON and semantic
// validity. Unknown members and integer enum representations are rejected.
public static class
    DataRelativePathTargetedConsumerCaseRepairApplyJournalJson
{
    private static readonly JsonSerializerOptions Options =
        CreateOptions();

    public static
        DataRelativePathTargetedConsumerCaseRepairApplyJournalJsonSerializationResult
        SerializeValidated(
            DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord
                record)
    {
        ArgumentNullException.ThrowIfNull(
            record
        );

        string? validationError =
            DataRelativePathTargetedConsumerCaseRepairApplyJournal.Validate(
                record
            );

        if (validationError is not null)
        {
            return new(
                State:
                    DataRelativePathTargetedConsumerCaseRepairApplyJournalJsonSerializationState
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
                    DataRelativePathTargetedConsumerCaseRepairApplyJournalJsonSerializationState
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
                    DataRelativePathTargetedConsumerCaseRepairApplyJournalJsonSerializationState
                        .SerializationFailed,
                Bytes:
                    null,
                Error:
                    ex.Message
            );
        }
    }

    public static
        DataRelativePathTargetedConsumerCaseRepairApplyJournalJsonDeserializationResult
        DeserializeValidated(
            ReadOnlySpan<byte> utf8Json)
    {
        DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord? record;

        try
        {
            record =
                JsonSerializer.Deserialize<
                    DataRelativePathTargetedConsumerCaseRepairApplyJournalRecord
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
                    DataRelativePathTargetedConsumerCaseRepairApplyJournalJsonDeserializationState
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
                    DataRelativePathTargetedConsumerCaseRepairApplyJournalJsonDeserializationState
                        .DeserializeFailed,
                Record:
                    null,
                Error:
                    "The targeted apply-journal JSON produced no record."
            );
        }

        string? validationError =
            DataRelativePathTargetedConsumerCaseRepairApplyJournal.Validate(
                record
            );

        if (validationError is not null)
        {
            return new(
                State:
                    DataRelativePathTargetedConsumerCaseRepairApplyJournalJsonDeserializationState
                        .InvalidRecord,
                Record:
                    null,
                Error:
                    validationError
            );
        }

        return new(
            State:
                DataRelativePathTargetedConsumerCaseRepairApplyJournalJsonDeserializationState
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
