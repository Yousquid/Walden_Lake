Shader "Walden/Lake"
{
    Properties
    {
        _BaseColor("Shallow water", Color) = (0.19,0.42,0.40,1)
        _DeepColor("Deep water", Color) = (0.075,0.23,0.28,1)
        _RippleScale("Ripple scale", Float) = 1.8
        _RippleOffset("Ripple time", Float) = 0
        _Smoothness("Smoothness", Range(0,1)) = 0.85
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _DeepColor;
            float _RippleScale;
            float _RippleOffset;
            half _Smoothness;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float fog:TEXCOORD1; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = world;
                output.positionCS = TransformWorldToHClip(world);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                float2 p = input.positionWS.xz * _RippleScale;
                float t = _RippleOffset;
                float ripple = sin(p.x * 2.3 + p.y * 1.1 + t) * .45 + sin(p.y * 3.8 - p.x * .7 - t * 1.3) * .3;
                float distanceFade = saturate(22 / max(1, distance(GetCameraPositionWS(), input.positionWS)));
                float3 n = normalize(float3(cos(p.x * 1.3 + p.y * .7 + t) * .018 * distanceFade, 1, cos(p.y * 1.8 - p.x * .4 - t * 1.3) * .022 * distanceFade));
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light sun = GetMainLight();
                half fresnel = pow(1 - saturate(dot(view, n)), 3);
                half3 water = lerp(_BaseColor.rgb, _DeepColor.rgb, saturate(.35 + ripple * .025 * distanceFade));
                half3 sky = lerp(half3(.12,.27,.32), half3(.20,.36,.41), fresnel);
                half3 color = lerp(water, sky, fresnel * .42);
                float specular = pow(saturate(dot(n, normalize(sun.direction + view))), lerp(60,180,_Smoothness));
                color *= .42 + sun.color * .58;
                color += sun.color * specular * .18;
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }
    }
}
