Shader "Custom/MarkerPylon"
{
    // Opaque, unlit, emissive edge-marker. No blending -> no overdraw, writes
    // depth and occludes, which is the cheap choice for Quest. Hue is driven by
    // world-space Z so the colour flows as the pylon rides the tile treadmill
    // and stays continuous across tile seams (same world-XZ idea as the floor).
    Properties
    {
        [Header(Rainbow Emission)]
        _Intensity   ("Emission Intensity", Range(0, 12)) = 4
        _HueScale    ("Hue Scale (world Z)", Range(0, 0.05)) = 0.003
        _FlowSpeed   ("Hue Flow Speed", Range(-2, 2)) = 0.15
        _Saturation  ("Saturation", Range(0, 1)) = 1
        _Value       ("Value", Range(0, 1)) = 1

        [Header(Shape)]
        _BaseGlow    ("Top Glow (relative to base)", Range(0, 1)) = 0.55
        _RimStrength ("Rim Strength", Range(0, 3)) = 0.8
        _RimPower    ("Rim Power", Range(0.5, 8)) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Back
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "MarkerPylonForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  heightN    : TEXCOORD2; // 0 at base, 1 at top (object space)
                float  fogFactor  : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _HueScale;
                float _FlowSpeed;
                float _Saturation;
                float _Value;
                float _BaseGlow;
                float _RimStrength;
                float _RimPower;
            CBUFFER_END

            float3 HSVtoRGB(float3 hsv)
            {
                float h = hsv.x, s = hsv.y, v = hsv.z;
                float c = v * s;
                float x = c * (1.0 - abs(fmod(h * 6.0, 2.0) - 1.0));
                float m = v - c;
                float3 rgb;
                if      (h < 1.0/6.0) rgb = float3(c, x, 0);
                else if (h < 2.0/6.0) rgb = float3(x, c, 0);
                else if (h < 3.0/6.0) rgb = float3(0, c, x);
                else if (h < 4.0/6.0) rgb = float3(0, x, c);
                else if (h < 5.0/6.0) rgb = float3(x, 0, c);
                else                  rgb = float3(c, 0, x);
                return rgb + m;
            }

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                o.heightN    = saturate(input.positionOS.y + 0.5); // built-in cube: -0.5..0.5
                o.fogFactor  = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Rainbow hue from world Z (+ time flow)
                float hue  = frac(input.positionWS.z * _HueScale + _Time.y * _FlowSpeed);
                float3 rgb = HSVtoRGB(float3(hue, _Saturation, _Value));

                // Vertical gradient: full at the base, easing toward _BaseGlow at the top
                float vGrad = lerp(1.0, _BaseGlow, input.heightN);

                // Cheap fresnel rim for a bit of edge pop
                float3 N   = normalize(input.normalWS);
                float3 V   = normalize(GetWorldSpaceViewDir(input.positionWS));
                float  rim = pow(1.0 - saturate(dot(N, V)), _RimPower) * _RimStrength;

                float3 color = rgb * _Intensity * vGrad + rgb * rim;

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
