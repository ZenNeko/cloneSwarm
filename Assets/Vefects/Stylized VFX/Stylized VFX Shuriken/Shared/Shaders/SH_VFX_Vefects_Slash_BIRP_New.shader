// Translated to URP by Antigravity Converter
Shader "Vefects/SH_VFX_Vefects_Slash_BIRP_New"
{
    Properties
	{
		[Space(13)][Header(Slash)][Space(13)] _Slash_Texture( "Slash Texture", 2D ) = "white" {}
		_Slash_Scale( "Slash Scale", Float ) = 1
		_Slash_Speed( "Slash Speed", Float ) = 1
		[Space(13)][Header(Slash Noise)][Space(13)] _Slash_Noise_Texture( "Slash Noise Texture", 2D ) = "white" {}
		_Slash_Noise_Scale( "Slash Noise Scale", Vector ) = ( 1, 1, 0, 0 )
		_Slash_Noise_Speed( "Slash Noise Speed", Vector ) = ( -1, 0.5, 0, 0 )
		_Slash_Noise_Intensity( "Slash Noise Intensity", Float ) = 1
		[Space(13)][Header(Emissive)][Space(13)] _Emissive_Slash_Texture( "Emissive Slash Texture", 2D ) = "white" {}
		_Emissive_Slash_Scale( "Emissive Slash Scale", Float ) = 1
		_Emissive_Slash_Speed( "Emissive Slash Speed", Float ) = 1
		_Emissive_Intensity( "Emissive Intensity", Float ) = 3
		[Space(13)][Header(Emissive Dissolve)][Space(13)] _Emissive_Dissolve_Texture( "Emissive Dissolve Texture", 2D ) = "white" {}
		_Emissive_Dissolve_Scale( "Emissive Dissolve Scale", Vector ) = ( 1, 1, 0, 0 )
		_Emissive_Dissolve_Speed( "Emissive Dissolve Speed", Vector ) = ( 1, 1, 0, 0 )
		[Space(13)][Header(Distortion)][Space(13)] _Distortion_Noise_Texture( "Distortion Noise Texture", 2D ) = "white" {}
		_Distortion_Noise_Scale( "Distortion Noise Scale", Vector ) = ( 1, 1, 0, 0 )
		_Distortion_Noise_Speed( "Distortion Noise Speed", Vector ) = ( 1, 1, 0, 0 )
		_Distortion_Intensity( "Distortion Intensity", Float ) = 1
		[Space(13)][Header(Color Noise)][Space(13)] _Color_Noise_Texture( "Color Noise Texture", 2D ) = "white" {}
		_ColorNoise_Scale( "Color Noise Scale", Vector ) = ( 1, 1, 0, 0 )
		_ColorNoise_Speed( "Color Noise Speed", Vector ) = ( 1, 1, 0, 0 )
		_Color_Boost( "Color Boost", Float ) = 1
		[Space(13)][Header(Opacity)][Space(13)] _Mask( "Mask", 2D ) = "white" {}
		_Opacity_Boost( "Opacity Boost", Float ) = 1
		[Space(13)][Header(Colors)][Space(13)] _Color_1( "Color 01", Color ) = ( 1, 0, 0.6261435, 0 )
		_Color_2( "Color 02", Color ) = ( 0.06587124, 0, 1, 0 )
		_Emissive_Color( "Emissive Color", Color ) = ( 1, 0, 0.6261435, 0 )
		_AdditiveLerp( "Additive Lerp", Float ) = 0
		[Space(33)][Header(Cutout)][Space(13)] _Cutout( "Cutout", 2D ) = "white" {}
		_CutoutErosion( "Cutout Erosion", Float ) = 0
		_CutoutErosionSmoothness( "Cutout Erosion Smoothness", Float ) = 0.05
		_CutoutRotation( "Cutout Rotation", Float ) = 0
		_CutoutOffset( "Cutout Offset", Vector ) = ( 0, 0, 0, 0 )
		[Space(33)][Header(AR)][Space(13)] _Cull( "Cull", Float ) = 2
		_Src( "Src", Float ) = 5
		_Dst( "Dst", Float ) = 10
		_ZWrite( "ZWrite", Float ) = 0
		_ZTest( "ZTest", Float ) = 2
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
			float2 uv_texcoord;
			float4 vertexColor : COLOR;
			float4 uv2_texcoord2;
		};

            // Uniforms inside Constant Buffer (SRP Batcher compatibility)
            CBUFFER_START(UnityPerMaterial)
                float _ZTest;
                float _Src;
                float _Dst;
                float _ZWrite;
                float _Cull;
                float4 _Color_1;
                float4 _Color_2;
                float2 _ColorNoise_Scale;
                float2 _ColorNoise_Speed;
                float _Color_Boost;
                float4 _Mask_ST;
                float _Slash_Scale;
                float _Slash_Speed;
                float2 _Distortion_Noise_Scale;
                float2 _Distortion_Noise_Speed;
                float _Distortion_Intensity;
                float _Slash_Noise_Intensity;
                float2 _Slash_Noise_Scale;
                float2 _Slash_Noise_Speed;
                float _Emissive_Slash_Scale;
                float _Emissive_Slash_Speed;
                float2 _Emissive_Dissolve_Scale;
                float2 _Emissive_Dissolve_Speed;
                float4 _Emissive_Color;
                float _Emissive_Intensity;
                float _Opacity_Boost;
                float _CutoutErosion;
                float _CutoutErosionSmoothness;
                float2 _CutoutOffset;
                float _CutoutRotation;
                float _AdditiveLerp;
            CBUFFER_END

            // Samplers (outside CBUFFER)
            uniform sampler2D _Color_Noise_Texture;
            uniform sampler2D _Mask;
            uniform sampler2D _Slash_Texture;
            uniform sampler2D _Distortion_Noise_Texture;
            uniform sampler2D _Slash_Noise_Texture;
            uniform sampler2D _Emissive_Slash_Texture;
            uniform sampler2D _Emissive_Dissolve_Texture;
            uniform sampler2D _Cutout;

            // The original surf function
            void surf( Input i , inout SurfaceOutput o )
		{
			float2 uv_TexCoord67 = i.uv_texcoord * _ColorNoise_Scale + ( _Time.y * _ColorNoise_Speed );
			float3 lerpResult86 = lerp( (_Color_1).rgb , (_Color_2).rgb , tex2D( _Color_Noise_Texture, uv_TexCoord67 ).r);
			float2 uv_Mask = i.uv_texcoord * _Mask_ST.xy + _Mask_ST.zw;
			float4 tex2DNode62 = tex2D( _Mask, uv_Mask );
			float2 appendResult30 = (float2(_Slash_Scale , 1.0));
			float2 appendResult25 = (float2(_Slash_Speed , 0.0));
			float2 uv_TexCoord38 = i.uv_texcoord * appendResult30 + ( _Time.y * appendResult25 );
			float2 uv_TexCoord14 = i.uv_texcoord * _Distortion_Noise_Scale + ( _Time.y * _Distortion_Noise_Speed );
			float Distortion31 = ( ( tex2D( _Distortion_Noise_Texture, uv_TexCoord14 ).r * 0.1 ) * _Distortion_Intensity );
			float2 uv_TexCoord49 = i.uv_texcoord * _Slash_Noise_Scale + ( _Time.y * _Slash_Noise_Speed );
			float clampResult66 = clamp( ( ( tex2D( _Slash_Texture, ( uv_TexCoord38 + Distortion31 ) ).r * _Slash_Noise_Intensity ) + tex2D( _Slash_Noise_Texture, uv_TexCoord49 ).g ) , 0.0 , 1.0 );
			float temp_output_69_0 = ( tex2DNode62.r * clampResult66 );
			float2 appendResult32 = (float2(_Emissive_Slash_Scale , 1.0));
			float2 appendResult23 = (float2(_Emissive_Slash_Speed , 0.0));
			float2 uv_TexCoord39 = i.uv_texcoord * appendResult32 + ( _Time.y * appendResult23 );
			float2 uv_TexCoord43 = i.uv_texcoord * _Emissive_Dissolve_Scale + ( _Time.y * _Emissive_Dissolve_Speed );
			float3 temp_output_107_0 = ( ( ( lerpResult86 * _Color_Boost ) * temp_output_69_0 ) + ( (i.vertexColor).rgb * ( ( ( saturate( (  (-1.0 + ( ( 1.0 - i.uv2_texcoord2.w ) - 0.0 ) * ( 0.0 - -1.0 ) / ( 1.0 - 0.0 ) ) + saturate( ( tex2D( _Emissive_Slash_Texture, ( uv_TexCoord39 + Distortion31 ) ).g * tex2D( _Emissive_Dissolve_Texture, uv_TexCoord43 ).r ) ) ) ) * tex2DNode62.r ) * (_Emissive_Color).rgb ) * _Emissive_Intensity ) ) );
			float2 temp_output_129_0 = ( i.uv_texcoord + _CutoutOffset );
			float cos131 = cos( radians( _CutoutRotation ) );
			float sin131 = sin( radians( _CutoutRotation ) );
			float2 rotator131 = mul( temp_output_129_0 - float2( 0.5,0.5 ) , float2x2( cos131 , -sin131 , sin131 , cos131 )) + float2( 0.5,0.5 );
			float smoothstepResult135 = smoothstep( _CutoutErosion , ( _CutoutErosion + _CutoutErosionSmoothness ) , tex2D( _Cutout, rotator131 ).g);
			float cutout136 = smoothstepResult135;
			float temp_output_118_0 = saturate( ( saturate( ( i.vertexColor.a * saturate( (  (0.0 + ( ( 1.0 - i.uv2_texcoord2.z ) - 0.0 ) * ( 2.0 - 0.0 ) / ( 1.0 - 0.0 ) ) + saturate( ( saturate( temp_output_69_0 ) * _Opacity_Boost ) ) ) ) ) ) * cutout136 ) );
			float3 lerpResult120 = lerp( temp_output_107_0 , saturate( ( temp_output_107_0 * temp_output_118_0 ) ) , _AdditiveLerp);
			o.Emission = lerpResult120;
			o.Alpha = temp_output_118_0;
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
                surfInput.uv_texcoord = input.texcoord;
                surfInput.vertexColor = input.color;
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
    CustomEditor "AmplifyShaderEditor.MaterialInspector"
}
