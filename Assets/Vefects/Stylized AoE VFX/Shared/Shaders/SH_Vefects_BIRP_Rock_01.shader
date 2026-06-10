// Translated to URP by Antigravity Converter
Shader "Vefects/SH_Vefects_BIRP_Rock_01"
{
    Properties
	{
		_Metallic("Metallic", Float) = 0
		_Smoothness("Smoothness", Float) = 0.3
		_Emissive("Emissive", Float) = 1
		_BaseEmissionMultiply("Base Emission Multiply", Float) = 0
		[Space(33)][Header(Main Texture)][Space(13)]_MainTexture1("Main Texture", 2D) = "white" {}
		[Space(33)][Header(Noise Texture)][Space(13)]_NoiseTexture("Noise Texture", 2D) = "white" {}
		_NoiseTextureSelector("Noise Texture Selector", Vector) = (0,1,0,0)
		_NoiseUVScale("Noise UV Scale", Vector) = (1,1,0,0)
		_NoiseUVPanSpeed("Noise UV Pan Speed", Vector) = (0.01,0.3,0,0)
		[Space(33)][Header(LUT)][Space(13)]_LUT("LUT", 2D) = "white" {}
		_LUTAmplitude("LUT Amplitude", Float) = 1
		_LUTOffset("LUT Offset", Float) = 0
		_LUTPanSpeed("LUT Pan Speed", Float) = 0
		[Space(33)][Header(Fresnel)][Space(13)]_FresnelScale("Fresnel Scale", Float) = 1
		_FresnelBias("Fresnel Bias", Float) = 0
		_FresnelPower("Fresnel Power", Float) = 1
		_FresnelMultiply("Fresnel Multiply", Float) = 1
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
			float2 uv_texcoord;
			float4 vertexColor : COLOR;
			float3 worldPos;
			float3 worldNormal;
		};

            // Uniforms inside Constant Buffer (SRP Batcher compatibility)
            CBUFFER_START(UnityPerMaterial)
                float _LUTPanSpeed;
                float4 _MainTexture1_ST;
                float _LUTAmplitude;
                float _LUTOffset;
                float2 _NoiseUVPanSpeed;
                float2 _NoiseUVScale;
                float4 _NoiseTextureSelector;
                float _FresnelMultiply;
                float _FresnelBias;
                float _FresnelScale;
                float _FresnelPower;
                float _Emissive;
                float _BaseEmissionMultiply;
                float _Metallic;
                float _Smoothness;
            CBUFFER_END

            // Samplers (outside CBUFFER)
            uniform sampler2D _LUT;
            uniform sampler2D _MainTexture1;
            uniform sampler2D _NoiseTexture;

            // The original surf function
            void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 temp_cast_0 = (_LUTPanSpeed).xx;
			float2 uv_MainTexture1 = i.uv_texcoord * _MainTexture1_ST.xy + _MainTexture1_ST.zw;
			float2 temp_cast_1 = (( ( tex2D( _MainTexture1, uv_MainTexture1 ).r * _LUTAmplitude ) + _LUTOffset )).xx;
			float2 panner25 = ( 1.0 * _Time.y * temp_cast_0 + temp_cast_1);
			float4 tex2DNode27 = tex2D( _LUT, panner25 );
			float2 panner14 = ( 1.0 * _Time.y * _NoiseUVPanSpeed + ( i.uv_texcoord * _NoiseUVScale ));
			float dotResult18 = dot( tex2D( _NoiseTexture, panner14 ) , _NoiseTextureSelector );
			float3 ase_worldPos = i.worldPos;
			float3 ase_viewVectorWS = ( _WorldSpaceCameraPos.xyz - ase_worldPos );
			float3 ase_viewDirWS = normalize( ase_viewVectorWS );
			float3 ase_worldNormal = i.worldNormal;
			float fresnelNdotV42 = dot( ase_worldNormal, ase_viewDirWS );
			float fresnelNode42 = ( _FresnelBias + _FresnelScale * pow( max( 1.0 - fresnelNdotV42 , 0.0001 ), _FresnelPower ) );
			float temp_output_34_0 = saturate( ( saturate( dotResult18 ) * saturate( ( ( i.vertexColor.a * _FresnelMultiply ) * saturate( fresnelNode42 ) ) ) ) );
			float4 lerpResult37 = lerp( float4( tex2DNode27.rgb , 0.0 ) , i.vertexColor , temp_output_34_0);
			o.Albedo = lerpResult37.rgb;
			o.Emission = ( ( ( i.vertexColor * temp_output_34_0 ) * _Emissive ) + float4( ( tex2DNode27.rgb * _BaseEmissionMultiply ) , 0.0 ) ).rgb;
			o.Metallic = _Metallic;
			o.Smoothness = _Smoothness;
			o.Alpha = 1;
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
                
                SurfaceOutputStandard o;
                o.Albedo = 0.0;
                o.Normal = float3(0,0,1);
                o.Emission = 0.0;
                o.Metallic = 0.0;
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
