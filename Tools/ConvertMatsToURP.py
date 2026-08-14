#!/usr/bin/env python3
"""Convert Built-in materials in ACTGameEditor to URP Lit / Unlit YAML."""

from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(r"D:\Work\ACTGameEditor\Assets")

URP_LIT = "933532a4fcc9baf4fa0491de14d08ed7"
URP_UNLIT = "650dd9526735d5b46b79224bc6e94025"
URP_PARTICLES_UNLIT = "0406db5a14f94604a8c57ccfbc9f3b46"
MAT_VERSION_GUID = "d0353a89b1f911e48b9e16bdc9f2e058"

STANDARD_MATS = [
    r"Res\SuperCharacterController\Materials\GreyGrid.mat",
    r"Res\SuperCharacterController\Materials\GreyGridLarger.mat",
    r"Res\SuperCharacterController\Materials\RedWall.mat",
    r"Res\SuperCharacterController\Materials\GreenWall.mat",
    r"Res\SuperCharacterController\Materials\PurpleWall.mat",
    r"Res\SuperCharacterController\Materials\ReflectiveOrange.mat",
    r"Res\SuperCharacterController\Materials\BluePlastic.mat",
    r"Res\SuperCharacterController\Materials\OrangeWall.mat",
    r"Res\Materials\bg.mat",
    r"Res\Materials\Bg_Defaut.mat",
    r"Res\Materials\Bg_Defaut 1.mat",
    r"Res\Model\sword-of-artorias\source\defaultMat.mat",
    r"Res\Anim\Materials\05 - Default.mat",
    r"Res\Anim\Materials\06 - Default.mat",
    r"Res\Anim\Materials\04 - Default.mat",
    r"Res\Anim\Materials\07 - Default.mat",
    r"Res\Anim\Materials\09 - Default.mat",
    r"Res\Anim\Materials\14 - Default.mat",
    r"Res\Anim\Materials\Blue.mat",
    r"Res\Anim\Materials\Blueisland_Character_Mat2.mat",
    r"Res\Anim\Materials\Material #157.mat",
    r"Res\Anim\Materials\Blueisland_Character_Mat1.mat",
    r"Res\Anim\Materials\Material #12.mat",
    r"Editor\EditorResources\Materials\Plane.mat",
    r"Editor\EditorResources\Materials\Sphere.mat",
]

TRANSPARENT_UNLIT = [
    r"Res\Materials\Cube_Tran.mat",
    r"Res\Materials\Bg_Tran.mat",
    r"Res\Materials\warmingRed.mat",
]

OPAQUE_UNLIT = [
    r"Res\Model\wanglingyongshi\wanglingyongshi\Materials\wanglingyongshi.mat",
    r"Res\Model\wanglingyongshi\wanglingyongshi_weapon\Materials\wanglingyongshi_weapon.mat",
]

HIT_ADDITIVE = [
    r"Game\Effects\HitEffect\New Material.mat",
]


def extract_name(text: str, path: Path) -> str:
    # Only same-line Material names; never cross newlines (avoids m_EditorClassIdentifier).
    for m in re.finditer(r"^  m_Name:\s*(.+?)\s*$", text, re.M):
        name = m.group(1).strip()
        if name and name != "m_EditorClassIdentifier:":
            return name
    return path.stem


def extract_tex(text: str, prop: str) -> tuple[str, str, str]:
    """Return (texture_ref, scale, offset) for a texture property."""
    pattern = (
        rf"- {re.escape(prop)}:\s*\n"
        rf"\s*m_Texture:\s*(.+)\n"
        rf"\s*m_Scale:\s*(.+)\n"
        rf"\s*m_Offset:\s*(.+)"
    )
    m = re.search(pattern, text)
    if m:
        return m.group(1).strip(), m.group(2).strip(), m.group(3).strip()

    # Legacy serializedVersion 2 / 3 format with nested data:
    legacy = (
        rf"name:\s*{re.escape(prop)}\s*\n"
        rf"\s*second:\s*\n"
        rf"\s*m_Texture:\s*(.+)\n"
        rf"\s*m_Scale:\s*(.+)\n"
        rf"\s*m_Offset:\s*(.+)"
    )
    m = re.search(legacy, text)
    if m:
        return m.group(1).strip(), m.group(2).strip(), m.group(3).strip()

    return "{fileID: 0}", "{x: 1, y: 1}", "{x: 0, y: 0}"


