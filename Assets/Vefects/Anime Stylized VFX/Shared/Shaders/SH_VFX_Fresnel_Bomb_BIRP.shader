// Translated to URP by Antigravity Converter
Shader "Vefects/SH_VFX_Fresnel_Bomb_BIRP"
{
    Properties
	{
		[Space(33)][Header(Dissolve)][Space(13)]_DissolveTexture("Dissolve Texture", 2D) = "white" {}
		_DissolveUVScale("Dissolve UV Scale", Vector) = (1,1,0,0)
		_DissolveUVSpeed("Dissolve UV Speed", Vector) = (0,0.3,0,0)
		_DissolveInvert("Dissolve Invert", Range( 0 , 1)) = 0
		_DissolveEro("Dissolve Ero", Float) = 0
		_ColorIn("Color In", Color) = (1,1,1,0)
		_ColorExt("Color Ext", Color) = (0,0.6901961,1,0)
		_FrBias1("Fr Bias", Float) = 0
		_FrScale1("Fr Scale", Float) = 1
		_FrColorScale("Fr Color Scale", Float) = 1
		_FrColorBias("Fr Color Bias", Float) = 0
		_FrPower1("Fr Power", Float) = 1
		_FrColorPower("Fr Color Power", Float) = 1
		_CorePower("Core Power", Float) = 1
		_CoreIntensity("Core Intensity", Float) = 0.6
		_GlowIntensity("Glow Intensity", Float) = 1
		_AddDiss("Add Diss", Float) = -0.690476
		_CoreColorDifferent("Core Color Different", Float) = 0.5
		_Brightness("Brightness", Float) = 1
		[Space(33)][Header(AR)][Space(13)]_Cull("Cull", Float) = 2
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
			float4 vertexColor : COLOR;
			float3 worldPos;
			float3 worldNormal;
			float4 uv_texcoord;
		};

            // Uniforms inside Constant Buffer (SRP Batcher compatibility)
            CBUFFER_START(UnityPerMaterial)
                float _Cull;
                float _Src;
                float _Dst;
                float _ZWrite;
                float _ZTest;
                float4 _ColorIn;
                float4 _ColorExt;
                float _FrColorBias;
                float _FrColorScale;
                float _FrColorPower;
                float _CoreColorDifferent;
                float _Brightness;
                float2 _DissolveUVSpeed;
                float2 _DissolveUVScale;
                float _DissolveInvert;
                float _DissolveEro;
                float _CorePower;
                float _CoreIntensity;
                float _GlowIntensity;
                float _FrBias1;
                float _FrScale1;
                float _FrPower1;
                float _AddDiss;
            CBUFFER_END

            // Samplers (outside CBUFFER)
            uniform sampler2D _DissolveTexture;

            // The original surf function
            void surf( Input i , inout SurfaceOutput o )
		{
			float3 ase_worldPos = i.worldPos;
			float3 ase_viewVectorWS = ( _WorldSpaceCameraPos.xyz - ase_worldPos );
			float3 ase_viewDirWS = normalize( ase_viewVectorWS );
			float3 ase_worldNormal = i.worldNormal;
			float fresnelNdotV39 = dot( ase_worldNormal, ase_viewDirWS );
			float fresnelNode39 = ( _FrColorBias + _FrColorScale * pow( 1.0 - fresnelNdotV39, _FrColorPower ) );
			float temp_output_43_0 = saturate( fresnelNode39 );
			float4 lerpResult38 = lerp( _ColorIn , _ColorExt , temp_output_43_0);
			float4 lerpResult57 = lerp( i.vertexColor , lerpResult38 , _CoreColorDifferent);
			o.Emission = ( lerpResult57 * _Brightness ).rgb;
			float2 panner19 = ( 1.0 * _Time.y * _DissolveUVSpeed + ( i.uv_texcoord.xy * _DissolveUVScale ));
			float4 tex2DNode16 = tex2D( _DissolveTexture, panner19 );
			float4 lerpResult23 = lerp( tex2DNode16 , ( 1.0 - tex2DNode16 ) , _DissolveInvert);
			float4 temp_output_30_0 = ( saturate( lerpResult23 ) + ( i.uv_texcoord.z + _DissolveEro ) );
			float4 temp_cast_1 = (_CorePower).xxxx;
			float4 temp_output_52_0 = saturate( ( ( pow( temp_output_30_0 , temp_cast_1 ) * _CoreIntensity ) + ( temp_output_30_0 * _GlowIntensity ) ) );
			float fresnelNdotV62 = dot( ase_worldNormal, ase_viewDirWS );
			float fresnelNode62 = ( _FrBias1 + _FrScale1 * pow( 1.0 - fresnelNdotV62, _FrPower1 ) );
			float temp_output_78_0 = saturate( fresnelNode62 );
			float4 temp_cast_2 = (temp_output_78_0).xxxx;
			float4 temp_cast_3 = (temp_output_43_0).xxxx;
			o.Alpha = saturate( ( ( i.vertexColor.a * saturate( step( temp_output_52_0 , temp_cast_2 ) ) ) + step( ( ( i.vertexColor.a + temp_output_52_0 ) + _AddDiss ) , temp_cast_3 ) ) ).r;
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
                surfInput.vertexColor = input.color;
                surfInput.worldPos = input.worldPos;
                surfInput.worldNormal = input.worldNormal;
                surfInput.uv_texcoord = input.texcoord;
                
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
