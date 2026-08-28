namespace CaseCompat.Bethesda.Plugins;

public sealed record SkyrimArmorAddonModelReference(
    string FormKey,
    string? EditorId,
    string Field,
    string GivenPath,
    string DataRelativePath
);

public sealed record SkyrimArmorAddonModelProbeResult(
    string FullPath,
    string ModKey,
    int ArmorAddonsExamined,
    IReadOnlyList<SkyrimArmorAddonModelReference> References
)
{
    public int ReferenceCount => References.Count;
}