def extract_float(text: str, prop: str, default: str) -> str:
    m = re.search(rf"- {re.escape(prop)}:\s*([^\n]+)", text)
    if m:
        return m.group(1).strip()
    # legacy
    m = re.search(
        rf"name:\s*{re.escape(prop)}\s*\n\s*second:\s*([^\n]+)", text
    )
    if m:
        return m.group(1).strip()
    return default


def extract_color(text: str, prop: str, default: str) -> str:
    m = re.search(rf"- {re.escape(prop)}:\s*(\{{[^}}]+\}})", text)
    if m:
        return m.group(1).strip()
    m = re.search(
        rf"name:\s*{re.escape(prop)}\s*\n\s*second:\s*(\{{[^}}]+\}})", text
    )
    if m:
        return m.group(1).strip()
    return default


def tex_block(name: str, tex: str, scale: str, offset: str) -> str:
    return (
        f"    - {name}:\n"
        f"        m_Texture: {tex}\n"
        f"        m_Scale: {scale}\n"
        f"        m_Offset: {offset}\n"
    )


def write_lit(path: Path) -> None:
    text = path.read_text(encoding="utf-8")
    name = extract_name(text, path)
    main_tex, main_scale, main_offset = extract_tex(text, "_MainTex")
    bump_tex, bump_scale, bump_offset = extract_tex(text, "_BumpMap")
    emission_tex, emission_scale, emission_offset = extract_tex(text, "_EmissionMap")
    metallic_tex, metallic_scale, metallic_offset = extract_tex(text, "_MetallicGlossMap")
    occlusion_tex, occlusion_scale, occlusion_offset = extract_tex(text, "_OcclusionMap")

    color = extract_color(text, "_Color", "{r: 1, g: 1, b: 1, a: 1}")
    emission = extract_color(text, "_EmissionColor", "{r: 0, g: 0, b: 0, a: 1}")
    metallic = extract_float(text, "_Metallic", "0")
    glossiness = extract_float(text, "_Glossiness", "0.5")
    bump_scale_f = extract_float(text, "_BumpScale", "1")
    occlusion = extract_float(text, "_OcclusionStrength", "1")

    keywords: list[str] = []
    if bump_tex != "{fileID: 0}":
        keywords.append("_NORMALMAP")
    if metallic_tex != "{fileID: 0}":
        keywords.append("_METALLICSPECGLOSSMAP")
    if emission_tex != "{fileID: 0}" or not re.match(
        r"\{r: 0, g: 0, b: 0", emission
    ):
        # keep emission keyword only if non-black or has map; LightBlueWall always had it
        pass

    kw_block = ""
    if keywords:
        kw_block = "\n".join(f"  - {k}" for k in keywords)
    else:
        kw_block = ""

    content = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &-7741268771140260385
