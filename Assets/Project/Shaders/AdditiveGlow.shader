// Additive sprite/particle shader that can go brighter than 1 (HDR), so the camera's Bloom picks it up.
// Vertex color tints it (sprites and particles pass their color that way); _Intensity lifts it into HDR;
// _Pulse makes it breathe over time without any script.
Shader "BoardGame/AdditiveGlow"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Intensity ("Intensity (HDR)", Float) = 2
        _Pulse ("Pulse Amount", Range(0, 1)) = 0
        _PulseSpeed ("Pulse Speed", Float) = 4
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Intensity;
            float _Pulse;
            float _PulseSpeed;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv) * i.color;
                float pulse = 1.0 - _Pulse * 0.5 * (1.0 + sin(_Time.y * _PulseSpeed));
                c.rgb *= _Intensity * pulse;
                return c;
            }
            ENDCG
        }
    }
}
