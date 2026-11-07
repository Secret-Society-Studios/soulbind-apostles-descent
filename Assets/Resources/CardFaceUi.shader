// The in-game card's UI mesh (CardBody): the art with the Collections card's diagonal gloss. The band
// follows the screen, so it slides over a card as the card moves, and drifts slowly while it rests.
// A negative uv marks a flat colour (the edge and shadow), which takes no art and no gloss.
Shader "SoulBind/Card Face UI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Sheen ("Sheen", Range(0, 1)) = 0.22
        _BandWidth ("Band width", Float) = 0.13
        _BandGap ("Band gap", Float) = 1.5
        _BandDrift ("Band drift", Float) = 0.1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" }

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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;
            float _Sheen;
            float _BandWidth;
            float _BandGap;
            float _BandDrift;

            struct Attributes
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 local : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.local = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                fixed4 color = input.color;
                if (input.uv.x >= 0.0)
                {
                    color *= tex2D(_MainTex, input.uv);
                    float2 place = input.vertex.xy / _ScreenParams.y; // screen heights
                    float along = frac((place.x + place.y + _Time.y * _BandDrift) / _BandGap) * _BandGap;
                    float band = saturate(1.0 - abs(along - _BandGap * 0.5) / _BandWidth);
                    color.rgb += band * band * _Sheen;
                }
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.local.xy, _ClipRect);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
