// Translated to URP by Antigravity Converter
Shader "Vefects/SH_VFX_Stylized_Dissolve_BIRP"
{
    Properties
	{
		[Space(33)][Header(Main Texture)][Space(13)]_Texture("Texture", 2D) = "white" {}
		_TextureChannel("Texture Channel", Vector) = (0,1,0,0)
		_TextureRotation("Texture Rotation", Float) = 0
		_TexturePanSpeed("Texture Pan Speed", Vector) = (0,0,0,0)
		[Space(33)][Header(Color Texture)][Space(13)]_ColorTexture("Color Texture", 2D) = "white" {}
		_ColorRotation("Color Rotation", Float) = 0
		[Space(33)][Header(Gradient Shape)][Space(13)]_GradientShape("Gradient Shape", 2D) = "white" {}
		_GradientShapeChannel("Gradient Shape Channel", Vector) = (0,1,0,0)
		_GradientShapeRotation("Gradient Shape Rotation", Float) = 0
		[Space(33)][Header(Gradient Map)][Space(13)]_GradientMap("Gradient Map", 2D) = "white" {}
		_GradientMapDisplacement("Gradient Map Displacement", Float) = 0.1
		_InvertGradient("Invert Gradient", Float) = 0
		[Space(33)][Header(Distortion)][Space(13)]_DistortionMask("Distortion Mask", 2D) = "white" {}
		_DistortionMaskChannel("Distortion Mask Channel", Vector) = (0,1,0,0)
		_DistortionMaskRotation("Distortion Mask Rotation", Float) = 0
		_DistortionMaskPanSpeed("Distortion Mask Pan Speed", Vector) = (0,0,0,0)
		_DistortionIntensity("Distortion Intensity", Float) = 0
		[Space(33)][Header(Dissolve)][Space(13)]_DissolveMask("Dissolve Mask", 2D) = "white" {}
		_DissolveMaskChannel("Dissolve Mask Channel", Vector) = (0,1,0,0)
		_DissolveMaskRotation("Dissolve Mask Rotation", Float) = 0
		_DissolveMaskPanSpeed("Dissolve Mask Pan Speed", Vector) = (0,0,0,0)
		_DissolveMaskInvert("Dissolve Mask Invert", Range( 0 , 1)) = 0
		_DissolveOffset("Dissolve Offset", Float) = 0
		[Space(33)][Header(Properties)][Space(13)]_EmissionIntensity("Emission Intensity", Float) = 1
		_CoreColor("Core Color", Color) = (1,1,1,0)
		_DifferentCoreColor("Different Core Color", Float) = 0
		_CorePower("Core Power", Float) = 1
		_CoreIntensity("Core Intensity", Float) = 0
		_GlowIntensity("Glow Intensity", Float) = 1
		_AlphaBoldness("Alpha Boldness", Float) = 1
		[Toggle(_CUSTOMPANSWITCH_ON)] _CustomPanSwitch("CustomPanSwitch", Float) = 0
		[Toggle(_MESHVERTEXCOLOR_ON)] _MeshVertexColor("MeshVertexColor", Float) = 0
		[Toggle(_STEP_ON)] _Step("Step", Float) = 0
		_ValueStep("Value Step", Float) = 0
		_ValueStepAdd("Value Step Add", Float) = 0.1
		[Space(33)][Header(Depth Fade)][Space(13)][Toggle(_USEDEPTHFADE_ON)] _UseDepthFade("Use Depth Fade", Float) = 0
		_DepthFadeIntensity("Depth Fade Intensity", Float) = 0
		[Space(33)][Header(AR)][Space(13)]_Cull("Cull", Float) = 2
		_Src("Src", Float) = 5
		_Dst("Dst", Float) = 10
		_ZWrite("ZWrite", Float) = 0
		_ZTest("ZTest", Float) = 2
		[HideInInspector] _texcoord2( "", 2D ) = "white" {}
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] _texcoord4( "", 2D ) = "white" {}
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
            #pragma shader_feature_local _CUSTOMPANSWITCH_ON
            #pragma shader_feature_local _STEP_ON
            #pragma shader_feature_local _MESHVERTEXCOLOR_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            

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
			float4 uv_texcoord;
			float2 uv2_texcoord2;
			float2 uv4_texcoord4;
			float4 vertexColor : COLOR;
			float4 screenPos;
		};

            // Uniforms inside Constant Buffer (SRP Batcher compatibility)
            CBUFFER_START(UnityPerMaterial)
                float _Cull;
                float _Src;
                float _Dst;
                float _ZWrite;
                float _ZTest;
                float4 _ColorTexture_ST;
                float _ColorRotation;
                float4 _DistortionMask_ST;
                float _DistortionMaskRotation;
                float2 _DistortionMaskPanSpeed;
                float4 _DistortionMaskChannel;
                float _DistortionIntensity;
                float4 _GradientShape_ST;
                float _GradientShapeRotation;
                float4 _GradientShapeChannel;
                float4 _DissolveMask_ST;
                float _DissolveMaskRotation;
                float2 _DissolveMaskPanSpeed;
                float4 _DissolveMaskChannel;
                float _DissolveMaskInvert;
                float _DissolveOffset;
                float _InvertGradient;
                float _GradientMapDisplacement;
                float4 _CoreColor;
                float4 _Texture_ST;
                float _TextureRotation;
                float2 _TexturePanSpeed;
                float4 _TextureChannel;
                float _CorePower;
                float _CoreIntensity;
                float _DifferentCoreColor;
                float _EmissionIntensity;
                float _GlowIntensity;
                float _AlphaBoldness;
                float _ValueStep;
                float _ValueStepAdd;
                float _DepthFadeIntensity;
            CBUFFER_END

            // Samplers (outside CBUFFER)
            uniform sampler2D _ColorTexture;
            uniform sampler2D _DistortionMask;
            uniform sampler2D _GradientMap;
            uniform sampler2D _GradientShape;
            uniform sampler2D _DissolveMask;
            uniform sampler2D _Texture;

            // The original surf function
            void surf( Input i , inout SurfaceOutput o )
		{
			float4 uvs_ColorTexture = i.uv_texcoord;
			uvs_ColorTexture.xy = i.uv_texcoord.xy * _ColorTexture_ST.xy + _ColorTexture_ST.zw;
			float cos190 = cos( radians( _ColorRotation ) );
			float sin190 = sin( radians( _ColorRotation ) );
			float2 rotator190 = mul( uvs_ColorTexture.xy - float2( 0.5,0.5 ) , float2x2( cos190 , -sin190 , sin190 , cos190 )) + float2( 0.5,0.5 );
			float2 temp_cast_0 = (0.0).xx;
			#ifdef _CUSTOMPANSWITCH_ON
				float2 staticSwitch85 = i.uv2_texcoord2;
			#else
				float2 staticSwitch85 = temp_cast_0;
			#endif
			float2 CustomUV89 = staticSwitch85;
			float4 uvs_DistortionMask = i.uv_texcoord;
			uvs_DistortionMask.xy = i.uv_texcoord.xy * _DistortionMask_ST.xy + _DistortionMask_ST.zw;
			float cos95 = cos( radians( _DistortionMaskRotation ) );
			float sin95 = sin( radians( _DistortionMaskRotation ) );
			float2 rotator95 = mul( uvs_DistortionMask.xy - float2( 0.5,0.5 ) , float2x2( cos95 , -sin95 , sin95 , cos95 )) + float2( 0.5,0.5 );
			float dotResult100 = dot( tex2D( _DistortionMask, ( rotator95 + uvs_DistortionMask.w + CustomUV89 + ( _Time.y * _DistortionMaskPanSpeed ) ) ) , _DistortionMaskChannel );
			float Disto107 = ( saturate( dotResult100 ) * _DistortionIntensity );
			float4 uvs_GradientShape = i.uv_texcoord;
			uvs_GradientShape.xy = i.uv_texcoord.xy * _GradientShape_ST.xy + _GradientShape_ST.zw;
			float cos218 = cos( radians( _GradientShapeRotation ) );
			float sin218 = sin( radians( _GradientShapeRotation ) );
			float2 rotator218 = mul( uvs_GradientShape.xy - float2( 0.5,0.5 ) , float2x2( cos218 , -sin218 , sin218 , cos218 )) + float2( 0.5,0.5 );
			float dotResult232 = dot( tex2D( _GradientShape, ( rotator218 + CustomUV89 + Disto107 ) ) , _GradientShapeChannel );
			float4 uvs_DissolveMask = i.uv_texcoord;
			uvs_DissolveMask.xy = i.uv_texcoord.xy * _DissolveMask_ST.xy + _DissolveMask_ST.zw;
			float cos112 = cos( radians( _DissolveMaskRotation ) );
			float sin112 = sin( radians( _DissolveMaskRotation ) );
			float2 rotator112 = mul( uvs_DissolveMask.xy - float2( 0.5,0.5 ) , float2x2( cos112 , -sin112 , sin112 , cos112 )) + float2( 0.5,0.5 );
			float dotResult122 = dot( tex2D( _DissolveMask, ( rotator112 + uvs_DissolveMask.w + CustomUV89 + ( _Time.y * _DissolveMaskPanSpeed ) + Disto107 ) ) , _DissolveMaskChannel );
			float temp_output_126_0 = saturate( dotResult122 );
			float lerpResult138 = lerp( temp_output_126_0 , saturate( ( 1.0 - temp_output_126_0 ) ) , _DissolveMaskInvert);
			float temp_output_145_0 = ( saturate( lerpResult138 ) + i.uv_texcoord.z + _DissolveOffset );
			float temp_output_225_0 = saturate( ( saturate( dotResult232 ) * temp_output_145_0 ) );
			float lerpResult196 = lerp( saturate( ( 1.0 - temp_output_225_0 ) ) , temp_output_225_0 , _InvertGradient);
			float2 temp_cast_4 = (( lerpResult196 + _GradientMapDisplacement )).xx;
			float3 temp_output_157_0 = (i.vertexColor).rgb;
			float3 temp_output_198_0 = ( saturate( ( (tex2D( _ColorTexture, ( rotator190 + CustomUV89 + Disto107 ) )).rgb + i.uv4_texcoord4.x ) ) * (tex2D( _GradientMap, temp_cast_4 )).rgb * temp_output_157_0 );
			float4 uvs_Texture = i.uv_texcoord;
			uvs_Texture.xy = i.uv_texcoord.xy * _Texture_ST.xy + _Texture_ST.zw;
			float cos129 = cos( radians( _TextureRotation ) );
			float sin129 = sin( radians( _TextureRotation ) );
			float2 rotator129 = mul( uvs_Texture.xy - float2( 0.5,0.5 ) , float2x2( cos129 , -sin129 , sin129 , cos129 )) + float2( 0.5,0.5 );
			float dotResult140 = dot( tex2D( _Texture, ( rotator129 + ( _Time.y * _TexturePanSpeed ) + CustomUV89 + Disto107 ) ) , _TextureChannel );
			float temp_output_147_0 = ( temp_output_145_0 * saturate( dotResult140 ) );
			float temp_output_153_0 = ( pow( temp_output_147_0 , _CorePower ) * _CoreIntensity );
			float4 lerpResult188 = lerp( float4( temp_output_198_0 , 0.0 ) , _CoreColor , saturate( temp_output_153_0 ));
			float4 lerpResult217 = lerp( float4( temp_output_198_0 , 0.0 ) , saturate( lerpResult188 ) , _DifferentCoreColor);
			float3 temp_cast_8 = (1.0).xxx;
			#ifdef _MESHVERTEXCOLOR_ON
				float3 staticSwitch159 = temp_output_157_0;
			#else
				float3 staticSwitch159 = temp_cast_8;
			#endif
			float3 temp_output_167_0 = saturate( ( ( i.vertexColor.a * saturate( ( temp_output_153_0 + ( temp_output_147_0 * _GlowIntensity ) ) ) * staticSwitch159 ) * _AlphaBoldness ) );
			float3 temp_cast_9 = (_ValueStep).xxx;
			float3 temp_cast_10 = (( _ValueStep + _ValueStepAdd )).xxx;
			float3 smoothstepResult170 = smoothstep( temp_cast_9 , temp_cast_10 , temp_output_167_0);
			#ifdef _STEP_ON
				float3 staticSwitch183 = saturate( smoothstepResult170 );
			#else
				float3 staticSwitch183 = temp_output_167_0;
			#endif
			float4 temp_output_234_0 = ( ( saturate( lerpResult217 ) * _EmissionIntensity ) * float4( staticSwitch183 , 0.0 ) );
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_screenPosNorm = ase_screenPos / ase_screenPos.w;
			ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
			float screenDepth213 = LinearEyeDepth(SampleSceneDepth(ase_screenPosNorm.xy ), _ZBufferParams);
			float distanceDepth213 = ( screenDepth213 - LinearEyeDepth( ase_screenPosNorm.z , _ZBufferParams) ) / ( _DepthFadeIntensity );
			float temp_output_236_0 = saturate( distanceDepth213 );
			#ifdef _USEDEPTHFADE_ON
				float4 staticSwitch238 = ( temp_output_234_0 * temp_output_236_0 );
			#else
				float4 staticSwitch238 = temp_output_234_0;
			#endif
			o.Emission = staticSwitch238.rgb;
			#ifdef _USEDEPTHFADE_ON
				float3 staticSwitch239 = ( staticSwitch183 * temp_output_236_0 );
			#else
				float3 staticSwitch239 = staticSwitch183;
			#endif
			o.Alpha = staticSwitch239.x;
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
                surfInput.uv2_texcoord2 = input.texcoord1.xy;
                surfInput.uv4_texcoord4 = input.texcoord3.xy;
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
