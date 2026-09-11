namespace CaseCompat.Bethesda.Assets;

// Genuine texture-path provenance retained from one .bgsm/.bgem material
// file's texture-slot fields.
public sealed record SkyrimMaterialFileTextureReference(
    string MaterialPhysicalPath,
    string SlotName,
    string GivenPath
);
