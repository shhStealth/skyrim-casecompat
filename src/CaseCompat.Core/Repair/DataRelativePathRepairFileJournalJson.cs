using System.Text.Json;
using System.Text.Json.Serialization;

namespace CaseCompat.Core.Repair;

public static class DataRelativePathRepairFileJournalJson
{
    private static readonly JsonSerializerOptions Options =
        CreateOptions();

    public static byte[] Serialize(
        DataRelativePathRepairFileJournalRecord record)
    {
        ArgumentNullException.ThrowIfNull(
            record
        );

        return JsonSerializer.SerializeToUtf8Bytes(
            record,
            Options
        );
    }

    public static DataRelativePathRepairFileJournalRecord?
        Deserialize(
            ReadOnlySpan<byte> utf8Json)
    {
        return JsonSerializer.Deserialize<
            DataRelativePathRepairFileJournalRecord
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