MonoBehaviour:
  m_ObjectHideFlags: 11
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {MAT_VERSION_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  version: 5
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 4800000, guid: {URP_LIT}, type: 3}}
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: 2000
  stringTagMap:
    RenderType: Opaque
  disabledShaderPasses: []
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
{tex_block("_BaseMap", main_tex, main_scale, main_offset)}{tex_block("_BumpMap", bump_tex, bump_scale, bump_offset)}{tex_block("_DetailAlbedoMap", "{fileID: 0}", "{x: 1, y: 1}", "{x: 0, y: 0}")}{tex_block("_DetailMask", "{fileID: 0}", "{x: 1, y: 1}", "{x: 0, y: 0}")}{tex_block("_DetailNormalMap", "{fileID: 0}", "{x: 1, y: 1}", "{x: 0, y: 0}")}{tex_block("_EmissionMap", emission_tex, emission_scale, emission_offset)}{tex_block("_MainTex", main_tex, main_scale, main_offset)}{tex_block("_MetallicGlossMap", metallic_tex, metallic_scale, metallic_offset)}{tex_block("_OcclusionMap", occlusion_tex, occlusion_scale, occlusion_offset)}{tex_block("_ParallaxMap", "{fileID: 0}", "{x: 1, y: 1}", "{x: 0, y: 0}")}{tex_block("_SpecGlossMap", "{fileID: 0}", "{x: 1, y: 1}", "{x: 0, y: 0}")}{tex_block("unity_Lightmaps", "{fileID: 0}", "{x: 1, y: 1}", "{x: 0, y: 0}")}{tex_block("unity_LightmapsInd", "{fileID: 0}", "{x: 1, y: 1}", "{x: 0, y: 0}")}{tex_block("unity_ShadowMasks", "{fileID: 0}", "{x: 1, y: 1}", "{x: 0, y: 0}")}    m_Ints: []
    m_Floats:
    - _AlphaClip: 0
    - _Blend: 0
    - _BumpScale: {bump_scale_f}
    - _ClearCoatMask: 0
    - _ClearCoatSmoothness: 0
    - _Cull: 2
    - _Cutoff: 0.5
    - _DetailAlbedoMapScale: 1
    - _DetailNormalMapScale: 1
    - _DstBlend: 0
    - _EnvironmentReflections: 1
    - _GlossMapScale: 0
    - _Glossiness: {glossiness}
    - _GlossyReflections: 0
    - _Metallic: {metallic}
    - _OcclusionStrength: {occlusion}
    - _Parallax: 0.02
    - _QueueOffset: 0
    - _ReceiveShadows: 1
    - _Smoothness: {glossiness}
    - _SmoothnessTextureChannel: 0
    - _SpecularHighlights: 1
    - _SrcBlend: 1
    - _Surface: 0
    - _WorkflowMode: 1
    - _ZWrite: 1
    m_Colors:
    - _BaseColor: {color}
    - _Color: {color}
    - _EmissionColor: {emission}
    - _SpecColor: {{r: 0.2, g: 0.2, b: 0.2, a: 1}}
  m_BuildTextureStacks: []
"""
    # Fix empty keywords representation
    if keywords:
        content = content.replace(
            "  m_ValidKeywords: []\n",
            "  m_ValidKeywords:\n" + "\n".join(f"  - {k}" for k in keywords) + "\n",
        )
    path.write_text(content, encoding="utf-8", newline="\n")
    print(f"[Lit] {path}")


def write_unlit(
    path: Path,
    *,
    transparent: bool,
    additive: bool = False,
    shader_guid: str = URP_UNLIT,
) -> None:
    text = path.read_text(encoding="utf-8")
    name = extract_name(text, path)
    main_tex, main_scale, main_offset = extract_tex(text, "_MainTex")
    color = extract_color(text, "_Color", "{r: 1, g: 1, b: 1, a: 1}")

    if additive:
        surface = 1
        blend = 1  # Additive
        src = 5  # SrcAlpha
        dst = 1  # One
        zwrite = 0
        queue = 3000
        render_type = "Transparent"
        keywords = ["_SURFACE_TYPE_TRANSPARENT"]
        disabled = ["DepthOnly", "SHADOWCASTER"]
    elif transparent:
        surface = 1
        blend = 0  # Alpha
        src = 5
        dst = 10
        zwrite = 0
        queue = 3000
        render_type = "Transparent"
        keywords = ["_SURFACE_TYPE_TRANSPARENT"]
        disabled = ["DepthOnly", "SHADOWCASTER"]
    else:
        surface = 0
        blend = 0
        src = 1
        dst = 0
        zwrite = 1
        queue = -1
        render_type = "Opaque"
        keywords = []
        disabled = []

    kw_yaml = "[]" if not keywords else "\n" + "\n".join(f"  - {k}" for k in keywords)
    disabled_yaml = "[]" if not disabled else "\n" + "\n".join(f"  - {d}" for d in disabled)

    # Particles Unlit uses slightly different props; stick to Unlit for simplicity
    # except hit effect which uses Particles Unlit Additive
    if shader_guid == URP_PARTICLES_UNLIT:
        content = _particles_unlit_content(
            name, main_tex, main_scale, main_offset, color, additive
        )
    else:
        content = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &-7741268771140260385
MonoBehaviour:
  m_ObjectHideFlags: 11
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {MAT_VERSION_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  version: 5
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 4800000, guid: {shader_guid}, type: 3}}
  m_ValidKeywords: {kw_yaml}
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: {queue}
  stringTagMap:
    RenderType: {render_type}
  disabledShaderPasses: {disabled_yaml}
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
{tex_block("_BaseMap", main_tex, main_scale, main_offset)}{tex_block("_MainTex", main_tex, main_scale, main_offset)}    m_Ints: []
    m_Floats:
    - _AlphaClip: 0
    - _Blend: {blend}
    - _Cull: 2
    - _Cutoff: 0.5
    - _DstBlend: {dst}
    - _QueueOffset: 0
    - _SrcBlend: {src}
    - _Surface: {surface}
    - _ZWrite: {zwrite}
    m_Colors:
    - _BaseColor: {color}
    - _Color: {color}
  m_BuildTextureStacks: []
"""
    path.write_text(content, encoding="utf-8", newline="\n")
    mode = "Add" if additive else ("Transparent" if transparent else "Opaque")
    print(f"[Unlit/{mode}] {path}")


