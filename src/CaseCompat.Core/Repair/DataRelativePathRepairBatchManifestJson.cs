using System.Text.Json;
using System.Text.Json.Serialization;

namespace CaseCompat.Core.Repair;

public static class DataRelativePathRepairBatchManifestJson
{
    private static readonly JsonSerializerOptions Options =
        CreateOptions();

    public static byte[] Serialize(
        DataRelativePathRepairBatchManifestRecord manifest)
    {
        ArgumentNullException.ThrowIfNull(
            manifest
        );

        return JsonSerializer.SerializeToUtf8Bytes(
            manifest,
            Options
        );
    }

    public static DataRelativePathRepairBatchManifestRecord?
        Deserialize(
            ReadOnlySpan<byte> utf8Json)
    {
        return JsonSerializer.Deserialize<
            DataRelativePathRepairBatchManifestRecord
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
