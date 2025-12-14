Shader "Hidden/DrunkDistortion"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        
        ZWrite Off
        ZTest Always
        Blend Off
        Cull Off

        Pass
        {
            Name "DrunkDistortion"
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Intensity;
            float _Speed;

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                
                // Create wavy distortion effect
                float time = _Time.y * _Speed;
                
                // Multiple wave frequencies for drunk effect
                float2 distortion = float2(
                    sin(uv.y * 10.0 + time) * 0.02,
                    cos(uv.x * 8.0 + time * 0.7) * 0.02
                );
                
                // Add chromatic aberration
                distortion += float2(
                    sin(time * 2.0) * 0.01,
                    cos(time * 1.5) * 0.01
                );
                
                // Apply distortion with intensity
                float2 distortedUV = uv + distortion * _Intensity;
                
                // Sample the texture with distorted UVs
                float4 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, distortedUV);
                
                // Optional: Add slight color shift for extra drunk feel
                float4 colorR = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, distortedUV + float2(0.003, 0.0) * _Intensity);
                float4 colorB = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, distortedUV - float2(0.003, 0.0) * _Intensity);
                
                color.r = colorR.r;
                color.b = colorB.b;
                
                return color;
            }
            ENDHLSL
        }
    }
}