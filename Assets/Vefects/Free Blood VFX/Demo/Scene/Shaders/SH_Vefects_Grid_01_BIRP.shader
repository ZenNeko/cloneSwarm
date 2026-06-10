// Translated to URP by Antigravity Converter
Shader "/_Vefects_/SH_Vefects_Grid_01_BIRP"
{
    Properties
	{
		_Masks( "Masks", 2D ) = "white" {}
		_Texture( "Texture", 2D ) = "white" {}
		_TileOverall( "Tile Overall", Float ) = 200
		_TileX( "Tile X", Float ) = 1
		_TileY( "Tile Y", Float ) = 1
		_ParallaxScale( "Parallax Scale", Float ) = 1
		_TextureTileOverall( "Texture Tile Overall", Float ) = 1
		_Normal( "Normal", 2D ) = "bump" {}
		_NormalIntensity( "Normal Intensity", Float ) = 1
		_RoughnessMin( "Roughness Min", Float ) = 0
		_RoughnessMax( "Roughness Max", Float ) = 1
		_Specular( "Specular", Float ) = 0.01
		_ColorOverall( "Color Overall", Color ) = ( 1, 1, 1, 0 )
		_Color01( "Color 01", Color ) = ( 1, 1, 1, 0 )
		_Color02( "Color 02", Color ) = ( 1, 1, 1, 0 )
		_MasksTileOverall( "Masks Tile Overall", Float ) = 1
		_RandomTileColorsMax( "Random Tile Colors Max", Float ) = 1
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Cull Back
        ZWrite Off
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

            // Depth Texture Support for URP
            #define SAMPLE_DEPTH_TEXTURE(tex, uv) tex2D(tex, uv).r
            #define LinearEyeDepth(depth) LinearEyeDepth(depth, _ZBufferParams)
            #define UNITY_DECLARE_DEPTH_TEXTURE(tex) sampler2D tex

            // Compatibility struct for SurfaceOutput
            struct SurfaceOutput
            {
                half3 Albedo;
                half3 Normal;
                half3 Emission;
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
		};

            // Uniforms inside Constant Buffer (SRP Batcher compatibility)
            CBUFFER_START(UnityPerMaterial)
                float _TileOverall;
                float _TileX;
                float _TileY;
                float _ParallaxScale;
                float _NormalIntensity;
                float _TextureTileOverall;
                float4 _Color01;
                float4 _Color02;
                float _MasksTileOverall;
                float _RandomTileColorsMax;
                float4 _ColorOverall;
                float _Specular;
                float _RoughnessMin;
                float _RoughnessMax;
            CBUFFER_END

            // Samplers (outside CBUFFER)
            uniform sampler2D _Normal;
            uniform sampler2D _Masks;
            uniform sampler2D _Texture;

            // The original surf function
            void surf( Input i , inout SurfaceOutputStandardSpecular o )
		{
			float2 appendResult23 = (float2(_TileX , _TileY));
			float2 UV27 = ( ( i.uv_texcoord * _TileOverall ) * appendResult23 );
			float2 Offset34 = ( ( saturate( tex2D( _Masks, UV27 ).g ) - 1 ) * float3( 0,0,0 ).xy * _ParallaxScale ) + UV27;
			float2 BOUV38 = Offset34;
			float3 lerpResult44 = lerp( float3( 0, 0, 1 ) , UnpackNormal( tex2D( _Normal, BOUV38 ) ) , _NormalIntensity);
			o.Normal = lerpResult44;
			float4 tex2DNode14 = tex2D( _Texture, ( BOUV38 * _TextureTileOverall ) );
			float4 tex2DNode10 = tex2D( _Masks, ( BOUV38 * _MasksTileOverall ) );
			float lerpResult65 = lerp( 0.0 , _RandomTileColorsMax , saturate( tex2DNode10.b ));
			float3 lerpResult57 = lerp( _Color01.rgb , _Color02.rgb , ( saturate( tex2DNode10.r ) * lerpResult65 ));
			o.Albedo = ( ( tex2DNode14.r * lerpResult57 ) * _ColorOverall.rgb );
			float3 temp_cast_0 = (_Specular).xxx;
			o.Specular = temp_cast_0;
			float lerpResult48 = lerp( _RoughnessMin , _RoughnessMax , tex2DNode14.g);
			o.Smoothness = lerpResult48;
			o.Alpha = 1;
		}

            // Vertex Shader inputs
            struct Attributes
            {
                float4 positionOS   : POSITION;
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
    CustomEditor "AmplifyShaderEditor.MaterialInspector"
}
