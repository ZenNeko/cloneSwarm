// Translated to URP by Antigravity Converter
Shader "Vefects/SH_Vefects_BIRP_Unlit_Master_01"
{
    Properties
	{
		_TexturesMultiplySoftLight("Textures Multiply / Soft Light", Float) = 0
		_MaskMultiplySubtract("Mask Multiply / Subtract", Float) = 0
		_ParticleColorLUT("Particle Color / LUT", Float) = 0
		_BlendWithSecondaryTexture("Blend With Secondary Texture", Float) = 0
		[Space(33)][Header(Noise)][Space(13)]_NoiseTexture("Noise Texture", 2D) = "white" {}
		_NoiseTextureSelector("Noise Texture Selector", Vector) = (0,1,0,0)
		_NoiseUVScale("Noise UV Scale", Vector) = (0.3,1,0,0)
		_NoiseUVSpeed("Noise UV Speed", Vector) = (0,0,0,0)
		[Space(33)][Header(Secondary Noise)][Space(13)]_SecondaryNoiseTexture("Secondary Noise Texture", 2D) = "white" {}
		_SecondaryNoiseTextureSelector("Secondary Noise Texture Selector", Vector) = (0,1,0,0)
		_SecondaryNoiseUVScale("Secondary Noise UV Scale", Vector) = (0.3,1,0,0)
		_SecondaryNoiseUVSpeed("Secondary Noise UV Speed", Vector) = (0,0,0,0)
		_ErosionSmoothness("Erosion Smoothness", Float) = 1
		_Emission("Emission", Float) = 1
		_DepthFade("Depth Fade", Float) = 1
		[Space(33)][Header(Distortion)][Space(13)]_DistortionNoise("Distortion Noise", 2D) = "white" {}
		_DistortionNoiseTextureSelector("Distortion Noise Texture Selector", Vector) = (0,1,0,0)
		_DistortionIntensity1("Distortion Intensity", Float) = 0.1
		_DistortionNoiseUVScale("Distortion Noise UV Scale", Vector) = (1,1,0,0)
		_DistortionNoiseUVPanSpeed("Distortion Noise UV Pan Speed", Vector) = (0.05,-0.2,0,0)
		[Space(33)][Header(Cutout)][Space(13)]_CutoutTexture("Cutout Texture", 2D) = "white" {}
		_CutoutTextureSelector("Cutout Texture Selector", Vector) = (0,1,0,0)
		_CutoutErosion("Cutout Erosion", Float) = 0
		_CutoutErosionSmoothness("Cutout Erosion Smoothness", Float) = 1
		_CutoutMaskStrength("Cutout Mask Strength", Float) = 1
		_FresnelErosion("Fresnel Erosion", Float) = 0.1
		_FresnelErosionSmoothness("Fresnel Erosion Smoothness", Float) = 0.3
		_FresnelFade("Fresnel Fade", Float) = 0
		[Space(33)][Header(LUT)][Space(13)]_LUT("LUT", 2D) = "white" {}
		_LUTAmplitude("LUT Amplitude", Float) = 1
		_LUTOffset("LUT Offset", Float) = 0
		_LUTPanSpeed("LUT Pan Speed", Float) = 0
		[Space(33)][Header(AR)][Space(13)]_Cull("Cull", Float) = 2
		_Src("Src", Float) = 5
		_Dst("Dst", Float) = 10
		_ZWrite("ZWrite", Float) = 0
		_ZTest("ZTest", Float) = 2
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] _texcoord2( "", 2D ) = "white" {}
		[HideInInspector] _texcoord3( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Cull [_Cull]
        ZWrite [_ZWrite]
        ZTest [_ZTest]
        Blend [_Src] [_Dst]

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            

            // Compatibility macros for old Unity shaders
            #define unity_ObjectToWorld GetObjectToWorldMatrix()
            #define unity_WorldToObject GetWorldToObjectMatrix()
            #define UNITY_PI 3.14159265358979323846
            #define INTERNAL_DATA

            // Compatibility functions
            inline float3 UnityWorldSpaceViewDir(float3 worldPos)
            {
                return _WorldSpaceCameraPos.xyz - worldPos;
            }

            inline float4 ASE_ComputeGrabScreenPos(float4 pos)
            {
                #if UNITY_UV_STARTS_AT_TOP
                float scale = -1.0;
                #else
                float scale = 1.0;
                #endif
                float4 o = pos;
                o.y = pos.w * 0.5f;
                o.y = (pos.y - o.y) * _ProjectionParams.x * scale + o.y;
                return o;
            }

            inline float3 HSVToRGB(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            inline float3 RGBToHSV(float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            // Compatibility structs for old Unity surface shaders
            struct SurfaceOutput
            {
                half3 Albedo;
                half3 Normal;
                half3 Emission;
                half Alpha;
            };

            struct SurfaceOutputStandard
            {
                half3 Albedo;
                half3 Normal;
                half3 Emission;
                half Metallic;
                half Smoothness;
                half Occlusion;
                half Alpha;
            };

            struct SurfaceOutputStandardSpecular
            {
                half3 Albedo;
                half3 Specular;
                half3 Normal;
                half3 Emission;
                half Smoothness;
                half Occlusion;
                half Alpha;
            };

            // Custom Input struct
            struct Input
		{
			float4 vertexColor : COLOR;
			float4 uv2_texcoord2;
			float4 uv_texcoord;
			float4 uv3_texcoord3;
			float3 worldPos;
			float3 worldNormal;
			float4 screenPos;
		};

            // Uniforms inside Constant Buffer (SRP Batcher compatibility)
            CBUFFER_START(UnityPerMaterial)
                float _Dst;
                float _ZWrite;
                float _ZTest;
                float _Cull;
                float _Src;
                float _LUTPanSpeed;
                float _ErosionSmoothness;
                float2 _NoiseUVSpeed;
                float2 _NoiseUVScale;
                float2 _DistortionNoiseUVPanSpeed;
                float2 _DistortionNoiseUVScale;
                float4 _DistortionNoiseTextureSelector;
                float _DistortionIntensity1;
                float4 _NoiseTextureSelector;
                float2 _SecondaryNoiseUVSpeed;
                float2 _SecondaryNoiseUVScale;
                float4 _SecondaryNoiseTextureSelector;
                float _TexturesMultiplySoftLight;
                float _BlendWithSecondaryTexture;
                float _CutoutErosion;
                float _CutoutErosionSmoothness;
                float4 _CutoutTexture_ST;
                float4 _CutoutTextureSelector;
                float _CutoutMaskStrength;
                float _MaskMultiplySubtract;
                float _LUTAmplitude;
                float _LUTOffset;
                float _ParticleColorLUT;
                float _Emission;
                float _FresnelErosion;
                float _FresnelErosionSmoothness;
                float _FresnelFade;
                float _DepthFade;
            CBUFFER_END

            // Samplers (outside CBUFFER)
            uniform sampler2D _LUT;
            uniform sampler2D _NoiseTexture;
            uniform sampler2D _DistortionNoise;
            uniform sampler2D _SecondaryNoiseTexture;
            uniform sampler2D _CutoutTexture;

            // The original surf function
            void surf( Input i , inout SurfaceOutput o )
		{
			float2 temp_cast_0 = (_LUTPanSpeed).xx;
			float2 panner61 = ( 1.0 * _Time.y * _NoiseUVSpeed + ( i.uv_texcoord.xy * _NoiseUVScale ));
			float2 appendResult139 = (float2(i.uv_texcoord.z , i.uv_texcoord.w));
			float2 panner47 = ( 1.0 * _Time.y * _DistortionNoiseUVPanSpeed + ( i.uv_texcoord.xy * _DistortionNoiseUVScale ));
			float dotResult80 = dot( tex2D( _DistortionNoise, panner47 ) , _DistortionNoiseTextureSelector );
			float UVNoise83 = ( ( ( saturate( dotResult80 ) + -0.5 ) * 2.0 ) * i.uv3_texcoord3.x );
			float2 temp_cast_2 = (UVNoise83).xx;
			float2 lerpResult55 = lerp( float2( 0,0 ) , temp_cast_2 , _DistortionIntensity1);
			float dotResult98 = dot( tex2D( _NoiseTexture, ( ( panner61 + appendResult139 ) + lerpResult55 ) ) , _NoiseTextureSelector );
			float temp_output_99_0 = saturate( dotResult98 );
			float2 panner90 = ( 1.0 * _Time.y * _SecondaryNoiseUVSpeed + ( i.uv_texcoord.xy * _SecondaryNoiseUVScale ));
			float2 appendResult141 = (float2(i.uv2_texcoord2.x , i.uv2_texcoord2.y));
			float2 temp_cast_4 = (UVNoise83).xx;
			float2 lerpResult94 = lerp( float2( 0,0 ) , temp_cast_4 , _DistortionIntensity1);
			float dotResult101 = dot( tex2D( _SecondaryNoiseTexture, ( ( panner90 + appendResult141 ) + lerpResult94 ) ) , _SecondaryNoiseTextureSelector );
			float temp_output_102_0 = saturate( dotResult101 );
			float lerpResult104 = lerp( saturate( ( temp_output_99_0 * temp_output_102_0 ) ) , saturate( ( 1.0 - ( ( 1.0 - temp_output_99_0 ) * ( 1.0 - temp_output_102_0 ) ) ) ) , _TexturesMultiplySoftLight);
			float lerpResult113 = lerp( temp_output_99_0 , lerpResult104 , _BlendWithSecondaryTexture);
			float2 uv_CutoutTexture = i.uv_texcoord * _CutoutTexture_ST.xy + _CutoutTexture_ST.zw;
			float dotResult77 = dot( float4( tex2D( _CutoutTexture, uv_CutoutTexture ).rgb , 0.0 ) , _CutoutTextureSelector );
			float smoothstepResult36 = smoothstep( _CutoutErosion , ( _CutoutErosion + _CutoutErosionSmoothness ) , saturate( dotResult77 ));
			float temp_output_40_0 = saturate( ( saturate( smoothstepResult36 ) * _CutoutMaskStrength ) );
			float lerpResult115 = lerp( saturate( ( lerpResult113 * temp_output_40_0 ) ) , saturate( ( lerpResult113 - ( 1.0 - temp_output_40_0 ) ) ) , _MaskMultiplySubtract);
			float smoothstepResult29 = smoothstep( i.uv2_texcoord2.z , ( i.uv2_texcoord2.z + _ErosionSmoothness ) , saturate( lerpResult115 ));
			float temp_output_30_0 = saturate( smoothstepResult29 );
			float2 temp_cast_7 = (( ( temp_output_30_0 * ( _LUTAmplitude * i.uv3_texcoord3.y ) ) + _LUTOffset )).xx;
			float2 panner133 = ( 1.0 * _Time.y * temp_cast_0 + temp_cast_7);
			float4 lerpResult135 = lerp( i.vertexColor , ( i.vertexColor * float4( tex2D( _LUT, panner133 ).rgb , 0.0 ) ) , _ParticleColorLUT);
			o.Emission = ( lerpResult135 * ( i.uv2_texcoord2.w * _Emission ) ).rgb;
			float3 ase_worldPos = i.worldPos;
			float3 ase_viewVectorWS = ( _WorldSpaceCameraPos.xyz - ase_worldPos );
			float3 ase_viewDirWS = normalize( ase_viewVectorWS );
			float3 ase_worldNormal = i.worldNormal;
			float fresnelNdotV67 = dot( ase_worldNormal, ase_viewDirWS );
			float fresnelNode67 = ( 0.0 + 1.0 * pow( max( 1.0 - fresnelNdotV67 , 0.0001 ), 1.0 ) );
			float smoothstepResult71 = smoothstep( _FresnelErosion , ( _FresnelErosion + _FresnelErosionSmoothness ) , ( 1.0 - fresnelNode67 ));
			float lerpResult146 = lerp( temp_output_30_0 , saturate( ( temp_output_30_0 * saturate( smoothstepResult71 ) ) ) , _FresnelFade);
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_screenPosNorm = ase_screenPos / ase_screenPos.w;
			ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
			float screenDepth26 = LinearEyeDepth(SampleSceneDepth(ase_screenPosNorm.xy ), _ZBufferParams);
			float distanceDepth26 = saturate( ( screenDepth26 - LinearEyeDepth( ase_screenPosNorm.z , _ZBufferParams) ) / ( _DepthFade ) );
			o.Alpha = saturate( ( saturate( ( i.vertexColor.a * lerpResult146 ) ) * distanceDepth26 ) );
		}

            // Vertex Shader inputs
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 color        : COLOR;
                float4 texcoord     : TEXCOORD0;
                float4 texcoord1    : TEXCOORD1;
                float4 texcoord2    : TEXCOORD2;
                float4 texcoord3    : TEXCOORD3;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color        : COLOR;
                float4 texcoord     : TEXCOORD0;
                float4 texcoord1    : TEXCOORD1;
                float4 texcoord2    : TEXCOORD2;
                float4 texcoord3    : TEXCOORD3;
                float3 worldPos     : TEXCOORD4;
                float4 screenPos     : TEXCOORD5;
                float3 worldNormal    : TEXCOORD6;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.color = input.color;
                output.texcoord = input.texcoord;
                output.texcoord1 = input.texcoord1;
                output.texcoord2 = input.texcoord2;
                output.texcoord3 = input.texcoord3;
                output.worldPos = vertexInput.positionWS;
                output.screenPos = ComputeScreenPos(vertexInput.positionCS);
                output.worldNormal = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                Input surfInput;
                surfInput.vertexColor = input.color;
                surfInput.uv2_texcoord2 = input.texcoord1;
                surfInput.uv_texcoord = input.texcoord;
                surfInput.uv3_texcoord3 = input.texcoord2;
                surfInput.worldPos = input.worldPos;
                surfInput.worldNormal = input.worldNormal;
                surfInput.screenPos = input.screenPos;
                
                SurfaceOutput o;
                o.Albedo = 0.0;
                o.Normal = float3(0,0,1);
                o.Emission = 0.0;
                o.Alpha = 0.0;
                
                surf(surfInput, o);
                
                half3 finalEmission = o.Emission + o.Albedo;
                return half4(finalEmission, o.Alpha);
            }
            ENDHLSL
        }
    }
    Fallback "Diffuse"
    CustomEditor "ASEMaterialInspector"
}
