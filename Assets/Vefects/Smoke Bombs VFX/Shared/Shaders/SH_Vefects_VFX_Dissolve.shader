// Translated to URP by Antigravity Converter
Shader "SH_Vefects_VFX_Dissolve"
{
    Properties
	{
		_Noise_01_Texture("Noise_01_Texture", 2D) = "white" {}
		_Noise_02_Texture("Noise_02_Texture", 2D) = "white" {}
		_Mask_Texture("Mask_Texture", 2D) = "white" {}
		_MaskMove_Texture("MaskMove_Texture", 2D) = "white" {}
		_Color("Color", Color) = (1,1,1,0)
		_NoiseDistortion_Texture("NoiseDistortion_Texture", 2D) = "white" {}
		_Noise_01_Scale("Noise_01_Scale", Vector) = (0.8,0.8,0,0)
		_Noise_02_Scale("Noise_02_Scale", Vector) = (1,1,0,0)
		_CamOffSet("CamOffSet", Float) = 0
		_NoiseDistortion_Scale("NoiseDistortion_Scale", Vector) = (1,1,0,0)
		_Noise_01_Speed("Noise_01_Speed", Vector) = (0.5,0.5,0,0)
		_Noise_02_Speed("Noise_02_Speed", Vector) = (-0.2,0.4,0,0)
		_MaskMove_Scale("MaskMove_Scale", Vector) = (1,1,0,0)
		_Mask_Scale("Mask_Scale", Vector) = (1,1,0,0)
		_Mask_Offset("Mask_Offset", Vector) = (0,0,0,0)
		_NoiseDistortion_Speed("NoiseDistortion_Speed", Vector) = (0.2,0.25,0,0)
		_Mask_Multiply("Mask_Multiply", Float) = 1
		_MaskMove_Multiply("MaskMove_Multiply", Float) = 1
		_Noises_Multiply("Noises_Multiply", Float) = 1
		_Mask_Power("Mask_Power", Float) = 1
		_Noises_Power("Noises_Power", Float) = 1
		_MaskMove_Power("MaskMove_Power", Float) = 1
		_Distortion("Distortion", Float) = 1
		_DistortionMask("DistortionMask", Float) = 0
		_Opacity_Boost("Opacity_Boost", Float) = 5
		_Emissive("Emissive", Float) = 1
		_Global_Speed("Global_Speed", Float) = 1
		_Mask_Speed("Mask_Speed", Float) = 0
		_Dissolve("Dissolve", Float) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] _texcoord2( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Cull Back
        ZWrite On
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

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

            // Depth/Screen Texture Support for URP
            #ifdef SAMPLE_DEPTH_TEXTURE
            #undef SAMPLE_DEPTH_TEXTURE
            #endif
            #define SAMPLE_DEPTH_TEXTURE(tex, uv) tex2D(tex, uv).r

            #ifndef LinearEyeDepth
            #define LinearEyeDepth(depth) LinearEyeDepth(depth, _ZBufferParams)
            #endif

            #ifndef UNITY_DECLARE_DEPTH_TEXTURE
            #define UNITY_DECLARE_DEPTH_TEXTURE(tex) sampler2D tex
            #endif

            #ifndef UNITY_SAMPLE_SCREENSPACE_TEXTURE
            #define UNITY_SAMPLE_SCREENSPACE_TEXTURE(tex, uv) tex2D(tex, uv)
            #endif
            #define _GrabTexture _CameraOpaqueTexture
            uniform sampler2D _CameraOpaqueTexture;

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
			float3 worldPos;
			float4 vertexColor : COLOR;
			float2 uv_texcoord;
			float4 uv2_texcoord2;
		};

            // Uniforms inside Constant Buffer (SRP Batcher compatibility)
            CBUFFER_START(UnityPerMaterial)
                float _CamOffSet;
                float4 _Color;
                float _Global_Speed;
                float2 _NoiseDistortion_Speed;
                float2 _NoiseDistortion_Scale;
                float _Distortion;
                float _DistortionMask;
                float _Mask_Speed;
                float2 _Mask_Scale;
                float2 _Mask_Offset;
                float _Mask_Power;
                float _Mask_Multiply;
                float2 _MaskMove_Scale;
                float _MaskMove_Power;
                float _MaskMove_Multiply;
                float2 _Noise_01_Speed;
                float2 _Noise_01_Scale;
                float2 _Noise_02_Speed;
                float2 _Noise_02_Scale;
                float _Noises_Power;
                float _Noises_Multiply;
                float _Emissive;
                float _Opacity_Boost;
                float _Dissolve;
            CBUFFER_END

            // Samplers (outside CBUFFER)
            uniform sampler2D _Mask_Texture;
            uniform sampler2D _NoiseDistortion_Texture;
            uniform sampler2D _MaskMove_Texture;
            uniform sampler2D _Noise_01_Texture;
            uniform sampler2D _Noise_02_Texture;

            // The original surf function
            void surf( Input i , inout SurfaceOutput o )
		{
			float global_speed200 = ( _Global_Speed * _Time.y );
			float2 uv_TexCoord30 = i.uv_texcoord * _NoiseDistortion_Scale;
			float2 panner79 = ( global_speed200 * _NoiseDistortion_Speed + uv_TexCoord30);
			float Distortion64 = ( ( tex2D( _NoiseDistortion_Texture, panner79 ).r * 0.1 ) * _Distortion );
			float2 appendResult264 = (float2(_Mask_Speed , 0.0));
			float2 uv_TexCoord216 = i.uv_texcoord * _Mask_Scale + _Mask_Offset;
			float2 panner266 = ( global_speed200 * appendResult264 + uv_TexCoord216);
			float2 uv_TexCoord212 = i.uv_texcoord * _MaskMove_Scale;
			float2 appendResult226 = (float2(i.uv2_texcoord2.z , i.uv2_texcoord2.w));
			float clampResult224 = clamp( ( saturate( ( ( tex2D( _Mask_Texture, ( ( Distortion64 * _DistortionMask ) + panner266 ) ).r * _Mask_Power ) * _Mask_Multiply ) ) * saturate( ( ( tex2D( _MaskMove_Texture, ( uv_TexCoord212 + appendResult226 ) ).r * _MaskMove_Power ) * _MaskMove_Multiply ) ) ) , 0.0 , 1.0 );
			float2 uv_TexCoord26 = i.uv_texcoord * _Noise_01_Scale;
			float2 panner78 = ( global_speed200 * _Noise_01_Speed + uv_TexCoord26);
			float2 uv_TexCoord58 = i.uv_texcoord * _Noise_02_Scale;
			float2 panner80 = ( global_speed200 * _Noise_02_Speed + uv_TexCoord58);
			float noise205 = saturate( ( ( ( tex2D( _Noise_01_Texture, ( Distortion64 + panner78 ) ).r * tex2D( _Noise_02_Texture, ( Distortion64 + panner80 ) ).r ) * _Noises_Power ) * _Noises_Multiply ) );
			float temp_output_207_0 = ( clampResult224 * noise205 );
			o.Emission = ( ( ( (_Color).rgb * (i.vertexColor).rgb ) * temp_output_207_0 ) * _Emissive );
			float temp_output_237_0 = ( saturate( ( temp_output_207_0 * _Opacity_Boost ) ) - ( i.uv2_texcoord2.x + _Dissolve ) );
			float clampResult238 = clamp( temp_output_237_0 , 0.0 , 1.0 );
			o.Alpha = ( i.vertexColor.a * clampResult238 );
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

            half4 frag(Varyings input) : SV_Target
            {
                Input surfInput;
                surfInput.worldPos = input.worldPos;
                surfInput.vertexColor = input.color;
                surfInput.uv_texcoord = input.texcoord;
                surfInput.uv2_texcoord2 = input.texcoord1;
                
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
