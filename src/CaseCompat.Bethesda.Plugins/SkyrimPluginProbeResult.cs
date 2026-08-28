namespace CaseCompat.Bethesda.Plugins;

public sealed record SkyrimPluginProbeResult(
    string FullPath,
    string ModKey,
    IReadOnlyList<string> Masters
)
{
    public int MasterCount => Masters.Count;
}
