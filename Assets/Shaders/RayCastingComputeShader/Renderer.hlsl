// Renderer.hlsl

#ifndef __RENDERER__
#define __RENDERER__

#include "Ray.hlsl"
#include "Scene.hlsl"
#include "IntersectInfo.hlsl"

#define UINTMAX 4294967295u

uint3 PCG3D(uint3 v)
{
    v = v * 1664525u + 1013904223u;
    v.x += v.y * v.z;
    v.y += v.z * v.x;
    v.z += v.x * v.y;
    v ^= v >> 16;
    v.x += v.y * v.z;
    v.y += v.z * v.x;
    v.z += v.x * v.y;
    return v;
}

float3 RayCasting(Ray ray, uint2 screenIndex)
{
    IntersectInfo isect = SceneIntersect(ray);

    if (isect.isHit)
    {
        return (float3) PCG3D((uint3) isect.triangleIndex) / (float3) UINTMAX;
    }
    else
    {
        return (float3) 0.0f;
    }
}

#endif // __RENDERER__