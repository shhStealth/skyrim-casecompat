using System.Text.Json;
using System.Text.Json.Serialization;

namespace CaseCompat.Core.Repair;

public static class DataRelativePathRepairDirectoryJournalJson
{
    private static readonly JsonSerializerOptions Options =
        CreateOptions();

    public static byte[] Serialize(
        DataRelativePathRepairDirectoryJournalRecord record)
    {
        ArgumentNullException.ThrowIfNull(
            record
        );

        return JsonSerializer.SerializeToUtf8Bytes(
            record,
            Options
        );
    }

    public static DataRelativePathRepairDirectoryJournalRecord?
        Deserialize(
            ReadOnlySpan<byte> utf8Json)
    {
        return JsonSerializer.Deserialize<
            DataRelativePathRepairDirectoryJournalRecord
        >(
            utf8Json,
            Options
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
                    null
            };

        options.Converters.Add(
            new JsonStringEnumConverter()
        );

        return options;
    }
}
