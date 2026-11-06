// The Collections' 3D card: unlit art with a glossy sheen that slides as the card turns. Drawn with
// _Mirror on, it is the card's reflection on the floor, fading out below _FloorY.
Shader "SoulBind/Card Gloss"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Sheen ("Sheen", Range(0, 1)) = 0.35
        _Mirror ("Reflection (0 or 1)", Float) = 0
        _FloorY ("Floor height", Float) = 0
        _FadeLength ("Reflection length", Float) = 0.6
        _MirrorAlpha ("Reflection strength", Range(0, 1)) = 0.4
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Cull Back
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Sheen;
                half _Mirror;
                float _FloorY;
                float _FadeLength;
                half _MirrorAlpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;      // on the art
                float2 cardUv : TEXCOORD1;  // 0 to 1 across the card
                float3 normalWS : TEXCOORD2;
                float worldY : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.cardUv = input.uv;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.worldY = positionWS.y;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half3 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb * _Color.rgb;

                // A side-on face is a little darker, and a diagonal band of light slides as it turns.
                color *= lerp(0.7, 1.0, abs(normal.z));
                float place = (input.cardUv.x + input.cardUv.y) * 0.7 + normal.x * 1.2 + normal.y * 0.8;
                float band = saturate(1.0 - abs(place - 0.7) * 4.0);
                color += band * band * _Sheen;

                half alpha = 1.0;
                if (_Mirror > 0.5)
                    alpha = saturate((input.worldY - (_FloorY - _FadeLength)) / _FadeLength) * _MirrorAlpha;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
