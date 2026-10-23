// Stack IT blocks: unlit, three exact face colours (the look's block tokens) picked by the
// object-space normal, so the bevels blend softly between faces. The camera sees the top, the
// -X face on its left and the -Z face on its right; the hidden faces reuse those colours for
// tumbling debris. _Glow adds emission (Neon blocks, perfect flashes) for bloom to catch.
Shader "StackIt/Block"
{
    Properties
    {
        _Top ("Top", Color) = (1, 1, 1, 1)
        _Left ("Left", Color) = (0.85, 0.85, 0.85, 1)
        _Right ("Right", Color) = (0.7, 0.7, 0.7, 1)
        _Glow ("Glow", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Top;
                half4 _Left;
                half4 _Right;
                half _Glow;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                float3 n = normalize(v.normalOS);
                // Weight per face; squaring sharpens the hand-off across the bevels.
                float top = saturate(n.y);
                float left = saturate(-n.x) + 0.6 * saturate(n.z);
                float right = saturate(-n.z) + 0.6 * saturate(n.x) + 0.8 * saturate(-n.y);
                float3 w = float3(top, left, right);
                w *= w;
                w /= max(w.x + w.y + w.z, 1e-4);
                o.color = _Top.rgb * w.x + _Left.rgb * w.y + _Right.rgb * w.z;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                return half4(i.color * (1.0 + _Glow), 1.0);
            }
            ENDHLSL
        }
    }
}
