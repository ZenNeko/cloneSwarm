// Translated to URP by Antigravity Converter
Shader "Vefects/SH_VFX_Vefects_Piercing_BIRP_New"
{
    Properties
	{
		_Color_1( "Color 01", Color ) = ( 1, 0, 0.6261435, 0 )
		_Color_2( "Color 02", Color ) = ( 0.06587124, 0, 1, 0 )
		[Space(33)][Header(Emissive Noise)][Space(13)] _Emissive_Noise_Texture( "Emissive Noise Texture", 2D ) = "white" {}
		_Texture0( "Emissive Noise Mask Texture", 2D ) = "white" {}
		_EmissiveDissolve_Scale( "Emissive Noise Scale", Vector ) = ( 1, 1, 0, 0 )
		_EmissiveDissolve_Speed( "Emissive Noise Speed", Vector ) = ( 1, 1, 0, 0 )
		_Emissive_Color( "Emissive Color", Color ) = ( 1, 0, 0.6261435, 0 )
		_Emissive_Intensity( "Emissive Intensity", Float ) = 3
		[Space(33)][Header(Piercing Texture)][Space(13)] _Piercing_Texture( "Piercing Texture", 2D ) = "white" {}
		_TextureSample1( "Piercing Noises Texture", 2D ) = "white" {}
		_Piercing_Noise_Scale( "Piercing Noise Scale", Vector ) = ( 1, 1, 0, 0 )
		_Piercing_Noise_Speed( "Piercing Noise Speed", Vector ) = ( -1, 0.5, 0, 0 )
		_Piercing_Noise_Intesnity( "Piercing Noise Intensity", Float ) = 3
		[Space(33)][Header(Distortion Noise)][Space(13)] _Distortion_Noise_Texture( "Distortion Noise Texture", 2D ) = "white" {}
		_Distortion_Noise_Scale( "Distortion Noise Scale", Vector ) = ( 1, 1, 0, 0 )
		_Distortion_Noise_Speed( "Distortion Noise Speed", Vector ) = ( 1, 1, 0, 0 )
		_Distortion_Intensity( "Distortion Intensity", Float ) = 1
		_Distortion_Mask( "Distortion Mask", 2D ) = "white" {}
		[Space(33)][Header(Color Noise Texture)][Space(13)] _Color_Noise_Texture( "Color Noise Texture", 2D ) = "white" {}
		_ColorNoise_Scale( "Color Noise Scale", Vector ) = ( 1, 1, 0, 0 )
		_ColorNoise_Speed( "Color Noise Speed", Vector ) = ( 1, 1, 0, 0 )
		_Color_Boost( "Color Boost", Float ) = 1
		[Space(33)][Header(Opacity Mask)][Space(13)] _Texture1( "Opacity Mask Texture", 2D ) = "white" {}
		_Opacity_Boost( "Opacity Boost", Float ) = 1
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
                float _Dst;
                float _ZTest;
                float _ZWrite;
                float _Src;
                float _Cull;
                float4 _Color_1;
                float4 _Color_2;
                float2 _ColorNoise_Scale;
                float2 _ColorNoise_Speed;
                float _Color_Boost;
                float2 _Distortion_Noise_Scale;
                float2 _Distortion_Noise_Speed;
                float4 _Distortion_Mask_ST;
                float _Distortion_Intensity;
                float2 _Piercing_Noise_Scale;
                float2 _Piercing_Noise_Speed;
                float _Piercing_Noise_Intesnity;
                float2 _EmissiveDissolve_Scale;
                float2 _EmissiveDissolve_Speed;
                float4 _Emissive_Color;
                float _Emissive_Intensity;
                float _Opacity_Boost;
                float4 _Texture1_ST;
            CBUFFER_END

            // Samplers (outside CBUFFER)
            uniform sampler2D _Color_Noise_Texture;
            uniform sampler2D _Piercing_Texture;
            uniform sampler2D _Distortion_Noise_Texture;
            uniform sampler2D _Distortion_Mask;
            uniform sampler2D _TextureSample1;
            uniform sampler2D _Texture0;
            uniform sampler2D _Emissive_Noise_Texture;
            uniform sampler2D _Texture1;

            // The original surf function
            void surf( Input i , inout SurfaceOutput o )
		{
			float2 uv_TexCoord165 = i.uv_texcoord * _ColorNoise_Scale + ( _Time.y * _ColorNoise_Speed );
			float3 lerpResult179 = lerp( (_Color_1).rgb , (_Color_2).rgb , tex2D( _Color_Noise_Texture, uv_TexCoord165 ).r);
			float2 uv_TexCoord111 = i.uv_texcoord * _Distortion_Noise_Scale + ( _Time.y * _Distortion_Noise_Speed );
			float2 uv_Distortion_Mask = i.uv_texcoord * _Distortion_Mask_ST.xy + _Distortion_Mask_ST.zw;
			float Distortion122 = ( ( ( tex2D( _Distortion_Noise_Texture, uv_TexCoord111 ).r * ( 1.0 - tex2D( _Distortion_Mask, uv_Distortion_Mask ).r ) ) * 0.1 ) * _Distortion_Intensity );
			float4 tex2DNode138 = tex2D( _Piercing_Texture, ( i.uv_texcoord + Distortion122 ) );
			float2 uv_TexCoord126 = i.uv_texcoord * _Piercing_Noise_Scale + ( _Time.y * _Piercing_Noise_Speed );
			float clampResult150 = clamp( ( tex2DNode138.r + ( tex2DNode138.r * ( tex2D( _TextureSample1, uv_TexCoord126 ).r * _Piercing_Noise_Intesnity ) ) ) , 0.0 , 1.0 );
			float3 baseColor194 = ( ( lerpResult179 * _Color_Boost ) * clampResult150 );
			float2 uv_TexCoord134 = i.uv_texcoord * _EmissiveDissolve_Scale + ( _Time.y * _EmissiveDissolve_Speed );
			float3 emission195 = ( (i.vertexColor).rgb * ( ( saturate( (  (-1.0 + ( ( 1.0 - i.uv2_texcoord2.w ) - 0.0 ) * ( 0.0 - -1.0 ) / ( 1.0 - 0.0 ) ) + saturate( ( tex2D( _Texture0, ( i.uv_texcoord + Distortion122 ) ).g * tex2D( _Emissive_Noise_Texture, ( uv_TexCoord134 + Distortion122 ) ).r ) ) ) ) * (_Emissive_Color).rgb ) * _Emissive_Intensity ) );
			o.Emission = ( baseColor194 + emission195 );
			float2 uv_Texture1 = i.uv_texcoord * _Texture1_ST.xy + _Texture1_ST.zw;
			float alpha196 = saturate( ( i.vertexColor.a * saturate( (  (0.0 + ( ( 1.0 - i.uv2_texcoord2.z ) - 0.0 ) * ( 2.0 - 0.0 ) / ( 1.0 - 0.0 ) ) + saturate( ( ( clampResult150 * _Opacity_Boost ) * tex2D( _Texture1, uv_Texture1 ).b ) ) ) ) ) );
			o.Alpha = alpha196;
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
