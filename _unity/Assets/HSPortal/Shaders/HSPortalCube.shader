Shader "HSPortal/Cube"
{
    Properties
    {
        _MainTex ("Diffuse", 2D) = "white" {}
        _EmissionTex ("Glow", 2D) = "black" {}
        _Color ("Color", Color) = (1,1,1,1)
        _EmissionStrength ("Emission", Float) = 1.35
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Lighting Off
        ZWrite On
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _EmissionTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _EmissionStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 d = tex2D(_MainTex, i.uv) * _Color;
                fixed4 e = tex2D(_EmissionTex, i.uv);
                // Diffuse art has a white disc; keep it out of 7DTD bloom, glow only the heart.
                fixed glow = max(e.r, max(e.g, e.b));
                return saturate(d * 0.62 + e * glow * _EmissionStrength);
            }
            ENDCG
        }
    }
    FallBack "Unlit/Texture"
}
