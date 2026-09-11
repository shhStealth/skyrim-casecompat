using System.Text;

namespace CaseCompat.Bethesda.Assets;

// Pure, minimal binary reader for the texture-slot fields of a .bgsm
// (BGSM) or .bgem (BGEM) material file. No filesystem access beyond the
// passed-in stream, no winner selection, no consumer-spelling
// classification, no mutation occurs here.
//
// The exact field layout (signature, version-gated field order, and the
// length-prefixed string encoding) is not otherwise documented; this was
// derived from ousnius/Material-Editor's MaterialLib (MIT licensed:
// https://github.com/ousnius/Material-Editor), used here purely as a
// format reference, not as a dependency. Every texture-path field this
// project cares about is read early in the stream (before
// RootMaterialPath/AnisoLighting/EmitEnabled and everything else), so
// this reader deliberately stops immediately after the last texture slot
// for the detected version rather than replicating every later
// conditional field - nothing past that point is ever consumed.
public static class SkyrimMaterialFileTextureReferenceExtractor
{
    private const uint BgsmSignature =
        0x4D534742u;

    private const uint BgemSignature =
        0x4D454742u;

    public static IReadOnlyList<SkyrimMaterialFileTextureReference> Extract(
        Stream materialData,
        string materialPhysicalPath)
    {
        ArgumentNullException.ThrowIfNull(
            materialData
        );

        ArgumentException.ThrowIfNullOrWhiteSpace(
            materialPhysicalPath
        );

        using var reader =
            new BinaryReader(
                materialData,
                Encoding.UTF8,
                leaveOpen:
                    true
            );

        uint signature =
            reader.ReadUInt32();

        uint version =
            reader.ReadUInt32();

        SkipBaseHeader(
            reader,
            version
        );

        return signature switch
        {
            BgsmSignature =>
                ExtractBgsmTextures(
                    reader,
                    version,
                    materialPhysicalPath
                ),

            BgemSignature =>
                ExtractBgemTextures(
                    reader,
                    version,
                    materialPhysicalPath
                ),

            _ =>
                Array.Empty<SkyrimMaterialFileTextureReference>()
        };
    }

    // Mirrors BaseMaterialFile.Deserialize's common header, shared by
    // both BGSM and BGEM, up to (not including) either subclass's own
    // texture-slot fields.
    private static void SkipBaseHeader(
        BinaryReader reader,
        uint version)
    {
        reader.ReadUInt32(); // tileFlags

        reader.ReadSingle(); // UOffset
        reader.ReadSingle(); // VOffset
        reader.ReadSingle(); // UScale
        reader.ReadSingle(); // VScale

        reader.ReadSingle(); // Alpha
        reader.ReadByte();   // alphaBlendMode byte
        reader.ReadUInt32(); // alphaBlendMode uint 1
        reader.ReadUInt32(); // alphaBlendMode uint 2
        reader.ReadByte();   // AlphaTestRef
        reader.ReadBoolean(); // AlphaTest

        reader.ReadBoolean(); // ZBufferWrite
        reader.ReadBoolean(); // ZBufferTest
        reader.ReadBoolean(); // ScreenSpaceReflections
        reader.ReadBoolean(); // WetnessControlScreenSpaceReflections
        reader.ReadBoolean(); // Decal
        reader.ReadBoolean(); // TwoSided
        reader.ReadBoolean(); // DecalNoFade
        reader.ReadBoolean(); // NonOccluder

        reader.ReadBoolean(); // Refraction
        reader.ReadBoolean(); // RefractionFalloff
        reader.ReadSingle();  // RefractionPower

        if (version < 10)
        {
            reader.ReadBoolean(); // EnvironmentMapping
            reader.ReadSingle();  // EnvironmentMappingMaskScale
        }
        else
        {
            reader.ReadBoolean(); // DepthBias
        }

        reader.ReadBoolean(); // GrayscaleToPaletteColor

        if (version >= 6)
        {
            reader.ReadByte(); // MaskWrites
        }
    }

    private static IReadOnlyList<SkyrimMaterialFileTextureReference>
        ExtractBgsmTextures(
            BinaryReader reader,
            uint version,
            string materialPhysicalPath)
    {
        var references =
            new List<SkyrimMaterialFileTextureReference>();

        void Add(
            string slotName)
        {
            AddIfPresent(
                references,
                materialPhysicalPath,
                slotName,
                ReadString(
                    reader
                )
            );
        }

        Add("DiffuseTexture");
        Add("NormalTexture");
        Add("SmoothSpecTexture");
        Add("GreyscaleTexture");

        if (version > 2)
        {
            Add("GlowTexture");
            Add("WrinklesTexture");
            Add("SpecularTexture");
            Add("LightingTexture");
            Add("FlowTexture");

            if (version >= 17)
            {
                Add("DistanceFieldAlphaTexture");
            }
        }
        else
        {
            Add("EnvmapTexture");
            Add("GlowTexture");
            Add("InnerLayerTexture");
            Add("WrinklesTexture");
            Add("DisplacementTexture");
        }

        return references.ToArray();
    }

    private static IReadOnlyList<SkyrimMaterialFileTextureReference>
        ExtractBgemTextures(
            BinaryReader reader,
            uint version,
            string materialPhysicalPath)
    {
        var references =
            new List<SkyrimMaterialFileTextureReference>();

        void Add(
            string slotName)
        {
            AddIfPresent(
                references,
                materialPhysicalPath,
                slotName,
                ReadString(
                    reader
                )
            );
        }

        Add("BaseTexture");
        Add("GrayscaleTexture");
        Add("EnvmapTexture");
        Add("NormalTexture");
        Add("EnvmapMaskTexture");

        if (version >= 11)
        {
            Add("SpecularTexture");
            Add("LightingTexture");
            Add("GlowTexture");
        }

        if (version >= 21)
        {
            Add("GlassRoughnessScratch");
            Add("GlassDirtOverlay");
        }

        return references.ToArray();
    }

    private static void AddIfPresent(
        List<SkyrimMaterialFileTextureReference> references,
        string materialPhysicalPath,
        string slotName,
        string givenPath)
    {
        if (string.IsNullOrEmpty(
                givenPath))
        {
            return;
        }

        references.Add(
            new SkyrimMaterialFileTextureReference(
                MaterialPhysicalPath:
                    materialPhysicalPath,
                SlotName:
                    slotName,
                GivenPath:
                    givenPath.Replace(
                        '\\',
                        '/'
                    )
            )
        );
    }

    // Length-prefixed (uint32 character count), then that many chars,
    // with a trailing NUL stripped if present - matches
    // BaseMaterialFile.ReadString exactly.
    private static string ReadString(
        BinaryReader reader)
    {
        uint length =
            reader.ReadUInt32();

        string raw =
            new(
                reader.ReadChars(
                    (int)length
                )
            );

        int nulIndex =
            raw.LastIndexOf(
                '\0'
            );

        return nulIndex >= 0
            ? raw.Remove(
                nulIndex,
                1
            )
            : raw;
    }
}
