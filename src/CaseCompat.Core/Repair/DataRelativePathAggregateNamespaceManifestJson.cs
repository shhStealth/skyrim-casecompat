using System.Text.Json;
using System.Text.Json.Serialization;

namespace CaseCompat.Core.Repair;

public static class DataRelativePathAggregateNamespaceManifestJson
{
    private static readonly JsonSerializerOptions Options =
        CreateOptions();

    public static byte[] Serialize(
        DataRelativePathAggregateNamespaceManifestRecord manifest)
    {
        ArgumentNullException.ThrowIfNull(
            manifest
        );

        return JsonSerializer.SerializeToUtf8Bytes(
            manifest,
            Options
        );
    }

    public static DataRelativePathAggregateNamespaceManifestRecord?
        Deserialize(
            ReadOnlySpan<byte> utf8Json)
    {
        return JsonSerializer.Deserialize<
            DataRelativePathAggregateNamespaceManifestRecord
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
