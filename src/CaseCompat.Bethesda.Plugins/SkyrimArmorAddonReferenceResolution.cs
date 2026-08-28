using CaseCompat.Core.Resolution;

namespace CaseCompat.Bethesda.Plugins;

public sealed record SkyrimArmorAddonReferenceResolution(
    SkyrimArmorAddonModelReference Reference,
    DataRelativePathResolution? Resolution,
    string? Error
)
{
    public bool ResolutionSucceeded =>
        Resolution is not null &&
        Error is null;
}

public sealed record SkyrimArmorAddonResolutionProbeResult(
    string FullPath,
    string ModKey,
    string DataRoot,
    int ArmorAddonsExamined,
    IReadOnlyList<SkyrimArmorAddonReferenceResolution> References
)
{
    public int ReferenceCount =>
        References.Count;

    public int ResolvedCount =>
        References.Count(reference =>
            reference.Resolution?.LinuxResolves == true
        );

    public int UnresolvedCount =>
        References.Count(reference =>
            reference.Resolution is not null &&
            !reference.Resolution.LinuxResolves
        );

    public int ErrorCount =>
        References.Count(reference =>
            reference.Error is not null
        );
}
