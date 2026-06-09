// Translated to URP by Antigravity Converter
Shader "Vefects/SH_Vefects_BIRP_Splashes_01"
{
    Properties
	{
		_Emission("Emission", Float) = 1
		_ErosionSmoothness("Erosion Smoothness", Float) = 1
		_ParticleColorLUT("Particle Color / LUT", Float) = 0
		_IsAdd("Is Add", Float) = 0
		[Space(33)][Header(Main Texture)][Space(13)]_MainTexture("Main Texture", 2D) = "white" {}
		_MainTextureChannel("Main Texture Channel", Vector) = (0,1,0,0)
		[Space(33)][Header(Distortion)][Space(13)]_DistortionNoise("Distortion Noise", 2D) = "white" {}
		_DistortionNoiseTextureSelector("Distortion Noise Texture Selector", Vector) = (0,1,0,0)
		_DistortionNoiseUVScale("Distortion Noise UV Scale", Vector) = (1,1,0,0)
		_DistortionNoiseUVPanSpeed("Distortion Noise UV Pan Speed", Vector) = (0.05,-0.2,0,0)
		_DistortionIntensity("Distortion Intensity", Float) = 0.03
		[Space(33)][Header(LUT)][Space(13)]_LUT("LUT", 2D) = "white" {}
		_LUTAmplitude("LUT Amplitude", Float) = 1
		_LUTOffset("LUT Offset", Float) = 0
		_LUTPanSpeed("LUT Pan Speed", Float) = 0
		_LUTErosionSmoothness("LUT Erosion Smoothness", Float) = 1
		[Space(33)][Header(Fresnel Opacity Edges)][Space(13)]_FresnelScale("Fresnel Scale", Float) = 1
		_FresnelPower("Fresnel Power", Float) = 1
		_FresnelBias("Fresnel Bias", Float) = 0
		_FrenselErosion("Frensel Erosion", Float) = 0
		_FrenselErosionSmoothstep("Frensel Erosion Smoothstep", Float) = 1
		[Space(33)][Header(AR)][Space(13)]_Cull("Cull", Float) = 2
		_Src("Src", Float) = 5
		_Dst("Dst", Float) = 10
		_ZWrite("ZWrite", Float) = 0
		_ZTest("ZTest", Float) = 2
		[HideInInspector] _texcoord2( "", 2D ) = "white" {}
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
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
			float4 screenPos;
			float3 worldPos;
			half ASEIsFrontFacing : VFACE;
			float3 worldNormal;
			INTERNAL_DATA
		};

            // Uniforms inside Constant Buffer (SRP Batcher compatibility)
            CBUFFER_START(UnityPerMaterial)
                float _Dst;
                float _ZWrite;
                float _ZTest;
                float _Cull;
                float _Src;
                float _LUTPanSpeed;
                float _LUTErosionSmoothness;
                float2 _DistortionNoiseUVPanSpeed;
                float2 _DistortionNoiseUVScale;
                float4 _DistortionNoiseTextureSelector;
                float _DistortionIntensity;
                float4 _MainTextureChannel;
                float _LUTAmplitude;
                float _LUTOffset;
                float _ParticleColorLUT;
                float _ErosionSmoothness;
                float _IsAdd;
                float _Emission;
                float _FrenselErosion;
                float _FrenselErosionSmoothstep;
                float _FresnelBias;
                float _FresnelScale;
                float _FresnelPower;
            CBUFFER_END

            // Samplers (outside CBUFFER)
            uniform sampler2D _LUT;
            uniform sampler2D _MainTexture;
            uniform sampler2D _DistortionNoise;

            // The original surf function
            void surf( Input i , inout SurfaceOutput o )
		{
			float2 temp_cast_0 = (_LUTPanSpeed).xx;
			float2 panner50 = ( 1.0 * _Time.y * _DistortionNoiseUVPanSpeed + ( i.uv_texcoord.xy * _DistortionNoiseUVScale ));
			float dotResult53 = dot( tex2D( _DistortionNoise, panner50 ) , _DistortionNoiseTextureSelector );
			float UVDist57 = ( ( saturate( dotResult53 ) + -0.5 ) * 2.0 );
			float dotResult64 = dot( tex2D( _MainTexture, ( i.uv_texcoord.xy + ( UVDist57 * _DistortionIntensity ) ) ) , _MainTextureChannel );
			float temp_output_65_0 = saturate( dotResult64 );
			float smoothstepResult69 = smoothstep( i.uv2_texcoord2.x , ( i.uv2_texcoord2.x + _LUTErosionSmoothness ) , temp_output_65_0);
			float2 temp_cast_3 = (( ( saturate( smoothstepResult69 ) * _LUTAmplitude ) + _LUTOffset )).xx;
			float2 panner42 = ( 1.0 * _Time.y * temp_cast_0 + temp_cast_3);
			float4 lerpResult35 = lerp( i.vertexColor , ( i.vertexColor * float4( tex2D( _LUT, panner42 ).rgb , 0.0 ) ) , _ParticleColorLUT);
			float smoothstepResult29 = smoothstep( i.uv2_texcoord2.x , ( i.uv2_texcoord2.x + _ErosionSmoothness ) , temp_output_65_0);
			float temp_output_30_0 = saturate( smoothstepResult29 );
			float4 lerpResult44 = lerp( lerpResult35 , ( lerpResult35 * temp_output_30_0 ) , _IsAdd);
			o.Emission = ( lerpResult44 * ( _Emission * i.uv_texcoord.z ) ).rgb;
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_screenPosNorm = ase_screenPos / ase_screenPos.w;
			ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
			float screenDepth26 = LinearEyeDepth(SampleSceneDepth(ase_screenPosNorm.xy ), _ZBufferParams);
			float distanceDepth26 = saturate( ( screenDepth26 - LinearEyeDepth( ase_screenPosNorm.z , _ZBufferParams) ) / ( i.uv_texcoord.w ) );
			float3 ase_worldPos = i.worldPos;
			float3 ase_viewVectorWS = ( _WorldSpaceCameraPos.xyz - ase_worldPos );
			float3 ase_viewDirWS = normalize( ase_viewVectorWS );
			float3 ase_worldNormal = i.worldNormal;
			float3 ase_vertexNormal = mul( unity_WorldToObject, float4( ase_worldNormal, 0 ) );
			ase_vertexNormal = normalize( ase_vertexNormal );
			float fresnelNdotV94 = dot( normalize( ( ( i.ASEIsFrontFacing > 0 ? +1 : -1 ) * ase_vertexNormal ) ), ase_viewDirWS );
			float fresnelNode94 = ( _FresnelBias + _FresnelScale * pow( max( 1.0 - fresnelNdotV94 , 0.0001 ), _FresnelPower ) );
			float smoothstepResult99 = smoothstep( _FrenselErosion , ( _FrenselErosion + _FrenselErosionSmoothstep ) , saturate( fresnelNode94 ));
			o.Alpha = saturate( ( saturate( ( saturate( ( temp_output_30_0 * i.vertexColor.a ) ) * distanceDepth26 ) ) * saturate( ( 1.0 - saturate( smoothstepResult99 ) ) ) ) );
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

            half4 frag(Varyings input, half facing : VFACE) : SV_Target
            {
                Input surfInput;
                surfInput.vertexColor = input.color;
                surfInput.uv2_texcoord2 = input.texcoord1;
                surfInput.uv_texcoord = input.texcoord;
                surfInput.screenPos = input.screenPos;
                surfInput.worldPos = input.worldPos;
                surfInput.ASEIsFrontFacing = facing;
                surfInput.worldNormal = input.worldNormal;
                
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
