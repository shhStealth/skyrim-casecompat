using Mutagen.Bethesda.Skyrim;

namespace CaseCompat.Bethesda.Plugins;

public sealed record SkyrimArmorRecord(
    string FormKey,
    string? EditorId,
    IReadOnlyList<string> ArmorAddonFormKeys
);

public sealed record SkyrimArmorRecordProbeResult(
    string FullPath,
    string ModKey,
    IReadOnlyList<SkyrimArmorRecord> Records
)
{
    public int RecordCount =>
        Records.Count;
}

public static class SkyrimArmorRecordProbe
{
    public static SkyrimArmorRecordProbeResult Inspect(
        string pluginPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            pluginPath
        );

        string fullPath =
            Path.GetFullPath(pluginPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "Plugin file was not found.",
                fullPath
            );
        }

        var mod =
            SkyrimMod.CreateFromBinaryOverlay(
                fullPath,
                SkyrimRelease.SkyrimSE
            );

        var records =
            new List<SkyrimArmorRecord>();

        foreach (
            var record
            in mod.EnumerateMajorRecords())
        {
            if (record is not IArmorGetter armor)
            {
                continue;
            }

            string[] armorAddonFormKeys =
                armor.Armature
                    .Select(link =>
                        link.FormKey.ToString()
                    )
                    .ToArray();

            records.Add(
                new SkyrimArmorRecord(
                    FormKey:
                        armor.FormKey.ToString(),
                    EditorId:
                        armor.EditorID,
                    ArmorAddonFormKeys:
                        armorAddonFormKeys
                )
            );
        }

        return new SkyrimArmorRecordProbeResult(
            FullPath: fullPath,
            ModKey: mod.ModKey.ToString(),
            Records: records.ToArray()
        );
    }
}
