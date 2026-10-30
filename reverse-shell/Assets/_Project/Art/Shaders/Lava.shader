// Track C's glowing pools (lava ~, ooze z): two layers of noise drifting against each other, cut into four flat colour
// bands (pixel-art lava that churns), self-lit (no shading) and fogged like the rest of the world. The mesh's UVs are
// world positions, so the flow runs on across neighbouring tiles. Colours, speeds and glow come from WorldEffects.
Shader "Reverse Shell/Lava"
{
    Properties
    {
        _MainTex ("Noise (grey, tiling)", 2D) = "gray" {}
        _C0 ("Darkest", Color) = (0.25, 0.05, 0.02, 1)
        _C1 ("Dark", Color) = (0.6, 0.12, 0.03, 1)
        _C2 ("Bright", Color) = (0.95, 0.38, 0.06, 1)
        _C3 ("Brightest", Color) = (1, 0.82, 0.35, 1)
        _Speed ("Flow (layer 1 xy, layer 2 zw)", Vector) = (0.06, 0.035, -0.045, 0.05)
        _Glow ("Glow", Float) = 1.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _C0, _C1, _C2, _C3;
            float4 _Speed;
            half _Glow;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y;
                float a = tex2D(_MainTex, i.uv + _Speed.xy * t).r;
                float b = tex2D(_MainTex, i.uv * 1.7 + _Speed.zw * t).r;
                // The two layers churn into each other; a slow swell lifts the whole pool now and then.
                float n = a * 0.6 + b * 0.4 + sin(t * 1.3 + a * 6.0) * 0.04;
                fixed4 c = n < 0.4 ? _C0 : (n < 0.5 ? _C1 : (n < 0.62 ? _C2 : _C3));
                c.rgb *= _Glow;
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    FallBack "Unlit/Texture"
}
