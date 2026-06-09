// Translated to URP by Antigravity Converter
Shader "Vefects/SH_Vefects_BIRP_Disc_Windup_01"
{
    Properties
	{
		_IsAdd("Is Add", Float) = 0
		[Space(33)][Header(Mask)][Space(13)]_MainMask("Main Mask", 2D) = "white" {}
		_MaskErosion("Mask Erosion", Float) = 0
		_MaskErosionSmoothness("Mask Erosion Smoothness", Float) = 0.3
		[Space(33)][Header(LUT)][Space(13)]_LUT("LUT", 2D) = "white" {}
		_LUTOffset("LUT Offset", Float) = 0
		_Emissive("Emissive", Float) = 1
		[Space(33)][Header(Distortion)][Space(13)]_DistortionNoise("Distortion Noise", 2D) = "white" {}
		_DistortionIntensity("Distortion Intensity", Float) = 0.1
		_DistortionNoiseUVScale("Distortion Noise UV Scale", Vector) = (1,1,0,0)
		_DistortionNoiseUVPanSpeed("Distortion Noise UV Pan Speed", Vector) = (0.05,-0.2,0,0)
		[Space(33)][Header(Refraction)][Space(13)]_RefractionNoise1("Refraction Noise", 2D) = "white" {}
		_RefractionAmount("Refraction Amount", Float) = 1
		_RefractionErosion("Refraction Erosion", Float) = 0
		_RefractionErosionSmoothness("Refraction Erosion Smoothness", Float) = 0.3
		[Space(33)][Header(Cutout)][Space(13)]_Cutout("Cutout", 2D) = "white" {}
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
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

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
			float4 screenPos;
			float4 uv_texcoord;
			float4 uv2_texcoord2;
			float4 vertexColor : COLOR;
		};

            // Uniforms inside Constant Buffer (SRP Batcher compatibility)
            CBUFFER_START(UnityPerMaterial)
                float _Dst;
                float _ZWrite;
                float _ZTest;
                float _Cull;
                float _Src;
                float _RefractionErosion;
                float _RefractionErosionSmoothness;
                float2 _DistortionNoiseUVPanSpeed;
                float2 _DistortionNoiseUVScale;
                float _DistortionIntensity;
                float _RefractionAmount;
                float _IsAdd;
                float _MaskErosion;
                float _MaskErosionSmoothness;
                float _LUTOffset;
                float _Emissive;
                float4 _Cutout_ST;
            CBUFFER_END

            // Samplers (outside CBUFFER)
            uniform sampler2D _RefractionNoise1;
            uniform sampler2D _DistortionNoise;
            uniform sampler2D _LUT;
            uniform sampler2D _MainMask;
            uniform sampler2D _Cutout;

            // The original surf function
            void surf( Input i , inout SurfaceOutput o )
		{
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_grabScreenPos = ASE_ComputeGrabScreenPos( ase_screenPos );
			float4 ase_grabScreenPosNorm = ase_grabScreenPos / ase_grabScreenPos.w;
			float2 appendResult100 = (float2(ase_grabScreenPosNorm.r , ase_grabScreenPosNorm.g));
			float2 appendResult49 = (float2(i.uv_texcoord.xy.x , ( i.uv_texcoord.xy.y + i.uv_texcoord.z )));
			float2 panner38 = ( 1.0 * _Time.y * _DistortionNoiseUVPanSpeed + ( i.uv_texcoord.xy * _DistortionNoiseUVScale ));
			float2 lerpResult29 = lerp( float2( 0,0.15 ) , ( ( (tex2D( _DistortionNoise, panner38 ).rgb).xy + -0.5 ) * 2.0 ) , _DistortionIntensity);
			float2 disUV84 = ( appendResult49 + lerpResult29 );
			float smoothstepResult70 = smoothstep( _RefractionErosion , ( _RefractionErosion + _RefractionErosionSmoothness ) , tex2D( _RefractionNoise1, disUV84 ).g);
			float temp_output_42_0 = saturate( ( 1.0 - i.uv_texcoord.xy.y ) );
			float temp_output_75_0 = saturate( ( saturate( smoothstepResult70 ) * temp_output_42_0 ) );
			float2 temp_cast_0 = (temp_output_75_0).xx;
			float temp_output_80_0 = ( _RefractionAmount * i.uv2_texcoord2.x );
			float2 lerpResult98 = lerp( float2( 0,0 ) , temp_cast_0 , temp_output_80_0);
			float4 screenColor101 = float4(SampleSceneColor(( appendResult100 + lerpResult98 )), 1.0);
			float4 lerpResult107 = lerp( screenColor101 , ( screenColor101 * temp_output_75_0 ) , _IsAdd);
			float smoothstepResult60 = smoothstep( _MaskErosion , ( _MaskErosion + _MaskErosionSmoothness ) , tex2D( _MainMask, disUV84 ).g);
			float temp_output_59_0 = saturate( ( saturate( smoothstepResult60 ) * temp_output_42_0 ) );
			float2 temp_cast_1 = (( ( i.uv_texcoord.w * temp_output_59_0 ) + _LUTOffset )).xx;
			float4 lerpResult87 = lerp( lerpResult107 , ( ( ( i.vertexColor * i.uv2_texcoord2.y ) * float4( tex2D( _LUT, temp_cast_1 ).rgb , 0.0 ) ) * _Emissive ) , temp_output_59_0);
			o.Emission = lerpResult87.rgb;
			float2 uv_Cutout = i.uv_texcoord * _Cutout_ST.xy + _Cutout_ST.zw;
			o.Alpha = saturate( ( saturate( ( temp_output_75_0 * i.vertexColor.a ) ) * tex2D( _Cutout, uv_Cutout ).g ) );
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
                surfInput.screenPos = input.screenPos;
                surfInput.uv_texcoord = input.texcoord;
                surfInput.uv2_texcoord2 = input.texcoord1;
                surfInput.vertexColor = input.color;
                
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
