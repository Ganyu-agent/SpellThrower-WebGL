// 칸 표시 전용. 스프라이트 기본 셰이더는 틴트를 정점 단계에서 fixed4 보간기에 실어
// 보내기 때문에 HDR(1 초과) 값이 빌드에서 잘려 블룸 임계값을 못 넘는다.
// 여기서는 틴트를 프래그먼트 단계에서 float4 로 곱해 어디서도 잘리지 않게 한다.
Shader "SpellThrower/TileGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _TintHDR ("HDR Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha   // 프리멀티플라이

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _TintHDR;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _TintHDR;
                c.rgb *= c.a;
                return c;
            }
            ENDHLSL
        }
    }
}
