// Dibuja solo la Secondary Texture "_EmissionTex" del sprite con un color HDR (> 1),
// sin luces 2D, para que el Bloom del post-procesado la haga brillar.
Shader "Dungeon/Sprite Emission Glow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _EmissionTex ("Emission Mask (Secondary Texture)", 2D) = "black" {}
        [HDR] _GlowColor ("Glow Color", Color) = (4, 0.35, 0.3, 1)
        _PulseSpeed ("Pulse Speed", Float) = 2.5
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.2

        // Legacy, igual que Sprite-Unlit-Default
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex GlowVertex
            #pragma fragment GlowFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            TEXTURE2D(_EmissionTex);
            SAMPLER(sampler_EmissionTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _GlowColor;
                half _PulseSpeed;
                half _PulseAmount;
            CBUFFER_END

            Varyings GlowVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 GlowFragment(Varyings input) : SV_Target
            {
                half4 mask = SAMPLE_TEXTURE2D(_EmissionTex, sampler_EmissionTex, input.uv);
                half pulse = 1.0h - _PulseAmount * (0.5h + 0.5h * sin(_Time.y * _PulseSpeed));
                half3 rgb = mask.rgb * _GlowColor.rgb * input.color.rgb * pulse;
                return half4(rgb, mask.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
