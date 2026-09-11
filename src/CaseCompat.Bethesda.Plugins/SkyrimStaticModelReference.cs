namespace CaseCompat.Bethesda.Plugins;

// Genuine requested-path provenance retained from one Static record's
// Model. Mirrors SkyrimArmorAddonModelReference's shape; Field is always
// "Model" here since Static carries a single model, not a paired
// Male/Female WorldModel.
public sealed record SkyrimStaticModelReference(
    string FormKey,
    string? EditorId,
    string Field,
    string GivenPath,
    string DataRelativePath
);
