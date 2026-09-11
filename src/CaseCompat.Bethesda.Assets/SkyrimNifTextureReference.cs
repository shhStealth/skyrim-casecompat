namespace CaseCompat.Bethesda.Assets;

// A texture-slot string is a direct request for a .dds file; a material
// reference is a request for an external .bgsm/.bgem file, which itself
// needs the standard classify/compose/candidate-resolve treatment before
// its own texture slots can be read (see
// SkyrimMaterialFileTextureReferenceExtractor).
public enum SkyrimNifTextureReferenceKind
{
    Texture,
    MaterialFile
}

// Genuine requested-path provenance retained from one texture or external
// material reference embedded in a mesh file - never a plugin record, so
// there is no FormKey/EditorId/winning-override provenance to retain here
// the way SkyrimArmorAddonModelReference and its siblings do.
//
// GivenPath is normalized to forward slashes at extraction time (NIF
// embeds these as backslash-separated strings) so it matches this
// project's established Data-relative path convention everywhere else;
// it is not otherwise rewritten.
public sealed record SkyrimNifTextureReference(
    string MeshPhysicalPath,
    string SlotOrFieldName,
    string GivenPath,
    SkyrimNifTextureReferenceKind Kind
);
