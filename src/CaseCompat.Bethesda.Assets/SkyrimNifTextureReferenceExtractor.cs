using NiflySharp;
using NiflySharp.Blocks;

namespace CaseCompat.Bethesda.Assets;

// Pure extraction of genuine texture/material-file requests embedded in
// one .nif mesh's shader properties. No filesystem access beyond the
// passed-in stream, no winner selection, no consumer-spelling
// classification, no mutation occurs here.
//
// Covers the two shader property kinds Skyrim actually uses for surface
// textures: BSLightingShaderProperty (the common case - either a shared
// BSShaderTextureSet block's texture-slot list, or, when
// RootMaterialName is set, an external .bgsm/.bgem material reference
// instead) and BSEffectShaderProperty (its own direct texture-slot
// fields, no separate TextureSet block). Any other shader property kind
// is skipped - this project has observed no others carrying texture
// paths in the wild.
public static class SkyrimNifTextureReferenceExtractor
{
    public static IReadOnlyList<SkyrimNifTextureReference> Extract(
        Stream nifData,
        string meshPhysicalPath)
    {
        ArgumentNullException.ThrowIfNull(
            nifData
        );

        ArgumentException.ThrowIfNullOrWhiteSpace(
            meshPhysicalPath
        );

        var references =
            new List<SkyrimNifTextureReference>();

        var nifFile =
            new NifFile();

        int loadResult =
            nifFile.Load(
                nifData,
                new NifFileLoadOptions()
            );

        if (loadResult != 0)
        {
            return references.ToArray();
        }

        foreach (INiShape shape in nifFile.GetShapes())
        {
            INiShader? shader =
                nifFile.GetShader(
                    shape
                );

            if (shader is BSLightingShaderProperty lightingShader)
            {
                ExtractLightingShader(
                    nifFile,
                    lightingShader,
                    meshPhysicalPath,
                    references
                );
            }
            else if (shader is BSEffectShaderProperty effectShader)
            {
                ExtractEffectShader(
                    effectShader,
                    meshPhysicalPath,
                    references
                );
            }
        }

        return references.ToArray();
    }

    private static void ExtractLightingShader(
        NifFile nifFile,
        BSLightingShaderProperty lightingShader,
        string meshPhysicalPath,
        List<SkyrimNifTextureReference> references)
    {
        if (TryGetUsablePath(
                lightingShader.RootMaterialName,
                out string normalizedMaterialName))
        {
            references.Add(
                new SkyrimNifTextureReference(
                    MeshPhysicalPath:
                        meshPhysicalPath,
                    SlotOrFieldName:
                        "RootMaterialName",
                    GivenPath:
                        normalizedMaterialName,
                    Kind:
                        SkyrimNifTextureReferenceKind.MaterialFile
                )
            );

            return;
        }

        if (!string.IsNullOrEmpty(
                lightingShader.RootMaterialName))
        {
            return;
        }

        if (!lightingShader.HasTextureSet)
        {
            return;
        }

        BSShaderTextureSet? textureSet =
            nifFile.GetBlock<BSShaderTextureSet>(
                lightingShader.TextureSetRef
            );

        if (textureSet is null)
        {
            return;
        }

        for (
            int index = 0;
            index < textureSet.Textures.Count;
            index++)
        {
            NiString4? texture =
                textureSet.Textures[
                    index
                ];

            if (!TryGetUsablePath(
                    texture?.Content,
                    out string normalized))
            {
                continue;
            }

            references.Add(
                new SkyrimNifTextureReference(
                    MeshPhysicalPath:
                        meshPhysicalPath,
                    SlotOrFieldName:
                        $"TextureSet[{index}]",
                    GivenPath:
                        normalized,
                    Kind:
                        SkyrimNifTextureReferenceKind.Texture
                )
            );
        }
    }

    private static void ExtractEffectShader(
        BSEffectShaderProperty effectShader,
        string meshPhysicalPath,
        List<SkyrimNifTextureReference> references)
    {
        (string FieldName, NiString4? Value)[] slots =
        {
            ("SourceTexture", effectShader.SourceTexture),
            ("GreyscaleTexture", effectShader.GreyscaleTexture),
            ("EnvMapTexture", effectShader.EnvMapTexture),
            ("NormalTexture", effectShader.NormalTexture),
            ("EnvMaskTexture", effectShader.EnvMaskTexture),
            ("ReflectanceTexture", effectShader.ReflectanceTexture),
            ("LightingTexture", effectShader.LightingTexture),
            ("EmitGradientTexture", effectShader.EmitGradientTexture)
        };

        foreach ((string fieldName, NiString4? value) in slots)
        {
            if (!TryGetUsablePath(
                    value?.Content,
                    out string normalized))
            {
                continue;
            }

            references.Add(
                new SkyrimNifTextureReference(
                    MeshPhysicalPath:
                        meshPhysicalPath,
                    SlotOrFieldName:
                        fieldName,
                    GivenPath:
                        normalized,
                    Kind:
                        SkyrimNifTextureReferenceKind.Texture
                )
            );
        }
    }

    // Converts to forward slashes and collapses repeated separators.
    // Real mesh-export tools sometimes emit doubled slashes
    // ("textures//foo//bar.dds") - Windows path resolution has always
    // tolerated this, so collapsing it here is normalization, not a
    // guess at the author's intent, and avoids rejecting a perfectly
    // real texture request as malformed.
    private static string Normalize(
        string givenPath)
    {
        string slashed =
            givenPath.Replace(
                '\\',
                '/'
            );

        var builder =
            new System.Text.StringBuilder(
                slashed.Length
            );

        bool previousWasSlash =
            false;

        foreach (char c in slashed)
        {
            bool isSlash =
                c == '/';

            if (isSlash && previousWasSlash)
            {
                continue;
            }

            builder.Append(
                c
            );

            previousWasSlash =
                isSlash;
        }

        return builder.ToString();
    }

    // An empty or whitespace-only slot is the ordinary way a
    // texture/material field goes unused (observed on a real install: a
    // single space, a bare newline). Some real meshes also leave a slot
    // as a directory-only placeholder with no filename at all (observed:
    // "textures/") - that is equally "nothing requested" from a consumer
    // standpoint, not a genuine (if malformed) request a case-fix could
    // ever target, so both are filtered here rather than reaching
    // evidence classification as an unparseable path. A leading
    // separator (observed: "/textures/...") is stripped rather than
    // rejected - real texture loaders have always tolerated it the same
    // way they tolerate the doubled separators Normalize also collapses.
    // A value with no directory component at all (observed: a bare
    // "nor") is also filtered - every real Skyrim texture/material path
    // is Data-relative starting under a named subtree (e.g.
    // "textures/..."), so a single bare component can never be a
    // legitimate reference, only leftover/placeholder mesh data.
    // Internal (not private) solely so its real-install-derived edge
    // cases can be unit tested directly - constructing a real .nif fixture
    // per edge case is unnecessary ceremony for what is otherwise pure
    // string normalization with no NiflySharp dependency.
    internal static bool TryGetUsablePath(
        string? content,
        out string normalized)
    {
        normalized =
            string.Empty;

        if (string.IsNullOrWhiteSpace(
                content))
        {
            return false;
        }

        string candidate =
            Normalize(
                content
            )
            .TrimStart(
                '/'
            );

        if (
            candidate.Length == 0 ||
            candidate.EndsWith(
                '/'
            ) ||
            !candidate.Contains(
                '/'
            ))
        {
            return false;
        }

        normalized =
            candidate;

        return true;
    }
}
