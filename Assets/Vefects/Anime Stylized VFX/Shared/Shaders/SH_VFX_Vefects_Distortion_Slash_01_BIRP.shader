// Translated to URP by Antigravity Converter
Shader "Vefects/SH_VFX_Vefects_Distortion_Slash_01_BIRP"
{
    Properties
	{
		[Space(33)][Header(Cookie Cutter)][Space(13)]_CookieCutter("Cookie Cutter", 2D) = "white" {}
		_CutoutMaskSelector("Cutout Mask Selector", Vector) = (0,1,0,0)
		[Space(33)][Header(Cutout)][Space(13)]_Cutout("Cutout", 2D) = "white" {}
		_CutoutErosion("Cutout Erosion", Float) = 0
		_CutoutErosionSmoothness("Cutout Erosion Smoothness", Float) = 0.05
		_CutoutRotation("Cutout Rotation", Float) = 0
		_CutoutOffset("Cutout Offset", Vector) = (0,0,0,0)
		[Space(33)][Header(Distortion Noise)][Space(13)]_DistortionNoise("Distortion Noise", 2D) = "white" {}
		_DistortionNoiseSelector("Distortion Noise Selector", Vector) = (0,1,0,0)
		_DistUVS("Dist UV S", Vector) = (1,1,0,0)
		_DistUVP("Dist UV P", Vector) = (0,0,0,0)
		_DistortionLerp("Distortion Lerp", Float) = 1
		[Space(33)][Header(Distortion Dist Noise)][Space(13)]_DistortionDist("Distortion Dist", 2D) = "white" {}
		_DistortionDistSelector("Distortion Dist Selector", Vector) = (0,1,0,0)
		_DistDistUVS("Dist Dist UV S", Vector) = (1,1,0,0)
		_DistDistUVP("Dist Dist UV P", Vector) = (0,0,0,0)
		_DistortionDistLerp("Distortion Dist Lerp", Float) = 0.1
		[Space(33)][Header(Depth Fade)][Space(13)][Toggle(_USEDEPTHFADE_ON)] _UseDepthFade("Use Depth Fade", Float) = 0
		_DepthFadeIntensity("Depth Fade Intensity", Float) = 0
		[Space(33)][Header(AR)][Space(13)]_Cull("Cull", Float) = 0
		_Src("Src", Float) = 5
		_Dst("Dst", Float) = 10
		_ZWrite("ZWrite", Float) = 0
		_ZTest("ZTest", Float) = 2
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

            #pragma shader_feature_local _USEDEPTHFADE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            // Compatibility macros for old Unity shaders
            #define unity_ObjectToWorld GetObjectToWorldMatrix()
            #define unity_WorldToObject GetWorldToObjectMatrix()
            #define UNITY_PI 3.14159265358979323846
            #define INTERNAL_DATA
            #define _LightColor0 _MainLightColor
            #define WorldNormalVector(data, normal) data.worldNormal

            // Compatibility functions
            inline float3 UnityWorldSpaceViewDir(float3 worldPos)
            {
                return _WorldSpaceCameraPos.xyz - worldPos;
            }

            inline float3 UnityWorldSpaceLightDir(float3 worldPos)
            {
                return _MainLightPosition.xyz;
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
			float4 screenPos;
			float4 uv_texcoord;
			float4 vertexColor : COLOR;
		};

            // Uniforms inside Constant Buffer (SRP Batcher compatibility)
            CBUFFER_START(UnityPerMaterial)
                float _Cull;
                float _Src;
                float _Dst;
                float _ZWrite;
                float _ZTest;
                float2 _DistUVP;
                float2 _DistUVS;
                float2 _DistDistUVP;
                float2 _DistDistUVS;
                float4 _DistortionDistSelector;
                float _DistortionDistLerp;
                float4 _DistortionNoiseSelector;
                float _CutoutErosion;
                float _CutoutErosionSmoothness;
                float2 _CutoutOffset;
                float _CutoutRotation;
                float4 _CookieCutter_ST;
                float4 _CutoutMaskSelector;
                float _DistortionLerp;
                float _DepthFadeIntensity;
            CBUFFER_END

            // Samplers (outside CBUFFER)
            uniform sampler2D _DistortionNoise;
            uniform sampler2D _DistortionDist;
            uniform sampler2D _Cutout;
            uniform sampler2D _CookieCutter;

            // The original surf function
            void surf( Input i , inout SurfaceOutput o )
		{
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_grabScreenPos = ASE_ComputeGrabScreenPos( ase_screenPos );
			float4 ase_grabScreenPosNorm = ase_grabScreenPos / ase_grabScreenPos.w;
			float2 appendResult18 = (float2(ase_grabScreenPosNorm.r , ase_grabScreenPosNorm.g));
			float2 panner35 = ( 1.0 * _Time.y * _DistUVP + ( i.uv_texcoord.xy * _DistUVS ));
			float2 panner50 = ( 1.0 * _Time.y * _DistDistUVP + ( i.uv_texcoord.xy * _DistDistUVS ));
			float dotResult55 = dot( tex2D( _DistortionDist, panner50 ) , _DistortionDistSelector );
			float2 temp_cast_1 = (saturate( dotResult55 )).xx;
			float2 lerpResult59 = lerp( float2( 0,0 ) , temp_cast_1 , _DistortionDistLerp);
			float dotResult37 = dot( tex2D( _DistortionNoise, ( panner35 + lerpResult59 ) ) , _DistortionNoiseSelector );
			float2 temp_output_81_0 = ( i.uv_texcoord.xy + _CutoutOffset );
			float cos68 = cos( radians( _CutoutRotation ) );
			float sin68 = sin( radians( _CutoutRotation ) );
			float2 rotator68 = mul( temp_output_81_0 - float2( 0.5,0.5 ) , float2x2( cos68 , -sin68 , sin68 , cos68 )) + float2( 0.5,0.5 );
			float smoothstepResult71 = smoothstep( _CutoutErosion , ( _CutoutErosion + _CutoutErosionSmoothness ) , tex2D( _Cutout, rotator68 ).g);
			float cutout72 = smoothstepResult71;
			float2 uv_CookieCutter = i.uv_texcoord * _CookieCutter_ST.xy + _CookieCutter_ST.zw;
			float dotResult41 = dot( tex2D( _CookieCutter, uv_CookieCutter ) , _CutoutMaskSelector );
			float2 temp_cast_4 = (saturate( ( saturate( dotResult37 ) * saturate( ( cutout72 * saturate( dotResult41 ) ) ) ) )).xx;
			float4 ase_screenPosNorm = ase_screenPos / ase_screenPos.w;
			ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
			float screenDepth89 = LinearEyeDepth(SampleSceneDepth(ase_screenPosNorm.xy ), _ZBufferParams);
			float distanceDepth89 = ( screenDepth89 - LinearEyeDepth( ase_screenPosNorm.z , _ZBufferParams) ) / ( _DepthFadeIntensity );
			#ifdef _USEDEPTHFADE_ON
				float staticSwitch92 = ( i.uv_texcoord.z * saturate( distanceDepth89 ) );
			#else
				float staticSwitch92 = i.uv_texcoord.z;
			#endif
			float2 lerpResult21 = lerp( float2( 0,0 ) , temp_cast_4 , ( _DistortionLerp * staticSwitch92 ));
			float4 screenColor12 = float4(SampleSceneColor(( appendResult18 + lerpResult21 )), 1.0);
			o.Emission = screenColor12.rgb;
			o.Alpha = i.vertexColor.a;
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
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                Input surfInput;
                surfInput.screenPos = input.screenPos;
                surfInput.uv_texcoord = input.texcoord;
                surfInput.vertexColor = input.color;
                
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
