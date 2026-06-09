// Translated to URP by Antigravity Converter
Shader "Vefects/SH_Vefects_BIRP_Fake_Decal_Unlit_Area_Whirlpool_Simple_01"
{
    Properties
	{
		_Emissive("Emissive", Float) = 1
		[Space(33)][Header(Decal)][Space(13)]_DecalTexture("Decal Texture", 2D) = "white" {}
		_DecalTextureSelector("Decal Texture Selector", Vector) = (0,1,0,0)
		_DecalUVScale("Decal UV Scale", Vector) = (1,1,0,0)
		_DecalUVPanSpeed("Decal UV Pan Speed", Vector) = (0,0,0,0)
		_DecalRotation("Decal Rotation", Float) = 0
		_DecalScaleFromCenter("Decal Scale From Center", Float) = 1
		_DecalScaleFromCenterNonUniform("Decal Scale From Center Non Uniform", Vector) = (1,1,0,0)
		[Space(33)][Header(Decal Op)][Space(13)]_DecalOpTexture("Decal Op Texture", 2D) = "white" {}
		_DecalOpTextureSelector("Decal Op Texture Selector", Vector) = (0,1,0,0)
		[Space(33)][Header(Fake Decal)][Space(13)]_FakeDecalDepthFade("Fake Decal Depth Fade", Float) = 1
		_FakeDecalDepthFadeErosion("Fake Decal Depth Fade Erosion", Float) = 0
		_FakeDecalDepthFadeErosionSmoothness("Fake Decal Depth Fade Erosion Smoothness", Float) = 0.1
		_ErosionSmoothness("Erosion Smoothness", Float) = 0.05
		_OpacityBoost("Opacity Boost", Float) = 1
		[Space(33)][Header(Radial)][Space(13)]_RadialUVDistortNoise("Radial UV Distort Noise", 2D) = "white" {}
		_RadialUVDistortScale("Radial UV Distort Scale", Vector) = (1,1,0,0)
		_RadialUVDistortSpeed("Radial UV Distort Speed", Vector) = (0.1,0.01,0,0)
		_RadialUVDistortIntensity("Radial UV Distort Intensity", Float) = 0.1
		[Space(33)][Header(LUT)][Space(13)]_LUT("LUT", 2D) = "white" {}
		_LUTAmplitude("LUT Amplitude", Float) = 1
		_LUTOffset("LUT Offset", Float) = 0
		_LUTPanSpeed("LUT Pan Speed", Float) = 0
		_LUTErosionSmoothness("LUT Erosion Smoothness", Float) = 1
		_TwistIntensity("Twist Intensity", Float) = -0.42
		_PolarCoordinatesRadialScale("Polar Coordinates Radial Scale", Float) = 0.5
		_PolarCoordinatesLengthScale("Polar Coordinates Length Scale", Float) = 1
		_PolarCoordinatesPanSpeed("Polar Coordinates Pan Speed", Vector) = (0.3,0,0,0)
		[Space(33)][Header(AR)][Space(13)]_Cull("Cull", Float) = 2
		_Src("Src", Float) = 5
		_Dst("Dst", Float) = 10
		_ZWrite("ZWrite", Float) = 0
		_ZTest("ZTest", Float) = 2
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] _texcoord2( "", 2D ) = "white" {}
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
			float4 uv_texcoord;
			float4 uv2_texcoord2;
			float4 vertexColor : COLOR;
			float4 screenPos;
		};

            // Uniforms inside Constant Buffer (SRP Batcher compatibility)
            CBUFFER_START(UnityPerMaterial)
                float _Src;
                float _Dst;
                float _ZWrite;
                float _ZTest;
                float _Cull;
                float _LUTPanSpeed;
                float _LUTErosionSmoothness;
                float2 _DecalUVPanSpeed;
                float _PolarCoordinatesRadialScale;
                float2 _DecalScaleFromCenterNonUniform;
                float _DecalScaleFromCenter;
                float _DecalRotation;
                float _PolarCoordinatesLengthScale;
                float2 _PolarCoordinatesPanSpeed;
                float _TwistIntensity;
                float2 _RadialUVDistortScale;
                float2 _RadialUVDistortSpeed;
                float _RadialUVDistortIntensity;
                float2 _DecalUVScale;
                float4 _DecalTextureSelector;
                float _LUTAmplitude;
                float _LUTOffset;
                float _Emissive;
                float _ErosionSmoothness;
                float4 _DecalOpTexture_ST;
                float4 _DecalOpTextureSelector;
                float _OpacityBoost;
                float _FakeDecalDepthFadeErosion;
                float _FakeDecalDepthFadeErosionSmoothness;
                float _FakeDecalDepthFade;
            CBUFFER_END

            // Samplers (outside CBUFFER)
            uniform sampler2D _LUT;
            uniform sampler2D _DecalTexture;
            uniform sampler2D _RadialUVDistortNoise;
            uniform sampler2D _DecalOpTexture;

            // The original surf function
            void surf( Input i , inout SurfaceOutput o )
		{
			float2 temp_cast_0 = (_LUTPanSpeed).xx;
			float eros79 = i.uv_texcoord.w;
			float2 _Vector1 = float2(0.5,0.5);
			float randomRotate57 = i.uv2_texcoord2.x;
			float cos52 = cos( ( ( ( _DecalRotation + randomRotate57 ) * ( 2.0 * UNITY_PI ) ) / 360.0 ) );
			float sin52 = sin( ( ( ( _DecalRotation + randomRotate57 ) * ( 2.0 * UNITY_PI ) ) / 360.0 ) );
			float2 rotator52 = mul( ( ( ( i.uv_texcoord.xy - _Vector1 ) / ( _DecalScaleFromCenterNonUniform * _DecalScaleFromCenter ) ) + _Vector1 ) - float2( 0.5,0.5 ) , float2x2( cos52 , -sin52 , sin52 , cos52 )) + float2( 0.5,0.5 );
			float2 decalUV87 = rotator52;
			float2 temp_output_34_0_g9 = ( decalUV87 - float2( 0.5,0.5 ) );
			float2 break39_g9 = temp_output_34_0_g9;
			float2 appendResult50_g9 = (float2(( _PolarCoordinatesRadialScale * ( length( temp_output_34_0_g9 ) * 2.0 ) ) , ( ( atan2( break39_g9.x , break39_g9.y ) * ( 1.0 / 6.28318548202515 ) ) * _PolarCoordinatesLengthScale )));
			float2 panner326 = ( 1.0 * _Time.y * _PolarCoordinatesPanSpeed + float2( 0,0 ));
			float2 break53_g9 = appendResult50_g9;
			float2 twistedUVs342 = ( ( appendResult50_g9 + panner326 ) + ( break53_g9.x * _TwistIntensity ) );
			float2 break234 = twistedUVs342;
			float2 appendResult183 = (float2(( (_RadialUVDistortScale).x * break234.x ) , ( break234.y * (_RadialUVDistortScale).y )));
			float2 panner165 = ( ( (_RadialUVDistortSpeed).x * _Time.y ) * float2( 1,0 ) + twistedUVs342);
			float2 panner166 = ( ( _Time.y * (_RadialUVDistortSpeed).y ) * float2( 0,1 ) + twistedUVs342);
			float2 appendResult182 = (float2((panner165).x , (panner166).y));
			float2 UV_Dist345 = ( (tex2D( _RadialUVDistortNoise, ( appendResult183 + appendResult182 ) )).rg * _RadialUVDistortIntensity );
			float2 panner247 = ( 1.0 * _Time.y * _DecalUVPanSpeed + ( ( twistedUVs342 + UV_Dist345 ) * _DecalUVScale ));
			float dotResult104 = dot( tex2D( _DecalTexture, panner247 ) , _DecalTextureSelector );
			float smoothstepResult93 = smoothstep( eros79 , ( eros79 + _LUTErosionSmoothness ) , saturate( dotResult104 ));
			float LUTOffset109 = i.uv2_texcoord2.y;
			float2 temp_cast_2 = (( ( saturate( smoothstepResult93 ) * _LUTAmplitude ) + ( _LUTOffset + LUTOffset109 ) )).xx;
			float2 panner98 = ( 1.0 * _Time.y * temp_cast_0 + temp_cast_2);
			float em78 = i.uv_texcoord.z;
			o.Emission = ( ( tex2D( _LUT, panner98 ).rgb * (i.vertexColor).rgb ) * ( _Emissive * em78 ) );
			float2 uv_DecalOpTexture = i.uv_texcoord * _DecalOpTexture_ST.xy + _DecalOpTexture_ST.zw;
			float dotResult107 = dot( tex2D( _DecalOpTexture, uv_DecalOpTexture ) , _DecalOpTextureSelector );
			float smoothstepResult82 = smoothstep( eros79 , ( eros79 + _ErosionSmoothness ) , saturate( dotResult107 ));
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_screenPosNorm = ase_screenPos / ase_screenPos.w;
			ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
			float screenDepth369 = LinearEyeDepth(SampleSceneDepth(ase_screenPosNorm.xy ), _ZBufferParams);
			float distanceDepth369 = saturate( ( screenDepth369 - LinearEyeDepth( ase_screenPosNorm.z , _ZBufferParams) ) / ( _FakeDecalDepthFade ) );
			float smoothstepResult372 = smoothstep( _FakeDecalDepthFadeErosion , ( _FakeDecalDepthFadeErosion + _FakeDecalDepthFadeErosionSmoothness ) , distanceDepth369);
			o.Alpha = saturate( ( saturate( ( saturate( ( saturate( saturate( smoothstepResult82 ) ) * _OpacityBoost ) ) * i.vertexColor.a ) ) * ( 1.0 - saturate( smoothstepResult372 ) ) ) );
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
                surfInput.uv_texcoord = input.texcoord;
                surfInput.uv2_texcoord2 = input.texcoord1;
                surfInput.vertexColor = input.color;
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
