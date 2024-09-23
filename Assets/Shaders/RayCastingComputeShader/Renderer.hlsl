// Renderer.hlsl

#ifndef __RENDERER__
#define __RENDERER__

#include "Ray.hlsl"
#include "Scene.hlsl"
#include "IntersectInfo.hlsl"
#include "Film.hlsl"

float3 Rand(int index)
{
    float floatIndex = (float) index;
    return float3(
        frac(sqrt(234.928f * floatIndex + 12.532f)),
        frac(sqrt(533.425f * floatIndex + 43.913f)),
        frac(sqrt(732.563f * floatIndex + 69.729f)));
}

void RayCasting(Ray ray, uint2 screenIndex)
{
    IntersectInfo isect = SceneIntersect(ray);

    if (isect.isHit)
    {
        float4 color = float4(Rand(isect.triangleIndex), 1.0f);
        SetFilmPixel(screenIndex, color);
        SetDepthPixel(screenIndex, isect.tHit);
    }
    else
    {
        float4 color = float4((float3) 0.0f, 1.0f);
        SetFilmPixel(screenIndex, color);
        SetDepthPixel(screenIndex, 1000.0f);
    }
}

#endif // __RENDERER__