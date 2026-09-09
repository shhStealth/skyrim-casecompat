using System.Security.Cryptography;
using System.Text;

// Deterministic per-Data-root state directory shared by every guided
// entry point (apply wizard, rollback wizard). Deriving this from a
// hash of the Data root, rather than letting the caller pick a
// location, is what lets repeat runs against the same install find
// the same plans/journal without the user having to remember or pass
// a path.
internal static class CaseCompatStateDirectory
{
    public static string Resolve(
        string dataRoot)
    {
        string? xdgDataHome =
            Environment.GetEnvironmentVariable(
                "XDG_DATA_HOME"
            );

        string baseDirectory =
            string.IsNullOrWhiteSpace(
                xdgDataHome)
                ? Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.UserProfile
                    ),
                    ".local",
                    "share"
                )
                : xdgDataHome;

        string hash =
            Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        dataRoot
                    )
                )
            )[..16].ToLowerInvariant();

        return Path.Combine(
            baseDirectory,
            "CaseCompat",
            "installs",
            hash
        );
    }
}
