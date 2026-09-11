namespace CaseCompat.Bethesda.Plugins;

// Genuine requested-path provenance retained from one Furniture record's
// Model. Mirrors SkyrimArmorAddonModelReference's shape; Field is always
// "Model" here since Furniture carries a single model, not a paired
// Male/Female WorldModel.
public sealed record SkyrimFurnitureModelReference(
    string FormKey,
    string? EditorId,
    string Field,
    string GivenPath,
    string DataRelativePath
);
