using GameFinder.Common;
using GameFinder.RegistryUtils;
using GameFinder.StoreHandlers.Steam;
using GameFinder.StoreHandlers.Steam.Models.ValueTypes;
using Microsoft.Extensions.Logging.Abstractions;
using NexusMods.Paths;

// Best-effort auto-detection of a Steam/Proton Skyrim Special Edition
// install for the guided wizard. Every result is optional - the wizard
// always lets the user confirm or override with a manual path, so a
// failure here never blocks the tool, it just falls back to asking.
internal static class SkyrimInstallDetection
{
    private const uint SkyrimSpecialEditionAppId =
        489830;

    public static SkyrimInstallDetectionResult Detect()
    {
        try
        {
            var handler =
                new SteamHandler(
                    FileSystem.Shared,
                    new InMemoryRegistry(),
                    NullLogger<SteamHandler>.Instance
                );

            foreach (var found in handler.FindAllGames())
            {
                if (!found.IsGame())
                {
                    continue;
                }

                SteamGame game =
                    found.AsGame();

                if (
                    game.AppId !=
                    AppId.From(
                        SkyrimSpecialEditionAppId))
                {
                    continue;
                }

                return DescribeInstall(
                    game
                );
            }

            return new SkyrimInstallDetectionResult(
                DataRoot: null,
                SkyrimCccPath: null,
                PluginsPath: null,
                LoadOrderPath: null,
                Note:
                    "Skyrim Special Edition was not found in any " +
                    "detected Steam library."
            );
        }
        catch (Exception ex)
        {
            return new SkyrimInstallDetectionResult(
                DataRoot: null,
                SkyrimCccPath: null,
                PluginsPath: null,
                LoadOrderPath: null,
                Note:
                    $"Automatic detection failed: {ex.Message}"
            );
        }
    }

    private static SkyrimInstallDetectionResult DescribeInstall(
        SteamGame game)
    {
        string installRoot =
            game.Path.ToString();

        string dataRoot =
            Path.Combine(
                installRoot,
                "Data"
            );

        string cccPath =
            Path.Combine(
                installRoot,
                "Skyrim.ccc"
            );

        string? pluginsPath =
            null;

        string? loadOrderPath =
            null;

        try
        {
            var protonPrefix =
                game.GetProtonPrefix();

            string driveC =
                (protonPrefix ??
                    throw new InvalidOperationException(
                        "No Proton prefix found."))
                    .GetVirtualDrivePath()
                    .ToString();

            string skyrimAppData =
                Path.Combine(
                    driveC,
                    "users",
                    "steamuser",
                    "AppData",
                    "Local",
                    "Skyrim Special Edition"
                );

            string candidatePlugins =
                Path.Combine(
                    skyrimAppData,
                    "Plugins.txt"
                );

            string candidateLoadOrder =
                Path.Combine(
                    skyrimAppData,
                    "loadorder.txt"
                );

            if (File.Exists(
                    candidatePlugins))
            {
                pluginsPath =
                    candidatePlugins;
            }

            if (File.Exists(
                    candidateLoadOrder))
            {
                loadOrderPath =
                    candidateLoadOrder;
            }
        }
        catch
        {
            // No Proton prefix yet (e.g. the game has never been
            // launched). Fall through and let the wizard ask manually.
        }

        return new SkyrimInstallDetectionResult(
            DataRoot:
                Directory.Exists(
                    dataRoot)
                    ? dataRoot
                    : null,
            SkyrimCccPath:
                File.Exists(
                    cccPath)
                    ? cccPath
                    : null,
            PluginsPath:
                pluginsPath,
            LoadOrderPath:
                loadOrderPath,
            Note:
                null
        );
    }
}

internal sealed record SkyrimInstallDetectionResult(
    string? DataRoot,
    string? SkyrimCccPath,
    string? PluginsPath,
    string? LoadOrderPath,
    string? Note);
