namespace CaseCompat.Bethesda.Plugins;

// Genuine requested-path provenance retained from one Container record's
// Model. Mirrors SkyrimArmorAddonModelReference's shape; Field is always
// "Model" here since Container carries a single model, not a paired
// Male/Female WorldModel.
public sealed record SkyrimContainerModelReference(
    string FormKey,
    string? EditorId,
    string Field,
    string GivenPath,
    string DataRelativePath
);
