using CaseCompat.Filesystem.Linux;

namespace CaseCompat.Bethesda.Assets;

// Identifies one physical file by its stable on-disk identity (device +
// inode), not by path. A wizard convergence loop reruns full asset
// discovery every pass, and a rename or alias fix changes a mesh's or
// material's PATH between passes but never its inode - caching extracted
// texture references by this identity instead of by path lets later
// passes skip re-opening and re-parsing a file whose bytes have not
// actually changed, without ever risking a stale result for a file that
// genuinely was replaced (a replaced file gets a new inode, so it is
// correctly treated as a cache miss).
public readonly record struct SkyrimAssetFileIdentity(
    uint DeviceMajor,
    uint DeviceMinor,
    ulong Inode
)
{
    public static bool TryInspect(
        string physicalPath,
        out SkyrimAssetFileIdentity identity)
    {
        LinuxFileIdentityResult result =
            LinuxFileIdentity.Inspect(
                physicalPath
            );

        if (
            !result.Success ||
            result.DeviceMajor is null ||
            result.DeviceMinor is null ||
            result.Inode is null)
        {
            identity =
                default;

            return false;
        }

        identity =
            new SkyrimAssetFileIdentity(
                result.DeviceMajor.Value,
                result.DeviceMinor.Value,
                result.Inode.Value
            );

        return true;
    }
}
