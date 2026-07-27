// Translated to URP by Antigravity Converter
Shader "SH_Vefects_VFX_Fresnel"
{
    Properties
	{
		_Noise_Color_Texture("Noise_Color_Texture", 2D) = "white" {}
		_Noise_01_Texture("Noise_01_Texture", 2D) = "white" {}
		_TextureSample0("Texture Sample 0", 2D) = "white" {}
		_Noise_02_Texture("Noise_02_Texture", 2D) = "white" {}
		_NoiseDistortion_Texture("NoiseDistortion_Texture", 2D) = "white" {}
		_NoiseColor_Scale("NoiseColor_Scale", Vector) = (1,1,0,0)
		_Noise_01_Scale("Noise_01_Scale", Vector) = (0.8,0.8,0,0)
		_Noise_02_Scale("Noise_02_Scale", Vector) = (1,1,0,0)
		_NoiseDistortion_Scale("NoiseDistortion_Scale", Vector) = (1,1,0,0)
		_Noise_01_Speed("Noise_01_Speed", Vector) = (0.5,0.5,0,0)
		_NoiseColor_Speed("NoiseColor_Speed", Vector) = (0,0,0,0)
		_Noise_02_Speed("Noise_02_Speed", Vector) = (-0.2,0.4,0,0)
		_NoiseColor_Power("NoiseColor_Power", Float) = 1
		_Vector0("Vector 0", Vector) = (1,1,0,0)
		_NoiseColor_Intensity("NoiseColor_Intensity", Float) = 1
		_Mask_Offset("Mask_Offset", Vector) = (0,0,0,0)
		_NoiseDistortion_Speed("NoiseDistortion_Speed", Vector) = (0.2,0.25,0,0)
		_NoiseDistortion_Intensity("NoiseDistortion_Intensity", Float) = 1
		_Opacity_Boost("Opacity_Boost", Float) = 10
		_Mask_Multiply("Mask_Multiply", Float) = 1
		_Opacity_Power("Opacity_Power", Float) = 1
		_Fresnel_Scale("Fresnel_Scale", Float) = 1
		_Fresnel_Power("Fresnel_Power", Float) = 5
		_Color_2("Color_2", Color) = (1,1,1,0)
		_Color_1("Color_1", Color) = (1,1,1,0)
		_Mask_Power("Mask_Power", Float) = 1
		_Opacity_DepthFade_Intensity("Opacity_DepthFade_Intensity", Float) = 1
		_DepthFade_Distance("DepthFade_Distance", Float) = 1
		_DistortionMask("DistortionMask", Float) = 0
		_Global_Speed("Global_Speed", Float) = 1
		_Dissolve("Dissolve", Float) = 0
		_Emissive_Intensity("Emissive_Intensity", Float) = 1
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
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
			float4 uv_texcoord;
			float4 vertexColor : COLOR;
			float3 worldPos;
			float3 worldNormal;
			float4 screenPos;
		};

            // Uniforms inside Constant Buffer (SRP Batcher compatibility)
            CBUFFER_START(UnityPerMaterial)
                float4 _Color_1;
                float4 _Color_2;
                float _Global_Speed;
                float2 _NoiseDistortion_Speed;
                float2 _NoiseDistortion_Scale;
                float _NoiseDistortion_Intensity;
                float2 _NoiseColor_Speed;
                float2 _NoiseColor_Scale;
                float _NoiseColor_Intensity;
                float _NoiseColor_Power;
                float _Emissive_Intensity;
                float _Fresnel_Scale;
                float _Fresnel_Power;
                float4 _CameraDepthTexture_TexelSize;
                float _DepthFade_Distance;
                float _DistortionMask;
                float2 _Vector0;
                float2 _Mask_Offset;
                float _Mask_Power;
                float _Mask_Multiply;
                float2 _Noise_01_Speed;
                float2 _Noise_01_Scale;
                float2 _Noise_02_Speed;
                float2 _Noise_02_Scale;
                float _Opacity_Power;
                float _Opacity_Boost;
                float _Dissolve;
                float _Opacity_DepthFade_Intensity;
            CBUFFER_END

            // Samplers (outside CBUFFER)
            uniform sampler2D _Noise_Color_Texture;
            uniform sampler2D _NoiseDistortion_Texture;
            UNITY_DECLARE_DEPTH_TEXTURE( _CameraDepthTexture );
            uniform sampler2D _TextureSample0;
            uniform sampler2D _Noise_01_Texture;
            uniform sampler2D _Noise_02_Texture;

            // The original surf function
            void surf( Input i , inout SurfaceOutput o )
		{
			float global_speed178 = ( _Global_Speed * _Time.y );
			float2 uvs_TexCoord30 = i.uv_texcoord;
			uvs_TexCoord30.xy = i.uv_texcoord.xy * _NoiseDistortion_Scale;
			float2 panner79 = ( global_speed178 * _NoiseDistortion_Speed + uvs_TexCoord30.xy);
			float Distortion64 = ( ( tex2D( _NoiseDistortion_Texture, panner79 ).r * 0.1 ) * _NoiseDistortion_Intensity );
			float2 uvs_TexCoord246 = i.uv_texcoord;
			uvs_TexCoord246.xy = i.uv_texcoord.xy * _NoiseColor_Scale;
			float2 panner248 = ( 1.0 * _Time.y * _NoiseColor_Speed + uvs_TexCoord246.xy);
			float clampResult235 = clamp( pow( ( tex2D( _Noise_Color_Texture, ( Distortion64 + panner248 ) ).r * _NoiseColor_Intensity ) , _NoiseColor_Power ) , 0.0 , 1.0 );
			float3 lerpResult239 = lerp( (_Color_1).rgb , (_Color_2).rgb , clampResult235);
			o.Emission = ( ( lerpResult239 * (i.vertexColor).rgb ) * _Emissive_Intensity );
			float3 ase_worldPos = i.worldPos;
			float3 ase_worldViewDir = normalize( UnityWorldSpaceViewDir( ase_worldPos ) );
			float3 ase_worldNormal = i.worldNormal;
			float fresnelNdotV167 = dot( ase_worldNormal, ase_worldViewDir );
			float fresnelNode167 = ( i.uv_texcoord.z + _Fresnel_Scale * pow( 1.0 - fresnelNdotV167, _Fresnel_Power ) );
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_screenPosNorm = ase_screenPos / ase_screenPos.w;
			ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
			float screenDepth137 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_screenPosNorm.xy ));
			float distanceDepth137 = abs( ( screenDepth137 - LinearEyeDepth( ase_screenPosNorm.z ) ) / ( _DepthFade_Distance ) );
			float clampResult136 = clamp( ( 1.0 - distanceDepth137 ) , 0.0 , 1.0 );
			float2 appendResult216 = (float2(0.0 , 0.0));
			float2 uvs_TexCoord215 = i.uv_texcoord;
			uvs_TexCoord215.xy = i.uv_texcoord.xy * _Vector0 + _Mask_Offset;
			float2 panner218 = ( global_speed178 * appendResult216 + uvs_TexCoord215.xy);
			float2 uvs_TexCoord26 = i.uv_texcoord;
			uvs_TexCoord26.xy = i.uv_texcoord.xy * _Noise_01_Scale;
			float2 panner78 = ( global_speed178 * _Noise_01_Speed + uvs_TexCoord26.xy);
			float2 uvs_TexCoord58 = i.uv_texcoord;
			uvs_TexCoord58.xy = i.uv_texcoord.xy * _Noise_02_Scale;
			float2 panner80 = ( global_speed178 * _Noise_02_Speed + uvs_TexCoord58.xy);
			float clampResult169 = clamp( ( ( fresnelNode167 + clampResult136 ) * ( pow( ( saturate( ( ( tex2D( _TextureSample0, ( ( Distortion64 * _DistortionMask ) + panner218 ) ).r * _Mask_Power ) * _Mask_Multiply ) ) * ( tex2D( _Noise_01_Texture, ( Distortion64 + panner78 ) ).r * tex2D( _Noise_02_Texture, ( Distortion64 + panner80 ) ).r ) ) , _Opacity_Power ) * _Opacity_Boost ) ) , 0.0 , 1.0 );
			float screenDepth261 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_screenPosNorm.xy ));
			float distanceDepth261 = abs( ( screenDepth261 - LinearEyeDepth( ase_screenPosNorm.z ) ) / ( _Opacity_DepthFade_Intensity ) );
			float clampResult262 = clamp( distanceDepth261 , 0.0 , 1.0 );
			o.Alpha = ( i.vertexColor.a * saturate( ( ( clampResult169 - ( i.uv_texcoord.w + _Dissolve ) ) * clampResult262 ) ) );
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
                surfInput.uv_texcoord = input.texcoord;
                surfInput.vertexColor = input.color;
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
