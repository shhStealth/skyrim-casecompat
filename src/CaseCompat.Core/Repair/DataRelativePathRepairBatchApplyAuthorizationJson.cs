using System.Text.Json;

namespace CaseCompat.Core.Repair;

public static class
    DataRelativePathRepairBatchApplyAuthorizationJson
{
    private static readonly JsonSerializerOptions Options =
        new()
        {
            WriteIndented =
                true,
            PropertyNamingPolicy =
                null
        };

    public static byte[] Serialize(
        DataRelativePathRepairBatchApplyAuthorizationRecord
            authorization)
    {
        ArgumentNullException.ThrowIfNull(
            authorization
        );

        return JsonSerializer.SerializeToUtf8Bytes(
            authorization,
            Options
        );
    }

    public static
        DataRelativePathRepairBatchApplyAuthorizationRecord?
        Deserialize(
            ReadOnlySpan<byte> utf8Json)
    {
        return JsonSerializer.Deserialize<
            DataRelativePathRepairBatchApplyAuthorizationRecord
        >(
            utf8Json,
            Options
        );
    }
}
