Shader "Swarm/ZoneObjectiveFill"
{
    Properties
    {
        _Progress   ("Progress",            Range(0,1)) = 0
        _ColorA     ("Fill Color (start)",  Color)      = (0.00, 0.60, 1.00, 0.85)
        _ColorB     ("Fill Color (end)",    Color)      = (0.00, 1.00, 0.30, 0.95)
        _ColorBg    ("Background Color",    Color)      = (0.00, 0.08, 0.15, 0.40)
        _RimColor   ("Rim Color",           Color)      = (0.00, 0.90, 1.00, 1.00)
        _RimWidth   ("Rim Width",   Range(0.01, 0.20))  = 0.055
        _PulseSpeed ("Pulse Speed", Float)              = 3.5
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent+1"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off           // วาดทั้ง 2 หน้า เพราะวางราบพื้น

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f     { float4 pos    : SV_POSITION; float2 uv : TEXCOORD0; };

            float  _Progress, _RimWidth, _PulseSpeed;
            float4 _ColorA, _ColorB, _ColorBg, _RimColor;

            #define TAU 6.28318530718

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // ── UV → centered (-1..1) ─────────────────────────────────
                float2 uv   = i.uv * 2.0 - 1.0;
                float  dist = length(uv);

                // Soft circular clip (anti-alias edge)
                float circleMask = 1.0 - smoothstep(0.93, 1.0, dist);
                if (circleMask <= 0.001) discard;

                // ── Radial fill ───────────────────────────────────────────
                // atan2(x, y): 0 = ด้านบน (+Y), เดินตามเข็มนาฬิกา
                float angle = atan2(uv.x, uv.y);
                if (angle < 0.0) angle += TAU;
                float t = angle / TAU;          // 0..1 clockwise from top

                bool inFill = (t < _Progress);
                bool inRim  = (dist > 1.0 - _RimWidth);

                // ── Pulse ─────────────────────────────────────────────────
                float pulse = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed);

                // ── Color selection ───────────────────────────────────────
                float4 fillCol = lerp(_ColorA, _ColorB, _Progress);
                float4 col;

                if (inRim)
                {
                    // Rim:밝게 filled 부분, 어둡게 empty 부분
                    float rimBright = inFill ? (0.75 + 0.25 * pulse) : 0.40;
                    col = _RimColor * rimBright;
                }
                else if (inFill)
                {
                    col      = fillCol;
                    col.rgb *= 0.85 + 0.15 * pulse;   // subtle breathing
                }
                else
                {
                    col = _ColorBg;
                }

                col.a *= circleMask;
                return col;
            }
            ENDCG
        }
    }

    // Built-in pipeline fallback (same logic)
    FallBack Off
}
