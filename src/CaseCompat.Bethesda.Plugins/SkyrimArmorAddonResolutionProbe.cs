using CaseCompat.Core.Resolution;

namespace CaseCompat.Bethesda.Plugins;

public static class SkyrimArmorAddonResolutionProbe
{
    public static SkyrimArmorAddonResolutionProbeResult Inspect(
        string pluginPath,
        string dataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            pluginPath
        );

        ArgumentException.ThrowIfNullOrWhiteSpace(
            dataRoot
        );

        string fullDataRoot =
            Path.GetFullPath(dataRoot);

        if (!Directory.Exists(fullDataRoot))
        {
            throw new DirectoryNotFoundException(
                fullDataRoot
            );
        }

        SkyrimArmorAddonModelProbeResult plugin =
            SkyrimArmorAddonModelProbe.Inspect(
                pluginPath
            );

        var references =
            new List<
                SkyrimArmorAddonReferenceResolution
            >();

        foreach (
            SkyrimArmorAddonModelReference reference
            in plugin.References)
        {
            try
            {
                DataRelativePathResolution resolution =
                    DataRelativePathResolver.ResolveFile(
                        fullDataRoot,
                        reference.DataRelativePath
                    );

                references.Add(
                    new SkyrimArmorAddonReferenceResolution(
                        Reference: reference,
                        Resolution: resolution,
                        Error: null
                    )
                );
            }
            catch (Exception ex)
            {
                references.Add(
                    new SkyrimArmorAddonReferenceResolution(
                        Reference: reference,
                        Resolution: null,
                        Error: ex.Message
                    )
                );
            }
        }

        return new SkyrimArmorAddonResolutionProbeResult(
            FullPath: plugin.FullPath,
            ModKey: plugin.ModKey,
            DataRoot: fullDataRoot,
            ArmorAddonsExamined:
                plugin.ArmorAddonsExamined,
            References: references.ToArray()
        );
    }
}
