// Translated to URP by Antigravity Converter
Shader "Vefects/SH_Vefects_BIRP_Leaf_01"
{
    Properties
	{
		_Cutoff( "Mask Clip Value", Float ) = 0.5
		_Metallic("Metallic", Float) = 0
		_Smoothness("Smoothness", Float) = 0
		_BackFaceMultiply("Back Face Multiply", Float) = 0.777
		_Emission("Emission", Float) = 1
		_ParticleColorLUT("Particle Color / LUT", Float) = 0
		_ErosionSmoothness("Erosion Smoothness", Float) = 1
		[Space(33)][Header(Main Texture)][Space(13)]_MainTexture("Main Texture", 2D) = "white" {}
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
		[Space(33)][Header(AR)][Space(13)]_Cull("Cull", Float) = 2
		_Src("Src", Float) = 5
		_Dst("Dst", Float) = 10
		_ZWrite("ZWrite", Float) = 0
		_ZTest("ZTest", Float) = 2
		_AlphaClip("Alpha Clip", Float) = 0.5
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
			half ASEIsFrontFacing : VFACE;
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
                float _LUTAmplitude;
                float _LUTOffset;
                float _ParticleColorLUT;
                float _BackFaceMultiply;
                float _Emission;
                float _Metallic;
                float _Smoothness;
                float _ErosionSmoothness;
                float _AlphaClip;
                float _Cutoff = 0.5;
            CBUFFER_END

            // Samplers (outside CBUFFER)
            uniform sampler2D _LUT;
            uniform sampler2D _MainTexture;
            uniform sampler2D _DistortionNoise;

            // The original surf function
            void surf( Input i , inout SurfaceOutputStandardSpecular o )
		{
			float2 temp_cast_0 = (_LUTPanSpeed).xx;
			float2 panner38 = ( 1.0 * _Time.y * _DistortionNoiseUVPanSpeed + ( i.uv_texcoord.xy * _DistortionNoiseUVScale ));
			float dotResult41 = dot( tex2D( _DistortionNoise, panner38 ) , _DistortionNoiseTextureSelector );
			float UVDist47 = ( ( saturate( dotResult41 ) + -0.5 ) * 2.0 );
			float4 tex2DNode16 = tex2D( _MainTexture, ( i.uv_texcoord.xy + ( UVDist47 * _DistortionIntensity ) ) );
			float smoothstepResult59 = smoothstep( i.uv2_texcoord2.x , ( i.uv2_texcoord2.x + _LUTErosionSmoothness ) , tex2DNode16.g);
			float2 temp_cast_2 = (( ( saturate( smoothstepResult59 ) * _LUTAmplitude ) + _LUTOffset )).xx;
			float2 panner65 = ( 1.0 * _Time.y * temp_cast_0 + temp_cast_2);
			float4 lerpResult69 = lerp( i.vertexColor , ( i.vertexColor * float4( tex2D( _LUT, panner65 ).rgb , 0.0 ) ) , _ParticleColorLUT);
			float4 lerpResult72 = lerp( lerpResult69 , ( lerpResult69 * _BackFaceMultiply ) , ( i.ASEIsFrontFacing > 0 ? +1 : -1 ));
			o.Albedo = lerpResult72.rgb;
			o.Emission = ( lerpResult72 * ( _Emission * i.uv_texcoord.z ) ).rgb;
			float3 temp_cast_6 = (_Metallic).xxx;
			o.Specular = temp_cast_6;
			o.Smoothness = _Smoothness;
			float smoothstepResult29 = smoothstep( i.uv2_texcoord2.x , ( i.uv2_texcoord2.x + _ErosionSmoothness ) , tex2DNode16.r);
			o.Alpha = saturate( ( saturate( smoothstepResult29 ) * i.vertexColor.a ) );
			clip( _AlphaClip - _Cutoff );
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
                
                
                return output;
            }

            half4 frag(Varyings input, half facing : VFACE) : SV_Target
            {
                Input surfInput;
                surfInput.vertexColor = input.color;
                surfInput.uv2_texcoord2 = input.texcoord1;
                surfInput.uv_texcoord = input.texcoord;
                surfInput.ASEIsFrontFacing = facing;
                
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
