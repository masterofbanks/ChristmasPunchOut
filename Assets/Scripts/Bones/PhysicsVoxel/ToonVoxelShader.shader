Shader "Custom/ToonVoxel"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        
        [Header(Toon Shading)]
        _Shades ("Shading Levels", Range(1, 10)) = 3
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.005
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        
        [Header(Lighting)]
        _ShadowTint ("Shadow Tint", Color) = (0.5, 0.5, 0.5, 1)
        _RampThreshold ("Ramp Threshold", Range(0, 1)) = 0.5
        _RampSmooth ("Ramp Smoothness", Range(0, 1)) = 0.1
        
        [Header(Specular)]
        _SpecularColor ("Specular Color", Color) = (1,1,1,1)
        _Glossiness ("Glossiness", Range(0, 1)) = 0.5
        _SpecularStrength ("Specular Strength", Range(0, 1)) = 0.1
        
        [Header(Rim Light)]
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimAmount ("Rim Amount", Range(0, 1)) = 0.7
        _RimThreshold ("Rim Threshold", Range(0, 1)) = 0.1
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 200
        
        // OUTLINE PASS
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };
            
            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // Expand vertices along normals for outline
                float3 normalOS = normalize(input.normalOS);
                float3 positionOS = input.positionOS.xyz + normalOS * _OutlineWidth;
                
                output.positionHCS = TransformObjectToHClip(positionOS);
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
        
        // MAIN TOON PASS
        Pass
        {
            Name "ToonShading"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _ShadowTint;
                float4 _SpecularColor;
                float4 _RimColor;
                float _Shades;
                float _RampThreshold;
                float _RampSmooth;
                float _Glossiness;
                float _SpecularStrength;
                float _RimAmount;
                float _RimThreshold;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // Sample texture
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                
                // Lighting setup
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 normalWS = normalize(input.normalWS);
                float3 lightDir = normalize(mainLight.direction);
                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                
                // Calculate N·L
                float NdotL = dot(normalWS, lightDir);
                
                // Toon ramp (stepped lighting)
                float lightIntensity = smoothstep(_RampThreshold - _RampSmooth, _RampThreshold + _RampSmooth, NdotL);
                lightIntensity = floor(lightIntensity * _Shades) / _Shades; // Quantize to steps
                
                // Apply shadow
                lightIntensity *= mainLight.shadowAttenuation;
                
                // Mix light and shadow colors
                float3 lighting = lerp(_ShadowTint.rgb, mainLight.color, lightIntensity);
                
                // Specular highlight (toon style)
                float3 halfVector = normalize(lightDir + viewDir);
                float NdotH = dot(normalWS, halfVector);
                float specular = pow(NdotH * lightIntensity, _Glossiness * 100);
                specular = smoothstep(0.005, 0.01, specular);
                float3 specularColor = specular * _SpecularColor.rgb * _SpecularStrength;
                
                // Rim lighting
                float rimDot = 1 - dot(viewDir, normalWS);
                float rimIntensity = rimDot * pow(NdotL, _RimThreshold);
                rimIntensity = smoothstep(_RimAmount - 0.01, _RimAmount + 0.01, rimIntensity);
                float3 rim = rimIntensity * _RimColor.rgb;
                
                // Combine all lighting
                float3 finalColor = albedo.rgb * lighting + specularColor + rim;
                
                // Add ambient lighting
                finalColor += SampleSH(normalWS) * albedo.rgb * 0.3;
                
                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }
        
        // Shadow caster pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back
            
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
    
    FallBack "Universal Render Pipeline/Lit"
}