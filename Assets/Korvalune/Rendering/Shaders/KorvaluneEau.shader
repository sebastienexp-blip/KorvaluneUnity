// Korvalune — eau des rivières et lacs. Deux couches de vagues (cartes normales qui défilent en sens contraires),
// reflet du ciel par effet Fresnel (plus réfléchissante en vue rasante), reflet du soleil, ombres reçues, couleur
// claire sur les hauts-fonds et sombre en profondeur, écume le long des berges et des objets qui touchent l'eau.
// La profondeur et l'écume demandent la texture de profondeur de la caméra : elles ne sont actives qu'en qualité
// haute (mot-clé global KORV_WATER_DEPTH, posé par le jeu) ; sinon l'eau garde une couleur moyenne uniforme.
Shader "Korvalune/Eau"
{
    Properties
    {
        _ShallowColor("Hauts-fonds", Color) = (0.20, 0.42, 0.42, 0.55)
        _DeepColor("Profondeur", Color) = (0.04, 0.12, 0.16, 0.92)
        _FoamColor("Écume", Color) = (0.85, 0.90, 0.88, 1)
        [Normal] _BumpMap("Vagues", 2D) = "bump" {}
        _BumpScale("Force des vagues", Range(0, 2)) = 0.7
        _Tiling("Échelle des vagues (par mètre)", Float) = 0.12
        _Speed("Vitesse", Float) = 0.03
        _Smoothness("Lissé", Range(0, 1)) = 0.92
        _DepthDistance("Profondeur pleine (m)", Float) = 2.5
        _FoamDistance("Largeur d'écume (m)", Float) = 0.45
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ KORV_WATER_DEPTH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                float4 _BumpMap_ST;
                half _BumpScale;
                float _Tiling;
                float _Speed;
                half _Smoothness;
                float _DepthDistance;
                float _FoamDistance;
            CBUFFER_END

            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                half fog : TEXCOORD2;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.screenPos = ComputeScreenPos(p.positionCS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.positionWS.xz * _Tiling;
                float t = _Time.y * _Speed;
                half3 n1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv + float2(t, t * 0.6)), _BumpScale);
                half3 n2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv * 1.7 + float2(-t * 0.8, t * 0.35)), _BumpScale);
                half3 nT = normalize(half3(n1.xy + n2.xy, n1.z * n2.z));
                half3 N = normalize(half3(nT.x, nT.z, nT.y));               // plan horizontal : tangente = x, bitangente = z
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half fres = 0.04h + 0.96h * pow(1.0h - saturate(dot(N, V)), 5.0h);

                half depthK = 0.6h, foam = 0.0h;
            #if defined(KORV_WATER_DEPTH)
                float2 suv = i.screenPos.xy / i.screenPos.w;
                float sceneZ = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float waterZ = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                // l'eau n'est dessinée que là où elle est devant la scène : une profondeur de scène plus proche que l'eau veut
                // dire que la texture de profondeur n'est pas disponible (certains téléphones) -> pas d'écume ni de profondeur,
                // sinon toute la rivière devenait de l'écume blanche
                if (sceneZ > waterZ - 0.02)
                {
                    float d = max(sceneZ - waterZ, 0.0);
                    depthK = saturate(d / max(_DepthDistance, 0.01));
                    half edge = saturate(1.0 - d / max(_FoamDistance, 0.01));
                    half fn = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv * 4.0 + float2(t * 2.0, -t)).r;
                    foam = edge * smoothstep(0.35h, 0.65h, fn + edge * 0.5h) * 0.8h;
                }
            #endif

                half3 body = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthK);
                Light L = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half shadow = L.shadowAttenuation;
                half3 amb = SampleSH(N);
                half3 lit = body * (amb + L.color * (0.35h + 0.4h * saturate(dot(N, L.direction))) * shadow);
                half3 refl = GlossyEnvironmentReflection(reflect(-V, N), 1.0h - _Smoothness, 1.0h);
                float3 H = normalize(L.direction + V);
                half spec = pow(saturate(dot(N, H)), 64.0h + 448.0h * _Smoothness) * 3.0h * shadow;
                half3 col = lerp(lit, refl, fres) + spec * L.color;
                col = lerp(col, _FoamColor.rgb * (amb + L.color * 0.6h * shadow), foam);
                half alpha = lerp(_ShallowColor.a, _DeepColor.a, depthK);
                alpha = max(alpha, fres * 0.9h); alpha = max(alpha, foam); alpha = saturate(alpha + spec);
                col = MixFog(col, i.fog);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
