// Scene.hlsl

#ifndef __SCENE__
#define __SCENE__

#include "IntersectInfo.hlsl"
#include "Triangle.hlsl"
#include "Ray.hlsl"

IntersectInfo SceneIntersect(Ray ray)
{
    IntersectInfo isect = IntersectInfoInitNone();
    
    for (int i = 0; i < GetTriangleCount(); i++)
    {
        Triangle tri = GetTriangle(i);
        IntersectInfo tempIsect = TriangleIntersect(tri, ray);
        if (tempIsect.isHit)
        {
            ray.tMax = tempIsect.tHit;
            isect = tempIsect;
            isect.triangleIndex = i;
        }
    }
    return isect;
}

bool SceneIsIntersect(Ray ray)
{
    for (int i = 0; i < GetTriangleCount(); i++)
    {
        Triangle tri = GetTriangle(i);
        if (TriangleIsIntersect(tri, ray))
        {
            return true;
        }
    }
    return false;
}

#endif // __SCENE__