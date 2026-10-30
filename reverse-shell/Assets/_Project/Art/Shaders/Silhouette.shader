// The player's outline when something stands in front of them (Track C). Every pixel of the sprite is tested against
// the scenery already drawn (ZTest Greater): only the pixels something covers pass, and of those only the sprite's
// edge is drawn (no fill by default), so the parts of you that are hidden show as an outline and nothing else changes.
// Used by PlayerSilhouette on a copy of the player's sprite.
Shader "Reverse Shell/Silhouette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Outline", Color) = (1, 0.9, 0.55, 1)
        _Fill ("Fill strength", Range(0, 1)) = 0
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

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float a = tex2D(_MainTex, i.uv).a;
                float2 t = _MainTex_TexelSize.xy;
                // An edge pixel: inside the sprite, with an empty pixel beside it.
                float around = tex2D(_MainTex, i.uv + float2(t.x, 0)).a * tex2D(_MainTex, i.uv - float2(t.x, 0)).a
                             * tex2D(_MainTex, i.uv + float2(0, t.y)).a * tex2D(_MainTex, i.uv - float2(0, t.y)).a;
                if (a < 0.5) discard;
                float edge = around < 0.5 ? 1.0 : _Fill;
                if (edge <= 0.0) discard;
                return fixed4(_Color.rgb, _Color.a * edge);
            }
            ENDCG
        }
    }
}
