using Mutagen.Bethesda.Skyrim;

namespace CaseCompat.Bethesda.Plugins;

/*
 * Genuine requested-path provenance retained from one HeadPart Parts entry.
 *
 * The reference represents the consumer request encoded by the winning/plugin
 * record itself. Exact GivenPath and DataRelativePath spelling is preserved.
 *
 * PartIndex is the original zero-based index in IHeadPartGetter.Parts. It is
 * not renumbered when another part has no usable FileName.
 *
 * PartType is retained as provenance rather than used as an inclusion filter.
 * Skyrim records legitimately use RaceMorph, Tri, and ChargenMorph entries for
 * .tri consumer requests.
 *
 * This record carries no filesystem, provider, archive, canonicalization,
 * repair-planning, authorization, execution, rollback, or recovery authority.
 */
public sealed record SkyrimHeadPartPartReference(
    string FormKey,
    string? EditorId,
    int PartIndex,
    Part.PartTypeEnum? PartType,
    string GivenPath,
    string DataRelativePath
);
