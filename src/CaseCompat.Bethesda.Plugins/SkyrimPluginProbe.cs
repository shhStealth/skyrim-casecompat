using Mutagen.Bethesda.Skyrim;

namespace CaseCompat.Bethesda.Plugins;

public static class SkyrimPluginProbe
{
    public static SkyrimPluginProbeResult Inspect(
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

        using var mod =
            SkyrimMod.CreateFromBinaryOverlay(
                fullPath,
                SkyrimRelease.SkyrimSE
            );

        string[] masters =
            mod.ModHeader.MasterReferences
                .Select(master =>
                    master.Master.FileName.ToString())
                .ToArray();

        return new SkyrimPluginProbeResult(
            FullPath: fullPath,
            ModKey: mod.ModKey.ToString(),
            Masters: masters
        );
    }
}
