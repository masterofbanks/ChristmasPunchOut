Shader "Hidden/PixelateEffectStencil"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _LowResTex ("Low Res Texture", 2D) = "white" {}
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        
        Stencil
        {
            Ref 1
            Comp NotEqual
            Pass Keep
        }

        Pass
        {
            Name "Pixelate Blit"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_LowResTex);
            SAMPLER(sampler_LowResTex);

            half4 frag(Varyings input) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_LowResTex, sampler_LowResTex, input.texcoord);
            }
            ENDHLSL
        }
    }
}