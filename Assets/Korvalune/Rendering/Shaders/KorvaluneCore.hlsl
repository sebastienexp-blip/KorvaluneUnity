// Korvalune — cœur commun des shaders PBR maison. Reprend le passage avant du shader Lit d'URP (mêmes fonctions :
// surface, données d'entrée, UniversalFragmentPBR, brouillard) et y insère nos couches :
//  KORV_WORLD : variation macro (casse la répétition des textures à grande échelle) et couche de dessus (mousse, poussière
//               ou neige déposée sur les faces tournées vers le ciel), réglées globalement par le jeu selon le lieu ;
//  KORV_RIM   : lumière de contour des personnages, ajoutée avant le brouillard.
#ifndef KORVALUNE_CORE_INCLUDED
#define KORVALUNE_CORE_INCLUDED

half4 _KorvRimColor;
half _KorvRimStrength;
half _KorvRimPower;
half4 _KorvTopColor;
half _KorvTopStrength;
half _KorvMacroStrength;

float KorvHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float KorvNoise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = KorvHash(i), b = KorvHash(i + float2(1, 0)), c = KorvHash(i + float2(0, 1)), d = KorvHash(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

void KorvWorldLayer(InputData d, inout SurfaceData s)
{
    float3 p = d.positionWS;
    float n1 = KorvNoise(p.xz * 0.07 + p.y * 0.05);
    float n2 = KorvNoise(p.xz * 0.33 + float2(17.3, 4.1) + p.y * 0.2);
    // variation de teinte et de valeur à grande échelle
    float macro = (n1 * 0.65 + n2 * 0.35) - 0.5;
    s.albedo *= 1.0 + macro * _KorvMacroStrength;
    s.occlusion *= lerp(1.0, 0.78 + 0.22 * n1, saturate(_KorvMacroStrength * 2.0));
    // couche déposée sur le dessus des surfaces
    half up = saturate((d.normalWS.y - 0.5) * 3.0);
    half mask = saturate(up * saturate(n2 * 1.5 - 0.15) * _KorvTopStrength);
    s.albedo = lerp(s.albedo, _KorvTopColor.rgb * (0.82 + 0.36 * n1), mask);
    s.smoothness = lerp(s.smoothness, _KorvTopColor.a, mask);
    s.metallic = lerp(s.metallic, 0.0, mask);
}

half3 KorvRim(InputData d)
{
    half fres = pow(saturate(1.0h - saturate(dot(d.normalWS, d.viewDirectionWS))), max(_KorvRimPower, 1.0h));
    Light mainLight = GetMainLight();
    return fres * _KorvRimStrength * (_KorvRimColor.rgb + mainLight.color * 0.3h);
}

// peau : la lumière diffuse sous la surface. Bande rougeâtre adoucie au terminateur (là où la lumière bascule dans
// l'ombre) et transparence des parties fines à contre-jour (oreilles, nez, doigts).
half3 KorvSkin(InputData d, SurfaceData s)
{
    Light L = GetMainLight(d.shadowCoord);
    half ndl = dot(d.normalWS, L.direction);
    half wrap = saturate((ndl + 0.55h) / 1.55h) - saturate(ndl);
    half back = pow(saturate(dot(d.viewDirectionWS, -L.direction)), 4.0h) * 0.22h;
    half sh = 0.45h + 0.55h * L.shadowAttenuation;
    return s.albedo * half3(0.95h, 0.32h, 0.20h) * (wrap * 0.55h + back) * L.color * L.distanceAttenuation * sh;
}

void KorvaluneFragment(Varyings input, out half4 outColor : SV_Target0)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);
#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif
    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);
#if defined(KORV_WORLD)
    KorvWorldLayer(inputData, surfaceData);
#endif
    half4 color = UniversalFragmentPBR(inputData, surfaceData);
#if defined(KORV_RIM)
    color.rgb += KorvRim(inputData) * color.a;
#endif
#if defined(_KORV_SKIN)
    color.rgb += KorvSkin(inputData, surfaceData);
#endif
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent(_Surface));
    outColor = color;
}
#endif
