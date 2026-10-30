// Track C's world look (built-in pipeline): flat toon shading in three steps, so 3D models read as pixel art once the
// PixelCamera has drawn them small.
//   Lighting   Lambert for solid things, wrapped toward half-Lambert for thin leaves (_Wrap 1: lit from both sides),
//              stepped into bright / mid / shadow bands; the sun's shadows and its dapple cookie still land on it, and
//              the scene's ambient and fog apply as usual. The models' vertex colours (the kit's baked shading) darken
//              the albedo, so trunks and grass roots read deeper.
//   Cutout     alpha-tested (_Cutoff) for leaves, flowers and grass, with Cull set per material (_Cull 0 = both sides).
//   Wind       a gentle sway that grows with height above the model's origin (_Wind 0 = still: bark, rocks).
//   Emission   _EmissionColor adds glow (lava, crystals, ooze); bloom picks it up with Graphics: Pretty.
//   Rim        _RimColor lights the edges that turn away from the camera (glassy ice trees); black = off.
//   Shade      _ShadeColor tints what the sun doesn't reach (the shadow band fully, the mid band half way, and the
//              ambient light), so pale frosted trees shade into light blue instead of washing out; white = off.
// Materials are made at runtime by NatureKit / WorldArt; web builds keep the shader through WebBuildSetup's list.
Shader "Reverse Shell/WorldToon"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        _Wind ("Wind sway", Range(0,2)) = 0
        _Wrap ("Light wrap (0 solid, 1 leaves)", Range(0,1)) = 0
        _VertexShade ("Vertex colour shading", Range(0,1)) = 0.85
        _ShadeHigh ("Lit band brightness", Range(0,1.5)) = 0.9
        _ShadeLow ("Shadow band brightness", Range(0,1)) = 0.3
        _ShadeMid ("Mid band brightness", Range(0,1)) = 0.6
        _ShadeColor ("Shadow tint", Color) = (1,1,1,1)
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,0)
        [HDR] _RimColor ("Rim (glass edge)", Color) = (0,0,0,0)
        _RimPower ("Rim tightness", Range(0.5,8)) = 2.5
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull [_Cull]
        LOD 200

        CGPROGRAM
        #pragma surface surf Toon vertex:vert alphatest:_Cutoff addshadow fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Wind, _Wrap, _VertexShade, _ShadeLow, _ShadeMid, _ShadeHigh;
        half4 _EmissionColor, _RimColor, _ShadeColor;
        half _RimPower;

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
            float3 viewDir;
            float3 worldNormal;
        };

        void vert(inout appdata_full v)
        {
            if (_Wind <= 0) return;
            float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
            float3 origin = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
            float height = max(0, world.y - origin.y);
            float t = _Time.y * 1.6 + world.x * 0.7 + world.z * 0.45;
            float3 sway = float3(sin(t), 0, sin(t * 0.83 + 1.3) * 0.6) * _Wind * height * 0.035;
            v.vertex.xyz += mul((float3x3)unity_WorldToObject, sway);
        }

        half4 LightingToon(SurfaceOutput s, half3 lightDir, half atten)
        {
            half ndl = dot(s.Normal, lightDir);
            half lit = saturate(lerp(ndl, ndl * 0.5 + 0.5, _Wrap)) * atten;
            half band = lit > 0.55 ? _ShadeHigh : (lit > 0.22 ? _ShadeMid : _ShadeLow);
            half3 tint = lit > 0.55 ? half3(1, 1, 1) : (lit > 0.22 ? lerp(half3(1, 1, 1), _ShadeColor.rgb, 0.5) : _ShadeColor.rgb);
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * band * tint;
            c.a = s.Alpha;
            return c;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            // The kit's vertex colours are grey shading (1 = none); _VertexShade 0 for meshes without them (the ruins).
            o.Albedo = c.rgb * lerp(1, IN.color.rgb, _VertexShade);
            o.Alpha = c.a;
            o.Emission = _EmissionColor.rgb * c.rgb + _EmissionColor.rgb * 0.25;
            // The ambient light takes the shadow tint too (nothing changes while it is white).
            o.Emission += ShadeSH9(half4(normalize(IN.worldNormal), 1)) * o.Albedo * (_ShadeColor.rgb - 1);
            // Glass: the edges turning away from the camera catch the light (stepped, to stay pixel-crisp).
            half rim = pow(1.0 - saturate(abs(dot(normalize(IN.viewDir), o.Normal))), _RimPower);
            o.Emission += _RimColor.rgb * (rim > 0.45 ? 1.0 : (rim > 0.2 ? 0.45 : 0.0));
        }
        ENDCG
    }
    FallBack "Legacy Shaders/Transparent/Cutout/Diffuse"
}
