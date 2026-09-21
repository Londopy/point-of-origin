// Flat-shaded vertex colours, opaque, depth-written. The pass carries no
// LightMode tag on purpose: every URP renderer, the 2D one included, draws
// untagged passes as SRPDefaultUnlit. Lives under Resources so it ships.
Shader "PointOfOrigin/VertexColor"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : COLOR;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                // vertex colours are authored as sRGB hex values; the project renders in linear space
                o.color = float4(SRGBToLinear(v.color.rgb), v.color.a);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return i.color;
            }
            ENDHLSL
        }
    }
}
