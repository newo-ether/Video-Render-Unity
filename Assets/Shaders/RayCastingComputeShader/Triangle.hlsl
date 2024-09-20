// Triangle.hlsl

#ifndef __TRIANGLE__
#define __TRIANGLE__

#include "IntersectInfo.hlsl"
#include "Ray.hlsl"

struct Triangle
{
    float4 vertex[3];
};

// Uniform Variable
StructuredBuffer<Triangle> triangleBuffer;

// Uniform Variable
int triangleCount;

IntersectInfo TriangleIntersect(Triangle tri, Ray ray)
{
    float3 e0 = tri.vertex[0].xyz - tri.vertex[2].xyz;
    float3 e1 = tri.vertex[1].xyz - tri.vertex[0].xyz;
    float3 e2 = tri.vertex[2].xyz - tri.vertex[1].xyz;
    
    float3 normal = normalize(cross(e1, e2));
    
    float t = dot(tri.vertex[0].xyz - ray.origin, normal) / dot(ray.dir, normal);
    
    if (t <= ray.tMin || t >= ray.tMax)
    {
        return IntersectInfoInitNone();
    }
    else
    {
        float3 p = RayAt(ray, t);
    
        float b0 = dot(cross(e0, p - tri.vertex[2].xyz), normal);
        float b1 = dot(cross(e1, p - tri.vertex[0].xyz), normal);
        float b2 = dot(cross(e2, p - tri.vertex[1].xyz), normal);
    
        if ((b0 >= 0 && b1 >= 0 && b2 >= 0) || (b0 <= 0 && b1 <= 0 && b2 <= 0))
        {
            float invbSum = 1.0f / (b0 + b1 + b2);
            
            b0 *= invbSum;
            b1 *= invbSum;
            b2 *= invbSum;
            
            return IntersectInfoInit(t, p, normal, -ray.dir, 0);
        }
        else
        {
            return IntersectInfoInitNone();
        }
    }
}

bool TriangleIsIntersect(Triangle tri, Ray ray)
{
    float3 e0 = tri.vertex[0].xyz - tri.vertex[2].xyz;
    float3 e1 = tri.vertex[1].xyz - tri.vertex[0].xyz;
    float3 e2 = tri.vertex[2].xyz - tri.vertex[1].xyz;
    
    float3 normal = normalize(cross(e1, e2));

    float t = dot(tri.vertex[0].xyz - ray.origin, normal) / dot(ray.dir, normal);
    
    if (t <= ray.tMin || t >= ray.tMax)
    {
        return false;
    }
    else
    {
        float3 p = RayAt(ray, t);
    
        float b0 = dot(cross(e0, p - tri.vertex[2].xyz), normal);
        float b1 = dot(cross(e1, p - tri.vertex[0].xyz), normal);
        float b2 = dot(cross(e2, p - tri.vertex[1].xyz), normal);
    
        if ((b0 >= 0 && b1 >= 0 && b2 >= 0) || (b0 <= 0 && b1 <= 0 && b2 <= 0))
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}

Triangle GetTriangle(int index)
{
    return triangleBuffer[index];
}

int GetTriangleCount()
{
    return triangleCount;
}

#endif // __TRIANGLE__