using CaseCompat.Core.Resolution;

namespace CaseCompat.Core.Findings;

public sealed record EffectiveAssetReferenceFinding(
    string ConsumerKind,
    string ConsumerFormKey,
    string? ConsumerEditorId,
    string WinningPluginName,
    int WinningLoadOrderIndex,
    bool WinnerSearchComplete,
    string ReferenceField,
    string RawPath,
    string RequestedPath,
    DataRelativePathResolution Resolution
)
{
    public bool LinuxResolves =>
        Resolution.LinuxResolves;

    public int EquivalentCandidateCount =>
        Resolution.CandidateCount;
}
