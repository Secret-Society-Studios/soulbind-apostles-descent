// The Time Stop look: drawn in the scene just above the map, so the tiles, pieces, cards and HUD stay
// untouched on top of it. TimeStopEffect uses it twice. With the blend OneMinusDstColor / OneMinusSrcAlpha
// it turns what is already on screen into a tinted negative, with no copy of the screen; with One / One
// it adds a colour to lift the negative's shadows. Either way the effect grows as a circle with a ring.
// CardCinematic uses it too, for the legendary cards' light, darkness and rays.
Shader "SoulBind/Time Stop Wave"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 1)
        _Center ("Centre (0 to 1 across the screen)", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Radius (screen heights)", Float) = 0
        _Ring ("Ring width (screen heights)", Float) = 0.05
        _RingGlow ("Ring brightness", Range(0, 1)) = 1
        _Reverse ("Time starting again (0 or 1)", Float) = 0
        _Soft ("Edge softness (screen heights)", Float) = 0.01
        _Rays ("Rays of light (0 to 1)", Range(0, 1)) = 0
        _Strength ("Strength", Range(0, 1)) = 1
        _Aspect ("Screen width / height", Float) = 1.78
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source blend", Float) = 4
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination blend", Float) = 10
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend [_SrcBlend] [_DstBlend]

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _Center;
                float _Radius;
                float _Ring;
                float _RingGlow;
                float _Reverse;
                float _Soft;
                float _Rays;
                float _Strength;
                float _Aspect;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screen : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screen = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.screen.xy / input.screen.w;
                float2 offset = (uv - _Center.xy) * float2(_Aspect, 1.0);
                float reach = length(offset);

                float inside = 1.0 - smoothstep(_Radius - max(_Soft, 0.001), _Radius, reach);
                float covered = _Reverse > 0.5 ? 1.0 - inside : inside;
                float ring = 1.0 - saturate(abs(reach - _Radius) / max(_Ring, 0.001));

                float amount = saturate(covered * _Strength);
                if (_Rays > 0.0)
                {
                    float angle = atan2(offset.y, offset.x);
                    float beams = pow(0.5 + 0.5 * sin(angle * 12.0 + _Time.y * 0.7), 4.0);
                    amount *= lerp(1.0, beams, _Rays);
                }
                half3 color = _Color.rgb * amount + ring * _RingGlow;
                return half4(color, saturate(amount + ring * _RingGlow));
            }
            ENDHLSL
        }
    }
}
