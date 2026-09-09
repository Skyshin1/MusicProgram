"""One-time mechanical adapter of the six supplied ASE surface graphs to URP.

Copies the authored Properties, surf and vertex equations verbatim. Does not
rewrite .mat files or shader GUIDs. Original shader sources are backed up under
Logs/OceanURPMigration/OriginalShaders before the mechanical rewrite.
"""
from pathlib import Path
import re
import textwrap

ROOT = Path(__file__).resolve().parents[1]
SHADERS = ROOT / 'Assets/Davis3D/OceanEnvironmentPack2/Shaders'
BACKUP = ROOT / 'Logs/OceanURPMigration/OriginalShaders'

FORWARD = '''
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
#pragma multi_compile_fragment _ _LIGHT_COOKIES
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
#pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
#pragma multi_compile _ SHADOWS_SHADOWMASK
#pragma multi_compile_fog
'''

def pass_block(name, light_mode, vert, frag, pragmas='', state=''):
    return f'''
        Pass
        {{
            Name "{name}"
            Tags {{ "LightMode" = "{light_mode}" }}
            {state}
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex {vert}
            #pragma fragment {frag}
            #pragma multi_compile_instancing
            {pragmas}
            ENDHLSL
        }}
'''

def polish(source):
    # Make legacy implicit casts explicit. Clamp only invalid fractional-power
    # inputs; nonnegative authored colors retain their original equations.
    source = source.replace('float3 ase_worldPos = mul( unity_ObjectToWorld, v.vertex );',
                            'float3 ase_worldPos = mul( unity_ObjectToWorld, v.vertex ).xyz;')
    source = source.replace('o.ase_texcoord5 = v.ase_texcoord4;',
                            'o.ase_texcoord5 = v.ase_texcoord4.xy;')
    source = source.replace('float3 ase_vertex3Pos = mul( unity_WorldToObject, float4( i.worldPos , 1 ) );',
                            'float3 ase_vertex3Pos = mul( unity_WorldToObject, float4( i.worldPos , 1 ) ).xyz;')
    source = re.sub(r'pow\( (desaturateVar\w+) , (temp_cast_\w+) \)', r'pow( max(\1, 0.0) , \2 )', source)
    source = re.sub(r'(float3 normalizeResult2_g\d+ = )normalize\(', r'\1SafeNormalize(', source)
    return re.sub(r'\n(?:[ \t]*\n){2,}', '\n\n', source)

def main():
    BACKUP.mkdir(parents=True, exist_ok=True)
    for name in ('Coral', 'Coral Tesselated', 'CoralSun', 'Kelp', 'Rocks', 'Shipwreck'):
        path = SHADERS / (name + '.shader')
        current = path.read_text(encoding='utf-8-sig')
        if 'OceanSurfaceCompatURP.hlsl' in current:
            # Deterministic maintenance of the generated wrapper; never re-run
            # against original graphs or rewrite material values.
            if '#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING' not in current:
                current = current.replace('#pragma multi_compile_fragment _ _LIGHT_COOKIES',
                    '#pragma multi_compile_fragment _ _LIGHT_COOKIES\n#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING\n#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION\n#pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS')
            current = polish(current)
            if current != path.read_text(encoding='utf-8-sig'):
                path.write_text(current, encoding='utf-8')
            print('Already ported: ' + name)
            continue
        original = BACKUP / path.name
        if not original.exists():
            original.write_text(current, encoding='utf-8')
        header = current[:current.index('\tSubShader')]
        chunk = current[current.index('CGINCLUDE') + len('CGINCLUDE'):current.index('ENDCG')]
        # The source contains a shadow-only redefinition of Unity surface macros.
        # These become unconditional adapter macros, defined in our URP include.
        chunk = re.sub(r'\s*#ifdef UNITY_PASS_SHADOWCASTER.*?#endif', '', chunk, count=1, flags=re.S)
        chunk = re.sub(r'^\s*#include.*$', '', chunk, flags=re.M)
        chunk = re.sub(r'^\s*#pragma target.*$', '', chunk, flags=re.M)
        keywords = '\n'.join(re.findall(r'^\s*#pragma shader_feature_local[^\n]*', chunk, re.M))
        chunk = re.sub(r'^\s*#pragma shader_feature_local[^\n]*', '', chunk, flags=re.M)
        chunk = re.sub(r'\s*struct appdata_full_custom\s*\{.*?\};', '', chunk, flags=re.S)
        chunk = chunk.replace('appdata_full_custom', 'OceanAttributes').replace('appdata_full', 'OceanAttributes')
        chunk = chunk.replace('half ASEVFace : VFACE;', 'half ASEVFace;')
        uniforms = re.findall(r'^\s*uniform\s+([^;]+);', chunk, re.M)
        chunk = re.sub(r'^\s*uniform\s+[^;]+;', '', chunk, flags=re.M)
        samplers = [u for u in uniforms if u.startswith('sampler')]
        constants = [re.sub(r'\s*=.*', '', u) for u in uniforms if not u.startswith('sampler')]
        decl = '\n'.join(s + ';' for s in samplers) + '\nCBUFFER_START(UnityPerMaterial)\n'
        decl += '\n'.join(c + ';' for c in constants) + '\nCBUFFER_END\n'
        flags = ''
        if 'vertexDataFunc' in chunk:
            flags += '#define OCEAN_VERTEX_ANIMATION 1\n'
        if name == 'CoralSun': flags += '#define OCEAN_CORAL_SUN 1\n'
        if name == 'Kelp': flags += '#define OCEAN_KELP 1\n'
        render_type = 'TransparentCutout' if name == 'Kelp' else 'Opaque'
        queue = 'AlphaTest' if name == 'Kelp' else 'Geometry'
        cull = 'Off' if name == 'Kelp' else 'Back'
        result = header + f'''    // URP port; original authored graph equations below are retained.
    SubShader
    {{
        Tags {{ "RenderPipeline"="UniversalPipeline" "RenderType"="{render_type}" "Queue"="{queue}" "UniversalMaterialType"="Lit" }}
        Cull {cull}
        HLSLINCLUDE
        #include "OceanSurfaceCompatURP.hlsl"
        {keywords}
        {flags}
        {decl}
        {chunk}
        #include "OceanSurfacePassesURP.hlsl"
        ENDHLSL
'''
        result += pass_block('ForwardLit', 'UniversalForwardOnly', 'OceanVertex', 'OceanFragment', FORWARD)
        result += pass_block('ShadowCaster', 'ShadowCaster', 'OceanShadowVertex', 'OceanDepthFragment', '#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW', 'ZWrite On ZTest LEqual ColorMask 0')
        result += pass_block('DepthOnly', 'DepthOnly', 'OceanVertex', 'OceanDepthFragment', state='ZWrite On ColorMask R')
        result += pass_block('DepthNormals', 'DepthNormalsOnly', 'OceanVertex', 'OceanDepthNormalsFragment', '#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT', 'ZWrite On')
        result += pass_block('Meta', 'Meta', 'OceanMetaVertex', 'OceanMetaFragment', state='Cull Off')
        result += '    }\n    Fallback "Hidden/Universal Render Pipeline/FallbackError"\n}\n'
        path.write_text(polish(result), encoding='utf-8')
        print('Ported: ' + name)

if __name__ == '__main__':
    main()