def _particles_unlit_content(
    name: str,
    main_tex: str,
    main_scale: str,
    main_offset: str,
    color: str,
    additive: bool,
) -> str:
    blend = 1 if additive else 0
    src = 5
    dst = 1 if additive else 10
    # ParticlesUnlit uses _Blend/_SrcBlend/_DstBlend; only surface keyword is required.
    keywords = ["_SURFACE_TYPE_TRANSPARENT"]
    kw = "\n".join(f"  - {k}" for k in keywords)
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &-7741268771140260385
MonoBehaviour:
  m_ObjectHideFlags: 11
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {MAT_VERSION_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  version: 5
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 4800000, guid: {URP_PARTICLES_UNLIT}, type: 3}}
  m_ValidKeywords:
{kw}
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: 3000
  stringTagMap:
    RenderType: Transparent
  disabledShaderPasses:
  - DepthOnly
  - SHADOWCASTER
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
{tex_block("_BaseMap", main_tex, main_scale, main_offset)}{tex_block("_MainTex", main_tex, main_scale, main_offset)}    m_Ints: []
    m_Floats:
    - _AlphaClip: 0
    - _Blend: {blend}
    - _BlendOp: 0
    - _CameraFadingEnabled: 0
    - _CameraFarFadeDistance: 2
    - _CameraNearFadeDistance: 1
    - _ColorMode: 0
    - _Cull: 2
    - _Cutoff: 0.5
    - _DistortionBlend: 0.5
    - _DistortionEnabled: 0
    - _DistortionStrength: 1
    - _DistortionStrengthScaled: 0.1
    - _DstBlend: {dst}
    - _FlipbookBlending: 0
    - _FlipbookMode: 0
    - _Mode: 0
    - _QueueOffset: 0
    - _SoftParticlesEnabled: 0
    - _SoftParticlesFarFadeDistance: 1
    - _SoftParticlesNearFadeDistance: 0
    - _SrcBlend: {src}
    - _Surface: 1
    - _ZWrite: 0
    m_Colors:
    - _BaseColor: {color}
    - _BaseColorAddSubDiff: {{r: 0, g: 0, b: 0, a: 0}}
    - _CameraFadeParams: {{r: 0, g: Infinity, b: 0, a: 0}}
    - _Color: {color}
    - _EmissionColor: {{r: 0, g: 0, b: 0, a: 1}}
    - _SoftParticleFadeParams: {{r: 0, g: 0, b: 0, a: 0}}
  m_BuildTextureStacks: []
"""


def main() -> None:
    for rel in STANDARD_MATS:
        write_lit(ROOT / rel)
    for rel in TRANSPARENT_UNLIT:
        write_unlit(ROOT / rel, transparent=True)
    for rel in OPAQUE_UNLIT:
        write_unlit(ROOT / rel, transparent=False)
    for rel in HIT_ADDITIVE:
        write_unlit(
            ROOT / rel,
            transparent=True,
            additive=True,
            shader_guid=URP_PARTICLES_UNLIT,
        )
    print("Done.")


if __name__ == "__main__":
    main()
