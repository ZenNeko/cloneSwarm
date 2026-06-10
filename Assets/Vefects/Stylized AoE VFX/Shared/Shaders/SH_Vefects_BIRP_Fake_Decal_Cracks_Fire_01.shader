// Translated to URP by Antigravity Converter
Shader "Vefects/SH_Vefects_BIRP_Fake_Decal_Cracks_Fire_01"
{
    Properties
	{
		_Specular("Specular", Float) = 0
		_Smoothness("Smoothness", Float) = 0
		_NoiseEmissive("Noise Emissive", 2D) = "white" {}
		_Emissive("Emissive", Float) = 1
		_EmissiveErosionSmoothness("Emissive Erosion Smoothness", Float) = 0.5
		_EmissivePanSpeed("Emissive Pan Speed", Float) = 0.3
		_NoiseSpots("Noise Spots", 2D) = "white" {}
		_EmissiveSpots("Emissive Spots", Float) = 1
		_EmissiveSpotsColor("Emissive Spots Color", Color) = (1,0.6235294,0,1)
		_SpotsPanSpeed("Spots Pan Speed", Float) = 0.3
		[Space(33)][Header(Decal)][Space(13)]_DecalTexture("Decal Texture", 2D) = "white" {}
		_DecalRotation("Decal Rotation", Float) = 0
		_DecalScaleFromCenter("Decal Scale From Center", Float) = 1
		_DecalScaleFromCenterNonUniform("Decal Scale From Center Non Uniform", Vector) = (1,1,0,0)
		_FakeDecalDepthFade("Fake Decal Depth Fade", Float) = 1
		_FakeDecalDepthFadeErosion("Fake Decal Depth Fade Erosion", Float) = 0
		_FakeDecalDepthFadeErosionSmoothness("Fake Decal Depth Fade Erosion Smoothness", Float) = 0.1
		_ErosionSmoothness("Erosion Smoothness", Float) = 0.25
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
                float2 _DecalScaleFromCenterNonUniform;
                float _DecalScaleFromCenter;
                float _DecalRotation;
                float _LUTAmplitude;
                float _LUTOffset;
                float _EmissivePanSpeed;
                float _EmissiveErosionSmoothness;
                float _Emissive;
                float _EmissiveSpots;
                float4 _EmissiveSpotsColor;
                float _SpotsPanSpeed;
                float _Specular;
                float _Smoothness;
                float _ErosionSmoothness;
                float _FakeDecalDepthFadeErosion;
                float _FakeDecalDepthFadeErosionSmoothness;
                float _FakeDecalDepthFade;
            CBUFFER_END

            // Samplers (outside CBUFFER)
            uniform sampler2D _LUT;
            uniform sampler2D _DecalTexture;
            uniform sampler2D _NoiseEmissive;
            uniform sampler2D _NoiseSpots;

            // The original surf function
            void surf( Input i , inout SurfaceOutputStandardSpecular o )
		{
			float2 temp_cast_0 = (_LUTPanSpeed).xx;
			float2 _Vector1 = float2(0.5,0.5);
			float randomRotate57 = i.uv2_texcoord2.x;
			float cos52 = cos( ( ( ( _DecalRotation + randomRotate57 ) * ( 2.0 * UNITY_PI ) ) / 360.0 ) );
			float sin52 = sin( ( ( ( _DecalRotation + randomRotate57 ) * ( 2.0 * UNITY_PI ) ) / 360.0 ) );
			float2 rotator52 = mul( ( ( ( i.uv_texcoord.xy - _Vector1 ) / ( _DecalScaleFromCenterNonUniform * _DecalScaleFromCenter ) ) + _Vector1 ) - float2( 0.5,0.5 ) , float2x2( cos52 , -sin52 , sin52 , cos52 )) + float2( 0.5,0.5 );
			float2 decalUV87 = rotator52;
			float4 tex2DNode24 = tex2D( _DecalTexture, decalUV87 );
			float crackTexture275 = tex2DNode24.g;
			float LUTMult109 = i.uv2_texcoord2.y;
			float2 temp_cast_1 = (( ( crackTexture275 * ( _LUTAmplitude + LUTMult109 ) ) + _LUTOffset )).xx;
			float2 panner98 = ( 1.0 * _Time.y * temp_cast_0 + temp_cast_1);
			o.Albedo = tex2D( _LUT, panner98 ).rgb;
			float mulTime343 = _Time.y * _EmissivePanSpeed;
			float2 panner324 = ( mulTime343 * float2( 0,-0.25 ) + ( decalUV87 * float2( 0.3,0.3 ) ));
			float2 panner325 = ( mulTime343 * float2( 0,0.05 ) + decalUV87);
			float erosEmi335 = i.uv2_texcoord2.w;
			float emissiveTexture274 = tex2DNode24.r;
			float smoothstepResult93 = smoothstep( erosEmi335 , ( erosEmi335 + _EmissiveErosionSmoothness ) , emissiveTexture274);
			float em78 = i.uv_texcoord.z;
			float spotsEmissive319 = i.uv2_texcoord2.z;
			float burnSpots276 = tex2DNode24.b;
			float mulTime341 = _Time.y * _SpotsPanSpeed;
			float2 temp_output_312_0 = ( decalUV87 * float2( 3,3 ) );
			float2 panner309 = ( mulTime341 * float2( 0,-0.2 ) + temp_output_312_0);
			float saferPower301 = abs( tex2D( _NoiseSpots, panner309 ).g );
			float2 panner310 = ( mulTime341 * float2( 0.05,0.1 ) + temp_output_312_0);
			float saferPower302 = abs( tex2D( _NoiseSpots, panner310 ).g );
			float saferPower297 = abs( ( ( burnSpots276 * saturate( ( ( pow( saferPower301 , 2.0 ) * pow( saferPower302 , 2.0 ) ) * 2.0 ) ) ) * 2.0 ) );
			o.Emission = max( ( ( saturate( ( saturate( ( tex2D( _NoiseEmissive, panner324 ).g * tex2D( _NoiseEmissive, panner325 ).g ) ) * saturate( smoothstepResult93 ) ) ) * (i.vertexColor).rgb ) * ( _Emissive * em78 ) ) , ( ( ( spotsEmissive319 * _EmissiveSpots ) * _EmissiveSpotsColor.rgb ) * saturate( ( pow( saferPower297 , 1.5 ) * 10.0 ) ) ) );
			float3 temp_cast_2 = (_Specular).xxx;
			o.Specular = temp_cast_2;
			o.Smoothness = _Smoothness;
			float eros79 = i.uv_texcoord.w;
			float smoothstepResult82 = smoothstep( eros79 , ( eros79 + _ErosionSmoothness ) , crackTexture275);
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_screenPosNorm = ase_screenPos / ase_screenPos.w;
			ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
			float screenDepth357 = LinearEyeDepth(SampleSceneDepth(ase_screenPosNorm.xy ), _ZBufferParams);
			float distanceDepth357 = saturate( ( screenDepth357 - LinearEyeDepth( ase_screenPosNorm.z , _ZBufferParams) ) / ( _FakeDecalDepthFade ) );
			float smoothstepResult360 = smoothstep( _FakeDecalDepthFadeErosion , ( _FakeDecalDepthFadeErosion + _FakeDecalDepthFadeErosionSmoothness ) , distanceDepth357);
			o.Alpha = saturate( ( saturate( ( saturate( smoothstepResult82 ) * i.vertexColor.a ) ) * ( 1.0 - saturate( smoothstepResult360 ) ) ) );
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
                
                SurfaceOutputStandardSpecular o;
                o.Albedo = 0.0;
                o.Specular = 0.0;
                o.Normal = float3(0,0,1);
                o.Emission = 0.0;
                o.Smoothness = 0.0;
                o.Occlusion = 1.0;
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
