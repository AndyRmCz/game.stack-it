// Rounded rectangle for uGUI, drawn from a signed distance field: crisp at any resolution, with an
// optional inner border and an outer soft shadow or glow. Every parameter travels in vertex data
// (see RoundedRect.cs), so all surfaces share this one material and batch together.
//   uv0.xy  position relative to the rect centre   uv0.zw  half size
//   uv1     fill colour (sRGB)                     uv2     border colour (sRGB)
//   uv3     shadow / glow colour (sRGB)
//   tangent radius, border width, shadow blur, shadow offset (down)
//   normal  x: shadow spread
// The vertex colour multiplies the result (CanvasGroup alpha and Graphic.color end up there).
Shader "StackIt/UI/RoundedRect"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha // premultiplied
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                float4 uv3 : TEXCOORD3;
                float4 tangent : TANGENT;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float4 shape : TEXCOORD0;
                float4 fill : TEXCOORD1;
                float4 border : TEXCOORD2;
                float4 shadow : TEXCOORD3;
                float4 params : TEXCOORD4;
                float4 local : TEXCOORD5;
                float spread : TEXCOORD6;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 _ClipRect;

            float4 Linear(float4 c)
            {
            #ifdef UNITY_COLORSPACE_GAMMA
                return c;
            #else
                return float4(UIGammaToLinear(saturate(c.rgb)), c.a);
            #endif
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.local = v.vertex;
                o.color = Linear(v.color);
                o.shape = v.uv0;
                o.fill = Linear(v.uv1);
                o.border = Linear(v.uv2);
                o.shadow = Linear(v.uv3);
                o.params = v.tangent;
                o.spread = v.normal.x;
                return o;
            }

            // Signed distance to a box of half size b with corner radius r (negative inside).
            float RoundBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 p = i.shape.xy;
                float2 hs = i.shape.zw;
                float radius = min(i.params.x, min(hs.x, hs.y));
                float borderWidth = i.params.y;
                float blur = i.params.z;
                float drop = i.params.w;

                float d = RoundBox(p, hs, radius);
                float aa = max(fwidth(d), 1e-4);
                float inside = saturate(0.5 - d / aa);
                float ring = borderWidth > 0.0 ? inside * saturate(0.5 + (d + borderWidth) / aa) : 0.0;

                float s = RoundBox(p + float2(0.0, drop), hs + i.spread, radius + i.spread);
                float shadowCover = blur > 0.001 ? 1.0 - smoothstep(-blur * 0.5, blur, s) : saturate(0.5 - s / aa);

                // Shadow under the fill, border over it; premultiplied "over".
                float sa = i.shadow.a * shadowCover;
                float fa = i.fill.a * inside;
                float ba = i.border.a * ring;
                float3 rgb = i.shadow.rgb * sa;
                float a = sa;
                rgb = i.fill.rgb * fa + rgb * (1.0 - fa);
                a = fa + a * (1.0 - fa);
                rgb = i.border.rgb * ba + rgb * (1.0 - ba);
                a = ba + a * (1.0 - ba);

                float4 o = float4(rgb * i.color.rgb, a) * i.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                o *= UnityGet2DClipping(i.local.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(o.a - 0.001);
                #endif
                return o;
            }
            ENDCG
        }
    }
}
