// The player's outline when something stands in front of them (Track C). Every pixel of the sprite is tested against
// the scenery already drawn (ZTest Greater): only the pixels something covers pass. There the outline is drawn just
// outside the figure (empty pixels next to a solid one), so it's one unbroken line all the way round however thin a limb
// is, with a faint fill inside so the hidden shape still reads.
// The depth test runs as if the sprite stood _DepthBias world units nearer the camera (slid along the camera ray, so
// nothing moves on screen): the ground her feet stand on no longer counts as covering them, only real scenery in front.
// Used by PlayerSilhouette on a copy of the player's sprite.
Shader "Reverse Shell/Silhouette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Outline", Color) = (1, 0.9, 0.55, 1)
        _Fill ("Fill strength", Range(0, 1)) = 0.18
        _DepthBias ("Depth bias toward the camera (world units)", Float) = 0.35
    }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        ZWrite Off
        ZTest Greater
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            half _Fill;
            float _DepthBias;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                // toward the camera along its ray (perspective) or its forward axis (orthographic)
                float3 toCam = unity_OrthoParams.w > 0.5 ? UNITY_MATRIX_V[2].xyz : normalize(_WorldSpaceCameraPos - world);
                o.pos = mul(UNITY_MATRIX_VP, float4(world + toCam * _DepthBias, 1.0));
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float a = tex2D(_MainTex, i.uv).a;
                float2 t = _MainTex_TexelSize.xy;
                // Inside the figure: the faint fill. Outside it: the outline, where any neighbour (8 around) is solid.
                if (a >= 0.5)
                {
                    if (_Fill <= 0.0) discard;
                    return fixed4(_Color.rgb, _Color.a * _Fill);
                }
                float near = max(max(tex2D(_MainTex, i.uv + float2(t.x, 0)).a, tex2D(_MainTex, i.uv - float2(t.x, 0)).a),
                                 max(tex2D(_MainTex, i.uv + float2(0, t.y)).a, tex2D(_MainTex, i.uv - float2(0, t.y)).a));
                near = max(near, max(max(tex2D(_MainTex, i.uv + t).a, tex2D(_MainTex, i.uv - t).a),
                                     max(tex2D(_MainTex, i.uv + float2(t.x, -t.y)).a, tex2D(_MainTex, i.uv + float2(-t.x, t.y)).a)));
                if (near < 0.5) discard;
                return fixed4(_Color.rgb, _Color.a);
            }
            ENDCG
        }
    }
}
